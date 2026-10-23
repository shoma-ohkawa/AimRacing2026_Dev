#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
自動周回の計測(run_lap_variants.bat)の結果を、周ごとの推移と、機能ごとの比較にまとめる(読み取り専用。Python の標準ライブラリだけで動く)

使い方:
    python lap_report.py                         既定の perf フォルダの、ラベルが lap_ で始まる起動を集計する
    python lap_report.py --idle                  放置監視(-perfidle)の記録を集計する(既定の idle フォルダの、ラベルが idle_ で始まる起動)。周回とは別のデータ
    python lap_report.py --root <perf の場所>
    python lap_report.py --prefix lap_ --out report.txt --log
    python lap_report.py --pc 4070               CPU・GPU の名前の一部で、1 台の PC の記録に絞る

    perf フォルダに複数の PC の記録が入っているときは、meta.json の cpu と gpu で見分けて、PC ごとに分けて集計する
    (別の PC の記録どうしは、同じ条件でも置き換えず、比べない)

何を出すか(起動 = 1 通りの条件。10 周):
    周ごとの FPS 中央値(走行中)、周ごとの private_mb・vram_mb(Result → Load の後 = 解放後の残り)、周ごとの Menu → Intro の work
    起動ごとの要約: private_mb の増え方(MB/周。最小二乗法の傾き)、FPS が崩れた周(1 周目の 8 割を下回った最初の周)、カクつきの秒数(frame_ms_max が 200 ms を超えた秒)、
    計測の開始直後の VRAM(他のアプリ = Unity Editor など が使っている分の目安。同じ PC の他の起動より 1000 MB 以上大きければ警告する)
    周ごとの最大 vram_mb・最大 private_mb、周ごとのカクつきの秒数
    State 別の FPS 中央値(最初の 5 周。Opening・Menu などで、DLSS を止めた影響を見る)
        CSV に state・lap・trans の列があれば、それで集計する(trans が 1 の行 = 遷移などの最中は除く)。無ければ、transitions.csv から推定する(旧記録)
    機能ごとの比較: DLSS を止める・映像(Timeline)を止める前後の、増え方の差
    同じ条件の複数回の記録: 傾きの中央値と範囲
    --idle: 放置監視の記録。時間あたりの増え方(MB/時間)、10 分ごとの推移、終了理由、Opening への遷移回数
    --log: 各起動のフォルダに保存された Player.log(run_lap_variants.bat が条件ごとに保存する)から、周回の進み具合の行
"""

import argparse
import json
import os
import re
import sys

COMPANY_NAME = "日本電子専門学校ゲーム制作研究科"  # ProjectSettings の companyName
PRODUCT_NAME = "AimRacing2026_Unity6.3"  # ProjectSettings の productName
DEFAULT_ROOT = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow", COMPANY_NAME, PRODUCT_NAME, "perf")
DEFAULT_IDLE_ROOT = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow", COMPANY_NAME, PRODUCT_NAME, "idle")
DEFAULT_STATE_NAMES = {0: "Splash", 1: "Opening", 2: "Menu", 4: "Intro", 5: "Racing", 6: "Result", 7: "Load"}  # meta.json に state_names が無いときの、state 列の番号と名前(GameStateId の値)
IDLE_WINDOW_SECONDS = 600.0  # 放置監視の推移を出す窓の長さ(秒。ゲーム側の FPS の判定の窓と同じ 10 分)

AFTER_RESULT_LOAD_SECONDS = 2.0  # Result → Load の何秒後の値を、「周の谷(解放後の残り)」とみなすか
RACE_SKIP_SECONDS = 3.0  # 走行の FPS を数えるとき、走行の開始から除く秒数(読み込み直後の揺れを避ける)
COLLAPSE_RATIO = 0.8  # FPS が 1 周目のこの割合を下回った最初の周を、「崩れた周」とする(目安)
GROW_MB_PER_LAP = 40.0  # private_mb が、1 周あたりこれ以上増えていれば「増える」と判定する(目安。根拠となる測定はない)
VRAM_FULL_RATIO = 0.97  # VRAM が総量のこの割合を超えたら、頭打ちの可能性を警告する
HALF = 0.5  # 「半分以下になった」の割合
STALL_MS = 200.0  # frame_ms_max がこの値を超えた秒を、「カクついた秒」とする(目安)
STATE_FPS_LAPS = 5  # State 別の FPS を数える周の数(基準が崩れ始める前の、最初の周)
STATE_SKIP_SECONDS = 1.0  # State に入ってから、FPS を数えない秒数(読み込み直後の揺れを避ける。自動操作の待ちを 3 秒に縮めたあとは、Opening・Result の滞在が 3 秒しかないので、長くできない)
STATE_MIN_ROWS = 3  # State 別の FPS を出すのに必要な、最低限の行数(1 秒 1 行。最初の STATE_FPS_LAPS 周の合計)
STATE_ORDER = ["Splash", "Opening", "Menu", "Intro", "Racing", "Result"]  # State 別の FPS の表の列の順
VRAM_START_ROWS = 5  # 「計測の開始直後の VRAM」を数える、最初の行数(1 秒 1 行。その最小値を使う)
VRAM_START_DIFF_MB = 1000.0  # 開始直後の VRAM が、同じ PC の他の起動より、これ以上大きければ警告する(目安。Unity Editor を開いたままだと約 3000 MB 増えた)
METRICS = [("private_mb", "private"), ("vram_mb", "vram"), ("ws_mb", "ws"), ("unity_alloc_mb", "alloc")]


def read_csv_rows(path):
    """CSV を、{列名: 数値(または文字列)} のリストにする。数値にできない列は文字列のまま。"""
    rows = []
    with open(path, "r", encoding="utf-8-sig") as f:
        header = f.readline().rstrip("\r\n").split(",")
        for line in f:
            line = line.rstrip("\r\n")
            if not line:
                continue
            parts = line.split(",")
            row = {}
            for i, name in enumerate(header):
                value = parts[i] if i < len(parts) else ""
                try:
                    row[name] = float(value)
                except ValueError:
                    row[name] = value
            rows.append(row)
    return rows


def read_perf(folder):
    rows = []
    for name in sorted(os.listdir(folder)):
        if re.match(r"^perf_\d{8}\.csv$", name):
            rows.extend(read_csv_rows(os.path.join(folder, name)))
    return rows


def read_processes(folder):
    """transitions.csv を、処理(START から TOTAL まで)ごとにまとめる。work は、work のステップの ms の合計(Boot の EngineStartup は除く)。"""
    path = os.path.join(folder, "transitions.csv")
    if not os.path.isfile(path):
        return []
    procs = []
    for r in read_csv_rows(path):
        kind = r.get("type")
        if kind == "mark":
            procs.append({"kind": r["kind"], "from": r["from"], "to": r["to"], "start": r["uptime_s"], "end": None, "work": 0.0})
        elif kind == "total" and procs:
            procs[-1]["end"] = r["uptime_s"]
        elif kind == "work" and procs and isinstance(r.get("ms"), float) and r["ms"] >= 0:
            if not (procs[-1]["kind"] == "Boot" and r.get("step") == "EngineStartup"):
                procs[-1]["work"] += r["ms"]
    return procs


def is_stalled(row):
    """その 1 秒に、STALL_MS を超えるフレームがあったか。"""
    value = row.get("frame_ms_max")
    return isinstance(value, float) and value > STALL_MS


def peak(rows, metric):
    """行のうち、指定の列の最大値。数値の行が無ければ None。"""
    values = [r[metric] for r in rows if isinstance(r.get(metric), float)]
    return max(values) if values else None


def state_visits(procs):
    """State に居た区間を、(入った時刻, State, 出る遷移の始まりの時刻)で、時刻順に返す。出る遷移が無ければ、3 つ目は None。"""
    entered = [(p["end"], p["to"]) for p in procs if p["end"] is not None and p["kind"] in ("Boot", "Transition", "Reset")]
    entered.sort(key=lambda e: e[0])
    leaves = sorted(p["start"] for p in procs if p["kind"] in ("Transition", "Reset"))
    visits = []
    for enter, state in entered:
        later = [s for s in leaves if s >= enter]
        visits.append((enter, state, later[0] if later else None))
    return visits


def fps_by_state(rows, visits, until):
    """State ごとの FPS の中央値。各 State に入って STATE_SKIP_SECONDS 後から、出る遷移が始まるまでの行を、State ごとに集める(uptime が until まで)。
    出る遷移の最中(フェードアウト・ロード)のフレームは、含めない。"""
    by_state = {}
    for enter, state, leave in visits:
        if enter > until:
            break
        low = enter + STATE_SKIP_SECONDS
        high = until if leave is None else min(leave, until)
        values = [r["fps"] for r in rows if low <= r["uptime_s"] < high and isinstance(r.get("fps"), float)]
        by_state.setdefault(state, []).extend(values)
    return {s: median(v) for s, v in by_state.items() if len(v) >= STATE_MIN_ROWS}


def parse_state_names(text):
    """meta.json の state_names("0:Splash,1:Opening,...")を、{番号: 名前} にする。無い・読めなければ既定の対応を返す。"""
    names = {}
    for part in (text or "").split(","):
        number, _, name = part.partition(":")
        if name and number.strip().lstrip("-").isdigit():
            names[int(number)] = name.strip()
    return names or dict(DEFAULT_STATE_NAMES)


def state_number(names, name):
    """State の名前から、state 列の番号を返す。無ければ -1。"""
    for number, state_name in names.items():
        if state_name == name:
            return number
    return -1


def has_flow_columns(rows):
    """CSV に state・lap・trans の列があるか(新しい記録の形式)。"""
    return bool(rows) and all(k in rows[0] for k in ("state", "lap", "trans"))


def settled_rows(rows):
    """遷移などの最中(trans が 1)ではなく、直前の行も同じ State で落ち着いていた行だけ。State に入った直後の 1 行は、揺れを避けるため除く。"""
    out = []
    previous = None
    for r in rows:
        if r.get("trans") == 0.0 and previous is not None and previous.get("trans") == 0.0 and previous.get("state") == r.get("state"):
            out.append(r)
        previous = r
    return out


def fps_by_state_columns(settled, names, max_lap):
    """State ごとの FPS の中央値(state・lap 列から)。lap が 1〜max_lap の、落ち着いた行だけを集める。"""
    by_state = {}
    for r in settled:
        if not isinstance(r.get("fps"), float) or not (1 <= r.get("lap", 0) <= max_lap):
            continue
        name = names.get(int(r["state"]))
        if name:
            by_state.setdefault(name, []).append(r["fps"])
    return {s: median(v) for s, v in by_state.items() if len(v) >= STATE_MIN_ROWS}


def value_at(rows, uptime, metric):
    """uptime に最も近い行の、指定の列の値。"""
    best = min(rows, key=lambda r: abs(r["uptime_s"] - uptime))
    return best[metric]


def median(values):
    s = sorted(values)
    n = len(s)
    if n == 0:
        return None
    return s[n // 2] if n % 2 else (s[n // 2 - 1] + s[n // 2]) / 2.0


def slope(values):
    """最小二乗法の傾き(1 標本あたりの増加量)。標本が 2 つ未満なら None。"""
    n = len(values)
    if n < 2:
        return None
    mean_x = (n - 1) / 2.0
    mean_y = sum(values) / n
    num = sum((i - mean_x) * (v - mean_y) for i, v in enumerate(values))
    den = sum((i - mean_x) ** 2 for i in range(n))
    return num / den if den else None


def parse_arguments_text(text):
    """meta.json の arguments(起動引数の全文)から、周回数とフラグを取り出す。"""
    text = text or ""
    laps = None
    m = re.search(r"-perfautolap\s+(\d+)", text, re.IGNORECASE)
    if m:
        laps = int(m.group(1))
    dlss_off = bool(re.search(r"-perfnodlss", text, re.IGNORECASE))
    return {
        "auto": bool(re.search(r"-perfautolap", text, re.IGNORECASE)),
        "laps": laps,
        "dlss_off": dlss_off,
        "dlss_map": bool(re.search(r"-perfdlssmaponly", text, re.IGNORECASE)) and not dlss_off,  # -perfnodlss が優先される
        "dlss_unload": bool(re.search(r"-perfdlssoffbeforeunload", text, re.IGNORECASE)) and not dlss_off,  # -perfnodlss が優先される
        "video_off": bool(re.search(r"-perfnovideo", text, re.IGNORECASE)),
    }


def find_laps(procs):
    """完了した周(Intro → Racing、Racing → Result、Result → Load がそろった周)を、順に返す。"""
    done = [p for p in procs if p["kind"] == "Transition" and p["end"] is not None]
    to_racing = [p for p in done if p["from"] == "Intro" and p["to"] == "Racing"]
    to_result = [p for p in done if p["from"] == "Racing" and p["to"] == "Result"]
    to_load = [p for p in done if p["from"] == "Result" and p["to"] == "Load"]
    menu_intro = [p for p in done if p["from"] == "Menu" and p["to"] == "Intro"]
    count = min(len(to_racing), len(to_result), len(to_load))
    laps = []
    for i in range(count):
        laps.append({
            "race_start": to_racing[i]["end"],
            "race_end": to_result[i]["start"],
            "valley": to_load[i]["end"] + AFTER_RESULT_LOAD_SECONDS,
            "menu_intro_work": menu_intro[i]["work"] if i < len(menu_intro) else None,
        })
    return laps


def pc_of(meta):
    """meta.json の cpu と gpu から、どの PC の記録かを表す。無ければ「不明」。"""
    cpu = (meta.get("cpu") or "").strip()
    gpu = (meta.get("gpu") or "").strip()
    return (cpu or "(CPU 不明)", gpu or "(GPU 不明)")


def analyze_run(folder):
    meta_path = os.path.join(folder, "meta.json")
    meta = {}
    if os.path.isfile(meta_path):
        with open(meta_path, "r", encoding="utf-8-sig") as f:
            meta = json.load(f)
    info = parse_arguments_text(meta.get("arguments", ""))
    result = {"folder": os.path.basename(folder), "label": meta.get("label") or os.path.basename(folder), "info": info,
              "gpu_memory_mb": meta.get("gpu_memory_mb"), "clean_exit": meta.get("clean_exit"), "pc": pc_of(meta), "note": None, "laps": [],
              "stop_reason": meta.get("stop_reason"), "editor_running": meta.get("editor_running"), "until": meta.get("until")}
    rows = read_perf(folder)
    if not rows or not info["auto"]:
        result["note"] = "perf CSV が無い、または -perfautolap の起動ではない"
        return result
    procs = read_processes(folder)
    laps = find_laps(procs)
    if not laps:
        result["note"] = "完了した周がない"
        return result
    names = parse_state_names(meta.get("state_names"))
    settled = settled_rows(rows) if has_flow_columns(rows) else None  # state・lap・trans の列があれば、それで集計する(無ければ従来どおり、transitions.csv から推定する)
    racing = state_number(names, "Racing")
    previous_valley = 0.0
    for index, lap in enumerate(laps, 1):
        if settled is not None:
            race = [r["fps"] for r in settled if r["lap"] == index and r["state"] == racing and isinstance(r.get("fps"), float)]
        else:
            race = [r["fps"] for r in rows if lap["race_start"] + RACE_SKIP_SECONDS <= r["uptime_s"] < lap["race_end"]]
        window = [r for r in rows if previous_valley < r["uptime_s"] <= lap["valley"]]  # 前の周の谷から、この周の谷までの行
        entry = {"fps": median(race), "menu_intro_work": lap["menu_intro_work"],
                 "vram_peak": peak(window, "vram_mb"), "private_peak": peak(window, "private_mb"),
                 "stall": sum(1 for r in window if is_stalled(r))}
        for metric, short in METRICS:
            entry[short] = value_at(rows, lap["valley"], metric)
        result["laps"].append(entry)
        previous_valley = lap["valley"]
    result["vram_max"] = max(r["vram_mb"] for r in rows)
    result["private_max"] = max(r["private_mb"] for r in rows)
    result["stall_total"] = sum(1 for r in rows if is_stalled(r))
    starts = [r["vram_mb"] for r in rows[:VRAM_START_ROWS] if isinstance(r.get("vram_mb"), float) and r["vram_mb"] >= 0]
    result["vram_start"] = min(starts) if starts else None
    state_laps = min(STATE_FPS_LAPS, len(laps))
    if settled is not None:
        result["state_fps"] = fps_by_state_columns(settled, names, state_laps)
    else:
        result["state_fps"] = fps_by_state(rows, state_visits(procs), laps[state_laps - 1]["valley"])
    result["state_fps_laps"] = state_laps
    result["private_slope"] = slope([e["private"] for e in result["laps"]])
    result["vram_slope"] = slope([e["vram"] for e in result["laps"]])
    first = result["laps"][0]["fps"]
    result["collapse_lap"] = None
    if first:
        for i, e in enumerate(result["laps"], 1):
            if e["fps"] is not None and e["fps"] < first * COLLAPSE_RATIO:
                result["collapse_lap"] = i
                break
    return result


def condition_name(info):
    if info["dlss_off"] and info["video_off"]:
        return "DLSS+映像オフ"
    if info["dlss_off"]:
        return "DLSSオフ"
    if info["dlss_map"] and info["dlss_unload"]:
        return "DLSS Mapのみ+離脱前オフ"
    if info["dlss_unload"] and info["video_off"]:
        return "DLSS 離脱前オフ+映像オフ"
    if info["dlss_unload"]:
        return "DLSS 離脱前オフ"
    if info["dlss_map"] and info["video_off"]:
        return "DLSS Mapのみ+映像オフ"
    if info["dlss_map"]:
        return "DLSS Mapのみ"
    if info["video_off"]:
        return "映像オフ"
    return "基準"


def sort_key(result):
    i = result["info"]
    order = {(False, False, False, False): 0, (True, False, False, False): 1, (False, False, False, True): 2, (True, False, False, True): 3,
             (False, True, False, False): 4, (False, False, True, False): 5, (False, True, True, False): 6, (False, True, False, True): 7}
    return (order.get((i["dlss_off"], i["dlss_map"], i["dlss_unload"], i["video_off"]), len(order)), result["label"])


def find(results, dlss_off, video_off, dlss_map=False, dlss_unload=False):
    for r in results:
        i = r["info"]
        if i["dlss_off"] == dlss_off and i["video_off"] == video_off and i["dlss_map"] == dlss_map and i["dlss_unload"] == dlss_unload and not r.get("note"):
            return r
    return None


def fmt_num(v, width, decimals=0, signed=False):
    if v is None:
        return "-".rjust(width)
    if abs(v) < 0.5 * 10 ** (-decimals):
        v = 0.0  # 「-0」と表示されないように
    return (("%+.*f" if signed else "%.*f") % (decimals, v)).rjust(width)


def lap_table(title, results, getter, decimals=0, width=7):
    n = max(len(r["laps"]) for r in results if not r.get("note"))
    lines = [title, "  %-16s " % "条件" + "".join(("%d周" % (i + 1)).rjust(width) for i in range(n))]
    for r in results:
        if r.get("note"):
            continue
        cells = []
        for i in range(n):
            v = getter(r["laps"][i]) if i < len(r["laps"]) else None
            cells.append(fmt_num(v, width, decimals))
        lines.append("  %-16s " % condition_name(r["info"]) + "".join(cells))
    return lines


def state_fps_table(results):
    """State 別の FPS 中央値の表(条件 × State)。DLSS を止めたとき、Map 以外の State の FPS が下がるかを見る。"""
    laps = min(r["state_fps_laps"] for r in results)
    lines = ["State 別の FPS 中央値(最初の %d 周、各 State に入って %.0f 秒後から、出る遷移が始まるまで。行数が %d 未満の State は -)" % (laps, STATE_SKIP_SECONDS, STATE_MIN_ROWS),
             "  %-16s " % "条件" + "".join(s.rjust(9) for s in STATE_ORDER)]
    for r in results:
        lines.append("  %-16s " % condition_name(r["info"]) + "".join(fmt_num(r["state_fps"].get(s), 9, 1) for s in STATE_ORDER))
    return lines


def comparisons(results):
    base = find(results, False, False)
    if not base or base.get("private_slope") is None or base["private_slope"] <= 0:
        return []
    lines = []
    for title, r in [("DLSS を止める", find(results, True, False)), ("映像(Timeline)を止める", find(results, False, True)), ("DLSS と映像の両方を止める", find(results, True, True)), ("DLSS を Map のカメラだけに残す", find(results, False, False, True)), ("離れる直前に DLSS を止める", find(results, False, False, False, True))]:
        if not r or r.get("private_slope") is None:
            continue
        cut = (1.0 - r["private_slope"] / base["private_slope"]) * 100.0
        lines.append("  %-26s %+7.0f → %+7.0f MB/周   減少 %.0f%%   FPS が崩れた周: %s → %s" % (
            title, base["private_slope"], r["private_slope"], cut, base["collapse_lap"] or "なし", r["collapse_lap"] or "なし"))
    return lines


def hints(results):
    """結果の読み方。計測した条件だけを材料に、目安の文を出す(断定ではない)。"""
    base, dlss, video, both = find(results, False, False), find(results, True, False), find(results, False, True), find(results, True, True)
    mapo = find(results, False, False, True)
    unlo = find(results, False, False, False, True)
    lines = []
    if not base or base.get("private_slope") is None:
        return ["  (基準の起動が無いので、機能ごとの比較はできません)"]
    b = base["private_slope"]
    if b < GROW_MB_PER_LAP:
        lines.append("  基準でも、private_mb は周ごとに %.0f MB しか増えない(目安 %.0f MB 未満)→ この条件では、増加を再現できていません。" % (b, GROW_MB_PER_LAP))
        return lines
    lines.append("  基準: private_mb は周ごとに %.0f MB 増える(目安 %.0f MB 以上 = 増える)。" % (b, GROW_MB_PER_LAP))
    if not (dlss or video or both or mapo or unlo):
        lines.append("  DLSS・映像を止めた起動が無いので、原因の比較はできません(run_lap_variants.bat で、nodlss・novideo・both も回す)。")
        return lines
    rd = lambda r: (r["private_slope"] if r and r.get("private_slope") is not None else None)
    drop = lambda v: v is not None and v <= b * HALF
    if dlss and drop(rd(dlss)):
        lines.append("  DLSS を止めると、増加が半分以下になる(%.0f → %.0f MB/周)→ DLSS が主因の可能性が高い。" % (b, rd(dlss)))
    elif dlss:
        lines.append("  DLSS を止めても、増加は半分以下にならない(%.0f → %.0f MB/周)→ DLSS だけが原因ではない。" % (b, rd(dlss)))
    if video and drop(rd(video)):
        lines.append("  映像(Timeline)を止めると、増加が半分以下になる(%.0f → %.0f MB/周)→ Timeline(Intro・OP)の再生が主因の可能性が高い。" % (b, rd(video)))
    elif video:
        lines.append("  映像を止めても、増加は半分以下にならない(%.0f → %.0f MB/周)→ 映像だけが原因ではない。" % (b, rd(video)))
    if both and rd(both) is not None:
        if drop(rd(both)):
            lines.append("  両方を止めると、増加が半分以下になる(%.0f → %.0f MB/周)。" % (b, rd(both)))
        else:
            lines.append("  両方を止めても、まだ %.0f MB/周 増える(基準の %.0f%%)→ DLSS・映像以外に、主な原因がある(Map・Opening の読み込み、音、など)。" % (rd(both), rd(both) / b * 100.0))
    if mapo and rd(mapo) is not None:
        if drop(rd(mapo)):
            lines.append("  DLSS を Map のカメラだけに残すと、増加が半分以下になる(%.0f → %.0f MB/周)→ 増加の主因は、Map 以外のシーン(Opening・Menu・Splash・Load・Result)のカメラの DLSS。Map だけに残す運用が、対策の候補。" % (b, rd(mapo)))
        else:
            lines.append("  DLSS を Map のカメラだけに残しても、増加は半分以下にならない(%.0f → %.0f MB/周)→ Map のカメラの DLSS も、増加の原因。" % (b, rd(mapo)))
        fps_text = "走行中の FPS(1 周目 → 最終周): 基準 %s → %s / Map のみ %s → %s" % (
            fmt_num(base["laps"][0]["fps"], 0, 1).strip(), fmt_num(base["laps"][-1]["fps"], 0, 1).strip(),
            fmt_num(mapo["laps"][0]["fps"], 0, 1).strip(), fmt_num(mapo["laps"][-1]["fps"], 0, 1).strip())
        if dlss:
            fps_text += " / DLSS オフ %s → %s" % (fmt_num(dlss["laps"][0]["fps"], 0, 1).strip(), fmt_num(dlss["laps"][-1]["fps"], 0, 1).strip())
        lines.append("  " + fps_text + "(Map のカメラの DLSS を残すことで、走行中の FPS が保たれているかを見る)。")
        drops = []
        for s in ("Opening", "Menu"):
            b_fps, m_fps = base["state_fps"].get(s), mapo["state_fps"].get(s)
            if b_fps is not None and m_fps is not None:
                drops.append("%s %.1f → %.1f" % (s, b_fps, m_fps))
        if drops:
            lines.append("  Map のみにしたときの、Map 以外の State の FPS(基準 → Map のみ): " + " / ".join(drops) + "。採否は、展示の解像度での FPS で決める。")
    if unlo and rd(unlo) is not None:
        if drop(rd(unlo)):
            lines.append("  シーンを離れる直前に DLSS を止めると、増加が半分以下になる(%.0f → %.0f MB/周)→ DLSS の資源は、DLSS が有効なまま破棄されると解放されない可能性が高い。DLSS を全シーンに残したまま、破棄の前に止める対策が候補。" % (b, rd(unlo)))
        else:
            lines.append("  シーンを離れる直前に DLSS を止めても、増加は半分以下にならない(%.0f → %.0f MB/周)→ 破棄の前に止めるだけでは、解放されない。" % (b, rd(unlo)))
        fps_text = "走行中の FPS(1 周目 → 最終周): 基準 %s → %s / 離脱前オフ %s → %s" % (
            fmt_num(base["laps"][0]["fps"], 0, 1).strip(), fmt_num(base["laps"][-1]["fps"], 0, 1).strip(),
            fmt_num(unlo["laps"][0]["fps"], 0, 1).strip(), fmt_num(unlo["laps"][-1]["fps"], 0, 1).strip())
        if mapo and rd(mapo) is not None:
            lines.append("  離脱前オフ %.0f MB/周 と Map のみ %.0f MB/周 の比較: %s。" % (rd(unlo), rd(mapo), "離脱前オフの方が少ない" if rd(unlo) < rd(mapo) else "Map のみの方が少ない、または同程度"))
        lines.append("  " + fps_text + "(全シーンで DLSS が有効なので、走行中の FPS は基準並みのはず)。")
    if dlss and video and rd(dlss) is not None and rd(video) is not None:
        share_d, share_v = (b - rd(dlss)) / b * 100.0, (b - rd(video)) / b * 100.0
        lines.append("  単独で止めたときの減少: DLSS %.0f%% / 映像 %.0f%%(合計が 100%% を超える、または両方を止めた結果と合わない場合は、条件の重なりや、ばらつきの可能性)。" % (share_d, share_v))
    return lines


def repeat_lines(repeats):
    """同じ条件の記録が複数あるとき、ばらつきを見るために、記録ごとの要約を古い順に並べる。"""
    lines = ["同じ条件の複数回の記録(ばらつきの確認。古い順)"]
    lines.append("  %-16s %-36s %5s | %9s %9s | %7s %7s | %9s | %9s %8s" % ("条件", "フォルダ", "周", "private傾き", "vram傾き", "FPS1周", "FPS最終", "FPS崩れ周", "開始VRAM", "最大VRAM"))
    for key in sorted(repeats, key=lambda k: sort_key(repeats[k][-1])):
        runs = repeats[key]
        for r in runs:
            requested = r["info"]["laps"]
            laps_text = "%d/%d" % (len(r["laps"]), requested) if requested else str(len(r["laps"]))
            total = r.get("gpu_memory_mb")
            vram_text = ("%.0f%%" % (r["vram_max"] / total * 100.0)) if total else "-"
            lines.append("  %-16s %-36s %5s | %s %s | %s %s | %9s | %s %8s" % (
                condition_name(r["info"]), r["folder"][:36], laps_text, fmt_num(r["private_slope"], 11, 0, True), fmt_num(r["vram_slope"], 9, 0, True),
                fmt_num(r["laps"][0]["fps"], 7, 1), fmt_num(r["laps"][-1]["fps"], 7, 1), str(r["collapse_lap"] or "なし"), fmt_num(r["vram_start"], 9), vram_text))
        slopes = [r["private_slope"] for r in runs if r.get("private_slope") is not None]
        if len(slopes) >= 2:
            spread = max(slopes) - min(slopes)
            mean = sum(slopes) / len(slopes)
            lines.append("  %-16s private 傾きの差: %.0f MB/周(平均 %.0f MB/周の %.0f%%)" % (condition_name(runs[-1]["info"]), spread, mean, (spread / mean * 100.0) if mean else 0.0))
            lines.append("  %-16s private 傾き: 中央値 %.0f MB/周(範囲 %.0f 〜 %.0f、%d 回)" % (condition_name(runs[-1]["info"]), median(slopes), min(slopes), max(slopes), len(slopes)))
        finals = [r["laps"][-1]["fps"] for r in runs if r["laps"][-1]["fps"] is not None]
        if len(finals) >= 2:
            lines.append("  %-16s FPS(最終周): 中央値 %.1f(範囲 %.1f 〜 %.1f、%d 回)" % (condition_name(runs[-1]["info"]), median(finals), min(finals), max(finals), len(finals)))
    return lines


def render(results, repeats=None):
    out = []
    out.append("自動周回の集計(private_mb の増え方 ≥ %.0f MB/周 を「増える」と判定。目安)" % GROW_MB_PER_LAP)
    out.append("")
    ok = [r for r in results if not r.get("note")]
    for r in results:
        if r.get("note"):
            out.append("  %s: %s" % (r["folder"], r["note"]))
    if not ok:
        return "\n".join(out)
    if len(ok) != len(results):
        out.append("")
    out.append("起動ごとの要約")
    out.append("  %-16s %-22s %5s | %9s %9s | %7s %7s | %9s %9s %12s | %-8s | %s" % ("条件", "ラベル", "周", "private傾き", "vram傾き", "FPS1周", "FPS最終", "FPS崩れ周", "200ms超(秒)", "開始VRAM(MB)", "判定", "終了理由"))
    warnings = []
    for r in ok:
        sp = r["private_slope"]
        verdict = "-" if sp is None else ("増える" if sp >= GROW_MB_PER_LAP else "増えない")
        requested = r["info"]["laps"]
        laps_text = "%d/%d" % (len(r["laps"]), requested) if requested else str(len(r["laps"]))
        out.append("  %-16s %-22s %5s | %s %s | %s %s | %9s %s %s | %-8s | %s" % (
            condition_name(r["info"]), r["label"][:22], laps_text, fmt_num(sp, 11, 0, True), fmt_num(r["vram_slope"], 9, 0, True),
            fmt_num(r["laps"][0]["fps"], 7, 1), fmt_num(r["laps"][-1]["fps"], 7, 1), str(r["collapse_lap"] or "なし"), fmt_num(r["stall_total"], 11), fmt_num(r["vram_start"], 12), verdict, r.get("stop_reason") or "-"))
        if r.get("editor_running"):
            warnings.append("  %s: 起動時に Unity Editor が動いていました(meta.json の editor_running)→ vram_mb は、他の起動と比べないでください。" % r["label"])
        if r.get("stop_reason") in ("vram_limit", "fps_drop"):
            warnings.append("  %s: 終了条件に達して終わっています(%s。-perfuntil)。周数は計画より少なくなります。" % (r["label"], r["stop_reason"]))
        if requested and len(r["laps"]) < requested:
            warnings.append("  %s: 周回が %d/%d 周で終わっています(中止、またはタイムアウト。Player.log の [AutoDemoLoop] を確認)。" % (r["label"], len(r["laps"]), requested))
        total = r.get("gpu_memory_mb")
        if total and r["vram_max"] >= total * VRAM_FULL_RATIO:
            warnings.append("  %s: VRAM が総量(%d MB)の %.0f%% に達した → 以降の増加は頭打ち(または性能の劣化)の可能性があります。" % (r["label"], total, r["vram_max"] / total * 100.0))
        if r.get("clean_exit") is False:
            warnings.append("  %s: 正常に終了していません(clean_exit が false)。" % r["label"])
    # 同じ PC の、すべての起動(同じ条件の古い記録も含む)の中で、開始直後の VRAM がいちばん小さい値と比べる
    every = list(ok)
    for runs in (repeats or {}).values():
        every.extend(runs)
    starts = [r["vram_start"] for r in every if r.get("vram_start") is not None]
    if len(starts) >= 2:
        lowest = min(starts)
        for r in ok:
            if r.get("vram_start") is not None and r["vram_start"] - lowest >= VRAM_START_DIFF_MB:
                warnings.append("  %s: 計測の開始直後の VRAM が %.0f MB で、同じ PC の他の起動(最小 %.0f MB)より %.0f MB 大きい → 他のアプリ(Unity Editor など)が VRAM を使っていた可能性があります。vram_mb(周ごとの最大・谷)は、他の起動と比べないでください。" % (
                    r["label"], r["vram_start"], lowest, r["vram_start"] - lowest))
    out.append("")
    out.extend(lap_table("周ごとの FPS 中央値(走行中)", ok, lambda e: e["fps"], 1))
    out.append("")
    out.extend(lap_table("周ごとの private_mb(Result → Load の後 = 解放後の残り。MB)", ok, lambda e: e["private"]))
    out.append("")
    out.extend(lap_table("周ごとの vram_mb(同じ時点。PC 全体の値。MB)", ok, lambda e: e["vram"]))
    out.append("")
    out.extend(lap_table("周ごとの unity_alloc_mb(同じ時点。Unity が把握するメモリ。MB)", ok, lambda e: e["alloc"]))
    out.append("")
    out.extend(lap_table("周ごとの最大 vram_mb(その周の間の最大。MB。総量に近いほど、性能が落ちやすい)", ok, lambda e: e["vram_peak"]))
    out.append("")
    out.extend(lap_table("周ごとの最大 private_mb(その周の間の最大。MB)", ok, lambda e: e["private_peak"]))
    out.append("")
    out.extend(lap_table("周ごとのカクつきの秒数(frame_ms_max が %.0f ms を超えた秒。周の谷までの区間)" % STALL_MS, ok, lambda e: e["stall"]))
    out.append("")
    out.extend(lap_table("周ごとの Menu → Intro の work(ms)", ok, lambda e: e["menu_intro_work"]))
    out.append("")
    out.extend(state_fps_table(ok))
    comp = comparisons(ok)
    if comp:
        out.append("")
        out.append("機能ごとの比較(private_mb の増え方)")
        out.extend(comp)
    out.append("")
    if repeats:
        out.extend(repeat_lines(repeats))
        out.append("")
    out.append("読み方の目安(断定ではありません。原因の候補を絞る材料)")
    out.extend(hints(ok))
    out.append("")
    out.append("alloc が増えない(≈ 一定)のに private が増えるなら、増えているのは Unity の管理の外(GPU・ドライバー・ネイティブ)。vram は PC 全体の値(他のアプリを閉じて回す)。")
    if warnings:
        out.append("")
        out.append("注意")
        out.extend(warnings)
    return "\n".join(out)


def read_lap_log_lines(path):
    """Player.log から、[PerfAutoLap]・[AutoDemoLoop] の行を取り出す。DLSS を止めた台数の行は多いので、最後の 1 行だけ残す。"""
    lines = []
    last_stop = None
    with open(path, "r", encoding="utf-8", errors="replace") as f:
        for line in f:
            if "[PerfAutoLap]" not in line and "[AutoDemoLoop]" not in line:
                continue
            line = line.rstrip("\r\n")
            if "止めました" in line:
                last_stop = line
                continue
            lines.append(line)
    if last_stop:
        lines.append(last_stop)
    return lines


def print_lines(lines, limit):
    for line in lines[-limit:]:
        text = "  " + line
        try:
            print(text)
        except UnicodeEncodeError:
            enc = sys.stdout.encoding or "utf-8"
            print(text.encode(enc, "replace").decode(enc))


def print_lap_log(root, folders=(), limit=40, allow_legacy=True):
    """周回の進み具合の行を出す。各起動のフォルダに Player.log があれば(run_lap_variants.bat が条件ごとに保存する)、それを読む。
    どの起動にも無ければ、perf フォルダの 1 つ上の Player.log を読む(直前の起動の分だけ。Unity が上書きするため)。"""
    saved = [(f, os.path.join(root, f, "Player.log")) for f in folders if os.path.isfile(os.path.join(root, f, "Player.log"))]
    print("")
    if saved:
        print("各起動の Player.log の周回ログ(起動のフォルダに保存された Player.log)")
        for name, path in saved:
            lines = read_lap_log_lines(path)
            print("  [%s]%s" % (name, "" if lines else " (周回の行がありません)"))
            print_lines(lines, limit)
        missing = [f for f in folders if f not in [name for name, _ in saved]]
        if missing:
            print("  Player.log が保存されていない起動(保存を始める前の記録、または保存に失敗した): " + ", ".join(missing))
        return
    if not allow_legacy:
        print("Player.log が保存された起動がありません(複数の PC の記録があるため、perf フォルダの 1 つ上の Player.log は、どの PC のものか分からないので読みません)")
        return
    path = os.path.join(os.path.dirname(os.path.abspath(root)), "Player.log")
    print("Player.log の周回ログ(%s)" % path)
    print("  (起動のフォルダに Player.log が保存されていないので、perf フォルダの 1 つ上を読みます。直前の起動の分だけです)")
    if not os.path.isfile(path):
        print("  (Player.log が見つかりません)")
        return
    lines = read_lap_log_lines(path)
    if not lines:
        print("  ([PerfAutoLap] の行がありません。-perfautolap 付きで起動していない、または別の起動の Player.log です)")
        return
    print_lines(lines, limit)


def slope_xy(xs, ys):
    """最小二乗法の傾き(x が 1 増えたときの y の増加量)。標本が 2 つ未満、または x が一定なら None。"""
    n = len(xs)
    if n < 2:
        return None
    mean_x = sum(xs) / n
    mean_y = sum(ys) / n
    den = sum((x - mean_x) ** 2 for x in xs)
    if not den:
        return None
    return sum((x - mean_x) * (y - mean_y) for x, y in zip(xs, ys)) / den


def read_meta(folder):
    """meta.json を読む。無い・読めなければ空の辞書。"""
    path = os.path.join(folder, "meta.json")
    if not os.path.isfile(path):
        return {}
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return {}


def numeric_pairs(rows, x_key, y_key):
    """x・y とも数値で、y が取得できている(0 以上の)行の、(x の一覧, y の一覧)。"""
    xs, ys = [], []
    for r in rows:
        x, y = r.get(x_key), r.get(y_key)
        if isinstance(x, float) and isinstance(y, float) and y >= 0:
            xs.append(x)
            ys.append(y)
    return xs, ys


def analyze_idle(folder):
    """放置監視(-perfidle)の 1 起動を集計する。時間あたりの増え方、10 分ごとの推移、Opening への遷移回数など。"""
    meta = read_meta(folder)
    result = {"folder": os.path.basename(folder), "label": meta.get("label") or os.path.basename(folder), "pc": pc_of(meta), "note": None,
              "planned": meta.get("planned"), "stop_reason": meta.get("stop_reason"), "editor_running": meta.get("editor_running"),
              "gpu_memory_mb": meta.get("gpu_memory_mb"), "clean_exit": meta.get("clean_exit"), "windows": []}
    rows = read_perf(folder)
    if len(rows) < 2:
        result["note"] = "perf CSV が無い、または行が少ない"
        return result
    names = parse_state_names(meta.get("state_names"))
    flow = has_flow_columns(rows)
    opening = state_number(names, "Opening")
    settled = settled_rows(rows) if flow else rows  # state 列が無い旧記録は、全行の FPS を使う
    t0 = rows[0]["uptime_s"]
    t_end = rows[-1]["uptime_s"]
    result["minutes"] = (t_end - t0) / 60.0
    for metric, key in (("private_mb", "private_slope"), ("vram_mb", "vram_slope")):
        xs, ys = numeric_pairs(rows, "uptime_s", metric)
        result[key] = slope_xy([(x - t0) / 3600.0 for x in xs], ys)  # MB/時間
    starts = [r["vram_mb"] for r in rows[:VRAM_START_ROWS] if isinstance(r.get("vram_mb"), float) and r["vram_mb"] >= 0]
    result["vram_start"] = min(starts) if starts else None
    result["vram_max"] = peak(rows, "vram_mb")
    result["private_max"] = peak(rows, "private_mb")
    result["stall_total"] = sum(1 for r in rows if is_stalled(r))
    k = 0
    while t0 + k * IDLE_WINDOW_SECONDS <= t_end:
        low, high = t0 + k * IDLE_WINDOW_SECONDS, t0 + (k + 1) * IDLE_WINDOW_SECONDS
        window = [r for r in rows if low <= r["uptime_s"] < high]
        if window:
            fps_rows = [r["fps"] for r in settled if low <= r["uptime_s"] < high and (not flow or r.get("state") == opening) and isinstance(r.get("fps"), float)]
            result["windows"].append({"minute": int(k * IDLE_WINDOW_SECONDS / 60.0), "private": window[-1]["private_mb"], "vram": window[-1]["vram_mb"],
                                      "vram_peak": peak(window, "vram_mb"), "fps": median(fps_rows) if len(fps_rows) >= STATE_MIN_ROWS else None,
                                      "stall": sum(1 for r in window if is_stalled(r))})
        k += 1
    procs = read_processes(folder)
    result["opening_entries"] = sum(1 for p in procs if p["end"] is not None and p["kind"] in ("Transition", "Reset") and p["to"] == "Opening")
    result["resets"] = sum(1 for p in procs if p["kind"] == "Reset")
    return result


def render_idle(results):
    """放置監視の集計。操作なしで Opening の繰り返しを測った記録で、周回とは別のデータ。"""
    out = ["放置監視の集計(操作なしで Opening の繰り返しを測った記録。周回とは別のデータ)", ""]
    ok = [r for r in results if not r.get("note")]
    for r in results:
        if r.get("note"):
            out.append("  %s: %s" % (r["folder"], r["note"]))
    if not ok:
        return "\n".join(out)
    out.append("起動ごとの要約(傾きは MB/時間。最初の窓 = 最初の 10 分)")
    out.append("  %-26s %7s %7s %-12s | %12s %10s | %9s %8s | %8s %8s | %9s %9s" % (
        "ラベル", "計画(分)", "実測(分)", "終了理由", "private傾き", "vram傾き", "開始VRAM", "最大VRAM", "FPS最初", "FPS最後", "200ms超(秒)", "Opening回"))
    warnings = []
    for r in ok:
        total = r.get("gpu_memory_mb")
        vram_text = ("%.0f%%" % (r["vram_max"] / total * 100.0)) if total and r.get("vram_max") is not None else "-"
        first_fps = r["windows"][0]["fps"] if r["windows"] else None
        last_fps = r["windows"][-1]["fps"] if r["windows"] else None
        out.append("  %-26s %7s %7.0f %-12s | %s %s | %s %8s | %s %s | %s %s" % (
            r["label"][:26], str(r["planned"] if r["planned"] is not None else "-"), r["minutes"], r.get("stop_reason") or "-",
            fmt_num(r["private_slope"], 12, 0, True), fmt_num(r["vram_slope"], 10, 0, True), fmt_num(r["vram_start"], 9), vram_text,
            fmt_num(first_fps, 8, 1), fmt_num(last_fps, 8, 1), fmt_num(r["stall_total"], 11), fmt_num(r["opening_entries"], 9)))
        if total and r.get("vram_max") is not None and r["vram_max"] >= total * VRAM_FULL_RATIO:
            warnings.append("  %s: VRAM が総量(%d MB)の %.0f%% に達した → 以降の増加は頭打ち(または性能の劣化)の可能性があります。" % (r["label"], total, r["vram_max"] / total * 100.0))
        if r.get("editor_running"):
            warnings.append("  %s: 起動時に Unity Editor が動いていました(meta.json の editor_running)→ vram_mb は、他の起動と比べないでください。" % r["label"])
        if r.get("clean_exit") is False:
            warnings.append("  %s: 正常に終了していません(clean_exit が false)。" % r["label"])
        if r.get("planned") and r["minutes"] < r["planned"] * 0.95 and r.get("stop_reason") not in ("vram_limit", "fps_drop"):
            warnings.append("  %s: 計画 %s 分に対して、%.0f 分で終わっています(終了理由: %s)。" % (r["label"], r["planned"], r["minutes"], r.get("stop_reason") or "不明"))
    for r in ok:
        out.append("")
        out.append("[%s] 10 分ごとの推移(窓の終わりの値。FPS は Opening 中の中央値)" % r["label"])
        out.append("  %6s %11s %9s %10s %8s %12s" % ("分", "private_mb", "vram_mb", "最大vram", "FPS", "カクつき(秒)"))
        for w in r["windows"]:
            out.append("  %6d %s %s %s %s %s" % (w["minute"], fmt_num(w["private"], 11), fmt_num(w["vram"], 9), fmt_num(w["vram_peak"], 10), fmt_num(w["fps"], 8, 1), fmt_num(w["stall"], 12)))
    out.append("")
    out.append("傾きは、全行の最小二乗法(MB/時間)。周回の傾き(MB/周)とは単位が違うので、数値を直接は比べない。vram は PC 全体の値(他のアプリを閉じて測る)。")
    if warnings:
        out.append("")
        out.append("注意")
        out.extend(warnings)
    return "\n".join(out)


def main():
    parser = argparse.ArgumentParser(description="自動周回の計測(run_lap_variants.bat)、または放置監視(--idle)の結果をまとめる(読み取り専用)")
    parser.add_argument("--root", default="", help="perf フォルダの場所(既定: 周回は %s、--idle のときは %s)" % (DEFAULT_ROOT, DEFAULT_IDLE_ROOT))
    parser.add_argument("--prefix", default="", help="集計する起動のラベルの先頭(既定: 周回は lap_、--idle のときは idle_)")
    parser.add_argument("--idle", action="store_true", help="放置監視(-perfidle)の記録を集計する(周回とは別のデータ)")
    parser.add_argument("--out", default="", help="結果をテキストファイルにも保存する")
    parser.add_argument("--log", action="store_true", help="周回の進み具合の行も出す(各起動のフォルダの Player.log。無ければ、直前の起動の Player.log)")
    parser.add_argument("--pc", default="", help="この文字列を CPU・GPU の名前に含む PC の記録だけを集計する(例: 4070)。大文字小文字は区別しない")
    args = parser.parse_args()

    root = args.root or os.environ.get("PERF_ROOT") or (DEFAULT_IDLE_ROOT if args.idle else DEFAULT_ROOT)
    prefix = args.prefix or ("idle_" if args.idle else "lap_")
    if not os.path.isdir(root):
        print("%s フォルダが見つかりません: %s" % ("idle" if args.idle else "perf", root))
        return 1
    groups = {}  # PC(CPU と GPU)ごとに、{"latest": 条件ごとの最新の記録, "history": 条件ごとの集計できた全記録(古い順), "idle": 放置監視の全記録}
    for name in sorted(os.listdir(root)):
        folder = os.path.join(root, name)
        if not os.path.isdir(folder):
            continue
        label = read_meta(folder).get("label") or name
        if not label.startswith(prefix):
            continue
        result = analyze_idle(folder) if args.idle else analyze_run(folder)
        if args.pc and args.pc.lower() not in " ".join(result["pc"]).lower():
            continue
        group = groups.setdefault(result["pc"], {"latest": {}, "history": {}, "idle": []})
        if args.idle:
            group["idle"].append(result)
            continue
        # 同じ PC の、同じ条件の起動が複数あれば、新しいものだけを残す(フォルダ名は、起動日時で並ぶ)。別の PC の記録は、同じ条件でも置き換えない。
        # 集計できなかった起動は、条件ごとの枠に入れない(正常な起動を上書きしない)
        if result.get("note"):
            key = ("note", result["folder"])
        else:
            key = (result["info"]["dlss_off"], result["info"]["dlss_map"], result["info"]["dlss_unload"], result["info"]["video_off"])
        group["latest"][key] = result
        if not result.get("note"):
            group["history"].setdefault(key, []).append(result)
    if not groups:
        print("ラベルが '%s' で始まる起動が見つかりません(%s)%s" % (prefix, root, ("。PC の指定: " + args.pc) if args.pc else ""))
        return 1

    blocks = []  # (見出し, 本文, 起動のフォルダ名の一覧)
    for pc in sorted(groups):
        group = groups[pc]
        if args.idle:
            runs = group["idle"]
            memory = next((r["gpu_memory_mb"] for r in runs if r.get("gpu_memory_mb")), None)
            header = "PC: %s / %s%s  (放置監視 %d 回)" % (pc[0], pc[1], (" / VRAM %d MB" % memory) if memory else "", len(runs))
            blocks.append((header, render_idle(runs), [r["folder"] for r in runs]))
            continue
        results = sorted(group["latest"].values(), key=sort_key)
        memory = next((r["gpu_memory_mb"] for r in results if r.get("gpu_memory_mb")), None)
        header = "PC: %s / %s%s  (条件 %d 通り)" % (pc[0], pc[1], (" / VRAM %d MB" % memory) if memory else "", len(results))
        body = render(results, {k: v for k, v in group["history"].items() if len(v) >= 2})
        blocks.append((header, body, [r["folder"] for r in results]))

    saved = []  # --out に保存する行

    def emit(line):
        saved.append(line)
        try:
            print(line)
        except UnicodeEncodeError:
            enc = sys.stdout.encoding or "utf-8"
            print(line.encode(enc, "replace").decode(enc))

    if len(blocks) > 1:
        emit("フォルダに %d 台の PC の記録が入っています。PC ごとに分けて集計します(別の PC の記録どうしは、同じ条件でも置き換えず、比べません。--pc で 1 台に絞れます)。" % len(blocks))
    for header, body, folders in blocks:
        if len(blocks) > 1:
            emit("")
            emit("=" * 100)
        emit(header)
        if len(blocks) > 1:
            emit("=" * 100)
        emit("")
        emit(body)
        if args.log:
            print_lap_log(root, folders, allow_legacy=(len(blocks) == 1))
    if args.out:
        with open(args.out, "w", encoding="utf-8") as f:
            f.write("\n".join(saved) + "\n")
        print("\n保存しました: " + os.path.abspath(args.out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
