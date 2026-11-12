# 一時メモ: 再構築・再作成の進捗(どの PC からでも再開する)

作成: 2026/11/12(同日に、方針と手順を追記) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/` ほか。計画(対象の一覧)は `Temp_RebuildTargets.md`

一時的なメモ。再構築・再作成が終わったら、`Temp_RebuildTargets.md` とあわせて削除する。`CLAUDE.md` の docs の表には載せていない(`CLAUDE.md` の「進行中の作業」から指す)。

- 確認: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- **この表と実際のコードが食い違ったら、git のコミット(コード)を正とする。** 表を直す。

## 1. 再開の手順(どの PC でも同じ)

1. `git pull`。ワークツリーは `main` だけを使う(`CLAUDE.md`)。
2. このファイルを読み、「4. 現在位置」から続ける。対象の詳細は `Temp_RebuildTargets.md`。全面刷新で失う現行の値・配線・呼び出し側は、`Temp_RebuildSnapshot.md`(控え)。
3. Unity は 6000.6.5f1。Editor で開いているシーン・Prefab は、ファイルを触る前に保存して閉じる(`.unity`・`.prefab` の YAML はマージが難しい: `VersionControl.md`)。
4. 作業の区切り(1 ファイルが終わるごと、または 1 日の終わり)で、「5. 進捗表」と「6. 作業ログ」を更新する。Unity で保存してから、コミットする。プッシュの前に、あなたへ確認する。
5. 別の PC で続ける前に、プッシュ済みであること。**同じ段を 2 台で同時に触らない。**

## 2. 決定事項(変えない前提)

| 日付 | 決定 | 出所 |
|---|---|---|
| 2026/11/12 | 展示の解像度は 2560×1440 × 3(描画 7680×1440) | あなたの指示 |
| 2026/11/12 | 計測は新しく取らず、`PerfRecords/` の現在の記録を使う。作り直した効果の確認は、全段のあとにまとめて行う | あなたの指示 |
| 2026/11/12 | 再構築・再作成の範囲は、自作スクリプト全体(先頭のヘッダが `@author 24CU0110 大川翔生` の 36 本・8,093 行) | あなたの指示 |
| 2026/11/12 | 自作でないもの(描画系スクリプト、Volume Profile、HDRP、シーン、他メンバーのモジュール)は、必要なものを `Temp_RebuildTargets.md` の「2」に一覧にした | あなたの指示 |
| 2026/11/12 | **方式は全面刷新。** ファイル名・クラス名・構成も変える。シーン・Prefab の参照は張り直し、Inspector の値は取り直す | あなたの回答 |
| 2026/11/12 | **API も変える。** 呼び出し側は、他メンバーのファイルを含めて直す | あなたの回答 |
| 2026/11/12 | 各段の確認は、コンパイル + Missing Script 0 + Editor での通し再生 | あなたの回答 |
| 2026/11/12 | コードの書式は、既存の自作ファイルに合わせる | あなたの回答 |
| 2026/11/12 | 計測の記録の形式(`perf_*.csv` の列、`transitions.csv` のステップ名)は維持する | あなたの回答 |
| 2026/11/12 | 進める順は、設計 → 控え → 段 0〜6 | あなたの回答 |
| 2026/11/12 | `docs/Temp_PortfolioRepository.md` は削除した。`Tools/Portfolio/`、`Templates/PortfolioKit/`、`E:\AimRacing\AimRacing2026_Portfolio` は、削除の選択がなかったので残している。GitHub の `AimRacing2026_Portfolio` と Release `v1.0.0` は公開中で、触らない | あなたの指示 |

根拠(計測と分析)は、`PerfFindings.md` の 2026/11/12 の追記と、`CurrentStateAnalysis.md`。

## 3. 方針と手順

### 方針

| 項目 | 内容 |
|---|---|
| 方式 | 全面刷新。ファイル名・クラス名・構成も変える。`.meta` の GUID も変わるので、シーン・Prefab の参照は張り直す |
| 内容 | API も変える。他メンバーのファイルを直すときは、直す箇所を示して、担当者へ確認してから |
| 確認 | 各段で、① コンパイル(私が Unity 付属の Roslyn で確認) ② Missing Script 0 ③ Editor での通し再生(あなたが確認)。実機ビルドと計測は、全段のあとにまとめて |
| 書式 | 既存の自作ファイルに合わせる(波括弧は改行、Allman、スペース 4)。`CLAUDE.md` の例(波括弧を 1 段下げ、タブ)とは違うが、「波括弧を改行する」の文言は満たす。命名・ヘッダ・コメントは `CLAUDE.md` どおり |
| 計測の形式 | 維持する。`perf_*.csv` の列(`schema` 2)、`transitions.csv` のステップ名、`state` 列の番号と State の対応(Splash 0、Opening 1、Menu 2、Intro 4、Racing 5、Result 6、Load 7)。`GameStateId` の値を変えるなら、計測の側で変換する。`lap_report.py`・`server.py`・`viewer.html` は、そのまま使う |

### 既定(あなたが変えなければ、この方針で進める。案)

| 項目 | 既定 |
|---|---|
| コミット | 段(またはフェーズ)の区切りごと。プッシュの前に、毎回あなたへ確認する |
| docs | 段ごとに、関連する docs を直す。節の見出しは変えない(他の docs とコードが参照している) |
| 他メンバーのファイル | 触る前に、直す箇所の一覧を見せる |
| 条件コンパイル | `DEVELOPMENT_BUILD` は Unity 6.6 で非推奨(警告 UAC0009)。置き換えは、`AppLog` を作るときに決める。調べた範囲(下)では、公式の置き換えの表は見つからなかった |

`DEVELOPMENT_BUILD` の調査(2026/11/12。`文書`、Web の要約。Unity 公式の移行表は未確認):

- Unity 6.6 で `UNITY_64` と `DEVELOPMENT_BUILD` が非推奨になった。`#if` と `[Conditional]` で使うと、`DEVELOPMENT_BUILD` は UAC0009 の警告。後継は、Player Setting の Managed Code Variant。6.6 では動く。フォーラムの告知は、6.8 で削除としている。
- `DEBUG` は、旧来は `UNITY_EDITOR || DEVELOPMENT_BUILD` と同じ。Managed Code Variant が Debug のときだけ成り立つ可能性がある(未確認)。`UNITY_ENABLE_CHECKS` は、`DEVELOPMENT_BUILD` と同じではない(Input System の PR の記述)。実行時の判定は `Debug.isDebugBuild`(ただし引数の評価は省けない)。
- 参照: [フォーラムの告知](https://discussions.unity.com/t/unity-64-and-development-build-deprecation-and-managed-code-variants/1721546)、[6.6 のアップグレードガイド](https://docs.unity.com/en-us/engine/6000.7/manual/upgrade-guides/upgrade-guide-unity66.md)。

### 手順

| フェーズ | 内容 | 成果物 |
|---|---|---|
| 0 設計 | 目標、構成(カメラ、遷移、State、画面出力)、名前と API の方針、移行計画を決める。**検討中の 2 件(カメラ構成、遷移の骨格)が決まるまで、段 0 以降は始めない** | `Temp_RebuildDesign.md`(一時。決まってから作る) |
| 1 控え | 全面刷新で失うものを、先に記録する。現行の Inspector の値(秒数・色・BGM 別の保持秒数・`GameStateId` の整数・各 State の設定)、シーンの配線(どのシーンの、どの GameObject に何が付いているか)、呼び出し側(他メンバーのファイルを含む)。**設計に依存しないので、設計と並行して進められる** | `Temp_RebuildSnapshot.md`(一時。2026/11/12 に作成済み) |
| 2 実装 | 段 0 → 6。各段: 設計の確認 → 実装 → コンパイル → シーンの張り直し → Missing Script 0 → Editor での通し再生 → docs → コミット | コード、シーン |
| 3 仕上げ | 実機ビルド、計測(まとめて。効果の確認)、docs の更新、一時ファイルの削除 | |

## 4. 現在位置

**フェーズ 0(設計)。検討中が 2 件。** フェーズ 1(控え)は、2026/11/12 に作成した(`Temp_RebuildSnapshot.md`)。コード・アセットの変更は、まだない。

検討中(あなたが決める):

| 項目 | 選択肢 | 材料 |
|---|---|---|
| カメラ構成 | (1) 常駐 1 台 + DLSS をシーン別に切り替え可能 (2) 常駐 1 台に統合 (3) シーンごとのカメラを維持し、DLSS を Map だけに残す | 増加の主因は DLSS のカメラの作成と破棄(`nodlss` で 95〜99% 減。`実測`)。(3) は実測済み: 1440p で +908 → +377 MB/周(2 回で一致)、Opening 43.8 / Menu 65.7 FPS(`実測`)。(1)(2) の効果は `推定`(破棄されたカメラが解放されない原因 A-4 が未特定)。計測は最後なので、(1) は戻せる保険つき |
| 遷移の骨格 | (1) 踏襲して整理 (2) State を純 C# クラスにする (3) シーン構成から変える | (1) は、現行の利点(`GameFlowGraph` の 1 か所定義、差分ロード、どのシーンからでも起動できる Bootstrap)を残せる(`CurrentStateAnalysis.md` の S-2)。(2)(3) は影響が大きく、Load/Unload の回数やメモリへの影響が未検証 |

控えで分かったこと(`コード`。詳細は `Temp_RebuildSnapshot.md`):

- `Map.unity` の `RacingState.m_enterEffectPattern` は 4 = `BlurFocus`。コードの既定と `GameStates.md` の記述は `LaunchCombo`。`BlurFocus` は、3 画面での動作と負荷が未確認。
- `garage!move.asset`(V-6)は、`Map.unity` の `Glitch` という Volume が使っている。未参照ではない。
- カメラは 6 シーンに 1 台ずつ(`Result` だけ +1 台、無効)。すべて DLSS が有効。`Map` のカメラは車両の子で、Cinemachine の Brain は `Map`・`Menu`・`Opening` にある。カメラ構成の設計の材料。
- 自作の型をシーンに置いているのは 14 種類。UnityEvent・`PlayerInput` から自作のメソッドを呼ぶ配線は、`Menu`(6 件)と `Opening`(2 件)。
- 自作の外から呼ばれるのは、`GameFlowRunner`、`GameStateId`、`AutoDemoDriver`、`SingletonMonoBehaviour`(4 クラスが継承)、`AppLog`(35 ファイル)など。

次の一手:

1. (あなた)検討中の 2 件を決める。
2. (あなた)`BlurFocus` の扱いを決める(下の「そのほかの未決」)。
3. 設計が決まったら、`Temp_RebuildDesign.md` を作り、段 0 に進む。

そのほかの未決:

| 項目 | 内容 |
|---|---|
| Opening・Menu の FPS の下限 | カメラ構成が (3) のとき、1440p で Opening が 43.8、Menu が 65.7 FPS になる。走行中の下限 50 は決まっているが、この 2 つは未決 |
| 他メンバー作成のファイル | 置き換える前に、担当者へ確認する(誰が、誰に聞くか) |
| 範囲の端 | `.cs` 以外(`Tools/PerfViewer/`、`Tools/Portfolio/`)を含めるか。ヘッダのない自作ファイルがあるか(未確認) |
| 突入演出の既定 | `Map.unity` は `BlurFocus`(4)、コードの既定は `LaunchCombo`。取り直すときに、どちらにするか。`BlurFocus` は未確認(3 画面の動作・負荷) |
| ポートフォリオの残り | `Tools/Portfolio/`、`Templates/PortfolioKit/`(未追跡で、この PC だけにある)、`E:\AimRacing\AimRacing2026_Portfolio` の扱い |

## 5. 進捗表

状態: `未着手` / `設計中` / `作業中` / `完了` / `保留`。

**全面刷新のため、ファイル名・構成が変わる。この表の行は、設計のあとで作り直す。** 区分は、作り直しの大きさの目安。

「完了」の条件: ① Unity 6000.6.5f1 でコンパイルが通る(`error CS` 0 件) ② 関連するシーン・Prefab で、Missing Script がない ③ Editor で通し再生できた(あなたが確認) ④ 関連する docs を直した ⑤ コミット済み。実機・計測は、全段のあと。

### 自作スクリプト(`Assets/#Scripts/` からのパス。現行のファイル)

| 段 | ファイル | 区分 | 状態 | 更新(日付・PC) | メモ |
|---|---|---|---|---|---|
| 0 | `Utility/SingletonMonoBehaviour.cs` | 再作成 | 未着手 | | |
| 0 | `Utility/AppLog.cs` | 再作成 | 未着手 | | |
| 1 | `Camera/MultiScreenOutput.cs` | 再構築 | 未着手 | | |
| 1 | `Camera/MultiScreenSourceCamera.cs` | 再構築 | 未着手 | | |
| 1 | `Camera/WindowsMonitorInfo.cs` | 再作成 | 未着手 | | |
| 1 | `Camera/MultiScreenTargetSelector.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameStateId.cs` | 再作成 | 未着手 | | 計測の `state` 列の番号の対応は保つ |
| 2 | `GameManager/Flow/IGameState.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameSceneGroup.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameFlowGraph.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameStateBindingBase.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameFlowRunner.cs` | 再構築 | 未着手 | | |
| 2 | `GameManager/Flow/FlowTiming.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/SessionCacheRefresher.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/TransitionFadeController.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/LoadState.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/SplashState.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/OpeningState.cs` | 再構築 | 未着手 | | |
| 3 | `GameManager/GameStates/MenuState.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/IntroState.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/RacingState.cs` | 再作成 | 未着手 | | |
| 3 | `GameManager/GameStates/ResultState.cs` | 再構築 | 未着手 | | |
| 4 | `UI/Loading/LoadingScreenController.cs` | 再作成 | 未着手 | | |
| 4 | `UI/Loading/TitleLogoOverlay.cs` | 再作成 | 未着手 | | |
| 4 | `UI/TimeRemainingCountdown.cs` | 再作成 | 未着手 | | |
| 4 | `Effect/RacingEnter/RacingEnterEffect.cs` | 再作成 | 未着手 | | |
| 4 | `Effect/RacingEnter/RacingEnterBlurPass.cs` | 再作成 | 未着手 | | |
| 4 | `Effect/RacingEnter/RacingEnterEffectPattern.cs` | 再作成 | 未着手 | | |
| 5 | `Perf/PerfCsvRecorder.cs` | 再作成 | 未着手 | | CSV の形式(`schema` 2)は変えない |
| 5 | `Perf/PerfAutoLapRunner.cs` | 再構築 | 未着手 | | |
| 5 | `Perf/PerfTransitionRecorder.cs` | 再作成 | 未着手 | | ステップ名は変えない |
| 5 | `Perf/PerfNativeMetrics.cs` | 再作成 | 未着手 | | |
| 5 | `Perf/PerfCsvFormat.cs` | 再作成 | 未着手 | | |
| 5 | `Utility/AutoDemoDriver.cs` | 再作成 | 未着手 | | |
| 6 | `Editor/ProjectZipWhitelist.cs` | 再作成 | 未着手 | | |
| 6 | `Editor/CurveSignAdjustWindow.cs` | 再作成 | 未着手 | | |

### 自作でないもの(`Temp_RebuildTargets.md` の「2」。まとまりごと)

| 節 | まとまり | 内容 | 状態 | 更新(日付・PC) | メモ |
|---|---|---|---|---|---|
| 2-A | カメラ構成 | `SwitchingCamera.cs`、`CustomizeCameraRotation.cs` | 未着手 | | |
| 2-A | Opening の負荷 | `EdgeSpectrumBars.cs`、`RingSpectrumBars.cs` | 未着手 | | |
| 2-A | Map の毎フレームの無駄 | `UtilityPoleCable.cs`(と Prefab)、`goal_destroyAtEndScene.cs`、`BlinkingUI.cs` | 未着手 | | |
| 2-A | 自作に合わせる | `DebugKey.cs`、`SplashFade.cs` | 未着手 | | 自作の API が決まってから |
| 2-A | 削除 | `Glitch.cs`、`GarageGlitchTitle.cs`、`CarVisible1.cs`、`Entry.cs`、`UI_Meter.cs` | 未着手 | | 消す前に参照を確認 |
| 2-B | Map の Volume | `Map2025_tmp terae.asset`(V-1)、`Map2025.asset`(V-2) | 未着手 | | |
| 2-B | Opening・Menu の Volume | `Opening 2025 Volume Profile.asset`(V-4)、`Opening2024 Volume Profile.asset`(V-5)、`MenuScene.asset`(V-7) | 未着手 | | |
| 2-B | HDRP の設定 | `HDRenderPipelineAsset.asset`(H-1)、`HDRenderPipelineGlobalSettings.asset`(H-2)、`ProjectSettings/` の 3 本(H-3) | 未着手 | | |
| 2-B | Rich FX・未使用 | `DefaultSettingsVolumeProfile.asset`(V-3)、Rich FX(V-10、M-3)、`garage!move.asset`(V-6)、`DefaultLookDevProfile.asset`(V-8) | 未着手 | | |
| 2-B | ライティング | `*.lighting`(L-3)、`OcclusionCullingData.asset`(L-4) | 未着手 | | |
| 2-C | シーン・Prefab | `Manager.unity` ほか 8 シーン、`chu~car~jo~.prefab`(L-1)、`CinemaCamera.prefab` | 未着手 | | 自作の再作成のあとで、参照を張り直す |
| 2-D | 他メンバーのモジュール | `SoundManager`、`GameManager`、`TCPserver` ほか | 未着手 | | 自作の API を変えたときに、呼び出し側を直す(担当者へ確認) |

## 6. 作業ログ(新しい順に追記。書き換えない)

| 日付 | PC | 内容 | コミット |
|---|---|---|---|
| 2026/11/12 | 開発用 `13600K_4070Ti` | フェーズ 1(控え)を作成した(`Temp_RebuildSnapshot.md`): 自作 14 種類の配線と Inspector の値、`PlayerInput`・UnityEvent の配線、呼び出し側、カメラ・Volume の置き方、Build Settings の順。`Map.unity` の `m_enterEffectPattern` が `BlurFocus` であること、`garage!move.asset` が使われていることが分かった。`Temp_RebuildTargets.md` の V-6 の行を直した。コード・アセットの変更はなし | 控えを追加したコミット(`git log -1 -- docs/Temp_RebuildSnapshot.md`) |
| 2026/11/12 | 開発用 `13600K_4070Ti` | 方針を決めた(全面刷新、API も変える、確認の方法、書式、計測の形式、進める順)。カメラ構成と遷移の骨格は検討中。`AppLog` の条件コンパイルを調べた(`DEVELOPMENT_BUILD` は 6.6 で非推奨)。コード・アセットの変更はなし | 控えを追加したコミット(`git log -1 -- docs/Temp_RebuildSnapshot.md`) |
| 2026/11/12 | 開発用 `13600K_4070Ti`(`D:\AimRacing\AimRacing2026_Dev`) | 範囲を決め、`Temp_RebuildTargets.md` と、この進捗表を作成した。コード・アセットの変更はなし。この PC では Unity Editor が動いていた(プロジェクトを開いているかは未確認)。未追跡の `Templates/` は、この PC だけにある | `bb7f39f` |

## 7. 更新のルール

- 状態を変えたら、「更新」の欄に日付と PC(`13600K_4070Ti` など、`CLAUDE.md` の PC の表のフォルダ名)を書く。
- 自作スクリプトを作り直したら、先頭のヘッダの最終更新日を直す(`CLAUDE.md`)。
- 作業ログは追記だけにする。決定が変わったら、「2. 決定事項」に行を足す(古い行は消さない)。
- 終わったら、`CLAUDE.md` の「進行中の作業」の節と、この 2 つの `Temp_Rebuild*.md` を削除する。
