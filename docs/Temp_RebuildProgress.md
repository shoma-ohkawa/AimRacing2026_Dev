# 一時メモ: 再構築・再作成の進捗(どの PC からでも再開する)

作成: 2026/11/12 / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/` ほか。計画(対象の一覧と、守ること)は `Temp_RebuildTargets.md`

一時的なメモ。再構築・再作成が終わったら、`Temp_RebuildTargets.md` とあわせて削除する。`CLAUDE.md` の docs の表には載せていない(`CLAUDE.md` の「進行中の作業」から指す)。

- 確認: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- **この表と実際のコードが食い違ったら、git のコミット(コード)を正とする。** 表を直す。

## 1. 再開の手順(どの PC でも同じ)

1. `git pull`。ワークツリーは `main` だけを使う(`CLAUDE.md`)。
2. このファイルを読み、「3. 現在位置」から続ける。対象の詳細は `Temp_RebuildTargets.md` の該当の段。
3. Unity は 6000.6.5f1。Editor で開いているシーン・Prefab は、ファイルを触る前に保存して閉じる(`.unity`・`.prefab` の YAML はマージが難しい: `VersionControl.md`)。
4. 作業の区切り(1 ファイルが終わるごと、または 1 日の終わり)で、「4. 進捗表」と「5. 作業ログ」を更新する。Unity で保存してから、コミット・プッシュする。
5. 別の PC で続ける前に、プッシュ済みであること。**同じ段を 2 台で同時に触らない。**

## 2. 決定事項(変えない前提)

| 日付 | 決定 | 出所 |
|---|---|---|
| 2026/11/12 | 展示の解像度は 2560×1440 × 3(描画 7680×1440) | あなたの指示 |
| 2026/11/12 | 計測は新しく取らず、`PerfRecords/` の現在の記録を使う | あなたの指示 |
| 2026/11/12 | 再構築・再作成の範囲は、自作スクリプト全体(先頭のヘッダが `@author 24CU0110 大川翔生` の 36 本・8,093 行) | あなたの指示 |
| 2026/11/12 | 自作でないもの(描画系スクリプト、Volume Profile、HDRP、シーン、他メンバーのモジュール)は、必要なものを `Temp_RebuildTargets.md` の「2」に一覧にした | あなたの指示 |
| 2026/11/12 | `docs/Temp_PortfolioRepository.md` は削除した。`Tools/Portfolio/`、`Templates/PortfolioKit/`、`E:\AimRacing\AimRacing2026_Portfolio` は、削除の選択がなかったので残している。GitHub の `AimRacing2026_Portfolio` と Release `v1.0.0` は公開中で、触らない | あなたの指示 |
| 2026/11/12 | 作り直した効果の確認は、作業が終わってから、既存の `run_lap_variants.bat` で行う(今回の作業では取らない) | あなたの指示 |

根拠(計測と分析)は、`PerfFindings.md` の 2026/11/12 の追記と、`CurrentStateAnalysis.md`。

## 3. 現在位置

**状態: 着手前。** コード・アセットの変更は、まだない。

次の一手(案。確定したら、ここを更新する):

1. 設計の前提の調査(読むだけ): `Load`・`Splash`・`Opening`・`Menu`・`Map`・`Result` の各シーンについて、カメラ・Volume・Cinemachine・Timeline・DLSS の設定の置き方を一覧にする。段 1(カメラ構成)の設計案の材料になる。
2. 段 0 の再作成: `Utility/SingletonMonoBehaviour.cs`、`Utility/AppLog.cs`。API(`HasInstance` など)を先に決める。
3. 段 1 の設計案を作る。常駐のカメラにまとめる案の効果は `推定`(破棄されたカメラの解放が起きない原因は未特定: A-4)。

未決(あなたの判断が要る):

| 項目 | 内容 |
|---|---|
| 区分と順 | `Temp_RebuildTargets.md` の区分(再構築 6 本、再作成 30 本)と、段の順は案 |
| Opening・Menu の FPS の下限 | DLSS を止める案(`maponly`)を採ると、1440p で Opening が 43.8、Menu が 65.7 FPS になる。走行中の下限 50 は決まっているが、この 2 つは未決 |
| 他メンバー作成のファイル | 置き換える前に、担当者へ確認する(誰が、誰に聞くか) |
| 範囲の端 | `.cs` 以外(`Tools/PerfViewer/`、`Tools/Portfolio/`)を含めるか。ヘッダのない自作ファイルがあるか(未確認) |
| ポートフォリオの残り | `Tools/Portfolio/`、`Templates/PortfolioKit/`(未追跡で、この PC だけにある)、`E:\AimRacing\AimRacing2026_Portfolio` の扱い |

## 4. 進捗表

状態: `未着手` / `設計中` / `作業中` / `完了` / `保留`。

「完了」の条件: ① Unity 6000.6.5f1 でコンパイルが通る(`error CS` 0 件) ② 関連するシーン・Prefab で、Missing Script がない ③ 関連する docs を直した ④ コミット・プッシュ済み。実機での確認は、メモ欄に書く。

### 自作スクリプト(`Assets/#Scripts/` からのパス)

| 段 | ファイル | 区分 | 状態 | 更新(日付・PC) | メモ |
|---|---|---|---|---|---|
| 0 | `Utility/SingletonMonoBehaviour.cs` | 再作成 | 未着手 | | |
| 0 | `Utility/AppLog.cs` | 再作成 | 未着手 | | |
| 1 | `Camera/MultiScreenOutput.cs` | 再構築 | 未着手 | | |
| 1 | `Camera/MultiScreenSourceCamera.cs` | 再構築 | 未着手 | | |
| 1 | `Camera/WindowsMonitorInfo.cs` | 再作成 | 未着手 | | |
| 1 | `Camera/MultiScreenTargetSelector.cs` | 再作成 | 未着手 | | |
| 2 | `GameManager/Flow/GameStateId.cs` | 再作成 | 未着手 | | 値は変えない(`Intro = 4`、欠番 `3`) |
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
| 5 | `Perf/PerfTransitionRecorder.cs` | 再作成 | 未着手 | | |
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
| 2-D | 他メンバーのモジュール | `SoundManager`、`GameManager`、`TCPserver` ほか | 未着手 | | 自作の API を変えたときに、呼び出し側を合わせる |

## 5. 作業ログ(新しい順に追記。書き換えない)

| 日付 | PC | 内容 | コミット |
|---|---|---|---|
| 2026/11/12 | 開発用 `13600K_4070Ti`(`D:\AimRacing\AimRacing2026_Dev`) | 範囲を決め、`Temp_RebuildTargets.md` と、この進捗表を作成した。コード・アセットの変更はなし。この PC では Unity Editor が動いていた(プロジェクトを開いているかは未確認)。未追跡の `Templates/` は、この PC だけにある | このファイルを追加したコミット(`git log -1 -- docs/Temp_RebuildProgress.md`) |

## 6. 更新のルール

- 状態を変えたら、「更新」の欄に日付と PC(`13600K_4070Ti` など、`CLAUDE.md` の PC の表のフォルダ名)を書く。
- 自作スクリプトを作り直したら、先頭のヘッダの最終更新日を直す(`CLAUDE.md`)。
- 作業ログは追記だけにする。決定が変わったら、「2. 決定事項」に行を足す(古い行は消さない)。
- 終わったら、`CLAUDE.md` の「進行中の作業」の節と、この 2 つの `Temp_Rebuild*.md` を削除する。
