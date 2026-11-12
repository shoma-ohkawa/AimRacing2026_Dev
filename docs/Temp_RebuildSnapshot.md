# 一時メモ: 控え(全面刷新の前の、現行の値・配線・呼び出し側)

作成: 2026/11/12 / 作成者: 24CU0110 大川翔生
対象: 自作スクリプト 36 本を置いているシーン・Prefab、それを呼ぶ他のファイル、カメラと Volume の置き方

一時的なメモ。再構築・再作成が終わったら、`Temp_RebuildTargets.md`・`Temp_RebuildProgress.md` とあわせて削除する。`CLAUDE.md` の docs の表には載せていない。

- 取得: 2026/11/12。`main` の `bb7f39f` の作業フォルダ。シーン・Prefab・`.asset` の YAML(`Force Text`)を読んで、スクリプトの GUID で自作のコンポーネントを探した(`コード`)。取得に使ったスクリプトは、コミットしていない。
- 全面刷新では、`.meta` の GUID が変わるので、ここに書いた配線と値は失われる。**取り直すときの正本**にする。
- 参照の `{fileID: N}` の後ろの `/* ... */` は、読みやすくするための注釈(同じファイル内の対象の名前、または別のファイルのパス)。
- 走査: 追跡している `.unity`・`.prefab`・`.asset` 313 本(合計 371 MB)。40 MB 超で読み飛ばしたもの: `Assets/Images/Font/sf-pro-rounded-regular SDF.asset`(134 MB)、`Assets/TextMesh Pro/Fonts/NotoSansJP-Regular SDF.asset`(137 MB)。

## 1. 自作スクリプトを置いている場所

| ファイル | GameObject | 型 | 有効 |
|---|---|---|---|
| `Assets/!Scenes/Load.unity` | `LoadState` | `LoadState` | ○ |
| `Assets/!Scenes/Load.unity` | `Main Camera` | `MultiScreenSourceCamera` | ○ |
| `Assets/!Scenes/Loading.unity` | `LoadingScreen` | `LoadingScreenController` | ○ |
| `Assets/!Scenes/Manager.unity` | `GameFlowRunner` | `GameFlowRunner` | ○ |
| `Assets/!Scenes/Manager.unity` | `MultiDisplay` | `MultiScreenOutput` | ○ |
| `Assets/!Scenes/Manager.unity` | `TitleLogoOverlay` | `TitleLogoOverlay` | ○ |
| `Assets/!Scenes/Manager.unity` | `TransitionFadeController` | `TransitionFadeController` | ○ |
| `Assets/!Scenes/Map.unity` | `'###Vehicle###'/MainCamera` | `MultiScreenSourceCamera` | ○ |
| `Assets/!Scenes/Map.unity` | `--Managers------------------------/FlowBinding` | `IntroState` | ○ |
| `Assets/!Scenes/Map.unity` | `--Managers------------------------/FlowBinding` | `RacingState` | ○ |
| `Assets/!Scenes/Menu.unity` | `Main Camera` | `MultiScreenSourceCamera` | ○ |
| `Assets/!Scenes/Menu.unity` | `Manager/FlowBinding` | `MenuState` | ○ |
| `Assets/!Scenes/Menu.unity` | `Time Remaining` | `TimeRemainingCountdown` | ○ |
| `Assets/!Scenes/Opening.unity` | `Managers/FlowBinding` | `OpeningState` | ○ |
| `Assets/!Scenes/Opening.unity` | `UICanvas/MainCamera` | `MultiScreenSourceCamera` | ○ |
| `Assets/!Scenes/Result.unity` | `Main Camera` | `MultiScreenSourceCamera` | ○ |
| `Assets/!Scenes/Result.unity` | `ResultSystem` | `ResultState` | ○ |
| `Assets/!Scenes/Splash.unity` | `FlowBinding` | `SplashState` | ○ |
| `Assets/!Scenes/Splash.unity` | `Main Camera` | `MultiScreenSourceCamera` | ○ |

シーン・Prefab・`.asset` に置かれている自作の型: 14 種類。置かれていない(コードからだけ使う、`static`、`Editor`、`enum` など): `AppLog`、`AutoDemoDriver`、`CurveSignAdjustWindow`、`FlowTiming`、`GameFlowGraph`、`GameSceneGroup`、`GameStateBindingBase`、`GameStateId`、`IGameState`、`MultiScreenTargetSelector`、`PerfAutoLapRunner`、`PerfCsvFormat`、`PerfCsvRecorder`、`PerfNativeMetrics`、`PerfTransitionRecorder`、`ProjectZipWhitelist`、`RacingEnterBlurPass`、`RacingEnterEffect`、`RacingEnterEffectPattern`、`SessionCacheRefresher`、`SingletonMonoBehaviour`、`WindowsMonitorInfo`。

## 2. Inspector の値(シーン・Prefab ごと)

値の読み取り(`コード`。enum の定義と照らした):

- `m_state`・`m_leavingState`: 0 = Splash、1 = Opening、2 = Menu、4 = Intro、5 = Racing、6 = Result、7 = Load(`GameStateId`)。
- `RacingState.m_enterEffectPattern: 4` は `BlurFocus`(`RacingEnterEffectPattern`: 0 None、1 VignetteOpen、2 ChromaticFlash、3 LaunchCombo、4 BlurFocus)。コードの既定は `LaunchCombo` で、`GameStates.md` も既定を `LaunchCombo` と書いているが、**`Map.unity` の値は `BlurFocus`**。`BlurFocus` は、3 画面での動作と負荷が未確認(`GameStates.md`)。
- `MultiScreenSourceCamera` と `LoadState` などの `m_Enabled: 1` だけのものは、Inspector で設定する値を持たない。

### `Assets/!Scenes/Load.unity`

#### `LoadState` の `LoadState`

```yaml
  m_Enabled: 1
```

#### `Main Camera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

### `Assets/!Scenes/Loading.unity`

#### `LoadingScreen` の `LoadingScreenController`

```yaml
  m_Enabled: 1
  m_fadeSettings:
  - m_leavingState: 0
    m_fadeInDuration: 0.3
    m_keepDuration: 1.5
    m_fadeOutDuration: 1
  - m_leavingState: 1
    m_fadeInDuration: 0.3
    m_keepDuration: 2.312
    m_fadeOutDuration: 1
  - m_leavingState: 2
    m_fadeInDuration: 0.3
    m_keepDuration: 0
    m_fadeOutDuration: 1
  - m_leavingState: 5
    m_fadeInDuration: 0.3
    m_keepDuration: 0
    m_fadeOutDuration: 1
  - m_leavingState: 6
    m_fadeInDuration: 0.3
    m_keepDuration: 0
    m_fadeOutDuration: 1
  m_titleLogoFadeInDelay: 0.3
  m_splashToOpeningBgmKeepOverrides:
  - m_bgmClipName: OP1
    m_keepDuration: 4.55
  - m_bgmClipName: OP2 0913 AM04_44修正済み差し替え希望
    m_keepDuration: 1.8
  - m_bgmClipName: OP3
    m_keepDuration: 3
```

### `Assets/!Scenes/Manager.unity`

#### `GameFlowRunner` の `GameFlowRunner`

```yaml
  m_Enabled: 1
  m_useLoadingScreen: 1
```

#### `MultiDisplay` の `MultiScreenOutput`

```yaml
  m_Enabled: 1
```

#### `TitleLogoOverlay` の `TitleLogoOverlay`

```yaml
  m_Enabled: 1
  m_logoImage: {fileID: 520000008} /* TitleLogoOverlay/TitleLogoCanvas/TitleLogo.Image */
  m_fadeInDuration: 0.5
  m_fadeOutDuration: 1
```

#### `TransitionFadeController` の `TransitionFadeController`

```yaml
  m_Enabled: 1
  m_stateFadeSettings:
  - m_state: 0
    m_fadeOutDuration: 1
    m_fadeInDuration: 0.2
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
  - m_state: 1
    m_fadeOutDuration: 2.5
    m_fadeInDuration: 0.2
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
  - m_state: 2
    m_fadeOutDuration: 3
    m_fadeInDuration: 0.2
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
  - m_state: 4
    m_fadeOutDuration: 0
    m_fadeInDuration: 0.2
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
  - m_state: 5
    m_fadeOutDuration: 1
    m_fadeInDuration: 0
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
  - m_state: 6
    m_fadeOutDuration: 1
    m_fadeInDuration: 0.2
    m_fadeOutColor: {r: 0, g: 0, b: 0, a: 1}
```

### `Assets/!Scenes/Map.unity`

#### `'###Vehicle###'/MainCamera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

#### `--Managers------------------------/FlowBinding` の `IntroState`

```yaml
  m_Enabled: 1
  m_moviePlay: {fileID: 1816762696} /* --Movie---------------------------/MovieTimeline.PlayableDirector */
  m_vehicle: {fileID: 6443976289017239432} /* '###Vehicle###'.VehicleController */
  m_startAction: {fileID: 1744739675} /* --InGame_UI-----------------------/UI_Canvas/UI_area/StartCountDown.UI_StarCountDown */
  m_driveSound: {fileID: 4955980257380195218} /* '###Vehicle###'/Sound.DriveSound */
  m_meterUIManager: {fileID: 1577089520274216529} /* MeterUIManager.MeterUIManager */
  m_okutamaNameDisplay: {fileID: 0}
```

#### `--Managers------------------------/FlowBinding` の `RacingState`

```yaml
  m_Enabled: 1
  m_vehicle: {fileID: 6443976289017239432} /* '###Vehicle###'.VehicleController */
  m_finishLine: {fileID: 683698340} /* .Line_StartFinish */
  m_chairController: {fileID: 5005934364370456132} /* '###Vehicle###'/Chair.ChairController */
  m_meterUIManager: {fileID: 1577089520274216529} /* MeterUIManager.MeterUIManager */
  m_okutamaNameDisplay: {fileID: 0}
  m_enterEffectPattern: 4
```

### `Assets/!Scenes/Menu.unity`

#### `Main Camera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

#### `Manager/FlowBinding` の `MenuState`

```yaml
  m_Enabled: 1
  m_onSteerRightEvent:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {fileID: 2090674161} /* TwoChoice.TwoChoice */
        m_TargetAssemblyTypeName: TwoChoice, Assembly-CSharp
        m_MethodName: SetRight
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {fileID: 0}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
  m_onSteerLeftEvent:
    m_PersistentCalls:
      m_Calls:
      - m_Target: {fileID: 2090674161} /* TwoChoice.TwoChoice */
        m_TargetAssemblyTypeName: TwoChoice, Assembly-CSharp
        m_MethodName: SetLeft
        m_Mode: 1
        m_Arguments:
          m_ObjectArgument: {fileID: 0}
          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine
          m_IntArgument: 0
          m_FloatArgument: 0
          m_StringArgument:
          m_BoolArgument: 0
        m_CallState: 2
  m_steerRange: 0.01
  m_twoChoice: {fileID: 2090674161} /* TwoChoice.TwoChoice */
  m_confirmMinDelay: 0.3
  m_bgmFadeOutDuration: 0.1
  m_entryTransmission: {fileID: 657252012} /* EntryTransmission */
  m_nameEntryUI: {fileID: 1956134398} /* EntryName */
  m_nameEntryContent: {fileID: 780855167} /* EntryName/Image.RectTransform */
  m_nameEntryScaleDuration: 0.2
  m_nameEntryGuideImage: {fileID: 780855168} /* EntryName/Image.Image */
  m_entryUI: {fileID: 1623425548} /* Thank you Enter Name */
  m_entryContent: {fileID: 1641012063} /* Thank you Enter Name/Image.RectTransform */
  m_entryScaleDuration: 0.3
  m_entryDisplayDuration: 3
  m_entryPlayerNameText: {fileID: 816211317} /* Thank you Enter Name/Text.TextMeshProUGUI */
  m_gasPedalGuideImage: {fileID: 150384248} /* EntryTransmission/ATMT/Determined by gas pedal_TEXT.Image */
  m_timeRemaining: {fileID: 1382933095} /* Time Remaining.TimeRemainingCountdown */
```

#### `Time Remaining` の `TimeRemainingCountdown`

```yaml
  m_Enabled: 1
  m_timeText: {fileID: 2068626404} /* Time Remaining/Text.TextMeshProUGUI */
```

#### `Manager/FlowBinding` の `PlayerInput`(イベントの登録は、下の「3」)

```yaml
  m_Enabled: 1
  m_Actions: {fileID: -944628639613478452, guid: c7e32a6328ba8304d91959a55853da46, type: 3} /* Assets/InputSystems/Aim2024Input.inputactions */
  m_NotificationBehavior: 2
  m_UIInputModule: {fileID: 0}
  m_DeviceLostEvent:
    m_PersistentCalls:
      m_Calls: []
  m_DeviceRegainedEvent:
    m_PersistentCalls:
      m_Calls: []
  m_ControlsChangedEvent:
    m_PersistentCalls:
      m_Calls: []
  m_NeverAutoSwitchControlSchemes: 0
  m_DefaultControlScheme:
  m_DefaultActionMap: MainSystem
  m_SplitScreenIndex: -1
  m_Camera: {fileID: 0}
```

### `Assets/!Scenes/Opening.unity`

#### `Managers/FlowBinding` の `OpeningState`

```yaml
  m_Enabled: 1
  m_BGMFadeTime: 0.1
  m_loopInterval: 1
  m_director: {fileID: 7539231741098441055} /* --------------------OpeningTimeline.PlayableDirector */
  m_opAudio: {fileID: 405666514} /* OP_Audio.AudioSource */
  m_fadeOutDuration: 1
```

#### `UICanvas/MainCamera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

#### `Managers/FlowBinding` の `PlayerInput`(イベントの登録は、下の「3」)

```yaml
  m_Enabled: 1
  m_Actions: {fileID: -944628639613478452, guid: c7e32a6328ba8304d91959a55853da46, type: 3} /* Assets/InputSystems/Aim2024Input.inputactions */
  m_NotificationBehavior: 2
  m_UIInputModule: {fileID: 0}
  m_DeviceLostEvent:
    m_PersistentCalls:
      m_Calls: []
  m_DeviceRegainedEvent:
    m_PersistentCalls:
      m_Calls: []
  m_ControlsChangedEvent:
    m_PersistentCalls:
      m_Calls: []
  m_NeverAutoSwitchControlSchemes: 0
  m_DefaultControlScheme:
  m_DefaultActionMap: MainSystem
  m_SplitScreenIndex: -1
  m_Camera: {fileID: 0}
```

### `Assets/!Scenes/Result.unity`

#### `Main Camera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

#### `ResultSystem` の `ResultState`

```yaml
  m_Enabled: 1
  m_resultUI: {fileID: 803870102} /* ResultCanvas/ResultRoot.CanvasGroup */
  m_thankYouCanvasGroup: {fileID: 812582672} /* Thank you.CanvasGroup */
  m_resultDisplayDuration: 30
  m_fadeDuration: 1
```

### `Assets/!Scenes/Splash.unity`

#### `FlowBinding` の `SplashState`

```yaml
  m_Enabled: 1
  m_splashFade: {fileID: 1988987373} /* Canvas.SplashFade */
```

#### `Main Camera` の `MultiScreenSourceCamera`

```yaml
  m_Enabled: 1
```

## 3. シーンの設定から、自作のメソッドを呼んでいる配線(UnityEvent・PlayerInput)

メソッド名は文字列で保存されている。メソッドの名前・型を変えると、実行時に呼ばれなくなる(エラーも出ない)。

| ファイル | 呼ぶ側(GameObject / コンポーネント) | 呼ばれる型 | メソッド | PlayerInput のアクション |
|---|---|---|---|---|
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnBrakePedal` | `'MainSystem/BrakePedal[/Logitech G923 Racing Wheel for PlayStation and PC/rz,/Logitech G29 Driving Force Racing Wheel/rz]'` |
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnBrakeTriger` | `'MainSystem/BrakeTriger[/Keyboard/downArrow,/Gamepad/leftTrigger]'` |
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnPedal` | `'MainSystem/AccelPedal[/Logitech G29 Driving Force Racing Wheel/z]'` |
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnSelect` | `'MainSystem/SteeringKeyboard[/Gamepad/leftStick/x,/Keyboard/leftArrow,/Keyboard/rightArrow]'` |
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnSelect` | `'MainSystem/Steering[/Logitech G29 Driving Force Racing Wheel/stick/x,/Keyboard/leftArrow,/Keyboard/rightArrow]'` |
| `Menu.unity` | `Manager/FlowBinding` / `PlayerInput` | `MenuState` | `OnTriger` | `'MainSystem/AccelTriger[/Keyboard/upArrow]'` |
| `Opening.unity` | `Managers/FlowBinding` / `PlayerInput` | `OpeningState` | `OnPedal` | `'MainSystem/AccelPedal[/Logitech G29 Driving Force Racing Wheel/z]'` |
| `Opening.unity` | `Managers/FlowBinding` / `PlayerInput` | `OpeningState` | `OnTriger` | `'MainSystem/AccelTriger[/Keyboard/upArrow]'` |

`PlayerInput` のアクション名は、`GameStates.md` の「入力の配線」と対応する。`AnimationEvent`・Timeline のシグナル(メソッド名の文字列)は読んでいない(`未確認`)。

## 4. 自作の型を使う、他のファイル(呼び出し側)

自作 36 本の外にある `.cs`(`Packages/` を除く)で、自作の型名を使っているもの。コメントだけの行は除く。API を変えたら、ここを直す。


### `AutoDemoDriver`(自作: `Utility/AutoDemoDriver.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Debug/DebugKey.cs` | `CancelLoop`×1、`IsLooping`×1、`Start`×2、`StartLoop`×1、`Tick`×2 |

### `GameFlowRunner`(自作: `GameManager/Flow/GameFlowRunner.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Debug/DebugKey.cs` | `CurrentId`×1、`EmergencyReset`×2、`Instance`×1 |
| `#Scripts/GameManager/GameManager.cs` | `CurrentId`×1 |
| `AimRacingRanking/Runtime/AimRacingResultFlowController.cs` | `AdvanceToNext`×1、`Instance`×1、`OnStateFadeInComplete`×2 |
| `AimRacingRanking/Runtime/NameEntry/AimRacingNameEntryController.cs` | `AdvanceToNext`×1、`Instance`×1 |
| `DrivingTools/AutoDriving/Runtime/TrackDriveRuntime.cs` | `EmergencyReset`×1、`Instance`×2、`OnStateFadeInComplete`×1 |
| `DrivingTools/AutoDriving/Runtime/TrackDriveSession.cs` | `OnStateFadeInComplete`×1 |
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | 型として参照(継承・宣言・引数など) |

### `GameStateBindingBase`(自作: `GameManager/Flow/GameStateBindingBase.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | 型として参照(継承・宣言・引数など) |

### `GameStateId`(自作: `GameManager/Flow/GameStateId.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Debug/DebugKey.cs` | `Load`×1、`Menu`×1、`Opening`×1、`Racing`×3、`Splash`×1 |
| `#Scripts/GameManager/GameManager.cs` | 型として参照(継承・宣言・引数など) |
| `#Scripts/Sound/AmbientSound.cs` | `Racing`×1 |
| `AimRacingRanking/Runtime/AimRacingRankingBridge.cs` | `Racing`×1、`Result`×1 |
| `AimRacingRanking/Runtime/AimRacingResultFlowController.cs` | `Result`×2 |
| `DrivingTools/AutoDriving/Runtime/TrackDriveRuntime.cs` | `Load`×1 |
| `DrivingTools/AutoDriving/Runtime/TrackDriveSession.cs` | `Menu`×1、`Opening`×1、`Racing`×1、`Result`×1 |
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | `Menu`×1 |

### `IntroState`(自作: `GameManager/GameStates/IntroState.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Editor/AimRacingPlayModeGuard.cs` | 型として参照(継承・宣言・引数など) |

### `MenuState`(自作: `GameManager/GameStates/MenuState.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `DrivingTools/AutoDriving/Runtime/TrackDriveSession.cs` | 型として参照(継承・宣言・引数など) |
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | 型として参照(継承・宣言・引数など) |

### `OpeningState`(自作: `GameManager/GameStates/OpeningState.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `DrivingTools/Validation/Editor/MenuInputValidation.cs` | 型として参照(継承・宣言・引数など) |

### `RacingState`(自作: `GameManager/GameStates/RacingState.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Editor/AimRacingPlayModeGuard.cs` | 型として参照(継承・宣言・引数など) |

### `SingletonMonoBehaviour`(自作: `Utility/SingletonMonoBehaviour.cs`)

| 参照しているファイル | 使っているメンバー |
|---|---|
| `#Scripts/Debug/DebugDevie.cs` | 型として参照(継承・宣言・引数など) |
| `#Scripts/GameManager/GameManager.cs` | 型として参照(継承・宣言・引数など) |
| `#Scripts/Input/HandleManager.cs` | 型として参照(継承・宣言・引数など) |
| `#Scripts/Sound/SoundManager.cs` | 型として参照(継承・宣言・引数など) |

### `AppLog`(自作: `Utility/AppLog.cs`)

自作 36 本の外で 35 ファイルが呼んでいる。メソッド別の回数: `Log`×66、`LogError`×50、`LogWarning`×22。

| ファイル | 回数 |
|---|---|
| `#Scripts/Camera/SwitchingCamera.cs` | `Log`×1、`LogError`×1 |
| `#Scripts/Debug/DebugInputVehicles.cs` | `LogError`×1 |
| `#Scripts/Device/DirectInputFFBBackend.cs` | `LogWarning`×2 |
| `#Scripts/Device/G923_FFBController.cs` | `Log`×5、`LogError`×1、`LogWarning`×4 |
| `#Scripts/Device/LogitechFFBBackend.cs` | `LogWarning`×1 |
| `#Scripts/Device/XInputFFBBackend.cs` | `LogWarning`×1 |
| `#Scripts/Effect/EdgeSpectrumBars.cs` | `Log`×1 |
| `#Scripts/GameManager/GameManager.cs` | `Log`×13、`LogError`×3、`LogWarning`×4 |
| `#Scripts/Input/AxisPressdOnce.cs` | `Log`×1 |
| `#Scripts/Input/VehicleUnityEvent.cs` | `Log`×1 |
| `#Scripts/Meter/MeterUIManager.cs` | `LogError`×1 |
| `#Scripts/Race/Line_StartFinish.cs` | `Log`×1 |
| `#Scripts/Sound/SoundManager.cs` | `Log`×3 |
| `#Scripts/Sound/Vehicle/DriveSound.cs` | `LogError`×1 |
| `#Scripts/Sound/Vehicle/TireSound.cs` | `LogError`×3 |
| `#Scripts/UI/BlinkingUI.cs` | `Log`×1 |
| `#Scripts/Vehicle/Controller/RaceStart.cs` | `LogError`×1 |
| `#Scripts/Vehicle/Legacy/Load.cs` | `LogError`×4 |
| `#Scripts/Vehicle/Legacy/Load2024.cs` | `LogError`×4 |
| `#Scripts/Vehicle/Physics/AeroDynamics.cs` | `LogError`×1 |
| `#Scripts/Vehicle/Physics/CenterOfMass.cs` | `LogError`×1 |
| `#Scripts/Vehicle/Powertrain/DriveModes.cs` | `Log`×1 |
| `#Scripts/Vehicle/Wheels/WheelController2026.cs` | `LogError`×1 |
| `#Scripts/WIZMO/ChairSafetyController.cs` | `Log`×2 |
| `#Scripts/WIZMO/LoadTransfer.cs` | `LogError`×1 |
| `#Scripts/WIZMO/WIZMODebug.cs` | `LogWarning`×2 |
| `AimRacingRanking/Core/Runtime/RankingCertificatePrinter.cs` | `Log`×2、`LogError`×1 |
| `AimRacingRanking/Core/Runtime/RankingStorage.cs` | `LogError`×2、`LogWarning`×1 |
| `AimRacingRanking/Core/Runtime/RankingUdpReceiver.cs` | `Log`×2、`LogWarning`×1 |
| `AimRacingRanking/Core/Runtime/RankingUdpSender.cs` | `Log`×3、`LogError`×2、`LogWarning`×5 |
| `AimRacingRanking/Runtime/AimRacingRankingBridge.cs` | `Log`×1、`LogError`×1 |
| `AimRacingRanking/Runtime/AimRacingRankingConfig.cs` | `Log`×1、`LogWarning`×1 |
| `AimRacingRanking/Runtime/AimRacingRankingOutbox.cs` | `LogError`×1 |
| `AimRacingRanking/Runtime/NameEntry/AimRacingNameEntryController.cs` | `Log`×3、`LogError`×1 |
| `AimRacingRanking/Runtime/NameEntry/TCPserver.cs` | `Log`×24、`LogError`×18 |

## 5. スクリプトの設定(実行順など)

- `.meta` で実行順(`executionOrder`)が 0 以外、または既定の参照があるもの: なし
- 属性(実行順・必須コンポーネント・Editor での実行・自動初期化など):

| ファイル | 属性 |
|---|---|
| `Camera/MultiScreenOutput.cs` | `[DisallowMultipleComponent]` |
| `Camera/MultiScreenSourceCamera.cs` | `[RequireComponent(typeof(Camera))]` |
| `Perf/PerfAutoLapRunner.cs` | `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` |
| `Perf/PerfCsvRecorder.cs` | `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` |

`ProjectSettings/ProjectSettings.asset` の関連する値:

```
scriptingDefineSymbols: {}
scriptingBackend:  Android: 1
apiCompatibilityLevel: 6
enableFrameTimingStats: 1
managedStrippingLevel:  Android: 1 | EmbeddedLinux: 1 | GameCoreScarlett: 1 | GameCoreXboxOne: 1 | Nintendo Switch: 1 | PS4: 1 | PS5: 1 | QNX: 1 | VisionOS: 1 | WebGL: 1 | Windows Store Apps: 1 | XboxOne: 1 | iPhone: 1 | tvOS: 1
defaultScreenWidth: 2560
defaultScreenHeight: 1440
fullscreenMode: 1
runInBackground: 1
productName: AimRacing2026_Unity6.3
bundleVersion: 0.24.0
```

ビルドに入るシーンの順(`ProjectSettings/EditorBuildSettings.asset`。`Build Settings` の並び):

| 順 | シーン | 有効 |
|---|---|---|
| 0 | `Assets/!Scenes/Load.unity` | ○ |
| 1 | `Assets/!Scenes/Splash.unity` | ○ |
| 2 | `Assets/!Scenes/Opening.unity` | ○ |
| 3 | `Assets/!Scenes/Menu.unity` | ○ |
| 4 | `Assets/!Scenes/Manager.unity` | ○ |
| 5 | `Assets/!Scenes/Map.unity` | ○ |
| 6 | `Assets/!Scenes/MapEnvironment.unity` | ○ |
| 7 | `Assets/!Scenes/Loading.unity` | ○ |
| 8 | `Assets/!Scenes/Result.unity` | ○ |

## 6. カメラ・Volume・Timeline の置き方(設計の材料)

カメラ構成(常駐の 1 台にするか)を決めるための、現行の置き方。シーンごとに、カメラ、`HDAdditionalCameraData` の DLSS の設定、Cinemachine、Volume、`PlayableDirector` を読んだ。Prefab の中にあるものは、下の表の「Prefab」を見る。

### `Assets/!Scenes/Load.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `Main Camera` | 1 | 0 | なし | -1 | 1 | 0 | `0,0,1,1` |

### `Assets/!Scenes/Loading.unity`

カメラなし。

### `Assets/!Scenes/Manager.unity`

カメラなし。

### `Assets/!Scenes/Map.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `'###Vehicle###'/MainCamera` | 1 | 0 | なし | 0 | 1 | 1 | `0,0,1,1` |

| 種類 | GameObject | 有効 | 内容 |
|---|---|---|---|
| `CinemachineBrain` | `'###Vehicle###'/MainCamera` | 1 |  |
| `CinemachineCamera` | `--Movie---------------------------/MovieCamera/Camera` | 1 |  |
| `CinemachineCamera` | `--Movie---------------------------/MovieCamera/Camera (1)` | 1 |  |
| `CinemachineCamera` | `--Movie---------------------------/MovieCamera/Camera (2)` | 1 |  |
| `CinemachineCamera` | `--Movie---------------------------/MovieCamera/Camera (3)` | 1 |  |
| `CinemachineHardLockToTarget` | `'###Vehicle###'/Bumper/cm` | 1 |  |
| `CinemachineSplineDolly` | `--Movie---------------------------/MovieCamera/Camera` | 1 |  |
| `CinemachineSplineDolly` | `--Movie---------------------------/MovieCamera/Camera (1)` | 1 |  |
| `CinemachineSplineDolly` | `--Movie---------------------------/MovieCamera/Camera (2)` | 1 |  |
| `CinemachineSplineDolly` | `--Movie---------------------------/MovieCamera/Camera (3)` | 1 |  |
| `CinemachineVirtualCamera` | `'###Vehicle###'/Bonnet` | 1 |  |
| `CinemachineVirtualCamera` | `'###Vehicle###'/Bumper` | 1 |  |
| `CinemachineVirtualCamera` | `'###Vehicle###'/POV` | 1 |  |
| `PlayableDirector` | `--Movie---------------------------/MovieTimeline` | 1 | asset={fileID: 11400000, guid: 2e7da4999a0662c4e92493c51f282603, type: 2} /* Assets/Timeline/Movie.playable */ |
| `Volume` | `--Rendering------------------------/Glitch` | 1 | m_IsGlobal=1 priority=0 weight=1 blendDistance=0 sharedProfile={fileID: 11400000, guid: a2c70679acaac0a4e87a3b922e89a21b, type: 2} /* Assets/##Rendering/garage!move.asset */ |
| `Volume` | `--Rendering------------------------/Global Volume` | 1 | m_IsGlobal=1 priority=0 weight=1 blendDistance=0 sharedProfile={fileID: 11400000, guid: b1097708d98c9fc47a2a776dc510f657, type: 2} /* Assets/###Map2026/###Map2025/Map Volume Setting/Map2025_tmp terae.asset */ |

### `Assets/!Scenes/MapEnvironment.unity`

カメラなし。

### `Assets/!Scenes/Menu.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `Main Camera` | 1 | 0 | なし | -1 | 1 | 1 | `0,0,1,1` |

| 種類 | GameObject | 有効 | 内容 |
|---|---|---|---|
| `CinemachineBrain` | `Main Camera` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/Bonnet` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/Bumper` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/POV` | 1 |  |
| `CinemachineVolumeSettings` | `` | 1 | Profile={fileID: 11400000, guid: 3734db448fa656843a999a72414f3269, type: 2} /* Assets/###Map2026/VolumeSetting/MenuScene.asset */ Weight=1 FocusTracking=3 FocusOffset=0 |
| `Volume` | `Local Volume` | 1 | m_IsGlobal=1 priority=100 weight=1 blendDistance=0 sharedProfile={fileID: 11400000, guid: f09d399f38e9b79498f21a35c4e5a8be, type: 2} /* Assets/##Rendering/Opening2024 Volume Profile.asset */ |

### `Assets/!Scenes/Opening.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `UICanvas/MainCamera` | 1 | 0 | なし | 0 | 1 | 1 | `0,0,1,1` |

| 種類 | GameObject | 有効 | 内容 |
|---|---|---|---|
| `CinemachineBrain` | `UICanvas/MainCamera` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC1.Front_Left` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC10.5` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC10.Curve 180` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC11.Low Const Camera` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC13.Car Appears Left` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC15.` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC16` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC17` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC18` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC19` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC20` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC8.Fast Low Road` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC9.Car Front Tracking` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC_N1` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC_N2` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC_N3` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC_N4` | 1 |  |
| `CinemachineCamera` | `OP_Dolly/OP_DC/DC_N5` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC1.Front_Left` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC10.5` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC10.Curve 180` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC11.Low Const Camera` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC13.Car Appears Left` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC15.` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC16` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC17` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC18` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC19` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC20` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC8.Fast Low Road` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC9.Car Front Tracking` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC_N1` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC_N2` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC_N3` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC_N4` | 1 |  |
| `CinemachineSplineDolly` | `OP_Dolly/OP_DC/DC_N5` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/Bonnet` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/Bumper` | 1 |  |
| `CinemachineVirtualCamera` | `Car2026/yaris/POV` | 1 |  |
| `PlayableDirector` | `--------------------OpeningTimeline` | 1 | asset={fileID: 11400000, guid: af9944d237f379147935e8b8954face5, type: 2} /* Assets/Timeline/OpeningTimeline.playable */ |
| `Volume` | `--Rendering------------------------/Local Volume` | 1 | m_IsGlobal=1 priority=100 weight=1 blendDistance=0 sharedProfile={fileID: 11400000, guid: 4c26b680a30935048904774683d4d4ab, type: 2} /* Assets/###Map2026/###Map2025/op/OP Volume Profile/Opening 2025 Volume Profile.asset */ |

### `Assets/!Scenes/Result.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `Main Camera` | 1 | 0 | なし | -1 | 1 | 0 | `0,0,1,1` |
| `PrintSystem/PrintCamera` | 0 | 0 | なし | 0 | 1 | 0 | `0,0,1,1` |

### `Assets/!Scenes/Splash.unity`

| カメラ(GameObject) | 有効 | TargetDisplay | TargetTexture | Depth | DLSS | 動的解像度 | Viewport |
|---|---|---|---|---|---|---|---|
| `Main Camera` | 1 | 0 | なし | -1 | 1 | 0 | `0,0,1,1` |

### 読み取れること(`コード`。上の表から)

- カメラは、`Load`・`Splash`・`Menu`・`Opening`・`Map`・`Result` の 6 シーンに 1 台ずつ。`Result` だけ、`PrintSystem/PrintCamera`(無効。証明書の印刷用とみられる)がもう 1 台ある。すべて `TargetDisplay` 0、DLSS が有効。
- 動的解像度は、`Map`・`Menu`・`Opening` のカメラで有効、`Load`・`Splash`・`Result` で無効。
- Cinemachine の Brain は、`Map`・`Menu`・`Opening` の 3 つ。`Load`・`Splash`・`Result` は、Cinemachine なしの普通のカメラ。
- `Map` のカメラは、車両(`'###Vehicle###'`)の子の `MainCamera`。常駐のカメラにまとめるなら、車両との親子関係と、Cinemachine の仮想カメラ(`Bonnet`・`Bumper`・`POV`、ムービー用のスプラインドリー 4 台)の扱いを決める必要がある。
- Volume は、`Map` が Global 2 つ(`Glitch` = `garage!move.asset`、`Global Volume` = `Map2025_tmp terae.asset`)、`Menu` が Local 1 つ(priority 100、`Opening2024 Volume Profile.asset`)、`Opening` が Local 1 つ(priority 100、`Opening 2025 Volume Profile.asset`)。`Load`・`Splash`・`Result` は、シーン内の Volume がなく、HDRP の既定(`Map2025.asset`)だけ(Prefab の中の Volume は読んでいない)。
- **`garage!move.asset`(V-6)は、`Map.unity` の `Glitch` という Volume が使っている。** `CurrentStateAnalysis.md` の「操作するスクリプトが未配置」は、スクリプトの話で、プロファイル自体は使われている。
- 実行時に動かすものは、`PlayableDirector` が 2 つ(`Map` の `Movie.playable`、`Opening` の `OpeningTimeline.playable`)。

カメラを持つ Prefab(追跡している `.prefab` のうち):

なし

## 7. 控えていないもの(`未確認`)

- シーンの `PrefabInstance` による、自作コンポーネントの値の上書き(Prefab 側の値だけを読んだ)。
- `AnimationEvent`・Timeline のシグナルなど、メソッド名の文字列での呼び出し。
- `.inputactions` と、`PlayerInput` の `m_DefaultActionMap`・`m_Camera` などの設定(上の「2」に、自作の型の値だけを載せた)。
- 実行時に生成するもの(`ResultState` が作る Result のカメラ、`RacingEnter` の Volume・Custom Pass、`MultiScreenOutput` の Canvas)は、コードにある。
