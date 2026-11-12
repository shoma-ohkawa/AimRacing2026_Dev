# 一時メモ: スクリプトの再構築・再作成の対象

作成: 2026/11/12(同日に、範囲を「自作スクリプト全体」へ変更) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/` ほか

一時的なメモ。再構築・再作成が終わったら削除する。`CLAUDE.md` の docs の表には載せていない。
進捗(現在位置、状態、作業ログ)は `Temp_RebuildProgress.md` に書く。

- 確認: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- **範囲(2026/11/12 に変更)**: 自作スクリプトは**すべて**再構築・再作成する。「自作」は、先頭のヘッダに `@author 24CU0110 大川翔生` があるもの(`CLAUDE.md` の規約)。`コード`で数えて 36 本・8,093 行(行数は 2026/11/12 の値)。
- 後半の「自作でないもの」は、再構築・再作成の目的(カメラ構成、Opening と Map の GPU 負荷)で必要になるもの。
- 影響の範囲は、docs の記述から挙げた。アセットの中身と、他メンバーのコードは読んでいない(`未確認`)。
- この一覧は案。2026/11/12 に、方式は**全面刷新**(ファイル名・構成も変える)、**API も変える**と決めた(`Temp_RebuildProgress.md` の「2」)。区分は、作り直しの大きさの目安で、ファイルの行は設計のあとで作り直す。

## 範囲の判定

| 項目 | 扱い |
|---|---|
| ヘッダが `@author 24CU0110 大川翔生` のもの(36 本) | 自作。下の「1」 |
| `Debug/DebugKey.cs` | 自作に含めない。ヘッダの担当者は西林竜輝で、2026/10/19 に自分が 2 か所(U キー、`KeysLocked`)を足した。「2-A」へ |
| `Tools/PerfViewer/`(Python・bat・html)、`Tools/Portfolio/` | `.cs` ではないので含めない(`CLAUDE.md` の「スクリプト」は `.cs`)。含めるなら足す |
| ヘッダのない自作ファイル | あるか未確認。docs で「担当は要確認」としたものは、自作に含めていない |

## 1. 自作スクリプト(36 本・8,093 行)

| 区分 | 意味 |
|---|---|
| 再構築 | 役割の分け方・持ち方を変えて書き直す(カメラ構成の影響を受けるもの) |
| 再作成 | 同じ役割・同じ外部 API で、新しく書く |

段は、依存の下から上へ進める順。段 1 は、計測で分かった最大の問題(A-1: DLSS のカメラの作成と破棄で、メモリと VRAM が増える)に直結するので、基盤(段 0)の直後に置く。

### 段 0: 基盤(全体が依存する)

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `Utility/SingletonMonoBehaviour.cs` | 51 | 再作成 | `Manager` に 6、`Map` に 1 のクラスが継承している。`HasInstance` を足す(起動順のエラー対策)。重複時の初期化を `Instance != this` に統一する案(S-1) |
| `Utility/AppLog.cs` | 78 | 再作成 | `Log` だけがリリースで除去される。`LogWarning`・`LogError` はリリースでも出す(2026/10/20 の決定)。`DEVELOPMENT_BUILD` の非推奨(E-2)の置き換え先が未調査 |

### 段 1: カメラ構成・画面出力

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `Camera/MultiScreenOutput.cs` | 556 | 再構築 | 出力元のカメラの持ち方が変わる。`SetSourceCamera` が古い出力先を外さない、起動順のエラーも解消する(S-6)。Editor では分割しない仕様を維持する |
| `Camera/MultiScreenSourceCamera.cs` | 48 | 再構築 | 「シーンのカメラを登録する」役割が、常駐のカメラでは要らなくなる可能性がある |
| `Camera/WindowsMonitorInfo.cs` | 447 | 再作成 | 2026/11/10 に作り直したばかりで、プレイヤーでの実機は未確認。2026/10/27 の記録に、Windows API が 2 台・Unity が 3 台と食い違った起動がある(`PerfFindings.md`) |
| `Camera/MultiScreenTargetSelector.cs` | 96 | 再作成 | Unity に依存しない判定。確認済みの 13 件(`MultiScreenOutput.md`)を、テストとして引き継ぐ |

### 段 2: 遷移エンジン

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `GameManager/Flow/GameStateId.cs` | 18 | 再作成 | 全面刷新では値を変えてよい。ただし、計測の `state` 列の番号と State の対応は、過去の記録と同じにする(変えるなら、計測の側で変換する) |
| `GameManager/Flow/IGameState.cs` | 22 | 再作成 | `Id` / `Enter` / `Tick` / `Exit` |
| `GameManager/Flow/GameSceneGroup.cs` | 34 | 再作成 | 並びは「共有シーン → 主シーン」 |
| `GameManager/Flow/GameFlowGraph.cs` | 47 | 再作成 | State とシーン、次の State の唯一の定義箇所 |
| `GameManager/Flow/GameStateBindingBase.cs` | 97 | 再作成 | 自己登録と初回起動の入口。`ResolveBootstrapCandidate()` の前提(同一 GameObject の兄弟) |
| `GameManager/Flow/GameFlowRunner.cs` | 433 | 再構築 | シーンの読込と、Loading 画面の組み立てが、カメラ構成の影響を受ける。`EmergencyReset` が `Exit()` を呼ばない問題も直す(S-2) |
| `GameManager/Flow/FlowTiming.cs` | 79 | 再作成 | 購読者がいなければ何もしない作り |
| `GameManager/Flow/SessionCacheRefresher.cs` | 78 | 再作成 | GC は 2 回に分ける。`FlowTiming` の呼び出し 3 か所 |
| `GameManager/Flow/TransitionFadeController.cs` | 201 | 再作成 | `Display.displays.Length` を基に Canvas を作る。出力枚数が食い違うときの扱いを決める |

### 段 3: 各 State

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `GameManager/GameStates/LoadState.cs` | 47 | 再作成 | FadeIn 完了後にリフレッシュ |
| `GameManager/GameStates/SplashState.cs` | 38 | 再作成 | `SplashFade.IsEndAnimation` を待つ |
| `GameManager/GameStates/OpeningState.cs` | 211 | 再構築 | 曲間の Splash 挿入で、Opening のカメラを無効にして戻している。未使用の定数とコメントの食い違いも直す |
| `GameManager/GameStates/MenuState.cs` | 309 | 再作成 | `OnNameReceived` の購読を `OnDestroy` でも解除する(S-3) |
| `GameManager/GameStates/IntroState.cs` | 96 | 再作成 | `Map` のシーンで、`RacingState` と同じ GameObject |
| `GameManager/GameStates/RacingState.cs` | 124 | 再作成 | 状態を整数の定数でなく `enum` にできる |
| `GameManager/GameStates/ResultState.cs` | 119 | 再構築 | `EnsureDisplayCamera()` が、Result のカメラを実行時に作っている |

### 段 4: 遷移演出・UI・突入演出

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `UI/Loading/LoadingScreenController.cs` | 327 | 再作成 | BGM 別の保持秒数がクリップ名の文字列一致(OP2 は作業メモ入りの名前)。`m_titleLogoFadeInDelay` の名前と用途が違う |
| `UI/Loading/TitleLogoOverlay.cs` | 76 | 再作成 | ロゴの Canvas と Loading 画面の Canvas が同じ描画順 1100(前後が不定。実機で未確認) |
| `UI/TimeRemainingCountdown.cs` | 74 | 再作成 | `OnStateFadeInComplete` を購読して減り始める |
| `Effect/RacingEnter/RacingEnterEffect.cs` | 282 | 再作成 | パラメーターがコードの定数。Inspector で調整できる形にする案(S-7) |
| `Effect/RacingEnter/RacingEnterBlurPass.cs` | 69 | 再作成 | `BeforePostProcess` の Custom Pass。`BlurFocus` の負荷は未測定 |
| `Effect/RacingEnter/RacingEnterEffectPattern.cs` | 18 | 再作成 | |

### 段 5: 計測

作り直した効果は、これで確認する。段 1〜4 のあとに作り直し、CSV の形式は変えない。

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `Perf/PerfCsvRecorder.cs` | 640 | 再作成 | `perf_*.csv`(`schema` 2)と `meta.json` |
| `Perf/PerfAutoLapRunner.cs` | 492 | 再構築 | DLSS を止める処理が、シーンのカメラを探している。カメラ構成が変わると条件(`nodlss`・`maponly`・`unloadoff`)の実装が変わる |
| `Perf/PerfTransitionRecorder.cs` | 382 | 再作成 | `transitions.csv` |
| `Perf/PerfNativeMetrics.cs` | 365 | 再作成 | Windows API と NVML |
| `Perf/PerfCsvFormat.cs` | 207 | 再作成 | ガベージを出さない書き込み |
| `Utility/AutoDemoDriver.cs` | 157 | 再作成 | F9・Y・U キーの自動走行。`CurrentLap` と `LastLoopCompleted` を計測が参照している |

### 段 6: エディタ拡張

| ファイル | 行 | 区分 | 備考 |
|---|---|---|---|
| `Editor/ProjectZipWhitelist.cs` | 1,040 | 再作成 | ホワイトリスト方式の Project Zip。標準の圧縮 API を使わず、ZIP を自前で組み立てている(理由は未記録: S-9) |
| `Editor/CurveSignAdjustWindow.cs` | 735 | 再作成 | `Map` のカーブ標識の `InPoint` / `OutPoint` を、道路に沿ってずらす(2026/10/27 作成) |

### 全面刷新で、控えるもの・取り直すもの・引き継ぐもの(2026/11/12 に方針を変更)

| 区分 | 内容 |
|---|---|
| 控える(フェーズ 1) | 現行の Inspector の値: `TransitionFadeController.m_stateFadeSettings`、`LoadingScreenController.m_fadeSettings`・`m_splashToOpeningBgmKeepOverrides`(BGM のクリップ名の文字列一致)、`OpeningState`・`MenuState` の秒数と範囲、`GameStateId` の整数(`m_leavingState`、`m_state`)。シーンの配線(`Manager`・`Load`・`Splash`・`Opening`・`Menu`・`Map`・`Result`・`Loading` の 8 シーン。`文書`: `GameFlow.md`)。呼び出し側(下の「2-D」と、`GameFlowRunner` を参照する他メンバーのファイル) |
| 取り直す | シーン・Prefab の参照(`.meta` の GUID も変わる)、Inspector の値、`GameStateId` の値、構成の前提(`Intro` と `Racing` が同じ GameObject、`Manager` を先に読み込む、など。`GameFlow.md`)。設計で決め直す |
| 引き継ぐ | 計測の形式: `perf_*.csv` の列(`schema` 2)、`transitions.csv` のステップ名(`GameFlowRunner.cs`・`SessionCacheRefresher.cs` の定数と一致させる。両ファイルのコメントが `Perf.md` の「transitions.csv」の節を参照している)、`state` 列の番号と State の対応(Splash 0、Opening 1、Menu 2、Intro 4、Racing 5、Result 6、Load 7)。展示の流れ(Load → Splash → Opening → Menu → Intro → Racing → Result → Load)と、控えた秒数 |
| 外部が呼ぶ API | 変えてよい。呼び出し側(「2-D」の他メンバーのファイル)も直す。直す箇所を示して、担当者へ確認する |
| docs | 作り直したら、`GameFlow.md`・`GameStates.md`・`TransitionEffects.md`・`MultiScreenOutput.md`・`CommonFoundation.md`・`Perf.md` を直す。節の見出しは、他の docs とコードが参照しているので変えない |
| 規約・書式 | ヘッダ、`m_` / `m_p` / `k_`、`return` の書き方、コメントは `CLAUDE.md` どおり。波括弧は、既存の自作ファイルに合わせる(Allman、スペース 4) |

## 2. 自作でないもの(必要なもの)

### 2-A 描画系スクリプト(他メンバー作成)

| 目的 | ファイル | 行 | 必要な内容 |
|---|---|---|---|
| カメラ構成 | `Camera/SwitchingCamera.cs` | 127 | Cinemachine のカメラを切り替える。常駐のカメラが Brain を持つ構成になると影響する。再作成か修正 |
| カメラ構成 | `Camera/CustomizeCameraRotation.cs` | 57 | Cinemachine の拡張。同上。Cinemachine 6.6.0 での挙動は未確認 |
| Opening の GPU・CPU | `Effect/EdgeSpectrumBars.cs` | 377 | 毎フレーム `mesh.Clear()` して全頂点を設定し直す。FFT を各コンポーネントが別々に取得する。再作成 |
| Opening の GPU・CPU | `Effect/RingSpectrumBars.cs` | 458 | 同上。`Opening` に 3 つある。再作成 |
| Map の毎フレームの無駄 | `Environment/UtilityPoleCable.cs` | 34 | Prefab 1 つに 1,125 本のケーブルを、毎フレーム再計算している。1 回計算して静的にする。Prefab は `Assets/###Map2022/Dentyu/Utility pole(Dentyu) (3).prefab`(スクリプトの GUID で検索した 1 件) |
| Map の毎フレームの無駄 | `Environment/goal_destroyAtEndScene.cs` | 36 | 現行のビルドにない "End" シーンを待っている。削除でもよい |
| Map の毎フレームの無駄 | `UI/BlinkingUI.cs` | 30 | `Update` で毎フレーム `AppLog.Log` を呼ぶ消し忘れ |
| 自作の再作成に合わせる | `Debug/DebugKey.cs` | 250 | `GameFlowRunner` を呼ぶ(F1・F2・F9)。自作の `AutoDemoDriver` を呼ぶ(F9・Y・U)。自分の編集 2 か所(U キー、`KeysLocked`)を含む |
| 自作の再作成に合わせる | `UI/Splash/SplashFade.cs` | 107 | `SplashState` が `IsEndAnimation` で終了を待つ。PageDown を直接読む |
| 削除 | `Effect/Glitch.cs` | 78 | 描画処理がコメントアウト。HDRP 17.6 では使えない旧方式。コードから参照あり(消す前に確認) |
| 削除 | `Effect/GarageGlitchTitle.cs` | 74 | 未参照。`sharedProfile`(アセット)を実行時に書き換える |
| 削除 | `Environment/CarVisible1.cs` | 32 | 未参照 |
| 削除 | `UI/Legacy/Entry.cs` | 236 | 旧方式の入力画面。現行のフローでは使われない。コードから参照のみ |
| 削除 | `Meter/UI_Meter.cs` | 207 | どのシーン・Prefab にも置かれていない。コードから参照のみ |

作り直さなくてよいもの(負荷が小さい): `UI/DisplaySectorAnimation.cs`、`UI/SpriteAnimationNew.cs`、`UI/Title/Start_UI.cs`、`UI/UI_StarCountDown.cs`、`Meter/LED_Controller.cs`、`Meter/LED_IndicatorController.cs`、`Meter/Load_Indicator.cs`(R-3、R-4)。

### 2-B Volume Profile・HDRP・ライティング

| ファイル | ID | 必要な内容 |
|---|---|---|
| `Assets/###Map2026/###Map2025/Map Volume Setting/Map2025_tmp terae.asset` | V-1 | 屋外コースの見た目。HDRISky と PBSky の両方を持ち、どちらを使うか未確認。負荷の大きい項目(VolumetricClouds、PBSky、SSAO、Fog、DoF)の項目別コストが未測定。V-2 とほぼ同一(二重管理)。Map の GPU を減らす対象 |
| `Assets/###Map2026/###Map2025/Map Volume Setting/Map2025.asset` | V-2 | HDRP アセットの既定として、全カメラの一番下の層になる。Map 向けの値が `Load`・`Splash`・`Result` にも効く。V-1 と統合するか、シーン別に分ける |
| `Assets/###Map2026/###Map2025/op/OP Volume Profile/Opening 2025 Volume Profile.asset` | V-4 | `Opening` の見た目。V-5 とほぼ同一(差は 14 行)。Opening を DLSS なしで 50 FPS にするための対象 |
| `Assets/##Rendering/Opening2024 Volume Profile.asset` | V-5 | `Menu` の見た目(2024 版)。V-4 と統合する |
| `Assets/###Map2026/VolumeSetting/MenuScene.asset` | V-7 | カメラの Cinemachine Volume Settings から参照される。カメラ構成の変更で、参照の持ち方が変わる |
| `Assets/##Rendering/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset` | V-3 | Rich FX の全 40 種を持つ。83 個がどのリストにも載っていない。`Map`・`Opening` の Static Lighting Sky も指す(消すときの影響範囲) |
| `Assets/##Rendering/HDRPDefaultResources/HDRenderPipelineAsset.asset` | H-1 | DLSS・動的解像度・影の設定。Lit シェーダーモードが Both(目的が未確認) |
| `Assets/##Rendering/HDRPDefaultResources/HDRenderPipelineGlobalSettings.asset` | H-2、V-10 | Rich FX の登録が 2 系統(新形式に使用中の ChromaLines がなく、未使用の Slice・Underwater が入っている) |
| `Assets/##Rendering/garage!move.asset` | V-6 | `Map.unity` の `Glitch` という Volume(Global、weight 1)が使っている(`Temp_RebuildSnapshot.md` の「6」)。未参照ではない。消すなら、`GarageGlitchTitle` と、この Volume を外してから |
| `Assets/##Rendering/HDRPDefaultResources/DefaultLookDevProfile.asset` | V-8 | LookDev(Editor)用。ゲームの実行には影響しない。不要 |
| `ProjectSettings/HDRPProjectSettings.asset`、`GraphicsSettings.asset`、`QualitySettings.asset` | H-3 | 内容は未精査(割り当ての確認のみ) |
| `Assets/##Rendering/LightingData/Setting/Map/Opening2024.lighting`、`Okutama.lighting` | L-3 | 名前が 2024 年版のまま。現行の構成に合っているか未確認 |
| `Assets/!Scenes/Map/OcclusionCullingData.asset` | L-4 | 現在の `Map` の構成と合っているか未確認 |
| `Assets/Plugins/InanEvin/Rich FX - HDRP Post Processing/`(`Resources/` に 88 ファイル) | M-3 | 40 種のシェーダーが `Resources/` にあり、使わなくても常にビルドに入る。使用は Glitch・ChromaLines・HueSaturationInvert の 3 種 |

### 2-C シーン・Prefab

| ファイル | 必要な内容 |
|---|---|
| `Assets/!Scenes/Manager.unity` | `GameFlowRunner`・`TransitionFadeController`・`TitleLogoOverlay`・`MultiScreenOutput` が置かれ、秒数などの Inspector の値を持つ。常駐カメラを置くなら、ここ |
| `Assets/!Scenes/Load.unity`、`Splash.unity`、`Opening.unity`、`Menu.unity`、`Map.unity`、`Result.unity` | 各 State と、シーンごとのカメラ(DLSS の設定、Cinemachine、Volume)。`Opening.unity` は 70 灯がすべて Mixed の Hard Shadow(L-1)。`Map.unity` は `IntroState` と `RacingState` が同一 GameObject |
| `Assets/!Scenes/Loading.unity` | `LoadingScreenController`。`MultiScreenSourceCamera` は付いていない |
| `Assets/!Scenes/MapEnvironment.unity` | 静的環境。MeshRenderer 159、Prefab インスタンス 79。ライトなし(L-2) |
| `Assets/###Map2026/###Map2025/op/chu~car~jo~.prefab` | `Opening` の Prefab。23 灯(L-1) |
| `Assets/###Map2026/###Map2025/Menu/CinemaCamera.prefab` | Menu のカメラ。カメラ構成の対象 |

### 2-D 自作が呼ぶ、他メンバーのモジュール(API を変えるので、呼び出し側を直す。担当者へ確認)

| ファイル | 呼ぶ側と内容 |
|---|---|
| `Sound/SoundManager.cs` | BGM の先行再生、`IsBGMPlaying`、`FadeOutBGM`、`StopBGM`、`ResetFadeState`、再生中のクリップ名の取得(`LoadingScreenController`、各 State、`SessionCacheRefresher`) |
| `GameManager/GameManager.cs`、`GameManager/DrivingSettings.cs` | WIZMO の参照、AT/MT の受け渡し、結果・ラップタイムのクリア。`GameFlowRunner.CurrentId` を読む。重複時の確認がない(S-1) |
| `WIZMO/ChairRideOperator.cs`、`WIZMO/ChairController.cs`、`Plugins/WIZMOTOOLS/Scripts/WIZMOController.cs` | 椅子の姿勢と走行動作 |
| `AimRacingRanking/Runtime/NameEntry/TCPserver.cs` | `MenuState` の名前入力(`OnNameReceived`)。`adb.exe` の端末指定が固定の可能性(E-1) |
| `AimRacingRanking/Runtime/NameEntry/AimRacingPlayerName.cs`、`AimRacingNameEntryController.cs` | `AimRacingPlayerName` は、`SessionCacheRefresher` がクリアする。`AimRacingNameEntryController` は、`GameFlowRunner` を参照する(`コード`: 参照の有無のみ) |
| `AimRacingRanking/Runtime/AimRacingResultFlowController.cs` | Result からの復帰。`AdvanceToNext()` と `OnStateFadeInComplete` を使う |
| `AimRacingRanking/Runtime/AimRacingRankingBridge.cs` | ランキングの送信。テンキー `+` / `-` |
| `DrivingTools/AutoDriving/Runtime/TrackDriveSession.cs`、`TrackDriveRuntime.cs` | `CurrentId`・`OnStateFadeInComplete`・`EmergencyReset(Load)` を使う。`AutoDemoDriver` の接続先 |
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | 現行の `MenuState` にない `m_selectionReady` をリフレクションで読む(未実行)。`MenuState` の作り直しで直す |
| `Input/MenuInputDeviceSharing.cs`、`Input/AxisPressdOnce.cs` | Opening・Menu の入力機器の共有と、ペダルの踏み込み判定 |
| `UI/TwoChoice.cs` | Menu の AT/MT 選択の演出 |
| `Race/Line_StartFinish.cs`、`Race/ReturnRoad.cs`、`Vehicle/Controller/VehicleController.cs`、`Meter/MeterUIManager.cs`、`UI/UI_StarCountDown.cs`、`Sound/Vehicle/DriveSound.cs` | `IntroState`・`RacingState` が、有効・無効や設定を切り替える。ゴールの検知(`Line_StartFinish.IsChecked`) |
| `Device/G923_FFBController.cs`、`Assets/Logitech SDK/` | ゴール時のフォースフィードバックの停止 |
| `Debug/DebugSystem.cs`、`WIZMO/WIZMODebug.cs` | デバッグキー(End・Delete、F11・F12)。`DebugKey.md` に載っている |

## 注意

- 他メンバー作成のファイルを置き換える前に、担当者へ確認する(B-1 のときと同じ)。担当は、ファイル先頭のヘッダで見ると、`AimRacingRanking/` が久場洸太、`WIZMO/ChairRideOperator.cs` が豊田達也、`WIZMO/ChairController.cs` が yuuha terae と中田樹希。`SoundManager` は小林大輝(`文書`)。
- 作り直した効果は、既存の `run_lap_variants.bat` で確認できる。今回は計測を取らない前提。
- 2-A〜2-C のうち、効果が測れているものはない。カメラ構成の効果は `推定`(破棄されたカメラの解放が起きない原因は未特定: A-4)。
