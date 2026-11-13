# 一時メモ: 再構築・再作成の設計(下書き)

作成: 2026/11/13 / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/` ほか。決定は `Temp_RebuildProgress.md` の「2. 決定事項」、対象の一覧は `Temp_RebuildTargets.md`、現行の値・配線は `Temp_RebuildSnapshot.md`

一時的なメモ。再構築・再作成が終わったら、`Temp_RebuildProgress.md` などとあわせて削除する。`CLAUDE.md` の docs の表には載せていない。

- 確認: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- **状態: 6 章の 7 件を決定した(2026/11/13)。** 決定済みの前提(下の「1」)の上に、設計を書いた。未決は 2 件(他メンバーの担当者、範囲の端・ポートフォリオ)。3 章の「案」(F-2〜F-7、C-8、3.6、3.7)は、案のまま。3.6 を確認してから、段 0 に進む。
- 根拠の計測は、`PerfFindings.md`(2026/11/13 の追記を含む)と `CurrentStateAnalysis.md`。

## 1. 前提(決定済み。変えない)

| 項目 | 内容 | 出所 |
|---|---|---|
| 展示の解像度 | 2560×1440 × 3(描画 7680×1440) | `Temp_RebuildProgress.md` の「2」(2026/11/12) |
| 範囲・方式 | 自作スクリプト 36 本を全面刷新。ファイル名・構成・API も変えてよい | 同上 |
| 優先順位 | ① A-1(カメラ構成) ② A-4 の調査(済) ③ A-2 ④ 実機で見る点 ⑤ 自作の動作不良 ⑥ 残りの作り直し | 同上(2026/11/13) |
| カメラ構成 | **(1) 常駐 1 台 + DLSS をシーン別に切り替え可能。** 効果は `推定`。切り分けの実験は取らない | 同上(2026/11/13) |
| 遷移の骨格 | **(1) 踏襲して整理。** 最も軽量で高速な方法 | 同上(2026/11/13) |
| 突入演出 | 一度廃止(`RacingEnter*` 3 ファイルは作り直さない) | 同上(2026/11/13) |
| 計測 | 新しく取らない。形式(`perf_*.csv` の列、`transitions.csv` のステップ名、`state` の番号)は維持する | 同上(2026/11/12) |
| 確認 | 各段で、コンパイル + Missing Script 0 + Editor での通し再生 | 同上 |

## 2. 目標

| ID | 目標 | 指標 | 現行 |
|---|---|---|---|
| G1 | 周回でメモリ・VRAM が増えない(A-1) | `private_mb`・`vram` の MB/周(1440p) | `base` +908、`maponly` +377、`nodlss` +15(`実測`)。**目標値は「6」で決定**(約 33 MB/周以下。`推定`) |
| G2 | 走行中 FPS を 50 以上に保つ(1440p) | `lap_report.py` の State 別 FPS | `base` 73.4〜74.9(`実測`)。Opening・Menu の下限は、(3) 相当へ戻す場合に決める(「6」) |
| G3 | ステージ・シーンを足しても、DLSS の作成回数とコードの変更範囲が増えない | シーンを足す手順の行数 | 現行は、シーンごとにカメラと DLSS の設定を持つ |
| G4 | 遷移が軽く、速い | 遷移の `work`(`transitions.csv`)、遷移ごとのメモリ | Menu → Intro の `work` は、1440p で 2.0〜3.6 秒、1080p で 1.4〜2.6 秒(`実測`) |
| G5 | 自作の動作不良になりうる箇所を直す | S-2、S-3、S-4 の項目 | 下の「3.3」 |
| G6 | 計測の形式を維持する | 過去の記録と同じ形式で出る | — |

目標でないもの: 見た目の変更、他メンバーの機能(椅子・サウンド・ランキング)、突入演出。

## 3. 設計

### 3.1 全体

```
Manager(常駐)
  GameFlowRunner / TransitionFadeController / TitleLogoOverlay / MultiScreenOutput
  CameraRig(新)  ── 常駐カメラ(Camera + HDAdditionalCameraData + CinemachineBrain)
                      DLSS は常時オン。出力先は MultiScreenOutput の RenderTexture
State のシーン(Load / Splash / Opening / Menu / Map / Result)
  カメラは置かない。置くのは、Cinemachine の仮想カメラ、Volume、StateCameraProfile(新)
```

- 変わるのは、カメラの持ち主だけ。シーンの切り替え(差分ロード)、State の呼び出し、フェードの構造は、踏襲する(`GameFlow.md`)。
- カメラはシーンと一緒に作られて破棄される現行の形をやめる。DLSS のコンテキストを作る回数が、シーンの数・周回の数に比例しなくなる(`推定`)。

### 3.2 カメラ構成

#### 現行(`コード`。`Temp_RebuildSnapshot.md` の「6」と、2026/11/13 に読み直した値)

| シーン | カメラ | FOV | near / far | カリング | Depth | 動的解像度 | Cinemachine |
|---|---|---|---|---|---|---|---|
| `Load` | `Main Camera` | 60 | 0.3 / 500 | レイヤー 10 を除く | -1 | **0** | なし |
| `Splash` | `Main Camera` | `Load` と同じ ID(同じ設定とみられる。動的解像度 0 だけ確認) | | | -1 | **0** | なし |
| `Opening` | `UICanvas/MainCamera` | 60 | 0.01 / 100 | レイヤー 10 を除く | 0 | 1 | Brain + 仮想カメラ 21 台(`CinemachineCamera` 18 + `CinemachineVirtualCamera` 3) |
| `Menu` | `Main Camera` | 50 | 0.01 / 100 | レイヤー 10 を除く | -1 | 1 | Brain + 仮想カメラ 3 台(`CinemachineVirtualCamera`) |
| `Map` | `'###Vehicle###'/MainCamera` | 50 | 0.01 / 500 | 全レイヤー | 0 | 1 | Brain + 仮想カメラ 7 台 |
| `Result` | `Main Camera`(+ 実行時の表示用カメラ、+ 無効の `PrintCamera`) | 60 | 0.3 / 1000 | レイヤー 0〜9 | -1 | **0** | なし |

- 全カメラで、クリアは Skybox(背景色は共通)、Viewport は 0,0,1,1、`TargetDisplay` 0、`allowDeepLearningSuperSampling` 1、品質 `1`(カメラごとの設定。`deepLearningSuperSamplingUseCustomQualitySettings` 1)。
- **DLSS が動くのは、動的解像度が有効な `Opening`・`Menu`・`Map` の 3 台だけ**(`コード`。`HDRenderPipeline.SetupPartnerUpscalers` は、`allowDynamicResolution` が無効なカメラでは DLSS を無効にして戻る)。`Load`・`Splash`・`Result` の DLSS は、設定が有効でも動いていない。
- DLSS のコンテキストは、品質・出力解像度・プリセット・入力解像度の上限・最適設定の使用が変わると、破棄して作り直される(`コード`。`DLSSPass.UpdateViewState`)。**動的解像度や DLSS をシーンごとに切り替えると、そのたびにコンテキストの破棄と作成が起きる**(`コード`。`BeginFrame` が、DLSS が無効になったカメラの状態を破棄する)。
- `HDAdditionalCameraData` の値(アンチエイリアス、Volume のレイヤーマスク、フレーム設定、露出)を読んだのは、`Map` だけ(`antialiasing` 0、`volumeLayerMask` 1)。他は**未読**。

#### 案: 常駐カメラ 1 台

| 項目 | 内容 |
|---|---|
| 置き場所 | `Manager.unity` の `CameraRig`。`Camera.main` のタグを付ける(`RingSpectrumBars`・`EdgeSpectrumBars` が `Camera.main` を使うため。`コード`) |
| 部品 | `Camera`、`HDAdditionalCameraData`、`CinemachineBrain`。DLSS は起動時に有効にして、実行中は変えない |
| 出力 | `MultiScreenOutput` が、起動時に 1 回だけ RenderTexture を設定する(Editor では設定しない)。`MultiScreenSourceCamera`、`SetSourceCamera` は廃止 |
| シーン別の値 | `StateCameraProfile`(新。各 State のシーンに置く)が持つ: カリング、クリア、Volume のレイヤーマスクなど。`CameraRig.Apply(_profile)` を、`GameFlowRunner` が **`Enter()` の前**(画面が覆われている間)に呼ぶ |
| レンズ(FOV・near・far) | Cinemachine の仮想カメラが持つ(Brain がカメラへ反映。`推定`。Cinemachine 6.6 での確認は未)。仮想カメラがない `Load`・`Splash`・`Result` は、`StateCameraProfile` に既定のレンズを持たせるか、仮想カメラを 1 台ずつ置く(未決) |
| 動的解像度 | **常時オン**。DLSS はシーンを切り替えても、止めない・作り直さない(G1) |
| シーン別の DLSS オフ | `StateCameraProfile` の項目として持つ。**既定は全シーンでオン。** 切り替えるとコンテキストが作り直される(上)ので、(3) 相当(Map だけ DLSS)へ戻す保険としてだけ使う |

#### 詰める点

| ID | 内容 | 状況 |
|---|---|---|
| C-1 | Brain の集約。仮想カメラはシーン側に残し、常駐カメラの Brain が選ぶ。シーンの遷移中は、新旧のシーンの仮想カメラが同時にある。優先度の衝突を避ける方法(シーンごとの帯、または古いシーンの仮想カメラを先に無効化)。`SwitchingCamera`(Map の Home キー)は、優先度 10 / 11 を直接書き換える(他メンバー作成) | 決定(6 章) |
| C-2 | `Map` のカメラは車両の子。常駐カメラへ移すと親子でなくなる。Brain が仮想カメラへ追従させるので、親子は不要とみられる(`推定`)。`VehicleController` などがこのカメラを参照していないかは未確認 | 要確認(段 1 の最初) |
| C-3 | `Load`・`Splash`・`Result` も DLSS を通る(常時オンのため)。見た目(UI・黒背景)と負荷が変わらないか | `未確認`。Editor と実機で確認 |
| C-4 | `Result` の表示用カメラ(`ResultState.EnsureDisplayCamera`)は、Overlay の Canvas を映すために実行時に作られている(`コード`)。常駐カメラ + 表示用の常駐カメラ(DLSS なし、描画なし)にするか、Canvas を RenderTexture 側へ載せるか。`PrintCamera`(無効、`Result` の Screen Space - Camera の Canvas が参照)は範囲外で、そのまま | 決定(6 章) |
| C-5 | Opening の曲間 Splash 挿入は、現行は Opening のカメラを `SetActive(false)` にしている(`OpeningState.cs` の 160〜185 行)。常駐カメラでは、それをしない。Splash の画面の間、Opening の 3D が透けないように、`StateCameraProfile` でカリングを切り替えるか、Opening の描画を止める | 決定(6 章) |
| C-6 | カメラを参照する Canvas: `Result` の Screen Space - Camera の Canvas(`PrintCamera` を参照)。`Menu` に `m_Camera` の参照が 1 件(何の参照かは未確認)。`Opening` の `EdgeSpectrumBars`・`RingSpectrumBars` は、親のカメラまたは `Camera.main` を使う | 要確認(段 1 の最初) |
| C-7 | `HDAdditionalCameraData` の全値(`Map` 以外)、仮想カメラのレンズと Brain の設定(ブレンド、更新方式)を、段 1 の最初に控える | 未取得 |
| C-8 | Editor でシーン単体を再生したとき、`Manager` が読み込まれるまでカメラがない(Editor のみ。一瞬)。影響は小さい | 受け入れる案 |
| C-9 | 他メンバーのファイル: `SwitchingCamera`、`CustomizeCameraRotation`(Cinemachine の拡張)、`EdgeSpectrumBars`、`RingSpectrumBars`。直す前に、担当者へ確認する | 未確認(担当者) |

### 3.3 遷移・State(骨格は踏襲)

構成は、`GameStateId` / `IGameState` / `GameSceneGroup` / `GameFlowGraph` / `GameFlowRunner` / `GameStateBindingBase` / `FlowTiming` / `SessionCacheRefresher` / `TransitionFadeController` のまま(`GameFlow.md`)。直す点:

| ID | 内容 | 出所 | 状況 |
|---|---|---|---|
| F-1 | 緊急リセットで、現 State の `Exit()` を呼ぶ(または購読を `OnDestroy` でも解除する)。`MenuState` の `TcpServer.OnNameReceived` の購読が残り、例外になる恐れ | S-2、S-3(優先順位 ⑤) | 決定(6 章): 購読を `OnDestroy` でも解除する。`Exit()` は呼ばない |
| F-2 | 遷移中の `AdvanceToNext()` の無視で、(Editor と Development Build で)ログを出す | S-2 | 案 |
| F-3 | 「Loading 画面を挟む State」をコードの定数でなく、`GameFlowGraph` のデータにする | S-2 | 案 |
| F-4 | 入口の State を `GameFlowGraph` に持たせ、`Intro` と `Racing` を同じ GameObject に置く前提をやめる(State を足しやすくなる。G3) | S-2 | 案 |
| F-5 | BGM 別の保持秒数を、クリップ名の文字列一致でなく、`AudioClip` の参照で照合する。見つからなければ警告 | S-4 | 案 |
| F-6 | ロゴ・Loading 画面・遷移フェードの描画順を、重ならない値にする(現在はロゴと Loading が同じ 1100) | S-4 | 案 |
| F-7 | `GameFlowRunner` が、シーンを読んだあと、`Enter()` の前に `CameraRig.Apply` を呼ぶ | 3.2 | 案 |
| F-8 | `GameStateId` の値は変えない(`Intro = 4`、`3` は欠番)。計測の `state` 列と対応するため | 計測の形式 | 決定(前提) |

| ID | 内容 | 状況 |
|---|---|---|
| P-1 | **Loading 画面を `Manager` に常駐させる。** 現行は遷移ごとに `Loading` シーンを追加ロードして破棄し、Canvas をモニター数ぶん作る。常駐にすると、遷移ごとのロード・破棄・Canvas の生成がなくなる(`推定`。軽量化。G4)。BGM の先行再生などの動作は、そのまま移す | 決定(6 章)。効果は未測定 |
| P-2 | Menu の間に `Map` を先読みして、Menu → Intro の `work` を縮める | 案。Map を先に常駐させるので、メモリが増える(「最も軽量」と食い違う)。計測してから決める。**本リビルドでは扱わない** |

### 3.4 画面出力

| 項目 | 内容 |
|---|---|
| `MultiScreenOutput` | 常駐カメラを直接持つ。`SetSourceCamera`・古い出力先を外さない問題(S-6)・起動順のエラーが、なくなる。Editor では分割しない仕様を維持する。再構築 |
| `WindowsMonitorInfo`、`MultiScreenTargetSelector` | 同じ役割で再作成。`MultiScreenTargetSelector` の確認済みの 13 件(`MultiScreenOutput.md`)を、テストとして引き継ぐ |
| 実機の確認 | ウィンドウの位置で対応を確かめる方式は、プレイヤーで未確認(`MultiScreenOutput.md`)。再作成のあと、実機で確認する(優先順位 ④) |
| `TransitionFadeController`、`LoadingScreenController` | モニター数を基に Canvas を作る方式は維持。台数が食い違うときの扱いを決める(段 2 で) |

### 3.5 突入演出(廃止)

- 外すもの: `RacingState.m_enterEffectPattern` と呼び出し、`Map.unity` の `m_enterEffectPattern`(4)、`GameStates.md` の突入演出の節、`RacingEnter*` 3 ファイル(作り直さない。現行のファイルは、段 4 の最後に削除)。
- 残るもの: HDRP の Custom Pass の設定は、実行時に登録されるので、シーン・アセットに残らない(`文書`: `GameStates.md`)。
- 必要になったら、あとで作り直す。`BlurFocus` の 3 画面での動作と負荷の確認は、そのときまで不要。

### 3.6 名前と API の方針(案)

「API も変える」(決定済み)の範囲で、**他メンバーのファイルへの影響を最小にする**案。

| 区分 | 方針 | 理由 |
|---|---|---|
| 外から呼ばれる 5 つ(`GameFlowRunner.Instance`、`AdvanceToNext`、`EmergencyReset`、`CurrentId`、`OnStateFadeInComplete`) | 名前と型を維持し、挙動だけ直す | 呼び出し側の他メンバー 7 ファイルの変更をなくす(`Temp_RebuildSnapshot.md` の「4」)。変えるなら担当者への確認が要る |
| `GameStateId`、`AutoDemoDriver`、`SingletonMonoBehaviour`、`AppLog` | 名前を維持する | `AppLog` は 35 ファイル、`SingletonMonoBehaviour` は 4 クラスが使う |
| 役割が変わるもの | 新しい名前にする: `CameraRig`(新)、`StateCameraProfile`(新)。`MultiScreenSourceCamera` は廃止 | 役割が違うので、名前を引き継がない |
| メンバ名 | `m_` / `m_p` / `m_b` / `k_`、引数は `_` 始まり、ヘッダ、波括弧の改行は `CLAUDE.md` と既存の自作ファイルのとおり | 規約 |
| 条件コンパイル | `AppLog` を作るときに決める(`DEVELOPMENT_BUILD` は 6.6 で非推奨) | `Temp_RebuildProgress.md` の「3」 |

### 3.7 計測との関係

- 形式は維持する(前提)。`PerfAutoLapRunner` の DLSS の条件は、カメラ構成で意味が変わる。

| 条件 | 現行 | 新構成での扱い(案) |
|---|---|---|
| `base` | 全シーンのカメラで DLSS | 常駐カメラで常時オン(新しい基準) |
| `nodlss` | DLSS をすべて止める | DLSS をオフにしたまま起動する(対照) |
| `maponly` | Map 以外のカメラの DLSS を止める | `StateCameraProfile` のシーン別オフで、Map のときだけオン(旧 `maponly` 相当。再作成が起きるので、効果は測るまで未確認) |
| `unloadoff` | 破棄の前に DLSS を止める | **意味を失う**(カメラを破棄しない)。bat と `lap_report.py` の条件の扱いは未決 |

- 新構成の基準は、旧 `base`(+908 MB/周)と、同じ解像度で比べる。計測は全段のあとにまとめて行う(前提)。

## 4. 移行計画

段の構成は `Temp_RebuildTargets.md` のまま。決定(カメラ構成、突入演出)で変わる点:

| 段 | 変わる点 |
|---|---|
| 0 | 変更なし(`SingletonMonoBehaviour` に `HasInstance`、`AppLog`) |
| 1 | `CameraRig`・`StateCameraProfile` を追加。`MultiScreenSourceCamera` を廃止。**シーンの 6 つ(と `Manager`)を一括で変える**: 常駐カメラとシーンのカメラが同時に RenderTexture へ描かないよう、導入と撤去を分けない。最初に C-2・C-6・C-7 を確認して控える |
| 2 | `GameFlowRunner` に `CameraRig.Apply`、緊急リセットの `Exit()`。`GameFlowGraph` に入口 State・Loading 画面を挟む State(F-3、F-4)。Loading 画面の常駐化(P-1)は、段 2 と段 4 にまたがる |
| 3 | `OpeningState` の曲間挿入からカメラ操作を外す。`ResultState` の表示用カメラ(C-4)。`RacingState` から突入演出を外す。`MenuState` の購読(F-1) |
| 4 | `RacingEnter*` 3 ファイルは作り直さない(`保留`)。現行のファイルは、この段の最後に削除。`LoadingScreenController` は F-5、F-6、P-1 |
| 5 | `PerfAutoLapRunner` の条件を再定義(3.7) |
| 6 | 変更なし |

- 各段の確認: コンパイル、Missing Script 0、Editor での通し再生。段 1 は、さらに 3 画面の実機でシーン 6 つの見た目(C-3)を目視する(あなたの確認)。
- 他メンバーのファイルは、直す箇所の一覧を見せ、担当者へ確認してから直す(`Temp_RebuildTargets.md` の「注意」)。
- コミットは、段の区切りごと。プッシュの前に、あなたへ確認する。

## 5. リスク

| ID | リスク | 対応 |
|---|---|---|
| R-1 | 常駐カメラでも増える(原因 H1・H2 が未切り分け。`PerfFindings.md`) | `StateCameraProfile` のシーン別オフで、(3) 相当(Map だけ DLSS)へ設定だけで戻す。ただし、戻すと DLSS の作成が周回ごとに 1 回起きる(旧 `maponly` の +377 MB/周に近い。`推定`) |
| R-2 | 常時オンで、`Load`・`Splash`・`Result` の見た目や負荷が変わる(C-3) | 段 1 の目視。問題があれば、この 3 シーンだけ DLSS オフ(再作成が起きるので、最後の手段) |
| R-3 | シーンごとのカメラの設定の取りこぼし(`HDAdditionalCameraData` の未読分) | 段 1 の最初に全値を控える(C-7) |
| R-4 | Cinemachine の優先度の衝突、Cinemachine 6.6 の挙動の違い(UP-5) | C-1 の設計。Editor での確認 |
| R-5 | DLSS のコンテキストが、動的解像度・品質・プリセットの変更で作り直される | 実行中に変えない制約にする(3.2) |
| R-6 | 他メンバーのファイルの変更に、担当者の確認が要る | 段 1・2 の前に、直す箇所の一覧を作る |
| R-7 | 計測が最後なので、効果の確認が遅い(`Temp_RebuildProgress.md` の「次の一手」の 4) | 未決。カメラに関わる段(段 1)だけ先に計測するかは、あなたが決める |

## 6. 未決と決定

2026/11/13 に、下書きの案どおりに 7 件を決めた(あなたの回答)。

| 項目 | 決定 | 状態 |
|---|---|---|
| G1 の目標値(MB/周) | **1 日 140〜160 プレイを、再起動なしで回せる増加。** 換算は約 33 MB/周以下(`推定`。1 周目の最大 VRAM 約 9.5 GB(10/22 の 4 条件で 9,034〜9,964 MB)から、総量 16,050 MB の 90%(仮の値)= 14,445 MB までの約 4,950 MB を、150 プレイで割った値)。旧 `base` +908、`nodlss` +15(`実測`)と比べる | 決定 |
| Opening・Menu の FPS の下限 | 常時オンでは DLSS が残るので、決めない。(3) 相当へ戻す場合に決める | 決定(保留) |
| C-1 の方式(優先度の衝突) | シーンごとの優先度の帯 | 決定 |
| C-4(`Result` の表示用カメラ) | 表示用の常駐カメラ(DLSS なし、描画なし) | 決定 |
| C-5(曲間の Splash 挿入) | `StateCameraProfile` のカリングで切り替える | 決定 |
| P-1(Loading 画面の常駐化) | 採用(軽量化)。効果は、全段のあとの計測で確認する | 決定 |
| F-1(緊急リセットの購読の残り) | `MenuState` の購読を `OnDestroy` でも解除する(`Exit()` は呼ばない。副作用が小さい) | 決定 |
| 他メンバーの担当者への確認(誰が、誰に) | 案がなかったので、未決のまま。段 1・2 の前に決める(R-6) | 未決 |
| 範囲の端(`.cs` 以外)、ポートフォリオの残り | 案がなかったので、未決のまま(`Temp_RebuildProgress.md` の「そのほかの未決」) | 未決 |

- 3 章の「案」(F-2〜F-7、C-8、3.6 の名前と API の方針、3.7 の計測の条件)は、この回答の対象に入っていない。**案のまま**で、該当する段の着手前に確認する。3.6 は、段 0 の前に確認する。
