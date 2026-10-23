# ゲーム進行(State 遷移エンジン)

作成: 2026/10/10(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/GameManager/Flow/`

ゲーム全体の進行を、State という単位で管理する。常駐シーン `Manager` を残し、State ごとのシーンを Additive でロード/アンロードして切り替える。

```
Load → Splash → Opening → Menu → Intro → Racing → Result ─┐
 ▲                                                         │
 └─────────────────────────────────────────────────────────┘
```

- 「どの State でどのシーンを読むか」「次の State は何か」は、`GameFlowGraph` の 1 か所に書く。
- 各 State は次を知らず、終わったら `AdvanceToNext()` を呼ぶ。
- 各 State はシーン内の MonoBehaviour で、起動時に自分から `GameFlowRunner` へ登録する。
- フェードと Loading 画面は別機能(`TransitionEffects.md`)。この機能が呼び出す側。

## 構成

| ファイル | 種別 | 役割 | 作成 / 更新 |
|---|---|---|---|
| `GameStateId.cs` | enum | State の識別子 | 09/01 / 10/19 |
| `IGameState.cs` | interface | `Id` / `Enter` / `Tick` / `Exit` | 09/01 / 09/01 |
| `GameSceneGroup.cs` | struct | 1 つの State が必要とするシーン名の組 | 09/01 / 09/01 |
| `GameFlowGraph.cs` | static class | State とシーン、State と次 State の対応(唯一の定義箇所) | 09/01 / 10/19 |
| `GameFlowRunner.cs` | シングルトン | 遷移エンジン本体 | 09/01 / 10/19 |
| `GameStateBindingBase.cs` | abstract MonoBehaviour | 各 State の基底。自己登録と初回起動の入口 | 09/01 / 10/19 |
| `FlowTiming.cs` | static class | 各ステップの所要時間を、計測側へ通知するだけ | 10/19 / 10/19 |

各 State の中身は `GameStates.md`。

## State とシーン

| 値 | State | 次 | 主シーン |
|---|---|---|---|
| 0 | Splash | Opening | `Splash` |
| 1 | Opening | Menu | `Opening` |
| 2 | Menu | Intro | `Menu` |
| 4 | Intro | Racing | `Map` |
| 5 | Racing | Result | `Map` |
| 6 | Result | Load | `Result` |
| 7 | Load | Splash | `Load` |

- 共有シーンは全 State 共通で、遷移しても読み直さない: `Manager`(常駐)、`MapEnvironment`(静的環境)。
- `GameStateId` の整数値は Inspector に保存されている(`LoadingScreenController.m_leavingState`、`TransitionFadeController.m_state`)。そのため `Intro = 4` を固定し、値 `3` は欠番にしてある。**並び替えと、欠番を詰める変更はしない。**
- `Intro` と `Racing` は同じ `Map` を使うので、遷移でシーンの差分がない。
- `GameSceneGroup` の並びは「共有シーン → 主シーン」。主シーンが `SetActiveScene` の対象。

| シーン | 置かれているもの |
|---|---|
| `Manager` | `GameFlowRunner`、`TransitionFadeController`、`TitleLogoOverlay` ほか |
| `Load` / `Splash` / `Opening` / `Menu` / `Result` | `LoadState` / `SplashState` / `OpeningState` / `MenuState` / `ResultState` |
| `Map` | `IntroState`、`RacingState`(**同一の GameObject**) |
| `Loading` | `LoadingScreenController`(遷移の裏で一時的に追加ロード) |

## GameFlowRunner

| メンバ | 内容 |
|---|---|
| `OnStateFadeInComplete(GameStateId)` | 遷移後の FadeIn が終わり、画面が完全に見えた直後に発火する static event。確実に描画されてから始めたい側が購読する |
| `CurrentId` | 現在の State |
| `Register` / `Unregister` | State の登録と解除。解除は、登録済みのインスタンスと一致するときだけ |
| `Bootstrap(_initialState, _startupSeconds, _managerLoadSeconds)` | 初回起動で、遷移ではなく直接その State に入る。後ろの 2 つは計測用(省略可) |
| `AdvanceToNext()` | `GameFlowGraph.NextState` に従って遷移する。定義がなければ警告のみ |
| `EmergencyReset(_targetId)` | 全シーンを破棄して、指定した State に入り直す(通常フローでは使わない) |
| `Update()` | 遷移中でなければ `Tick(Time.deltaTime)` を呼ぶ |
| `m_useLoadingScreen` | `false` で、全遷移が従来のフェードだけに戻る(`Manager.unity` では `true`) |

| 定数 | 値 | 意味 |
|---|---|---|
| `k_LoadingSceneName` | `"Loading"` | 遷移の裏で追加ロードするシーン |
| `k_StableFrameCount` | 2 | シーン切替後、描画が安定するまで隠したまま待つフレーム数 |
| `k_LoadingScreenLeavingStates` | Splash, Opening, Menu, Racing, Result | Loading 画面を挟む離脱元 |

`GameStateBindingBase` の `Awake` は、`Manager` が読み込み済みなら即座に `Register` し、未読込なら初回起動に入る。`OnDestroy` で `Unregister` する。

## 処理フロー

### 初回起動(Bootstrap)

どのシーンから再生しても、`Manager` が自動で読み込まれ、そのシーンの入口の State で始まる。

1. 最初のシーンの State の `Awake`: `Manager` が読込済みなら `Register` して終了。他の State が読込中なら、完了を待って `Register` のみ。1 番手(`s_isBootstrapping` で判定)なら、次へ。
2. `Manager` を Additive で読み込み、自分を `Register`。
3. `Bootstrap(ResolveBootstrapCandidate())`: 初期 State を登録 → 未読込のシーンを読む → 2 フレーム黒のまま待つ → タイトルロゴを非表示に保つ → `Enter()` → FadeIn → `OnStateFadeInComplete` を発火。

`ResolveBootstrapCandidate()` は、同じ GameObject 上の `GameStateBindingBase` のうち、他の兄弟から `NextState` の遷移先として指されていないものを入口にする。`Map` では `IntroState` が選ばれる。**同一 GameObject の兄弟だけが対象なので、`Intro` と `Racing` は同じ GameObject に置く。**

### 通常の遷移(`AdvanceToNext`)

遷移中は、新しい `AdvanceToNext` / `EmergencyReset` を無視する(ログも出ない)。

1. FadeOut(離脱元 State の秒数・色)
2. (Loading 画面あり)`Loading` を追加ロード → `Configure` → `PlayFadeIn`
3. 現 State の `Exit()`
4. シーンの差分ロード/アンロード
5. `TriggerOpeningTitleReveal()`(Splash → Opening のみ)
6. `SetActiveScene`、`m_currentId` 更新、`EnsureTitleLogoHiddenFor`
7. 遷移先 State の `Enter()`
8. (Loading 画面あり)2 フレーム待つ → `WaitForKeep` → `PlayFadeOut` → `Loading` をアンロード
9. FadeIn(遷移先 State の秒数)
10. `OnStateFadeInComplete` を発火

| 遷移 | Loading 画面 |
|---|---|
| Load → Splash | なし(`Load` は自前のカメラを持つ中継シーン) |
| Splash → Opening / Opening → Menu / Menu → Intro | あり |
| Intro → Racing | なし(シーン差分がない) |
| Racing → Result / Result → Load | あり |

- 手順 6 で遷移中のフラグが戻るので、手順 7 以降も新しい State の `Tick` は動く。画面が見えるまで入力を受けたくない State(`OpeningState`、`LoadState`、`TimeRemainingCountdown` など)は、`OnStateFadeInComplete` を購読して受付を始める。
- 未登録の State はフェードせず素通しする(FadeIn は即座に透明へ)。画面が黒のまま固定されるのを防ぐ。

### 緊急リセット(`EmergencyReset`)

Loading 画面を使わず、**現 State の `Exit()` も呼ばない。**

1. FadeOut → `m_states.Clear()` → `Manager` 以外の全シーンをアンロード
2. 指定 State のシーングループを読み直す(`Manager` を除く)
3. `SetActiveScene`、`m_currentId` 更新、`EnsureTitleLogoHiddenFor`
4. `Enter()`(シーンの `Awake` で再登録済み) → FadeIn → `OnStateFadeInComplete`

### タイトルロゴの安全網

`Opening` と `Menu` 以外の State に入る直前に `TitleLogoOverlay.ForceHide()` を呼ぶ。Loading 画面を通らない遷移でも、ロゴが残らないようにするため。

### 計測点(`FlowTiming`)

各ステップの終わりを `Begin` / `Step` / `End` で通知する。購読者(`PerfTransitionRecorder`)がいなければ、null 判定だけで何もしない。詳細は `Perf.md` の「transitions.csv」の節。

- 種類は `Boot`、`Transition`、`Reset`、`Refresh`(`SessionCacheRefresher`)の 4 つ。
- 各ステップは `Work`(ロード・Exit・Enter・描画の安定待ち)と `Wait`(フェード・保持秒数)に分ける。エンジンの比較は `Work` の合計で行う。
- `End` は `OnStateFadeInComplete` の発火より前に通知する。従来の `[LoadTiming]` の合計とは、数値が一致しない。
- 新しい処理を足すときは、`Begin` から `End` の間で `Step` を通知する。`Begin` なしの `Step` / `End` は無視される。

## 外部との接点

| 方向 | API | 相手 |
|---|---|---|
| 進める | `AdvanceToNext()` | `SplashState`、`OpeningState`、`MenuState`、`IntroState`、`RacingState`、`LoadState`、`AimRacingResultFlowController` |
| 進める | `EmergencyReset(Load)` | `DebugKey`(F1)、`OpeningState.ReturnToLoad`、`TrackDriveRuntime` |
| 進める | `EmergencyReset(Racing)` | `DebugKey`(F2) |
| 読む | `CurrentId` | `GameManager.CurrentGameState`、`TrackDriveSession`、`MenuInputValidation`(Editor) |
| 読む | `OnStateFadeInComplete` | `OpeningState`、`LoadState`、`TimeRemainingCountdown`、`AimRacingResultFlowController`、`TrackDriveSession`、`TrackDriveRuntime` |
| 呼ぶ | — | `TransitionFadeController`、`LoadingScreenController`、`TitleLogoOverlay`、`AppLog` |

## 新しい State を足す手順

1. `GameStateId` の**末尾**に値を足す。
2. `GameFlowGraph` の `SceneGroups`(主シーン + `Manager` + `MapEnvironment`)と `NextState` に足す。
3. `GameStateBindingBase` を継承し、`Id` を定義して `Enter` / `Tick` / `Exit` を `override` する。進むときは `AdvanceToNext()`。
4. そのシーンにコンポーネントを置き、Build Settings に登録する。
5. 離脱時に Loading 画面を挟むなら、`k_LoadingScreenLeavingStates` に足す。
6. 秒数を使うなら、`TransitionFadeController.m_stateFadeSettings` と `LoadingScreenController.m_fadeSettings` に Inspector で登録する(未登録だとフェードしない)。

## 守る前提

- `Intro` と `Racing` は同じ GameObject に置く。
- `GameStateId` は末尾にだけ足す。`Intro = 4` と、欠番 `3` は変えない。
- `Manager` がロード済みであること。`GameFlowRunner.Instance` が `null` で State が登録されるとエラーになる(通常は `GameStateBindingBase` が先に読む)。
- 遷移中の `AdvanceToNext` は無視される。`Tick` 内で毎フレーム呼ぶ実装(`SplashState` など)も、遷移開始で `Tick` が止まるので二重遷移にならない。

## 未対応・要確認

| 項目 | 内容 |
|---|---|
| `EmergencyReset` で `Exit()` が呼ばれない | `MenuState` は `Enter()` で `TcpServer.OnNameReceived`(static event)を購読し、`Exit()` で解除する。Menu で名前を待つ間にリセットされると、購読が残ったまま破棄される。次の名前受信で、破棄済みのハンドラが呼ばれて例外になり、新しい `MenuState` に届かない恐れ(未検証)。展示で F1 リセットする運用で起こりうる。対策案は、`OnDestroy` でも解除する、またはリセット前に `Exit()` を呼ぶ(`Exit()` の内容次第で副作用あり) |
| コメントと実装の食い違い | `GameFlowRunner.cs` の `EnsureTitleLogoHiddenFor` 内に「F1/F2 の対象は Splash/Racing のみ」とあるが、F1 の遷移先は `Load`(未修正)。`AimRacingResultFlowController.cs`(他者作成)に「Result → Splash」とあるが、`NextState` は Result → Load。`WaitForKeep` 付近のコメントは 10/19 に修正済み |

## PDCA

担当: 9 名のチーム制作のうち、画面遷移の仕組み。期間: 2026/09/01〜09/18。

| | 内容 |
|---|---|
| 目標 | 旧構成(Ingame / Movie / End)を作り直す。① State を足しても影響範囲が広がらない ② 長時間の展示でも安定して回る。達成の基準の数値は未記載 |
| 課題 | 遷移の定義が分散していた / ロードとフェードが重なるとフェードが効かない / 長時間運用で前のプレイの状態とガベージが積み重なる / 読込直後は描画が安定しない / 遷移のどこに時間がかかるか分からない |
| 検証・結果 | 7 つの State に置き換えた。遷移の定義は `GameFlowGraph` の 1 か所で、`AdvanceToNext()` だけで進める。Loading 画面を挟まない State なら `GameFlowRunner` を変えずに足せる(挟む場合は `k_LoadingScreenLeavingStates` に 1 行)。遷移時間は `transitions.csv` に記録できる(2026/10/19 実装。開発用 PC の実機で記録を確認。周回が進むと Menu → Intro が延びる: `PerfFindings.md` の C-1) |
| 未記録 | 各対策の前後比較(ほぼ実装のみの確認) / リフレッシュ後のメモリ、繰り返しプレイでの推移 / 展示(TGS2026)の結果 / エンジンの変更前後の遷移時間(エンジンを変更していないので未実施) |
| 次の候補 | `EmergencyReset` の件を実機で確認し、`OnDestroy` でも購読を解除する / コメントの食い違いを直す |
