using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

//メニュー入力が届く先と受付状態を実行中に確認する検証クラス
[InitializeOnLoad]
public static class MenuInputValidation
{
    //実行中の実機診断は必要な時だけ有効化してログを増やさない
    static bool diagnostics;
    //名前受信後の実シーン検査で使用する模擬キーボード
    static Keyboard testKeyboard;
    static double nameDeadline;
    static double nameStarted;
    static int namePhase;
    static double next;
    static MenuInputValidation() { EditorApplication.update += Inspect; }

    //実際の入力マップと受付フラグを読み取り専用で記録する関数
    static void Inspect()
    {
        if (!diagnostics || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) { return; }
        next = EditorApplication.timeSinceStartup + 10;
        foreach (var state in Object.FindObjectsByType<GameStateBindingBase>(FindObjectsSortMode.None))
        {
            if (!(state is OpeningState) && !(state is MenuState)) { continue; }
            string values = "";
            foreach (var field in state.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(bool)) { values += " " + field.Name + "=" + field.GetValue(state); }
            }
            Debug.Log("[MenuInputValidation] " + state.GetType().Name + values);
        }
        foreach (var input in Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None))
        {
            Debug.Log($"[MenuInputValidation] {input.name} map={input.currentActionMap?.name} active={input.inputIsActive} devices={input.devices.Count}");
        }
    }

    //別の受付が機器を取得済みの状態を空シーンで再現して検査を開始する関数
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        SessionState.SetBool("MenuInputValidation.Pending", true);
        EditorApplication.isPaused = false;
        EditorApplication.EnterPlaymode();
    }

    //Play開始後にPlayerInputの通常の初期化を通した検査を予約する関数
    [InitializeOnLoadMethod]
    static void Hook()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MenuInputValidation.Pending", false)) { return; }
            SessionState.SetBool("MenuInputValidation.Pending", false);
            EditorApplication.delayCall += CheckSharing;
        };
    }

    //二つのPlayerInputで機器の割り当てが空でもメニューが入力を受け取れるか検査する関数
    static void CheckSharing()
    {
        if (Time.frameCount < 3)
        {
            EditorApplication.isPaused = false;
            EditorApplication.delayCall += CheckSharing;
            return;
        }
        try
        {
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystems/Aim2024Input.inputactions");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            testKeyboard = keyboard;
            var owner = CreateInput("Owner", asset);
            InputSystem.AddDevice<Gamepad>();
            var menu = CreateInput("Menu", asset);
            InputUser.PerformPairingWithDevice(keyboard, owner.user);
            if (menu.user.valid) { menu.user.UnpairDevices(); }
            int presses = 0;
            menu.actions.FindAction("MainSystem/AccelTriger").performed += _ => presses++;
            MenuInputDeviceSharing.Ensure(menu);
            ValidateReleasedPedal(menu);
            var action = menu.actions.FindAction("MainSystem/AccelTriger");
            Debug.Log($"[MenuInputValidation] enabled={action.enabled} controls={action.controls.Count} map={menu.currentActionMap?.name} active={menu.inputIsActive}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            FlushInput();
            if (presses != 1)
            {
                Debug.Log($"[MenuInputValidation] key={keyboard[Key.UpArrow].ReadValue()} deviceEnabled={keyboard.enabled} phase={action.phase} time={Time.time}");
                InputState.Change(keyboard, new KeyboardState(Key.UpArrow));
                Debug.Log($"[MenuInputValidation] directChange presses={presses} key={keyboard[Key.UpArrow].ReadValue()}");
                foreach (var control in action.controls) { Debug.Log($"[MenuInputValidation] control={control.path} id={control.device.deviceId} test={keyboard.deviceId}"); }
                throw new System.Exception("共有後の上矢印が届きません count=" + presses);
            }
            bool ownerStillPaired = false;
            foreach (var device in owner.devices) { if (device == keyboard) { ownerStillPaired = true; } }
            if (!ownerStillPaired) { throw new System.Exception("既存受付から機器を奪っています"); }
            //名前入力後の別PlayerInputでも同じ機器を共有できることを確認する
            var nextMenu = CreateInput("AfterNameEntry", asset);
            if (nextMenu.user.valid) { nextMenu.user.UnpairDevices(); }
            MenuInputDeviceSharing.Ensure(nextMenu);
            int nextPresses = 0;
            nextMenu.actions.FindAction("MainSystem/AccelTriger").performed += _ => nextPresses++;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            FlushInput();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            FlushInput();
            if (nextPresses != 1) { throw new System.Exception("次のメニューでキーが届きません"); }
            Debug.Log("[MenuInputValidation] PASS shared keyboard / owner preserved / next menu");
            //F3を使わず本番の名前受信処理から選択画面まで検査する
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            FlushInput();
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode("Assets/!Scenes/Menu.unity",
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            nameStarted = EditorApplication.timeSinceStartup;
            nameDeadline = nameStarted + 120;
            namePhase = 0;
            EditorApplication.update += CheckNameEntry;
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    //本番のNAMEメッセージから選択と確認の入力まで検査する関数
    static void CheckNameEntry()
    {
        EditorApplication.isPaused = false;
        try
        {
            if (EditorApplication.timeSinceStartup > nameDeadline) { throw new System.Exception("名前受信後の進行が時間切れです"); }
            var menu = Object.FindFirstObjectByType<MenuState>();
            var runner = Object.FindFirstObjectByType<GameFlowRunner>();
            var server = Object.FindFirstObjectByType<TcpServer>();
            if (menu == null || runner == null || server == null || runner.CurrentId != GameStateId.Menu) { return; }
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            if (namePhase == 0)
            {
                if (EditorApplication.timeSinceStartup - nameStarted < 5) { return; }
                typeof(TcpServer).GetMethod("ProcessMessage", flags).Invoke(server, new object[] { "NAME|InputValidation" });
                namePhase = 1;
                Debug.Log("[MenuInputValidation] real NAME message dispatched without F3");
                return;
            }
            if (!(bool)typeof(MenuState).GetField("m_selectionReady", flags).GetValue(menu)) { return; }
            if (namePhase == 1)
            {
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.LeftArrow));
                FlushInput();
                if (!(bool)typeof(MenuState).GetField("m_isSelected", flags).GetValue(menu)) { throw new System.Exception("名前受信後の左右選択が届きません"); }
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.UpArrow));
                FlushInput();
                if (!(bool)typeof(MenuState).GetField("m_isConfirming", flags).GetValue(menu)) { throw new System.Exception("名前受信後の確認へ進めません"); }
                InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                FlushInput();
                nameStarted = EditorApplication.timeSinceStartup;
                namePhase = 2;
                return;
            }
            if (EditorApplication.timeSinceStartup - nameStarted < 0.5) { return; }
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.UpArrow));
            FlushInput();
            if (!(bool)typeof(MenuState).GetField("m_triger", flags).GetValue(menu)) { throw new System.Exception("二回目の決定が届きません"); }
            Debug.Log("[MenuInputValidation] PASS NAME receive / selection visible / left select / first confirm / second confirm");
            EditorApplication.update -= CheckNameEntry;
            EditorApplication.Exit(0);
        }
        catch (System.Exception error)
        {
            Debug.LogException(error);
            EditorApplication.update -= CheckNameEntry;
            EditorApplication.Exit(1);
        }
    }

    //本番と同じアクションとマップで入力受付を生成する関数
    static void FlushInput()
    {
        var update = InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsManually ? InputUpdateType.Manual : InputUpdateType.Dynamic;
        typeof(InputSystem).GetMethod("Update", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(InputUpdateType) }, null).Invoke(null, new object[] { update });
    }

    //実機を接続せず同じ軸名の模擬ペダルで静止状態と踏み直しの受付を確認する関数
    static void ValidateReleasedPedal(PlayerInput input)
    {
        const string layout = "HID::Logitech G923 Racing Wheel for PlayStation and PC";
        InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Joystick\",\"controls\":[{\"name\":\"z\",\"layout\":\"Axis\",\"offset\":16,\"format\":\"FLT\"}]}");
        var wheel = InputSystem.AddDevice(layout);
        var pedal = wheel.GetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("z");
        MenuInputDeviceSharing.Ensure(input);
        var detector = new AxisPressdOnce();
        float previous = float.NaN;
        var action = input.actions.FindAction("MainSystem/AccelPedal");
        action.performed += context => detector.AxisCheck((1f - context.ReadValue<float>()) * 0.5f);
        InputSystem.QueueDeltaStateEvent(pedal, 1f);
        FlushInput();
        detector.Reset();
        MenuInputDeviceSharing.ObserveReleasedPedal(input, detector, ref previous);
        InputSystem.QueueDeltaStateEvent(pedal, -1f);
        FlushInput();
        if (!detector.PressedOnce) { throw new System.Exception("静止ペダルからの踏み込みが届きません"); }
        InputSystem.RemoveDevice(wheel);
        Debug.Log("[MenuInputValidation] PASS simulated G923 released pedal / press after reset");
    }

    static PlayerInput CreateInput(string name, InputActionAsset asset)
    {
        var host = new GameObject(name);
        host.SetActive(false);
        var input = host.AddComponent<PlayerInput>();
        input.actions = Object.Instantiate(asset);
        input.defaultActionMap = "MainSystem";
        host.SetActive(true);
        return input;
    }
}
