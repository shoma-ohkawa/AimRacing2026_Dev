# 計測(パフォーマンス記録・周回・放置監視)

作成: 2026/10/16(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/Perf/`、`Assets/#Scripts/GameManager/Flow/FlowTiming.cs`、`Tools/PerfViewer/`

リリースビルドを起動引数付きで動かし、FPS・CPU・GPU・メモリ・GC と、遷移・ロード時間を記録する。記録を集計・グラフにして、変更前後の比較と、展示の連続稼働での劣化の検知に使う。

```
 ゲーム(リリースビルド、-perf)                               同じ PC
  PerfCsvRecorder ──── 1 秒ごとに 1 行 ────▶ perf_日付.csv ──┐
  PerfTransitionRecorder ── 処理ごと ─────▶ transitions.csv ─┼─▶ lap_report.py(集計)
  PerfAutoLapRunner(周回 / 放置、終了条件)                   └─▶ server.py → viewer.html(グラフ)
```

- 起動引数 `-perf` がなければ何もしない(本番の動作を変えない)。
- **周回**(`-perfautolap`)と**放置**(`-perfidle`)は、別のデータ。出力先も集計も分ける。

## 構成

| ファイル | 役割 |
|---|---|
| `Perf/PerfCsvRecorder.cs` | 記録の本体。約1秒ごとに1行を CSV に追記し、`meta.json` を書く。`state`・`lap`・`trans` 列と、1行ごとの通知(`OnRow`) |
| `Perf/PerfNativeMetrics.cs` | Windows API と NVML から、CPU 使用率・メモリ・GPU 使用率・VRAM・温度を取る。Unity Editor の起動も検出する |
| `Perf/PerfCsvFormat.cs` | 数値や日時を、ガベージを出さずに文字列にする |
| `Perf/PerfTransitionRecorder.cs` | `FlowTiming` の通知を受けて、遷移・ロード時間を `transitions.csv` に書く |
| `Perf/PerfAutoLapRunner.cs` | 自動周回と放置監視を始める。DLSS・映像を止める。終了条件(`-perfuntil`)を判定する |
| `GameManager/Flow/FlowTiming.cs` | 遷移エンジンの各ステップの所要時間を通知するだけ。購読者がいなければ何もしない |
| `Tools/PerfViewer/run_lap_variants.bat` | 周回を、条件ごとに無人で回す。`Player.log` を保存し、集計まで行う。引数なしならコンソールで設定を聞く |
| `Tools/PerfViewer/run_idle.bat` | 放置監視を、条件ごとに無人で回す |
| `Tools/PerfViewer/run_exhibition_data.bat` | 展示用 PC で足りないデータ(`base`・`maponly` の 30 周、放置の `base` 120 分)を、上の 2 本を呼んで続けて取る。`pc:NAME` で、新しい起動フォルダを `PerfRecords` へコピーする |
| `Tools/PerfViewer/lap_report.py` | 周回(`--idle` で放置)の結果を集計する。PC ごとに分ける |
| `Tools/PerfViewer/server.py`、`viewer.html` | CSV を折れ線グラフにする(`127.0.0.1` のみ、読み取り専用) |
| `Tools/PerfViewer/start_viewer.bat` | `server.py --open` を起動してブラウザを開く(`--root`・`--port` はそのまま渡せる) |
| `Tools/PerfViewer/run_game_perf.bat` | ビルドのフォルダにコピーして、`-perf` 付きでゲームを起動する(`run_game_perf.bat [ラベル] [ビルドのフォルダ]`) |
| `Tools/PerfViewer/collect_result.bat`(`.ps1`) | 新しい記録(既定は最新の1つ。`-Last N`、`-Root <perf>`)を、列ごとの `-1` と 0 の行数つきで、1つのテキストにまとめる。読み取り専用で、`perf_result_<日時>.txt` として `perf` フォルダの1つ上にも保存する |

本体側のフック: `GameFlowRunner.cs`(`FlowTiming` の呼び出し 9 か所)、`SessionCacheRefresher.cs`(3 か所)、`GameStateBindingBase.cs`(`Bootstrap` へ計測用の秒数を渡す)、`DebugKey.cs`(`KeysLocked`)、`AutoDemoDriver.cs`(`CurrentLap`、`LastLoopCompleted`)。

## 使い方

### 準備

- Unity で **Development Build を OFF** にしてビルドする。Player Settings の **Frame Timing Stats** を有効にする(無効だと CPU・GPU の時間が `-1`)。
- 計測の前に **Unity Editor を閉じ**、他の GPU アプリも閉じる(`vram_mb` は PC 全体の値。Editor で約 3.2 GB 増える)。Editor が動いていても中断はせず、`meta.json` の `editor_running` と `start_vram_mb` に記録する。
- 計測中はキーボードに触らない(`-perfautolap`・`-perfidle` のとき、Esc 以外のデバッグキーは無効)。

### 通常の計測

```
.\<ビルド名>.exe -perf -perflabel baseline
```

- 保存先(既定): `%USERPROFILE%\AppData\LocalLow\日本電子専門学校ゲーム制作研究科\AimRacing2026_Unity6.3\perf\<ラベル>_<起動日時>\`(`-perfout` で変更)。
- 開始時に `Player.log` へ `[PerfCsvRecorder] 記録を開始しました: <出力先>` が1行出る。
- 止め方は普通の終了(Esc)。`meta.json` の `clean_exit` が `true` になる。強制終了でも、書き終えた行までの CSV は残る(`clean_exit` は `false`)。

### 周回

```
Tools\PerfViewer\run_lap_variants.bat [周回数] [条件] [xN] [until] [build:DIR]
```

- 引数なしで起動すると、周回数・条件・ラウンド数・`until`・ビルドの場所をコンソールで聞く(Enter で既定値)。開始前に、同じ実行になる引数つきのコマンドを表示する。
- 流れ: 起動 → Splash が見えて5秒後に自動周回を始める(U キーと同じ。Opening・Menu の自動入力 → Racing の自動走行 → Result。各周の Result は3秒後に Enter で抜ける)→ 最後の Result → Load → Refresh を55秒待つ → 終了。1周は約4分。1周が900秒で終わらなければ中止する。
- 既定: 10周 × 3ラウンド × 4条件 = 12本(約8時間)。`xN` は、同じ条件を回すラウンド数。条件は順に回す(順序と時刻の影響を、条件に偏らせないため)。
- `-perf` で起動して、手動で U キーを押しても、10周を回せる。

### 放置

```
Tools\PerfViewer\run_idle.bat [分] [条件] [xN] [until] [build:DIR]
```

- Splash が見えて5秒後から、操作なしで Opening の繰り返し(PV のループ)を測る。曲間の Splash 挿入と、約90.8秒での Load への折り返しを含み、約105秒周期で Load → Splash → Opening に戻る。
- 既定: 120分 × 4条件(約8時間)。出力先は `...\idle\`。

### 条件(周回・放置とも)

| 条件 | ラベル | 追加される引数 | 動作 |
|---|---|---|---|
| `base` | `lap_base` / `idle_base` | なし | 何も止めない(基準) |
| `nodlss` | `lap_nodlss` / `idle_nodlss` | `-perfnodlss` | 全シーンのカメラの DLSS を止める。原因の確認用の対照 |
| `maponly` | `lap_dlssmap` / `idle_dlssmap` | `-perfdlssmaponly` | `Map` シーンのカメラだけ DLSS を残し、他のシーンでは止める |
| `unloadoff` | `lap_dlssunload` / `idle_dlssunload` | `-perfdlssoffbeforeunload` | DLSS は全シーンで有効のまま、遷移・リセットの開始時に、離れるシーンのカメラだけ止める |

- `-perfnodlss` が最優先。あれば、`-perfdlssmaponly` と `-perfdlssoffbeforeunload` は無効になる。
- 映像(Timeline)を止める `-perfnovideo` は、ゲーム側に残っている。メモリ増加の主因でないと分かったため(下の PDCA)、2026/10/22 に bat の条件から外した。

### 終了条件(`until`)

基本は、固定の周回数・時間で測る。`until` を付けると、次のどちらかに達した時点で終える(周回数・分は上限になる)。

- VRAM が、GPU の総量(`SystemInfo.graphicsMemorySize`)の 99% 以上(1行=1秒の値で判定。ロード時の一瞬のピークでも終わる)。
- FPS の中央値が、基準の 8 割未満。周回は、周ごとの走行中(`state` が Racing、`trans` が 0)の中央値を、1周目と比べる。放置は、Opening 中の中央値を10分ごとに出し、最初の10分と比べる。

終えた理由は `meta.json` の `stop_reason`(`completed`・`time_limit`・`vram_limit`・`fps_drop`・`no_splash`・`loop_not_started`・`loop_cancelled`・`quit`)。

### 集計とグラフ

```
python Tools/PerfViewer/lap_report.py --root "PerfRecords/<PC>/perf" [--log] [--out 保存先] [--pc 名前の一部]
python Tools/PerfViewer/lap_report.py --idle --root "PerfRecords/<PC>/idle"
```

- 集計は PC ごとに分ける(`meta.json` の CPU と GPU で見分ける)。別の PC の記録は、同じ条件でも置き換えず、比べない。
- `--log`: 各起動のフォルダに保存された `Player.log` から、周回の進み具合の行を出す。
- グラフ: `Tools\PerfViewer\start_viewer.bat`(`python server.py --open [--root <perf または idle>] [--port <番号>]`)→ `http://127.0.0.1:8765/`。起動ごとに色分けして重ね、0秒を揃えて比べられる。記録中の起動は、1秒ごとに追従する。

### 回収先

プロジェクト直下の `PerfRecords\<PC>\perf\`(周回)と `...\idle\`(放置)に、起動のフォルダごとコピーする。`5600X_3060Ti`・`13600K_4070Ti`・`Ultra9_5060Ti`(展示用)の3台。

### 展示用 PC のまとめ取り

```
Tools\PerfViewer\run_exhibition_data.bat [lap] [idle] [pc:NAME] [build:DIR] [test] [check]
```

VRAM が上限に達した後に FPS が落ち始める周と、来場者がいない間の増え方を、無人で続けて取る。`run_lap_variants.bat` と `run_idle.bat` を呼ぶだけで、ラベル、`Player.log` の保存、集計の出力は変わらない。

| 順 | 内容 | 呼ぶ bat | 時間の目安 |
|---|---|---|---|
| 1 | `base` を 30 周(`until` なし) | `run_lap_variants.bat 30 base x1` | 約 2 時間 |
| 2 | `maponly` を 30 周(`until` なし) | `run_lap_variants.bat 30 maponly x1` | 約 2 時間 |
| 3 | 放置の `base` を 120 分 | `run_idle.bat 120 base x1` | 2 時間 |

- `until` を付けない。付けると VRAM が総量の 99% に達した時点で止まり、その先で FPS が落ち始める周が見えない。
- 合計は約 6 時間。FPS が崩れて周が遅くなると延びる(1 周が 900 秒で終わらなければ中止)。

| 引数 | 内容 |
|---|---|
| `lap` / `idle` | 順 1・2 だけ / 順 3 だけ。どちらもなければ 3 つとも |
| `pc:NAME` | 終了後に、今回できた起動フォルダだけを `PerfRecords\NAME\perf\` と `...\idle\` へコピーする。元のフォルダは残す。なければコピーしない。`NAME` は英数字・`_`・`.`・`-` のみ(`..` は不可) |
| `build:DIR` | ビルドのフォルダ(既定は 2 本と同じ) |
| `test` | 短い試走(`base`・`maponly` を 1 周ずつ、放置 3 分。約 11 分)。コピーしない |
| `check` | 計画とゲームの場所だけを表示する。何も起動しない |

- 先に `test` で通す。試走の起動フォルダはゲームの出力先に残るので、同じ場所を `lap_report.py` で集計すると混ざる。
- 呼ばれる 2 本は最後に `pause` で止まるので、空行を流し込んで進める。
- 実行前の起動フォルダの一覧と比べて、今回できたフォルダを見分ける。`meta.json` の `stop_reason` と `clean_exit` を表示する。
- コピーは `robocopy`。起動フォルダごとに、元と先のファイル数が一致するかを確認して表示する。
- 実行の記録(開始・終了の時刻、呼んだコマンド、新しい起動フォルダ)は `%TEMP%\run_exhibition_data_日時.log`。
- Unity Editor(`Unity.exe`)が動いていると警告する。中断はしない。

確認: ゲームの代わりのスタブ 2 本で、引数の受け渡し、`pause` を止めないこと、新しい起動フォルダだけのコピー、ファイル数の確認、`pc:` の不正な名前の拒否を確認した(2026/10/27)。**本物のゲームでの実行は未確認。**

## 起動引数

| 引数 | 内容 |
|---|---|
| `-perf` | 計測を有効にする(なければ何もしない) |
| `-perflabel <名前>` | 計測の名前。出力フォルダ名に使う(既定: `run`) |
| `-perfout <フォルダ>` | 出力先(既定: `perf`。`-perfidle` のときは `idle`) |
| `-perfautolap <周回数>` | 自動周回(既定10) |
| `-perfidle <分>` | 放置監視(既定120)。`-perfautolap` があれば、そちらが優先 |
| `-perfuntil` | 終了条件に達した時点で終える |
| `-perfquit` | 終わったら、アプリを終了する |
| `-perfnodlss` / `-perfdlssmaponly` / `-perfdlssoffbeforeunload` / `-perfnovideo` | DLSS・映像を止める(上の条件) |

## 記録の形式

### 出力フォルダ

`<ラベル>_<起動日時>\` の中に、`meta.json`(条件。終了時に `clean_exit` が `true`)、日ごとの `perf_yyyyMMdd.csv`、`transitions.csv`、`Player.log`(bat で回した起動だけ)。文字コードは UTF-8(BOM なし)、区切りはカンマ、小数は `.`。容量は1日約12 MB(1行約136 B。実記録の130 B/行 + 新しい3列)。

### perf_*.csv(約1秒ごとに1行。取得できない値は `-1`)

| 列 | 内容 | 取り方 |
|---|---|---|
| `wall` / `uptime_s` | 行を書いた時刻 / 起動からの秒数 | `DateTime.Now` / `Time.realtimeSinceStartupAsDouble` |
| `fps` / `frame_ms_avg` / `frame_ms_max` | FPS / フレーム時間の平均 / 最大(カクつきの検出) | `Time.unscaledDeltaTime` |
| `cpu_main_ms` / `cpu_render_ms` | メイン / レンダースレッドの CPU 時間 | `FrameTimingManager` |
| `cpu_process_pct` | 本アプリの CPU 使用率(全論理コア合計に対する割合) | `GetProcessTimes` |
| `gpu_ms` | GPU 時間の平均(取れないフレームは除く) | `FrameTimingManager` |
| `gpu_pct` / `vram_mb` / `gpu_temp_c` | GPU 使用率 / VRAM 使用量(PC 全体) / 温度 | NVML |
| `ws_mb` / `private_mb` | 物理メモリ / 本アプリ専用のコミット済みメモリ | `GetProcessMemoryInfo` |
| `unity_alloc_mb` / `unity_reserved_mb` | Unity が確保して使用中 / 予約しているメモリ | `Profiler` |
| `gc0` | GC の回数(起動からの累計) | `GC.CollectionCount` |
| `mono_used_mb` / `mono_heap_mb` | マネージドヒープの使用量 / 確保量 | `Profiler` |
| `state` | 最後に FadeIn が完了した State の番号(`GameStateId` の値。まだ不明なら -1) | `GameFlowRunner.OnStateFadeInComplete` |
| `lap` | 周回の番号(周回の繰り返し中でなければ 0) | `AutoDemoDriver.CurrentLap` |
| `trans` | 起動・遷移・リセット・リフレッシュの最中なら 1 | `FlowTiming` |

- 2026/10/22 に、`gc1`・`gc2`(`gc0` と全行で同じ値)を削除し、`state`・`lap`・`trans` を足した(`schema` 2)。それ以前の記録に、この3列はない。
- `meta.json` の主な項目: `label`、`start`、`arguments`、`mode`(manual / lap / idle)、`planned`、`until`、`stop_reason`、`start_vram_mb`、`editor_running`、`state_names`、`development_build`、`scripting_backend`、`cpu`、`gpu`、`gpu_memory_mb`、`nvml_available`、`frame_timing_enabled`、`screen`、`fullscreen_mode`、`displays`、`display_sizes`、`clean_exit`。比べる計測の環境が同じかを、後から確かめるために使う。

### transitions.csv(遷移・ロード時間)

1行が1ステップ。同じ `seq` の行が、1つの処理(`START` から `TOTAL`)。`TOTAL` のない `seq` は、中断された処理で、集計に使わない。列: `wall`、`uptime_s`、`seq`、`kind`、`from`、`to`、`step`、`detail`、`type`、`ms`、`gc0_d`、`gc2_d`、`ws_mb`、`mono_mb`。

- 処理の種類(`kind`): `Boot`(起動)、`Transition`(通常の遷移)、`Reset`(緊急リセット)、`Refresh`(Load でのリフレッシュ)。
- 各ステップは `work`(ロード・Exit・Enter・描画の安定待ち `StableFrames` など、実装で変わる作業)と `wait`(フェード・保持秒数など、設定で決まる待ち)に分ける。**エンジンの比較は `work` の合計で見る。**
- 主なステップ: `FadeOut`(wait)、`Load` / `LoadingFadeIn`、`Exit`、`Load` / `Unload`(シーンごと)、`Activate`、`Enter`、`StableFrames`、`Keep`(wait)、`LoadingFadeOut`(wait)、`FadeIn`(wait)、`EngineStartup`(Boot のみ)、`ResetState` / `GC1` / `UnloadUnusedAssets` / `GC2`(Refresh)。名前は `GameFlowRunner.cs`・`SessionCacheRefresher.cs` の定数と一致させる。
- `End` は `OnStateFadeInComplete` の発火より前に通知する。従来の `[LoadTiming]` の合計とは一致しない。
- 1つの処理に溜められるステップは64件まで。ファイルへは、処理が終わった時に1度だけ書く(遷移の最中の I/O を避ける)。

## 集計の目安(`lap_report.py`)

| 項目 | 目安 |
|---|---|
| 「増える」と判定 | `private_mb` が 40 MB/周 以上 |
| 周の値 | Result → Load の2秒後(解放後の残り) |
| FPS が「崩れた」周 | 1周目の8割を下回った最初の周 |
| カクつき | `frame_ms_max` が 200 ms を超えた秒 |
| VRAM の警告 | 最大 VRAM が総量の 97% を超えた / 開始時の VRAM が同じ PC の他の起動より 1,000 MB 以上大きい |
| State 別の FPS | 最初の5周。`state`・`lap`・`trans` 列があれば、それで集計(落ち着いた行だけ)。なければ `transitions.csv` から推定(旧記録) |

出力: 起動ごとの要約(増え方、FPS、崩れた周、カクつき、開始 VRAM、判定、終了理由)、周ごとの表(FPS、`private_mb`・`vram_mb`・`unity_alloc_mb`、最大値、カクつき、Menu → Intro の `work`)、State 別の FPS、機能ごとの比較(基準に対する減少)、同じ条件の複数回(傾きの中央値と範囲)、読み方の目安、注意。`--idle` は、時間あたりの増え方(MB/時間)、10分ごとの推移、終了理由、Opening への遷移回数。

## 設計の要点と制約

- フレームごとの値は保存せず、約1秒で集約する。複数日の連続稼働でも、メモリが増えない。
- 1行ごとに `Flush` する(共有を許可して追記)。ビューアが読んでいても書け、強制終了でも直前の行まで残る。日が変わると、新しい CSV に切り替える。
- 計測中に文字列やオブジェクトを作らない(GC の計測を汚さないため)。数値は `PerfCsvFormat` が直接書く。
- 書き込みに失敗したら、閉じて次の行で開き直す(警告は最初の1回)。
- ログは `AppLog` ではなく生の `Debug.Log` / `LogWarning`(`AppLog.Log` はリリースで除去されるため)。
- 遷移エンジンは、計測側の存在を知らない。`Perf` を削除しても壊れない。
- サーバーは `127.0.0.1` だけで待ち受け、読み取り専用。`Host` が `127.0.0.1:<ポート>` / `localhost:<ポート>` でない要求と、`..`・`\` を含む `run` の名前を拒否する。
- 環境検査(Unity Editor の起動、開始時の VRAM)は、記録だけ。中断はしない。

## 限界

- 1秒より短いカクつきは、`frame_ms_max` にだけ残る。
- `gpu_ms` は `FrameTimingManager` の値で、取れないフレームは0で返る(平均から除く)。1秒に1つも取れなければ `-1`。
- GPU の値は NVML(NVIDIA のみ)。`vram_mb` は PC 全体の値。`gfx_driver_mb` はリリースで0を返すので記録しない。
- `transitions.csv` の秒数は、フレーム単位(60 fps で約17 ms)。非同期ロードは、同じフレームの他の処理の影響を受ける。起動後の最初の遷移は遅い(ディスクのキャッシュ)。`wait` は、フレームが止まると実時間で伸びる。
- 条件を順に回すので、順序と時刻の影響が混ざりうる(`xN` のラウンドで偏らせない)。

## 確認状況

| 確認 | 内容 |
|---|---|
| 実機 | 開発用 PC 2台と展示用 PC で、リリースビルドの `-perf` が記録できた(`clean_exit` が `true`、`transitions.csv` あり)。周回は、3060 Ti と展示用 PC で5条件 × 10周。2026/10/22 に、展示用 PC のリリースビルドで `-perfuntil`(`stop_reason` が `vram_limit`)と新しい列(`state`・`lap`・`trans`)の値を記録できた(4 本の CSV。`PerfFindings.md` の追記) |
| 2026/10/22 の変更 | Unity の DLL を参照した単体コンパイル(Windows 分岐・非 Windows 分岐とも、エラー0)。新しい列の集計は、旧推定と一致。バッチは、ゲームの代わりの exe で、引数・ラウンドの順序・対話・再入力を確認 |

**未確認**
- Unity 本体でのコンパイル。
- 実機での `-perfidle`、`-perfuntil` の FPS 低下による終了(`fps_drop`。VRAM 上限による終了は確認済み)、Unity Editor の検出(プロセス名 `Unity`)。
- `state`・`lap`・`trans` 列の値を、`transitions.csv` から推定した値と突き合わせること(値が入っていることは確認済み)。
- 展示用 PC の記録での、列ごとの `-1` の行数の確認(`collect_result.bat -Root <perf>` で確認できる)。
- 計測自身の負荷(所要時間への影響。Unity の Mono 上のメモリ確保)。
- 0時をまたぐ複数日の記録。ビューアのブラウザのメモリ(1週間以上)。

## 取り除くとき

- 記録をやめるだけなら、`-perf` を付けない。
- コードを外すなら、`Assets/#Scripts/Perf/` を `.meta` ごと削除する。遷移エンジン側も外すなら、`FlowTiming.cs` と、その呼び出し(`GameFlowRunner`、`GameStateBindingBase` の `Bootstrap` への引数、`SessionCacheRefresher`)を外す。`Tools/PerfViewer/` も、不要なら削除する。
- 取った記録は、`...\perf\`・`...\idle\` の下を削除する。

## PDCA

担当: 9名のチーム制作のうち、計測の仕組み(記録・周回・放置・集計・グラフ)。期間: 2026/10/16〜。

| | 内容 |
|---|---|
| 目標 | ① 変更前後を、同じ条件で比べられる ② 展示の連続稼働での劣化を見つけられる ③ リリースビルドで測り、計測自身の負荷が小さい ④ 来場者のプレイを模した周回と、来場者がいない間の放置を、無人で回せる ⑤ ばらつき・環境の汚染・粒度を扱え、より正確。達成の基準の数値は未記載 |
| 目標の基準(運用条件) | 2026/10/27 に追記。上の目標の「達成の基準の数値は未記載」を、この行で補う。展示(TGS2026)の運用は、次のとおり。稼働は1日7〜8時間 × 4日間 / 来場者は毎時20人前後(1日140〜160プレイ、4日間で560〜640プレイ。1プレイ約3分。換算なので推定) / 走行中の FPS は50以上 / 再起動は都度行ってよい。自動再起動の仕組みはなく、手動で行う(FPS の落ち方は見た目で判断) / 過去の展示では、FPS が1桁まで落ちたら手動で再起動した。起きたのは5〜8プレイのあたり(回数は不正確。どの PC・ビルドの展示かは未確認)。この過去の数字は、現行の展示用 PC の実測(`base` は8周で VRAM 99%、FPS はまだ崩れていない: `PerfFindings.md` の追記)と同じものとして扱わない。未確認: 1台で毎時20人にできるか(自動周回の1周は約4分で、Racing だけで約171秒。台数と実際のプレイ時間は未確認) |
| 未記載 | 目標を置いたきっかけ。目標 ①③ は旧 docs の設計から、②⑤ は 2026/10/22 の決定から整理した |
| 課題 | 施策ごとの FPS の効果が切り分けられていない(各条件1枚のスクリーンショット) / 遷移のどこに時間がかかるか分からない / 周回でメモリと VRAM が増え、FPS が崩れる / 毎フレームの `Debug.Log` で `Player.log` が10周で73.5 MB / Unity Editor で VRAM が約3.2 GB増え、比較が汚れる / 別の PC の記録が混ざる / 条件を手で回す手間 / 放置中の記録がない |
| 検証・結果 | 3PCで確認した(実測。数値と課題の一覧は `PerfFindings.md`)。DLSS が増加の主因(`nodlss` で約98〜99%減)で、映像は主因でない。`maponly` は増加を約半分以下にするが、Opening・Menu の FPS が下がる。`unloadoff`(破棄の前に DLSS を止める)は、展示用 PC で増加を減らさなかった(+872 MB/周。基準は +813〜+976。2026/10/22。`PerfFindings.md` の追記)。`Player.log` の肥大は、毎フレームのログ5か所の削除で解消(1周あたり約24.6万行 → 約720行)。遷移の内訳は `transitions.csv` に記録できる。周回と放置を別データにし、条件を `xN` で順に回し、`until` で上限まで測れる(`until` は展示用 PC で、VRAM が上限に達して終わることを確認した: 2026/10/22。放置監視と `xN` の複数ラウンドは、実機で未確認) |
| 未記録 | 計測自身の負荷 / 放置監視の実機での記録 / `until` の FPS 低下による終了の記録 / 新しい列(`state`・`lap`・`trans`)の値と `transitions.csv` の突き合わせ / `unloadoff` の、展示用 PC 以外(2 台)での実測 / `unloadoff` の止める処理が解放に結びついたか(止めた後に 1 フレーム描画されたか) / 展示(TGS2026)での運用の結果 |
| 次の候補 | 実機で `-perfidle` を確認する(`-perfuntil` の VRAM 上限による終了と新しい列は、展示用 PC で確認済み) / `unloadoff` を他の 2 台でも測るかを決める(展示用 PC では効かなかった) / 複数日の連続稼働で日またぎを確認する |
