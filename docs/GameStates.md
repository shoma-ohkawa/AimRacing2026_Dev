# 各 State の実装

作成: 2026/10/10(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/GameManager/GameStates/`

遷移エンジンが管理する 7 つの State(Load / Splash / Opening / Menu / Intro / Racing / Result)の中身。1 プレイを、入力の受付、次へ進む条件、椅子(WIZMO)の姿勢、BGM で組み立てる。

どの State も `GameStateBindingBase` を継承した MonoBehaviour で、対応するシーンに 1 つ置く。`Enter()` / `Tick()` / `Exit()` を呼ばれ、次へ進めるときは `GameFlowRunner.Instance.AdvanceToNext()` だけを呼ぶ(`GameFlow.md`)。

## 構成

| ファイル | State | 役割 | 作成 / 更新 |
|---|---|---|---|
| `LoadState.cs` | Load | 起動時とリセット時の中継。リフレッシュを実行して Splash へ | 09/16 / 09/16 |
| `SplashState.cs` | Splash | ロゴ演出の終了を待つ。椅子を降車姿勢に保つ | 09/01 / 09/01 |
| `OpeningState.cs` | Opening | OP ムービーと BGM。入力で Menu へ。曲間の Splash 挿入と Load への折り返し | 09/01 / 09/17 |
| `MenuState.cs` | Menu | 名前入力の待機、AT/MT 選択、二段階の確定 | 09/01 / 09/18 |
| `IntroState.cs` | Intro | イントロ映像。終了時に走行の準備を整えて Racing へ | 09/01 / 09/14 |
| `RacingState.cs` | Racing | 走行の開始設定、突入演出、ゴール処理 | 09/01 / 10/15 |
| `ResultState.cs` | Result | 結果の表示と Thank you への切り替え | 09/01 / 09/14 |

## State 一覧

| State | シーン | 次へ進む条件 | 椅子 | BGM |
|---|---|---|---|---|
| Load | `Load` | 自身の FadeIn が完了し、リフレッシュが終わったら | 触らない | なし |
| Splash | `Splash` | ロゴ演出(`SplashFade`)が終わったら | 降車姿勢を毎フレーム | なし |
| Opening | `Opening` | ペダルを一度離してから踏む、または ↑キー | 入力時に乗車姿勢 | OP |
| Menu | `Menu` | AT/MT を選び、アクセル(↑キー)を 2 回 | 触らない | Entry |
| Intro | `Map` | イントロ映像の終了(PageDown でスキップ可) | 走行用の姿勢 | InGame |
| Racing | `Map` | ゴールの検知 | 走行中は動かし、ゴールで停止 | InGame → Result を先行再生 |
| Result | `Result` | この State 自身は進めない。結果画面の制御側が一定時間後(または手動)に進める | 降車姿勢を毎フレーム | Result |

## 共通の約束

1. `GameStateBindingBase` を継承し、`Id` で自分の `GameStateId` を返す。
2. 初期化は `Enter()`、毎フレームは `Tick(float _deltaTime)`、後片付けは `Exit()`。`Tick` は遷移処理の間は呼ばれない。
3. 次へ進めるのは `AdvanceToNext()` だけ。一度しか呼ばないよう、フラグ(`m_changed`、`m_hasFinished` など)で守る。
4. **画面が見える前に入力を受け付けない。** `Enter()` から Loading 画面やフェードが終わるまでの間も、`Tick` と入力のコールバックは動く。準備完了のフラグが立つまで入力を無視する。Opening は `OnStateFadeInComplete` で `m_isReady` を立てる。Menu は UI の表示完了で `m_isEntryTransmissionActive` を立てる。
5. **BGM は、再生中かを確認してから鳴らす。** Loading 画面の側で先行再生していることがあるため、`Enter()` で `IsBGMPlaying()` を確認する。
6. **椅子の姿勢は `ChairRideOperator` で設定する。** インスタンスを `new` して持たないと WIZMO を制御できない。`WIZMOController` は `GameManager.Instance.WIZMO` から取り、`null` なら何もしない。
7. 外部機器(Logitech SDK など)の呼び出しは、例外で進行が止まらないよう `try/catch` で囲む。

## State ごとの詳細

### Load

`Enter()` で自身の FadeIn 完了(`OnStateFadeInComplete`)を待つ。受けたらすぐ購読を解除し、`SessionCacheRefresher.Refresh()` を実行して、終わったら `AdvanceToNext()`(Splash へ)。画面が見えてから GC などを実行するので、重さが Load 画面の表示前に出ない。`OnDestroy()` で購読を解除する。

### Splash

`Enter()` で `GameManager.Instance.WIZMO` を取得。`Tick()` で椅子を降車姿勢にし、`SplashFade.IsEndAnimation` が `true` なら `AdvanceToNext()`。`SplashFade` は登録された画像を「FadeIn → 保持 → FadeOut」で順に出し、PageDown で早送りできる。

### Opening

OP ムービー(Timeline)と BGM を流し、来場者の入力で Menu へ進める。

- `OnEnable()` / `OnDisable()` で `OnStateFadeInComplete` を購読 / 解除。FadeIn 完了で `m_isReady = true`。
- `Enter()`: 入力機器の共有設定、Timeline の再生、BGM の再生(再生中でなければ)、WIZMO の取得。
- `Tick()`: ① 曲間の Splash 挿入の判定 ② Load への折り返しの判定 ③ 入力による遷移。
- 入力は `OnPedal`(ペダル軸)と `OnTriger`(↑キー)。`m_isReady` までは無視する。
- 入力時の処理: BGM をフェードアウト → 決定音 `OPPushSE` → OP 音声 `m_opAudio` をフェードアウト → `m_changed = true` → `AdvanceToNext()` → 椅子を乗車姿勢に。

**曲間の Splash 挿入(`PlaySplashInterlude`)**: Timeline の時刻が「BGM の長さ + `m_loopInterval`」に達したとき、1 回だけ。Timeline を止め、BGM をフェードアウト → 白で FadeOut(0.3 秒) → `Splash` を Additive で読み込み、Opening のカメラを無効化 → FadeIn → `SplashFade` の終了を待つ → 白で FadeOut → カメラを戻して `Splash` をアンロード → Timeline を 0 秒から再生して BGM を再生 → FadeIn。挿入中は、入力と折り返しの判定を止める。

**Load への折り返し(`ReturnToLoad`)**: Timeline が `(5540 − 90) / 60 ≈ 90.8` 秒に達したとき(60 fps の 5540 フレームの 90 フレーム手前)。入力は待たない。画面の FadeOut と同じ秒数で BGM と OP 音声をフェードアウトし、`EmergencyReset(Load)` で最初から繰り返す(PV・デモ用)。

Inspector: `m_BGMFadeTime`(0.1)、`m_loopInterval`(1)、`m_director`、`m_opAudio`、`m_fadeOutDuration`(既定 1)。

### Menu

```
名前入力待ち ─(名前を受信 / F3)→ Entry 案内 ─(3 秒)→ AT/MT 選択
                                                          │ ハンドルの左右で選ぶ
                                                          ▼
                                            1 回目のアクセル → 最終確認
                                                ▲ ブレーキで戻る │
                                                          ▼
                                            2 回目のアクセル → 確定 → Intro
```

- `Enter()`: 入力機器の共有設定、BGM(Entry)、既定の選択を AT、UI の初期化、名前入力用の TCP サーバーの開始と `OnNameReceived` の購読。`Exit()` で停止と購読解除。
- `Tick()`: F3 で名前入力をスキップ(名前は `Staff`)。2 回目のアクセルまたはトリガーで確定したら、BGM をフェードアウトし、`DrivingSettings` を `GameManager` に保存して `AdvanceToNext()`。
- 画面は Scale の 0 ↔ 等倍で出入りする。名前を受信すると、案内を縮小 → Entry を拡大 → 3 秒後に縮小 → AT/MT 選択を表示(ガイド音声)。

| 変数 | 意味 |
|---|---|
| `m_isNameEntryDone` | 名前を受信済みか(二重発火の防止) |
| `m_isEntryTransmissionActive` | AT/MT 選択の UI が表示され終えたか。`false` の間は `OnPedal`・`OnSelect` を無視 |
| `m_isSelected` | AT/MT のどちらかを選んだか。`false` の間はアクセルを受け付けない |
| `m_isConfirming` | 1 回目のアクセルで最終確認に入っているか。`true` の間はハンドルの選択を受け付けない |
| `m_triger` | 2 回目の確定が入ったか |
| `m_changed` | 遷移を始めたか(`m_triger` とは別に持つ) |

- 左右の選択: ハンドルの値が `m_steerRange`(0.01)を超えて右なら MT、左なら AT。
- 1 回目: アクセル(ペダル軸の踏み込み、または ↑キー)で最終確認に入る。AT/MT の音声を再生し、`TwoChoice` が中央へ移動・拡大する。
- 2 回目: 確認に入ってから `m_confirmMinDelay`(0.3 秒)後のアクセル。決定音 `EntryPushSE`。1 回目が押した時と離した時の両方で呼ばれても、直後に確定しないための最短間隔。
- 戻る: 最終確認中にブレーキ(ペダル軸、↓キー、ゲームパッドの LT)で、確認を解除して選択へ戻る。確定済みなら何もしない。
- Inspector: `m_steerRange` 0.01、`m_confirmMinDelay` 0.3、`m_bgmFadeOutDuration` 0.1、`m_nameEntryScaleDuration` 0.2、`m_entryScaleDuration` 0.3、`m_entryDisplayDuration` 3。

### Intro

- `Enter()`: BGM を止めて InGame を再生、車両を接地させる準備、カウントダウンと走行音を無効化、椅子を走行用の姿勢に、`k_MoviePlayDelaySeconds`(0.5 秒、実時間)後に映像を再生。
- `Tick()`: メーターを隠す。映像が終わったら `ChangeToRacing()`。映像がなければすぐ進める。PageDown で映像の終端の 0.1 秒手前へ飛ぶ。
- `ChangeToRacing()`(一度だけ): メーターを表示、カウントダウンと走行音を有効化、キーボード用の代替アクセル入力(`StartGateKeyboardFallback`)を取り付け、`OkutamaName` を隠して `AdvanceToNext()`。

Intro → Racing は同じ `Map` なので、シーンの読み込みは起きない。

### Racing

- `Enter()`: ゴール処理の状態を初期化、椅子の走行動作を開始、メーターを表示、`OkutamaName` を隠す(F2 で Intro を飛ばした入場でも残さないため)、車両の `PullUp(true)`、`DrivingSettings.isAT` に応じて AT/MT を設定、最後に突入演出を再生。
- `Exit()`: 再生中の突入演出を止め、実行時に作った Volume と Custom Pass を取り除く。
- `Tick()`: F5(AT/MT の切り替え)、テンキー `*`(レースの完了)。ゴールを検知したら下記。

**ゴール処理**(`Line_StartFinish.IsChecked` が `true`):
1. 一度だけ: 音楽の再開、ゴールの SE、状態を「ゴール後」に。
2. 一度だけ: Logitech のフォースフィードバックを停止。ハンコンの切断や SDK 未接続の例外は `try/catch` で警告にとどめ、続行する。
3. 一度だけ: `WaitFadeOut(k_GoalFadeDelaySeconds = 0)` を開始。Result の BGM を先行再生し、`AdvanceToNext()` で Result へ。
4. ゴール後は毎フレーム、椅子の走行動作を止める。

**突入演出**(`Assets/#Scripts/Effect/RacingEnter/`、2026/10/15 追加): `RacingState.m_enterEffectPattern` で選ぶ(既定 `LaunchCombo`、`None` で無し)。ポストプロセスのみ。ビネット・色収差・ブルーム・露出・彩度は、優先度 1000 のグローバル Volume を実行時に作り、重みを 1 → 0 へ動かす。ぼかしは、全体の Custom Pass を実行時に登録して半径を動かす。シーンの Volume(優先度 0 と 100)とシーンのファイルは変えない。演出の終了・`Exit()`・破棄のどれでも取り除く。時間は実時間で進み、ポーズ中も止まらない。値は `RacingEnterEffect.cs` の定数で調整する。カメラ演出(画角・揺れ)は取りやめた。

| パターン | 内容 |
|---|---|
| `None` | 演出なし |
| `VignetteOpen` | 周辺が暗く(ビネット 0.9・露出 −1 EV)、色の薄い(彩度 −60)視界が、1.8 秒で明るく開く。最初の 25% は暗がりを保つ |
| `ChromaticFlash` | 露出 +3.5 EV で白く飛び、ブルームと色収差を最大から 1.2 秒で収める |
| `LaunchCombo` | 露出 +2 EV の光とブルーム、彩度 +25、色収差 0.8、ビネット 0.7 を 1.6 秒で収める |
| `BlurFocus` | 描画済みの画像全体がガウスぼかし(半径 32)でぼけた状態から、2.5 秒で鮮明になる。最初の 10% はぼけを保つ |

`BlurFocus` は `BeforePostProcess` に Custom Pass(`RacingEnterBlurPass`)を登録する。`AfterPostProcess` では、DLSS のアップスケール後にバッファの大きさが食い違い、映る範囲がずれたため。半径は作業用バッファ(画面の半分)のピクセルで数えるので、フル解像度では約 2 倍に見える。メーターなどの Overlay の UI は、ポストプロセスの対象外でぼけない。値はすべて推定で、Unity 上の見え方に合わせて調整する。**確認状況: 単体コンパイルのみ。Unity 上の見た目と 3 画面での動作は未確認。**

### Result

- `Enter()`: Overlay の Canvas を映すカメラを用意、Result の BGM(Racing で先行再生済みなら鳴らさない)、WIZMO の取得、結果 UI を表示して Thank you を透明に、`ShowThankYouAfterDelay` を開始。
- `Tick()`: 椅子を降車姿勢に(毎フレーム)。
- `EnsureDisplayCamera()`: Result シーンに「描画先がテクスチャでない有効なカメラ」がないときだけ、黒背景のカメラを作る(Overlay の Canvas の表示に必要)。
- Inspector: `m_resultDisplayDuration` 30、`m_fadeDuration` 1。

| 経過 | 内容 |
|---|---|
| 0 秒 | 結果 UI を表示 |
| 30 秒 | 結果 UI のフェードアウトを開始(入力を無効化) |
| 31 秒 | 結果 UI が消え、Thank you の FadeIn を開始 |
| 32 秒 | Thank you が完全に表示 |
| 37 秒 | タイトルへの復帰 |

タイトルへの復帰(Result BGM のフェードアウトと `AdvanceToNext()`)は、`AimRacingResultFlowController`(別のメンバーの担当)が行う。待ち時間 `m_returnDelaySeconds`(37 秒)は、`ResultState` の 32 秒に Thank you の表示 5 秒を足した値。`ResultState` の秒数を変えたら、復帰側も合わせて変える。

## 入力の配線

`Opening` と `Menu` の `PlayerInput` が、アクションごとに State のメソッドを呼ぶ。

| アクション | 入力元 | Opening | Menu |
|---|---|---|---|
| `AccelPedal` | ハンコンのアクセル軸 | `OnPedal` | `OnPedal` |
| `AccelTriger` | キーボード ↑ | `OnTriger` | `OnTriger` |
| `Steering` | ハンコンのハンドル軸、キーボード ← → | — | `OnSelect` |
| `SteeringKeyboard` | ゲームパッドの左スティック、キーボード ← → | — | `OnSelect` |
| `BrakeTriger` | キーボード ↓、ゲームパッドの LT | — | `OnBrakeTriger` |
| `BrakePedal` | ハンコンのブレーキ軸 | — | `OnBrakePedal` |

- ペダル軸は `1 − (値 + 1) / 2` で正規化する(0 が離した状態、1 が踏み切り)。
- 「踏んだ」の判定は `AxisPressdOnce`。**一度 0.1 以下まで離したことを確認してから、0.8 以上に踏み込んだときだけ**踏まれたとする。踏んだままシーンが切り替わっても、すぐには反応しない。
- Menu の検出器は 3 つ(1 回目のアクセル、2 回目のアクセル、ブレーキ)。1 回目の足を離さないと 2 回目にならない。キャンセルで 3 つとも `Reset()`。
- `OnTriger` / `OnBrakeTriger` は、押した時と離した時の両方で呼ばれうる。Menu は最短間隔(`m_confirmMinDelay`)とフラグで二重の確定を防ぐ。
- 常駐の `PlayerInput` が機器を先に取得していても入力が空にならないよう、Opening と Menu の `Enter()` で `MenuInputDeviceSharing.Ensure()` を呼ぶ。

## 椅子と BGM

| `ChairRideOperator` | 設定 | 使う State |
|---|---|---|
| `Ride`(乗車位置) | roll・pitch・yaw・heave・sway・surge を 0、accel と speed を 0.1 | Opening(入力時に 1 回) |
| `Drive`(走行位置) | `Ride` と同じで heave を 0.5 | Intro(`Enter()` で 1 回) |
| `RideOff`(降車位置) | 各軸 0、accel と speed を 0.1 | Splash・Result(毎フレーム) |

| State | 再生 | 停止・フェード |
|---|---|---|
| Opening | OP(先行再生済みなら鳴らさない) | 入力時に `m_BGMFadeTime`。折り返し時は画面のフェードと同じ秒数 |
| Menu | Entry をループ(再生中でなければ) | 確定時に `FadeOutBGM`。直後に `StopBGM` は呼ばない(フェードが途切れる) |
| Intro | InGame をループ(再生前に `StopBGM`) | — |
| Racing | ゴール時に Result をループで先行再生 | — |
| Result | Result(先行再生済みなら鳴らさない) | 復帰時に結果画面の制御側がフェードアウト |

`PlayAndSwitchBGM` は、呼ぶたびに内部で「次に鳴らす曲」を切り替える。同じ曲を 2 か所から鳴らすと 2 回目が別の曲へのクロスフェードになるので、`IsBGMPlaying()` で確認する。Result のループ指定は Racing と Result でそろえる。

## 他のメンバーのモジュール

State が呼び出す側。いずれも自作ではない。

| モジュール | 用途 |
|---|---|
| `ChairRideOperator`(豊田達也)、`WIZMOController`(プラグイン)、`ChairController`(要確認) | 椅子の姿勢と走行動作 |
| `SoundManager`(小林大輝が作成、後に更新あり) | BGM と SE |
| `GameManager`、`DrivingSettings`(要確認) | WIZMO の参照、AT/MT の受け渡し、ラップタイム |
| `DebugKey`(要確認) | デバッグキー(F3、F5、テンキー `*`) |
| `TcpServer`(要確認) | Menu の名前入力の受信 |
| `MenuInputDeviceSharing`、`AxisPressdOnce`(担当は要確認) | 機器の共有、ペダルの踏み込み判定 |
| `TwoChoice`(作成者不明、更新は鈴木友也) | AT/MT 選択の演出 |
| `SplashFade`(担当は要確認) | Splash のロゴ演出 |
| `AimRacingResultFlowController`、`AimRacingPlayerName`(要確認) | Result からの復帰、プレイヤー名の保持 |
| `VehicleController`、`Line_StartFinish`、`MeterUIManager`、`UI_StarCountDown`、`DriveSound`(要確認) | 走行側。Intro と Racing が有効・無効や設定を切り替える |
| `LogitechGSDK` | ゴール時のフォースフィードバック停止 |

## 未対応・要確認

| 項目 | 内容 |
|---|---|
| `EmergencyReset` で `Exit()` が呼ばれない | Menu の `OnNameReceived` の購読が残る恐れ。詳細と対策案は `GameFlow.md` |
| `OpeningState` の食い違い | `k_SecondSongStartTime`(113.3 秒)は宣言のみで未使用。`PlaySplashInterlude` のコメントは「2 曲目から再開」だが、実装は Timeline を 0 秒から再生。意図かコメントの古さかを確認する |
| 曲間挿入と折り返しの先後 | 曲間挿入は「BGM の長さ + 1 秒」、折り返しは約 90.8 秒で判定する。BGM の長さは曲ごとに違うので、どちらが先に起きるかが曲で変わる |
| 入力の検証ツール | `MenuInputValidation`(`Assets/DrivingTools/Validation/Editor/`)が、現行の `MenuState` にない `m_selectionReady`(`m_isEntryTransmissionActive` に当たる)をリフレクションで読む。そのまま実行すると失敗する可能性(未実行) |
| 静止ペダルの対策 | `MenuInputDeviceSharing.ObserveReleasedPedal` は検証ツールだけが呼び、`OpeningState`・`MenuState` は呼んでいない。実機での影響は未確認 |
| Result の待ちとポーズ | `ResultState` の待ちは `WaitForSeconds`(ゲーム内時間)、復帰側は `Time.unscaledTime`。F4 のポーズ中は、Thank you への切り替えが止まり、復帰のタイマーは進む |
| その他 | ペダル軸の正規化式が 3 か所に重複(`OpeningState` 1、`MenuState` 2)。`RacingState` の状態が整数の定数(`k_InitialState`、`k_GoalState`)で、`bool` か `enum` にできる。`Ride` と `RideOff` が現在は同じ値 |

## PDCA

担当: 9 名のチーム制作のうち、7 つの State の実装。入力機器の設定、TCP の名前入力、椅子、サウンド、ランキングは別のメンバーで、State は呼び出す側。期間: 2026/09/01〜09/18。

| | 内容 |
|---|---|
| 目標 | 来場者が 1 プレイを迷わず終えられる。① 画面が見える前の入力、離した時の通知、連打で、遷移や確定が早まったり二重に起きたりしない ② ハンコンの切断や入力受付による機器の先取りがあっても止まらない ③ 椅子と BGM が場面に合う ④ Opening から繰り返し再生できる(PV・デモ用)。達成の基準の数値は未記載 |
| 課題 | 画面が見える前に入力が通る / 多重入力で確定が早まる・二重に起きる / BGM が意図と違う曲になる・途切れる / 機器の状態で進行が止まる / Intro を経由しない入場で表示が残る / BGM の長さの更新漏れで曲間の判定が早く成立した(`SoundManager` は 2026/09/13 に修正) / Result の長さと復帰のタイミングが別々に設定されている |
| 検証・結果 | 7 つの State を実装した。入力のガード(準備完了のフラグ、離してから踏む判定、二段階の確定と最短間隔)、BGM のガード、椅子の姿勢、ゴール時の例外保護を実装。Result は表示 30 秒から復帰 37 秒まで整理し、Thank you が約 5 秒表示されることをシーンの値から確認した。ほとんどは実装とコードの確認のみ |
| 未記録 | 来場者による操作の確認結果、誤入力の発生状況 / ゴールから Result への遷移時間 / Loading 中に操作して無視されることの確認 / ハンコンを切断した状態での実機確認 / 展示(TGS2026)の結果 |
| 次の候補 | 緊急リセット時の購読の解除 / `OpeningState` の未使用定数とコメントの整理、曲間挿入と折り返しの先後の確認 / 入力の検証ツールの修正と実行 / 静止ペダルの対策を使うかの判断 / ペダル軸の正規化式の共通化 |
