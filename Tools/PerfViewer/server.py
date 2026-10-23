#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PerfViewer のサーバー(Python の標準ライブラリだけで動く)

ゲーム(-perf)が書き続ける CSV を読み、ブラウザに渡す。読み取り専用で、ファイルは作らない・変えない。
同じ PC のブラウザだけから見られるよう、127.0.0.1 でだけ待ち受ける。

使い方:
    python server.py                    既定の出力先を読む
    python server.py --root <perf の場所>
    python server.py --port 8765 --open

提供するもの:
    GET /                  viewer.html
    GET /api/runs          起動(計測 1 回分)の一覧
    GET /api/data?run=<フォルダ名>&since=<行番号>&limit=<件数>   since 行目以降の行(リアルタイムは since を進めて呼ぶ)
    GET /api/transitions?run=<フォルダ名>&since=<行番号>&limit=<件数>   遷移・ロード時間(transitions.csv)の since 行目以降の行。ファイルが無い起動は、行なしで返す
"""

import argparse
import json
import math
import os
import re
import sys
import threading
import time
import webbrowser
from datetime import datetime
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
VIEWER_HTML = os.path.join(HERE, "viewer.html")

COMPANY_NAME = "日本電子専門学校ゲーム制作研究科"  # ProjectSettings の companyName
PRODUCT_NAME = "AimRacing2026_Unity6.3"  # ProjectSettings の productName
DEFAULT_ROOT = os.path.join(os.path.expanduser("~"), "AppData", "LocalLow", COMPANY_NAME, PRODUCT_NAME, "perf")
DEFAULT_PORT = 8765
LOCAL_HOST = "127.0.0.1"  # 外には公開しない

INDEX_STEP = 1000  # 何行ごとにバイト位置を覚えるか(途中の行へ素早く飛ぶため)
READ_CHUNK_BYTES = 8 * 1024 * 1024  # 1 回に読むバイト数
DEFAULT_LIMIT = 20000  # 1 回の応答の既定の最大行数
MAX_LIMIT = 100000  # 1 回の応答の最大行数
LIVE_SECONDS = 5.0  # 最後の更新からこの秒数以内なら「記録中」とみなす
CSV_NAME = re.compile(r"^perf_\d{8}\.csv$")
TRANSITIONS_NAME = "transitions.csv"  # 遷移・ロード時間の記録(ゲーム側の PerfTransitionRecorder が書く。日では分けない)
TRANSITION_NUMERIC_COLUMNS = frozenset(["uptime_s", "seq", "ms", "gc0_d", "gc2_d", "ws_mb", "mono_mb"])  # 数値として返す列(それ以外は文字列)
FOLDER_DATE = re.compile(r"_(\d{8})_(\d{6})$")


def safe_print(text):
    """コンソールの文字コードで表せない文字があっても止まらないように表示する。"""
    try:
        print(text, flush=True)
    except UnicodeEncodeError:
        enc = sys.stdout.encoding or "utf-8"
        print(text.encode(enc, "replace").decode(enc), flush=True)


class FileIndex:
    """1 つの CSV(1 日分)の、行数と途中の行へ飛ぶための位置を持つ。書き込み中のファイルでも、最後まで書き終わった行だけを数える。"""

    def __init__(self, path, parser=None):
        self.path = path
        self.parser = parser or parse_perf_row  # (行の文字列, 列名のリスト) -> 値のリスト
        self.header = None  # 列名のリスト
        self.offsets = []  # offsets[k] = データ行 k*INDEX_STEP の先頭のバイト位置
        self.rows = 0  # データ行の数(空行は数えない)
        self.scanned = 0  # 数え終えたバイト位置(最後の改行の直後)

    def refresh(self):
        """前回の続きから末尾まで読み、行数と位置を更新する。"""
        try:
            size = os.path.getsize(self.path)
        except OSError:
            return

        if size < self.scanned:  # 作り直された場合は最初から数え直す
            self.header = None
            self.offsets = []
            self.rows = 0
            self.scanned = 0

        if size == self.scanned:
            return

        with open(self.path, "rb") as f:
            f.seek(self.scanned)
            offset = self.scanned
            carry = b""
            while True:
                data = f.read(READ_CHUNK_BYTES)
                if not data:
                    break
                data = carry + data
                last = data.rfind(b"\n")
                if last < 0:
                    carry = data
                    continue
                complete, carry = data[: last + 1], data[last + 1:]
                for line in complete.split(b"\n")[:-1]:
                    line_length = len(line) + 1
                    self._count_line(line, offset, line_length)
                    offset += line_length
                self.scanned = offset

    def _count_line(self, line, offset, line_length):
        text = line.decode("utf-8", "replace").rstrip("\r")
        if self.header is None:
            if text.startswith("﻿"):
                text = text[1:]
            self.header = text.split(",")
            self.offsets.append(offset + line_length)  # データ行 0 の先頭
            return
        if not text:
            return
        self.rows += 1
        if self.rows % INDEX_STEP == 0:
            self.offsets.append(offset + line_length)  # データ行 self.rows の先頭

    def read_range(self, local_since, limit):
        """このファイルの local_since 行目から最大 limit 行を、値のリストにして返す。"""
        if self.header is None or local_since >= self.rows or limit <= 0:
            return []

        index = local_since // INDEX_STEP
        position = self.offsets[index]
        skip = local_since - index * INDEX_STEP
        out = []

        with open(self.path, "rb") as f:
            f.seek(position)
            for line in iter(f.readline, b""):
                position += len(line)
                if position > self.scanned:
                    break
                text = line.decode("utf-8", "replace").strip("\r\n")
                if not text:
                    continue
                if skip > 0:
                    skip -= 1
                    continue
                out.append(self.parser(text, self.header))
                if len(out) >= limit:
                    break
        return out


def parse_row(text, columns):
    """CSV の 1 行を、[日時の文字列, 数値, 数値, ...] にする。数値にできない値は None。"""
    parts = text.split(",")
    row = [parts[0]]
    for part in parts[1:columns]:
        try:
            value = float(part)
            row.append(value if math.isfinite(value) else None)
        except ValueError:
            row.append(None)
    while len(row) < columns:
        row.append(None)
    return row


def parse_perf_row(text, header):
    """perf_*.csv の 1 行を、列名の数に合わせて値のリストにする。"""
    return parse_row(text, len(header))


def parse_transition_row(text, header):
    """transitions.csv の 1 行を、値のリストにする。数値の列は数値(取得できなかった -1 はそのまま -1)、それ以外は文字列。"""
    parts = text.split(",")
    row = []
    for index, name in enumerate(header):
        part = parts[index] if index < len(parts) else ""
        if name in TRANSITION_NUMERIC_COLUMNS:
            try:
                value = float(part)
                row.append(value if math.isfinite(value) else None)
            except ValueError:
                row.append(None)
        else:
            row.append(part)
    return row


class RunIndex:
    """1 回の起動(1 フォルダ)の、日ごとの CSV と、遷移・ロード時間の CSV をまとめて扱う。"""

    def __init__(self, folder):
        self.folder = folder
        self.files = {}  # ファイル名 -> FileIndex
        self.transitions = None  # transitions.csv の FileIndex(ファイルが無ければ None)
        self.lock = threading.Lock()

    def refresh(self):
        try:
            names = sorted(n for n in os.listdir(self.folder) if CSV_NAME.match(n))
        except OSError:
            names = []
        for name in names:
            if name not in self.files:
                self.files[name] = FileIndex(os.path.join(self.folder, name))
        for name in list(self.files):
            if name not in names:
                del self.files[name]
        for name in names:
            self.files[name].refresh()
        self._refresh_transitions()

    def _refresh_transitions(self):
        path = os.path.join(self.folder, TRANSITIONS_NAME)
        if not os.path.isfile(path):
            self.transitions = None
            return
        if self.transitions is None:
            self.transitions = FileIndex(path, parse_transition_row)
        self.transitions.refresh()

    def total(self):
        return sum(fi.rows for fi in self.files.values())

    def transitions_total(self):
        return self.transitions.rows if self.transitions else 0

    def transitions_header(self):
        return (self.transitions.header or []) if self.transitions else []

    def read_transitions(self, since, limit):
        """transitions.csv の since 行目以降を、最大 limit 行返す(ファイルが無ければ空)。"""
        return self.transitions.read_range(since, limit) if self.transitions else []

    def header(self):
        for name in sorted(self.files):
            if self.files[name].header:
                return self.files[name].header
        return []

    def read(self, since, limit):
        rows = []
        base = 0
        for name in sorted(self.files):
            fi = self.files[name]
            if len(rows) < limit and since < base + fi.rows:
                rows.extend(fi.read_range(max(0, since - base), limit - len(rows)))
            base += fi.rows
        return rows

    def updated(self):
        """最後に更新された時刻(エポック秒)。CSV が無ければ meta.json かフォルダの時刻。"""
        latest = 0.0
        candidates = [os.path.join(self.folder, n) for n in self.files] or [self.folder]
        for path in candidates:
            try:
                latest = max(latest, os.path.getmtime(path))
            except OSError:
                pass
        return latest


class Registry:
    """起動(フォルダ)ごとの RunIndex を覚えておく。"""

    def __init__(self, root):
        self.root = root
        self.runs = {}
        self.lock = threading.Lock()

    def valid_name(self, name):
        if not name or name in (".", "..") or os.path.basename(name) != name or "/" in name or "\\" in name:
            return False
        return os.path.isdir(os.path.join(self.root, name))

    def get(self, name):
        with self.lock:
            run = self.runs.get(name)
            if run is None:
                run = RunIndex(os.path.join(self.root, name))
                self.runs[name] = run
            return run

    def list_runs(self):
        out = []
        try:
            entries = [e for e in os.scandir(self.root) if e.is_dir()]
        except OSError:
            return out
        for entry in entries:
            run = self.get(entry.name)
            with run.lock:
                run.refresh()
                meta = read_meta(os.path.join(entry.path, "meta.json"))
                start = (meta or {}).get("start") or folder_start(entry.name)
                updated = run.updated()
                out.append({
                    "name": entry.name,
                    "label": (meta or {}).get("label") or entry.name,
                    "start": start,
                    "rows": run.total(),
                    "transition_rows": run.transitions_total(),
                    "files": sorted(run.files),
                    "updated": updated,
                    "live": (time.time() - updated) < LIVE_SECONDS and run.total() > 0,
                    "meta": meta,
                })
        out.sort(key=lambda r: r["start"] or "", reverse=True)
        return out


def read_meta(path):
    try:
        with open(path, "r", encoding="utf-8-sig") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def folder_start(name):
    m = FOLDER_DATE.search(name)
    if not m:
        return ""
    try:
        return datetime.strptime(m.group(1) + m.group(2), "%Y%m%d%H%M%S").strftime("%Y-%m-%dT%H:%M:%S")
    except ValueError:
        return ""


class Handler(BaseHTTPRequestHandler):
    server_version = "PerfViewer/1.0"
    registry = None
    port = DEFAULT_PORT

    def log_message(self, fmt, *args):  # 1 秒ごとの問い合わせでコンソールが埋まらないようにする
        pass

    def _host_ok(self):
        host = (self.headers.get("Host") or "").lower()
        return host in ("%s:%d" % (LOCAL_HOST, self.port), "localhost:%d" % self.port)

    def _send(self, status, body, content_type):
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def _send_json(self, payload, status=200):
        body = json.dumps(payload, separators=(",", ":"), allow_nan=False).encode("utf-8")
        self._send(status, body, "application/json; charset=utf-8")

    def do_GET(self):
        if not self._host_ok():
            self._send(403, b"forbidden", "text/plain")
            return

        url = urlparse(self.path)
        try:
            if url.path in ("/", "/viewer.html"):
                with open(VIEWER_HTML, "rb") as f:
                    self._send(200, f.read(), "text/html; charset=utf-8")
            elif url.path == "/api/runs":
                self._send_json({"root": self.registry.root, "runs": self.registry.list_runs()})
            elif url.path == "/api/data":
                self._handle_data(parse_qs(url.query))
            elif url.path == "/api/transitions":
                self._handle_transitions(parse_qs(url.query))
            elif url.path == "/favicon.ico":
                self._send(204, b"", "image/x-icon")
            else:
                self._send(404, b"not found", "text/plain")
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception as exc:  # 想定外の失敗は、サーバーを止めずに知らせる
            try:
                self._send_json({"error": str(exc)}, 500)
            except OSError:
                pass

    def _handle_data(self, query):
        name = (query.get("run") or [""])[0]
        if not self.registry.valid_name(name):
            self._send_json({"error": "run が見つかりません"}, 404)
            return
        try:
            since = max(0, int((query.get("since") or ["0"])[0]))
            limit = min(MAX_LIMIT, max(1, int((query.get("limit") or [str(DEFAULT_LIMIT)])[0])))
        except ValueError:
            self._send_json({"error": "since と limit は整数で指定します"}, 400)
            return

        run = self.registry.get(name)
        with run.lock:
            run.refresh()
            rows = run.read(since, limit)
            total = run.total()
            header = run.header()
            live = (time.time() - run.updated()) < LIVE_SECONDS
        self._send_json({"run": name, "columns": header, "since": since, "rows": rows, "total": total, "live": live})

    def _handle_transitions(self, query):
        name = (query.get("run") or [""])[0]
        if not self.registry.valid_name(name):
            self._send_json({"error": "run が見つかりません"}, 404)
            return
        try:
            since = max(0, int((query.get("since") or ["0"])[0]))
            limit = min(MAX_LIMIT, max(1, int((query.get("limit") or [str(DEFAULT_LIMIT)])[0])))
        except ValueError:
            self._send_json({"error": "since と limit は整数で指定します"}, 400)
            return

        run = self.registry.get(name)
        with run.lock:
            run.refresh()
            rows = run.read_transitions(since, limit)
            total = run.transitions_total()
            header = run.transitions_header()
        self._send_json({"run": name, "columns": header, "since": since, "rows": rows, "total": total})


def main():
    parser = argparse.ArgumentParser(description="PerfViewer のサーバー(読み取り専用。127.0.0.1 だけで待ち受ける)")
    parser.add_argument("--root", default=os.environ.get("PERF_ROOT", DEFAULT_ROOT), help="perf フォルダの場所(既定: %(default)s)")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="ポート番号(既定: %(default)s)")
    parser.add_argument("--open", action="store_true", help="起動後にブラウザを開く")
    args = parser.parse_args()

    root = os.path.abspath(args.root)
    Handler.registry = Registry(root)
    Handler.port = args.port

    try:
        server = ThreadingHTTPServer((LOCAL_HOST, args.port), Handler)
    except OSError as exc:
        safe_print("サーバーを起動できません(ポート %d が使用中の可能性があります): %s" % (args.port, exc))
        safe_print("別のポートを --port で指定してください。")
        return 1

    server.daemon_threads = True
    url = "http://%s:%d/" % (LOCAL_HOST, args.port)
    safe_print("PerfViewer を起動しました: " + url)
    safe_print("読み取り先: " + root + ("" if os.path.isdir(root) else "  (まだ存在しません。ゲームを -perf で起動すると作られます)"))
    safe_print("止めるときは Ctrl+C")
    if args.open:
        threading.Timer(0.5, lambda: webbrowser.open(url)).start()

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
