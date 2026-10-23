# デバッグキー一覧

作成: 2026/10/15(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象: `Assets/#Scripts/Debug/DebugKey.cs` の `DebugKeyAction`(24 個)と、プロジェクト内でキーボードを直接読んでいるデバッグ用の処理

確認方法: コードと、シーン・`.inputactions` への配置を読んで確認(2026/10/15。F9 / Y / U と `KeysLocked` は 2026/10/19)。実機での動作確認は行っていない。

「Action」は `DebugKeyAction` の名前。`—` は `DebugKeyAction` を使わず、生のキー入力や Input Action で読むもの。「場面」の `常時` は `Manager` シーンに置かれ、どの State でも動く。

## キー別の一覧

| キー | 処理 | Action | 実装 | 場面 |
|---|---|---|---|---|
| Esc | アプリを終了する | `QuitApplication` | `DebugKey` | 常時 |
| F1 | BGM と G923 の FFB を止め、Load へ緊急リセット | `ReturnToTitle` | `DebugKey` | 常時 |
| F2 | BGM と G923 の FFB を止め、Racing へ緊急リセット | `StartInGame` | `DebugKey` | 常時 |
| F3 | 名前入力をスキップする(名前は `Staff`) | `SkipNameEntry` | `MenuState` | Menu |
| F3 | Finish ラインを通過したことにして、デバッグ完走 | — | `Line_StartFinish`(`m_debugKeyCode` 284) | Map |
| F4 | ポーズの切り替え(BGM のミュートと `timeScale` の 0 / 1) | `PauseGame` | `DebugKey` | 常時 |
| F5 | AT / MT の切り替え | `ChangeTransmission` | `RacingState` | Racing |
| F5 | G923 切断後の FFB の復帰(復帰待ちのときだけ) | — | `Aim2024Input` の `ResumeFromG923Disconnect` → `G923_FFBController` | Map / Menu / Opening / Splash |
| F6 | 椅子の倍率を「弱」の CSV 設定に読み込む | — | `ChairController` | Map |
| F6 | G923 の FFB モードを Child(弱)に | — | `G923_FFBController` | Manager / Map |
| F7 | G923 の FFB モードを Standard(中)に | — | `G923_FFBController` | Manager / Map |
| F8 | G923 の FFB モードを StrongShake(強)に | — | `G923_FFBController` | Manager / Map |
| F9 | BGM と FFB を止めて Load へ緊急リセットし、AT の自動走行を始める | `StartAutoDemo` | `DebugKey` → `AutoDemoDriver.Start()` → `TrackDriveSession.BeginRuntime(false)` | 常時 |
| F11 | 椅子を緊急停止(`CloseSIMVR`) | `ShutdownWizmo` | `WIZMODebug` | 常時 |
| F12 | 椅子を再接続(`OpenSIMVR`) | `ReconnectWizmo` | `WIZMODebug` | 常時 |
| F12 | 椅子を緊急停止 | — | `ChairSafetyController` | 常時 |
| Shift + F12 | 椅子の緊急停止を解除 | — | `ChairSafetyController` | 常時 |
| Insert | Editor の再生を一時停止 | `PauseEditor` | `DebugKey` | Editor のみ |
| Home | 次のカメラへ切り替え | `SwitchCamera` | `SwitchingCamera` | Map |
| End | 車をショートカット地点へ移動し、速度を 0 に | `MoveCarToShortcut` | `DebugSystem` | Map |
| Delete | 車を 3 m 持ち上げ、回転と速度を戻す | `ResetCar` | `DebugSystem` | Map |
| PageUp | WIZMO の状態ログの表示を切り替え | `ToggleWizmoState` | `DebugKey` → `GameManager` | 常時 |
| PageDown | Intro の映像を終端の 0.1 秒手前へ飛ばす | `SkipMovie` | `IntroState` | Intro |
| PageDown | Splash のロゴ演出を早送り | `SkipMovie` | `SplashFade` | Splash |
| 0 | クラクション(`YankieGodHorn`)。4.08 秒のクールダウン | `PlayHorn` | `DebugKey` | Menu / Racing |
| Backspace | Start ラインを通過したことにして、計測を始める | — | `Line_StartFinish`(`m_debugKeyCode` 8) | Map |
| K / L / J | デバッグ BGM の再生 / 次へ / 前へ | — | `SoundManager` | `m_bDebug` が有効のときだけ(現在は無効) |
| Y | F9 と同じ処理で、MT の自動走行を始める | `StartAutoDemoManual` | `DebugKey` → `AutoDemoDriver.Start(true)` → `TrackDriveSession.BeginRuntime(true)` | Splash / Opening / Racing のみ(`IsManualDemoKeyEnabled`。名前入力とメニュー選択を妨げないため) |
| U | MT の自動走行を **10 周**自動で繰り返す。各周の Result は 3 秒後に Enter を自動で押して抜け、Splash に戻った所でリセットなしに次の周を始める。繰り返し中にもう一度押すと中止(詳細は `Perf.md` の「周回」) | `StartAutoDemoLoop` | `DebugKey` → `AutoDemoDriver.StartLoop(true)`(中止は `CancelLoop`) | 開始は Y と同じ。中止はどの場面でも受け付ける |
| テンキー `.` | デバイス情報の画面表示を切り替え | `ToggleDeviceInfo` | `DebugKey` → `DebugDevice` | 常時 |
| テンキー `+` | ランキングのネットワークを再び有効にし、保留分を再送 | `EnableRankingNetwork` | `AimRacingRankingBridge` | ネットワークが無効のとき |
| テンキー `-` | ランキングのネットワークを無効にし、オフラインで続ける | `DisableRankingNetwork` | `AimRacingRankingBridge` | LAN 送信の失敗後、手動切り替え待ちのとき |
| テンキー `*` | デバッグ完走(`timeScale` が 0 なら 1 に戻す) | `CompleteRace` | `RacingState` | Racing |

**自動周回の計測中は、Esc 以外の `DebugKeyAction` を受け付けない。** 起動引数 `-perfautolap`(`Perf.md` の「周回」)で起動したときだけ `DebugKey.KeysLocked` が立つ。計測の途中でキーが押されて周回が乱れるのを防ぐため。止まるのは `DebugKey` 経由のキーだけで、上の表の `—`(生のキー入力や Input Action)は止まらない。通常の起動では何も変わらない。

## 処理がない Action

| Action | キー |
|---|---|
| `ToggleScreenLog` | F8 |
| `ToggleInputDebug` | 0 |

## `DebugKeyAction` の割り当て(`GetKeyCode`)

| Action | キー | Action | キー |
|---|---|---|---|
| `QuitApplication` | Esc | `ToggleInputDebug` | 0 |
| `ReturnToTitle` | F1 | `ResetCar` | Delete |
| `StartInGame` | F2 | `MoveCarToShortcut` | End |
| `SkipNameEntry` | F3 | `SkipMovie` | PageDown |
| `PauseGame` | F4 | `CompleteRace` | テンキー `*` |
| `ChangeTransmission` | F5 | `PlayHorn` | 0 |
| `ToggleScreenLog` | F8 | `EnableRankingNetwork` | テンキー `+` |
| `ShutdownWizmo` | F11 | `DisableRankingNetwork` | テンキー `-` |
| `ReconnectWizmo` | F12 | `ToggleDeviceInfo` | テンキー `.` |
| `PauseEditor` | Insert | `ToggleWizmoState` | PageUp |
| `SwitchCamera` | Home | `StartAutoDemo` | F9 |
| `StartAutoDemoManual` | Y | `StartAutoDemoLoop` | U |
