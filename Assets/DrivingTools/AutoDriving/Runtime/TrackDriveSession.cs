using System;
using System.IO;
using System.Globalization;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Splines;

//明示的に開始した時だけ通常の車両入力で実コースを走りログを残す検証クラス
#if UNITY_EDITOR
[InitializeOnLoad]
#endif
public static partial class TrackDriveSession
{
    //検証中だけ入力を送り通常プレイへ持ち越さない状態
    static bool active;
    //DrivingValidationWindowが現在の実行状態を表示するための読み取り専用参照
    public static bool IsActive => active;
    //位置を書き換えず走行目標の検索に使う道路中心線
    static Vector3[] route;
    //本番の車両と到達判定を読む参照
    static VehicleController vehicle;
    static Line_StartFinish goal;
    //開始時刻と記録間隔を管理する時刻
    static double started;
    static double nextLog;
    static double stuckSince;
    //ヘアピンで別区間を拾わないための近傍番号と進行方向
    static int nearest;
    static int direction;
    //走行結果を保存するファイル
    static StreamWriter log;
    //実際の入力経路でモード変更を検証するための押下と解除の予約
    static int modeStep = -1;
    static double modeNext;
    //各輪の要求音量と実際のFMOD出力を比較する音源一覧
    static TireSound[] tireSounds;
    //四つの条件を同じ走行入力で比較するための選択で本番データへ保存しない
    static int profile;
    //発進前の踏み戻しと燃料カットを音の出力と同時に記録する開始時刻
    static float profileStarted;
    static DriveSound driveSound;
    //本番カメラを変えず車体とタイヤの相対運動を見るためのシーン追従表示
    static bool followScene;

    //ハンドル入力と早めの操舵を同じ条件で比較する検証設定
    static bool analogTest;
    static bool earlyTurn;
    static bool armed;
    //通常走行より高い進入速度で滑りの回復を確認する検証専用条件
    static bool driftChallenge;
    //本番の保存値を変更せず今回の調整前相当と比較する検証条件
    static bool previousTrackTuning;
    //実クラッチの変速手順だけを検証する入力状態
    static bool leverTest;
    static float leverShiftStarted = -1f;
    static bool leverShiftUp;
    static bool leverShiftSent;

    //Opening/MenuのFadeIn完了通知(GameFlowRunner.OnStateFadeInComplete)を受けて自動入力を開始するための状態
    static int openingSkipStep = -1;
    static double openingSkipNext;
    static int nameEntryStep = -1;
    static double nameEntryNext;
    //Resultを抜けるEnterの押下と解除の予約と、自動操作の周回で走った後だけ押すための状態
    static int resultReturnStep = -1;
    static double resultReturnNext;
    static bool autoReturnFromResult;
    static Steering steeringComponent;
    static Rigidbody vehicleBody;
    static VehicleContactProbe contactProbe;
    //自動入力が本番の速度別舵角と入力カーブを無視しないための読取専用設定
    static float steeringMechanicalLimit;
    static float steeringLowLimit;
    static float steeringHighLimit;
    static float steeringReductionStart;
    static float steeringReductionEnd;
    static float steeringInputExponent;
    static float steeringInputBoost;

    //速度別舵角のモード倍率を検証用入力の逆算へ反映する値
    static float steeringNormalScale;
    static float steeringSportScale;
    static float steeringTrackScale;

    //検証パネルまたは自動走行メニューで選んだ条件を次の再生へ引き継ぐ関数
    public static void RunFromWindow(int _profile, bool _early, bool _challenge, bool _previous, bool _leverTest, bool _hairpinTest = false)
    {
        //Track自動走行では進入制御を有効にし比較用の通常走行では無効にする
        TrackDrivePreferences.SetBool(HairpinTestKey, _hairpinTest);
        //前回のクラッチ試験を通常の自動走行へ持ち越さない
        TrackDrivePreferences.SetBool("Sep11LeverTest", _leverTest);
        ArmRecheck(_profile, _early, _challenge, _previous);
    }

    //本番データを保存せず検証条件だけを再生時へ引き継ぐ関数
    static void ArmRecheck(int selectedProfile, bool early, bool challenge = false, bool previous = false)
    {
#if UNITY_EDITOR
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
        //本番と同じ常駐管理オブジェクトの初期化を経由する
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/!Scenes/Load.unity");
        TrackDrivePreferences.SetInt("Sep11RecheckProfile", selectedProfile);
        TrackDrivePreferences.SetBool("Sep11RecheckEarly", early);
        TrackDrivePreferences.SetBool("Sep15DriftChallenge", challenge);
        TrackDrivePreferences.SetBool("Sep15PreviousTrackTuning", previous);
        TrackDrivePreferences.SetBool("Sep11RecheckArmed", true);
        //ドメイン再読み込みを無効にしたEditorでも今回の選択条件を反映する
        profile = selectedProfile;
        earlyTurn = early;
        driftChallenge = challenge;
        previousTrackTuning = previous;
        leverTest = TrackDrivePreferences.GetBool("Sep11LeverTest", false);
        analogTest = true;
        armed = true;
        EditorApplication.isPlaying = true;
    #endif
    }

    //ゲーム遷移を飛ばさずRacing到達を監視し、到達したら実走検証を開始する関数(Opening/Menuの自動入力はOnStateFadeInCompleteへ移譲)
    static void UpdateRecheck()
    {
        if (!armed || !Application.isPlaying || IsDriverPaused) { return; }
        var flow = UnityEngine.Object.FindFirstObjectByType<GameFlowRunner>();
        if (flow == null) { return; }
        if (flow.CurrentId != GameStateId.Racing) { return; }
        armed = false;
        TrackDrivePreferences.SetBool("Sep11RecheckArmed", false);
        Start();
        followScene = true;
    }

    //画面が見えてから実際にキーを押すまで空ける秒数(周回の時間効率のため、3秒に短縮している)
    const double k_OpeningSkipDelaySeconds = 3d; //Openingが見えてから、UpArrowでスキップするまで
    const double k_NameEntrySkipDelaySeconds = 3d; //Menuが見えてから、F3(SkipNameEntry)を押すまで。この後にAT/MTの選択入力が続く
    const double k_ResultReturnDelaySeconds = 3d; //Resultが見えてから、Enter(End/BackToStart)でタイトルへ戻るまで

    //GameFlowRunnerのFadeIn完了(=画面が確実に見えた瞬間)を受けてOpeningスキップ/名前入力/Resultからの復帰の自動化を開始する関数
    static void OnGameStateFadeInComplete(GameStateId _state)
    {
        //Resultは、自動操作の周回(ArmRuntime)で走った後だけ、Enterで戻る。走行の終了でarmedは既に下りているため、別の状態で判定する
        if (_state == GameStateId.Result && autoReturnFromResult && Application.isPlaying)
        {
            autoReturnFromResult = false; //1回の走行につき1回だけ押す
            resultReturnStep = 0; //Resultが実際に描画されてから3秒後にEnterを1回押して、Resultを抜ける
            resultReturnNext = Time.time + k_ResultReturnDelaySeconds;
            return;
        }

        if (!armed || !Application.isPlaying) { return; } //手動プレイ・検証未開始中は何もしない
        if (_state == GameStateId.Opening)
        {
            openingSkipStep = 0; //Opening本編が実際に描画されてから3秒後にUpArrowを1回押してスキップする
            openingSkipNext = Time.time + k_OpeningSkipDelaySeconds;
        }
        else if (_state == GameStateId.Menu)
        {
            nameEntryStep = 0; //Menuが実際に描画されてから3秒後にF3(SkipNameEntry)を押して名前入力を完了させる
            nameEntryNext = Time.time + k_NameEntrySkipDelaySeconds;
        }
    }

    //Sceneビューだけを車体の後方斜め上から追従させる関数(DrivingValidationWindowから呼ばれる)
    public static void ToggleVehicleView() { followScene = !followScene; }

    //通常モードのATで検証する選択を行う関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/Profile Normal AT")]
    #endif
    public static void NormalAT() { profile = 0; }
    //通常モードのMTで検証する選択を行う関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/Profile Normal MT")]
    #endif
    public static void NormalMT() { profile = 1; }
    //経験者モードのATで検証する選択を行う関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/Profile Track AT")]
    #endif
    public static void TrackAT() { profile = 2; }
    //経験者モードのMTで検証する選択を行う関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/Profile Track MT")]
    #endif
    public static void TrackMT() { profile = 3; }

    //数字キーを順番に押し最後にTrackで走行する関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/Test Mode Keys and Track")]
    #endif
    public static void TestModeKeys()
    {
        if (!Application.isPlaying) { return; }
        modeStep = 0;
        modeNext = 0d;
    }

    //タイヤ音源の出力を読み取りパラメーター送信だけで成功と判断しない関数
    static float ReadTirePeak(TireSound sound)
    {
        var field = typeof(TireSound).GetField("m_tireEventInst", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var instance = (FMOD.Studio.EventInstance)field.GetValue(sound);
        if (!instance.isValid() || instance.getChannelGroup(out var group) != FMOD.RESULT.OK) { return 0f; }
        if (group.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, out var dsp) != FMOD.RESULT.OK) { return 0f; }
        dsp.setMeteringEnabled(false, true);
        if (dsp.getMeteringInfo(IntPtr.Zero, out FMOD.DSP_METERING_INFO output) != FMOD.RESULT.OK) { return 0f; }
        float peak = 0f;
        for (int i = 0; i < output.numchannels; i++) { peak = Mathf.Max(peak, output.peaklevel[i]); }
        return peak;
    }

    //実機を変更せずメニューの通常入力経路へ短いキー入力を送る検証状態
    static int menuStep = -1;
    static double menuNext;
    //低フレームレートでも入力更新をまたげる押下時間
    const double k_MenuKeyHoldSeconds = 0.5d;
    static readonly double[] k_MenuStepIntervals = { k_MenuKeyHoldSeconds, 2d, k_MenuKeyHoldSeconds, 2d, k_MenuKeyHoldSeconds };
    //セレクト(押下→解除)の後に2秒待ち、1回目のアクセル(押下→解除)の後にさらに2秒待ってから最終決定(押下→解除)する

    //オープニングと選択画面の入力を再現する関数(DrivingValidationWindowから呼ばれる)
    public static void SendMenuInputs()
    {
        if (!Application.isPlaying) { return; }
        menuStep = 0;
        menuNext = 0d;
    }

    //押下と解除を別フレームにして通常のInputActionへ通知する関数
    static void UpdateMenuInputs()
    {
        //OpeningのFadeIn完了を受け、UpArrowを1回だけ押してOpeningをスキップする
        if (openingSkipStep >= 0 && Application.isPlaying && !IsDriverPaused && Time.time >= openingSkipNext)
        {
            var openingKeyboard = UnityEngine.InputSystem.Keyboard.current;
            if (openingKeyboard != null)
            {
                var openingState = openingSkipStep == 0 ? new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.UpArrow)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(openingKeyboard, openingState);
            }
            openingSkipStep++;
            openingSkipNext = Time.time + k_MenuKeyHoldSeconds;
            if (openingSkipStep >= 2) { openingSkipStep = -1; }
        }
        //ResultのFadeIn完了を受け、Enter(End/BackToStart)を1回押して、Resultからタイトルへ戻る
        if (resultReturnStep >= 0 && Application.isPlaying && !IsDriverPaused && Time.time >= resultReturnNext)
        {
            var resultKeyboard = UnityEngine.InputSystem.Keyboard.current;
            if (resultKeyboard != null)
            {
                var resultState = resultReturnStep == 0 ? new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(resultKeyboard, resultState);
            }
            resultReturnStep++;
            resultReturnNext = Time.time + k_MenuKeyHoldSeconds;
            if (resultReturnStep >= 2) { resultReturnStep = -1; }
        }
        //MenuのFadeIn完了を受け、F3(SkipNameEntry)を押して名前入力を即完了させ、
        //EntryName退出+EntryTransmission入場の演出時間だけ空けてからAT/MT選択入力を開始する
        if (nameEntryStep >= 0 && Application.isPlaying && !IsDriverPaused && Time.time >= nameEntryNext)
        {
            var nameKeyboard = UnityEngine.InputSystem.Keyboard.current;
            if (nameKeyboard != null)
            {
                var nameState = nameEntryStep == 0 ? new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.F3)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(nameKeyboard, nameState);
            }
            nameEntryStep++;
            if (nameEntryStep >= 2)
            {
                nameEntryStep = -1;
                SendMenuInputs();
                //待ち時間だけに頼らず選択画面の準備完了も下で確認する
                menuNext = Time.time + 4.4d;
            }
            else
            {
                nameEntryNext = Time.time + k_MenuKeyHoldSeconds;
            }
        }
        //メニュー操作とは別にキーの押下と解除を通常の入力更新へ送る
        if (modeStep >= 0 && Application.isPlaying && !IsDriverPaused && Time.realtimeSinceStartupAsDouble >= modeNext)
        {
            var modeKeyboard = UnityEngine.InputSystem.Keyboard.current;
            if (modeKeyboard != null)
            {
                var keys = new[] { UnityEngine.InputSystem.Key.Digit2, UnityEngine.InputSystem.Key.Digit1, UnityEngine.InputSystem.Key.Digit3 };
                var modeState = modeStep % 2 == 0 ? new UnityEngine.InputSystem.LowLevel.KeyboardState(keys[modeStep / 2])
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState();
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(modeKeyboard, modeState);
            }
            modeStep++;
            modeNext = Time.realtimeSinceStartupAsDouble + 2d;
            if (modeStep >= 6) { modeStep = -1; }
        }
        if (menuStep < 0 || !Application.isPlaying || IsDriverPaused) { return; }
        if (Time.time < menuNext) { return; }
        //演出や一時停止で選択入力が早過ぎても未選択のまま決定へ進めない
        if (menuStep == 0 && !IsTransmissionSelectionReady()) { return; }
        var keyboard = UnityEngine.InputSystem.Keyboard.current;
        if (keyboard == null) { menuStep = -1; return; }
        var select = profile % 2 == 0 ? UnityEngine.InputSystem.Key.LeftArrow : UnityEngine.InputSystem.Key.RightArrow;
        var key = menuStep < 2 ? select : UnityEngine.InputSystem.Key.UpArrow;
        var state = menuStep % 2 == 0 ? new UnityEngine.InputSystem.LowLevel.KeyboardState(key)
            : new UnityEngine.InputSystem.LowLevel.KeyboardState();
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, state);
        int finishedMenuStep = menuStep;
        menuStep++;
        if (menuStep >= 6) { menuStep = -1; }
        else { menuNext = Time.time + k_MenuStepIntervals[finishedMenuStep]; }
    }

    //ゲーム側を書き換えずATとMTの選択受付開始を読み取る関数
    static bool IsTransmissionSelectionReady()
    {
        var menu = UnityEngine.Object.FindFirstObjectByType<MenuState>();
        if (menu == null) { return false; }
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var field = typeof(MenuState).GetField("m_isEntryTransmissionActive", flags);
        return field != null && (bool)field.GetValue(menu);
    }

    //エディタ終了時と再生停止時に検証入力を解放する関数
    static TrackDriveSession()
    {
#if UNITY_EDITOR
        armed = TrackDrivePreferences.GetBool("Sep11RecheckArmed", false);
        leverTest = TrackDrivePreferences.GetBool("Sep11LeverTest", false);
        driftChallenge = TrackDrivePreferences.GetBool("Sep15DriftChallenge", false);
        previousTrackTuning = TrackDrivePreferences.GetBool("Sep15PreviousTrackTuning", false);
        if (armed)
        {
            profile = TrackDrivePreferences.GetInt("Sep11RecheckProfile", 2);
            earlyTurn = TrackDrivePreferences.GetBool("Sep11RecheckEarly", false);
            analogTest = true;
        }
        EditorApplication.update += UpdateRecheck;
        EditorApplication.update += Tick;
        EditorApplication.update += UpdateMenuInputs;
        GameFlowRunner.OnStateFadeInComplete += OnGameStateFadeInComplete; //Opening/MenuのFadeIn完了を検知して自動入力を開始する
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                armed = false;
                TrackDrivePreferences.SetBool("Sep11RecheckArmed", false);
                TrackDrivePreferences.SetBool("Sep11LeverTest", false);
                TrackDrivePreferences.SetBool("Sep15DriftChallenge", false);
                TrackDrivePreferences.SetBool("Sep15PreviousTrackTuning", false);
                Stop();
            }
        };
        //再生開始時のドメイン再読み込みでは開始予約を維持し走行中の入力だけを解放する
        AssemblyReloadEvents.beforeAssemblyReload += () => { if (active) { Stop(); } };
    #endif
    }

    //再生中の車へ検証入力を送り始める関数(DrivingValidationWindowから呼ばれる)
    public static void Start()
    {
        if (!Application.isPlaying || active) { return; }
        vehicle = UnityEngine.Object.FindFirstObjectByType<VehicleController>();
        if (vehicle == null) { Debug.LogError("[Sep11Course] 車両の読み込み後に開始してください。"); return; }
        SplineContainer road = null;
        goal = null;
        foreach (SplineContainer candidate in UnityEngine.Object.FindObjectsByType<SplineContainer>(FindObjectsSortMode.None))
        {
            if (candidate.name == "RoadSpline") { road = candidate; }
        }
        foreach (Line_StartFinish candidate in UnityEngine.Object.FindObjectsByType<Line_StartFinish>(FindObjectsSortMode.None))
        {
            if (candidate.name == "GoalGate_AsamaOne") { goal = candidate; }
        }
        if (road == null || goal == null) { Debug.LogError("[Sep11Course] 道路またはゴールがありません。"); return; }
        route = new Vector3[4001];
        for (int i = 0; i < route.Length; i++) { route[i] = road.EvaluatePosition(0, i / 4000f); }
        nearest = FindNearest(vehicle.transform.position, 0, route.Length - 1);
        direction = FindNearest(goal.transform.position, 0, route.Length - 1) > nearest ? 1 : -1;
        routeProgressMeters = 0f;
        BuildRacingLine();
        started = Time.realtimeSinceStartupAsDouble;
        profileStarted = Time.time;
        raceStarted = -1f;
        lastDriverTime = -1f;
        desiredSlip = 0f;
        driverSteer = 0f;
        ResetManualDriver();
        driftPursuitBlend = 0f;
        driverRoadError = 0f;
        nextDriverShift = 0f;
        driverPhase = 0;
        driverCurvature = 0f;
        ResetHairpinDrift();
        leverShiftStarted = -1f;
        driveSound = vehicle.GetComponentInChildren<DriveSound>();
        //別の子階層へ配置された部品も本番が使う参照から読み取る
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        steeringComponent = (Steering)typeof(VehicleController).GetField("m_steering", flags).GetValue(vehicle);
        vehicleBody = (Rigidbody)typeof(VehicleController).GetField("m_rigidbody", flags).GetValue(vehicle);
        if (steeringComponent == null || vehicleBody == null) { Debug.LogError("[Sep11Course] 車両部品の初期化が未完了です。"); return; }
        ReadSteeringCalibration();
        //比較用の一時変更は再生中の車だけに適用しシーンやPrefabへ保存しない
        if (previousTrackTuning) { ApplyPreviousTrackTuning(); }
        contactProbe = vehicleBody.gameObject.AddComponent<VehicleContactProbe>();
        //検証条件だけを選び車両のトルクや摩擦などの値は変更しない
        vehicle.Transmission.Type = profile % 2 == 0 ? Transmission.TransmissionType.Automatic : Transmission.TransmissionType.Manual;
        vehicle.passDifferential.SetDriveMode(profile >= 4 ? Differential.DriveMode.Sport
            : profile >= 2 ? Differential.DriveMode.Track : Differential.DriveMode.Normal);
        nextLog = started;
        stuckSince = -1;
        string path = Path.Combine(Application.persistentDataPath, "TrackDrive_" + (profile % 2 == 0 ? "AT_" : "MT_")
            + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv");
        log = new StreamWriter(path);
        log.WriteLine("seconds,index,kph,rpm,gear,accel,brake,steer,grounded,esc,slip,mode,wheelFR,wheelFL,wheelRR,wheelRL,visualError,track,"
            + "frontRatio,soundRequest,soundParam,soundPeak,simSeconds,pullUp,revCut,gas,drivePeak,transmission,"
            + "analog,early,lateral,steerAngle,yawRate,wallContacts,wallImpulse,"
            + "loadFR,loadFL,loadRR,loadRL,compressionFR,compressionFL,compressionRR,compressionRL,"
            + "sideSlipFR,sideSlipFL,sideSlipRR,sideSlipRL,pitchDegrees,rollDegrees,bodyKinematic,"
            + "trackAllowance,slipRate,yawAssist,escBrakeWheel,driftChallenge,previousTrackTuning,targetKph,targetSlip,raceSeconds,"
            + "driverPhase,roadCurvature,limitingCornerDistance,lineOffset,lineError,handbrake,hairpinAttempts,gripRR,gripRL,entryIntent");
        tireSounds = vehicle.GetComponentsInChildren<TireSound>();
        active = true;
        vehicle.ClearPhysicalClutchPedal();
        if (!leverTest) { vehicle.SelectPaddleClutch(); }
        Debug.Log("[Sep11Course] 自動入力で実走開始 " + path);
    }

    //異常時や手動停止時にペダルとハンドルを確実に離す関数(DrivingValidationWindowから呼ばれる)
    public static void Stop()
    {
        //手動停止後にRacingへ入っても自動入力を再開しない
        armed = false;
        TrackDrivePreferences.SetBool("Sep11RecheckArmed", false);
        followScene = false;
        ResetHairpinDrift();
        //次の通常走行や手動検証へ急カーブ試験の選択を持ち越さない
        TrackDrivePreferences.SetBool(HairpinTestKey, false);
        if (active && vehicle != null)
        {
            vehicle.Accel = 0f;
            vehicle.Brake = 0f;
            vehicle.SetSteeringInput(0f, true);
            if (leverTest) { vehicle.ClearPhysicalClutchPedal(); vehicle.SelectPaddleClutch(); }
        }
        active = false;
        //ゴール後の惰性移動を走行テスト中の接触と混ぜないよう記録を終了する
        if (contactProbe != null) { UnityEngine.Object.Destroy(contactProbe); }
        contactProbe = null;
        menuStep = -1;
        modeStep = -1;
        openingSkipStep = -1;
        nameEntryStep = -1;
        log?.Dispose();
        log = null;
    }

    //現在の車体に最も近い道路点を限定範囲から求める関数
    static int FindNearest(Vector3 position, int first, int last)
    {
        int result = first;
        float best = float.PositiveInfinity;
        for (int i = first; i <= last; i++)
        {
            float distance = (route[i] - position).sqrMagnitude;
            if (distance >= best) { continue; }
            result = i;
            best = distance;
        }
        return result;
    }

    //指定距離だけ先の道路点を取得する関数
    static Vector3 Ahead(float distance)
    {
        return RoutePoint(Mathf.Max(0f, distance));
    }

    //既存の操舵設定を読むだけにして検証のために本番の値を上書きしない関数
    static void ReadSteeringCalibration()
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        float Read(string name) => (float)typeof(Steering).GetField(name, flags).GetValue(steeringComponent);
        steeringMechanicalLimit = Mathf.Min(Read("m_maxSteerAngle"), Read("m_steeringRange") * 0.5f / Mathf.Max(1f, Read("m_steeringGearRatio")));
        steeringLowLimit = Read("m_lowSpeedMaxSteerAngle");
        steeringHighLimit = Read("m_highSpeedMaxSteerAngle");
        steeringReductionStart = Read("m_steeringReductionStartKph");
        steeringReductionEnd = Read("m_fullReductionSpeedKph");
        steeringInputExponent = Read("m_highSpeedInputExponent");
        steeringInputBoost = Read("m_analogMidInputBoost");
        steeringNormalScale = Read("m_normalHighSpeedAngleScale");
        steeringSportScale = Read("m_sportHighSpeedAngleScale");
        steeringTrackScale = Read("m_trackHighSpeedAngleScale");
    }

    //必要な前輪角を本番の速度別入力カーブの逆算でハンドル入力へ変換する関数
    static float CalibratedInput(float angle, float speed)
    {
        float speedRatio = Mathf.InverseLerp(steeringReductionStart, steeringReductionEnd, Mathf.Abs(speed));
        speedRatio = speedRatio * speedRatio * (3f - 2f * speedRatio);
        float modeScale = steeringNormalScale;
        modeScale += (steeringSportScale - steeringNormalScale) * vehicle.passDifferential.SportHandlingBlend;
        modeScale += (steeringTrackScale - steeringNormalScale) * vehicle.passDifferential.TrackHandlingBlend;
        float speedLimit = Mathf.Lerp(steeringLowLimit, steeringHighLimit, speedRatio) * Mathf.Lerp(1f, modeScale, speedRatio);
        float limit = Mathf.Min(steeringMechanicalLimit, speedLimit);
        float exponent = Mathf.Lerp(1f, steeringInputExponent, speedRatio);
        float target = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(angle) / Mathf.Max(0.01f, limit)), 1f / exponent);
        float lower = 0f;
        float upper = 1f;
        //単調な中間入力補正を二分探索で逆算し速度が上がるほど切り足りなくなる誤差を除く
        for (int i = 0; i < 16; i++)
        {
            float middle = (lower + upper) * 0.5f;
            float shaped = middle + steeringInputBoost * middle * middle * (1f - middle);
            if (shaped < target) { lower = middle; }
            else { upper = middle; }
        }
        if (Mathf.Abs(angle) < 0.0001f) { return 0f; }
        return Mathf.Sign(angle) * (lower + upper) * 0.5f;
    }

    //位置や速度を強制せずアクセルとブレーキと操舵だけで実走する関数
    //ペダルを切って変速し回転合わせ後に徐々につなぐ操作を模擬する関数
    static void UpdateLeverTest()
    {
        if (leverShiftStarted < 0f)
        {
            vehicle.SetPhysicalClutchPedal(0f);
            bool up = vehicle.EngineRPM > 6900f && vehicle.ActiveGear < 6;
            bool down = vehicle.EngineRPM < 3300f && vehicle.ActiveGear > 1;
            if (!up && !down) { return; }
            leverShiftStarted = Time.time;
            leverShiftUp = up;
            leverShiftSent = false;
        }
        float elapsed = Time.time - leverShiftStarted;
        vehicle.SetPhysicalClutchPedal(elapsed < 0.4f ? 1f : 1f - Mathf.InverseLerp(0.4f, 0.7f, elapsed));
        vehicle.Accel = 0f;
        if (elapsed >= 0.12f && !leverShiftSent)
        {
            bool shifted = vehicle.RequestLeverShift(leverShiftUp);
            Debug.Log($"[LeverCourse] request up={leverShiftUp} accepted={shifted} pedal={vehicle.Clutch}");
            leverShiftSent = true;
        }
        float targetRPM = vehicle.Transmission.GetSynchronizedRPM(vehicle.ActiveGear, vehicle.KPH);
        if (elapsed >= 0.12f && vehicle.EngineRPM < targetRPM) { vehicle.Accel = Mathf.Clamp01((targetRPM - vehicle.EngineRPM) / 1000f); }
        if (elapsed >= 0.7f) { leverShiftStarted = -1f; }
    }

    //道路曲率と制動距離から速度を計画する関数
    static float ComputeTargetSpeed(float minCurveSpeedKph)
    {
        return PlannedSpeed(minCurveSpeedKph);
    }

    //位置や速度を強制せず通常入力で実コースを進める関数
    static void Tick()
    {
        if (!active || !Application.isPlaying || IsDriverPaused || vehicle == null) { return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (lastDriverTime == Time.time) { return; }
        float deltaTime = lastDriverTime < 0f ? Time.fixedDeltaTime : Mathf.Clamp(Time.time - lastDriverTime, 0f, 0.1f);
        lastDriverTime = Time.time;
        if (!vehicle.IsPullUp && raceStarted < 0f) { raceStarted = Time.time; }
        if (goal.IsChecked || now - started > 600d)
        {
            float raceSeconds = ReadOfficialRaceSeconds();
            float targetSeconds = IsManualTrackDrive ? ManualTargetRaceSeconds : TargetRaceSeconds;
            bool achieved = goal.IsChecked && raceSeconds > 0f && raceSeconds <= targetSeconds;
            string result = $"goal={goal.IsChecked} raceSeconds={raceSeconds:F3} target{targetSeconds:F0}={achieved}"
                + $" wallContacts={contactProbe.WallContacts}";
            Debug.Log("[TrackDrive] " + result);
            //大量の既存ログに埋もれても結果画面と同じ完走時間を後から確認できるよう保存する
            if (log?.BaseStream is FileStream file) { File.WriteAllText(file.Name + ".result.txt", result); }
            Stop();
            return;
        }
        nearest = FindNearest(vehicle.transform.position, Mathf.Max(0, nearest - 35), Mathf.Min(route.Length - 1, nearest + 35));
        UpdateRouteProgress();
        float speed = Mathf.Abs(vehicle.KPH);
        //早めの操舵条件では速度に応じて四分の一秒ぶん先の目標を見る
        float input = CalculateDriverInput(speed, deltaTime);
        //直線は先読みでカーブが無い限り常にフル加速、カーブは手前から先読み減速し抜けたら再加速する(driftChallenge時はカーブ目標速度を高めにして限界付近を検証する)
        float minimumTarget = IsHighSpeedTrackDrive ? MinimumTrackSpeedKph : 25f;
        float target = ComputeTargetSpeed(minimumTarget);
        //大きく横滑りした時は目標速度を下げて切り返しの安定を優先する
        if (Mathf.Abs(vehicle.ESCSlipAngle) > 25f) { target = Mathf.Min(target, 65f); }
        //発進前に踏む離すを試してから全開にし待機音の連動を確認する
        float elapsed = Time.time - profileStarted;
        float waitingThrottle = elapsed < 0.8f ? 0.6f : elapsed < 1.8f ? 0f : 1f;
        vehicle.Accel = vehicle.IsPullUp ? waitingThrottle : CalculateDriverThrottle(speed, target);
        //コースティング(惰性)だけに頼らず、目標超過を検知した瞬間から必ずしっかりブレーキを踏む(base+超過分に応じた追加分)
        vehicle.Brake = vehicle.IsPullUp || speed <= target + 2f ? 0f : Mathf.Clamp01((speed - target - 2f) * 0.08f);
        //急カーブでは固定の最低速度へ再加速させず曲率から求めた目標を優先する
        if (vehicle.Brake > 0f) { vehicle.Accel = 0f; }
        UpdateDriftPedals(speed, target);
        UpdateHairpinDrift(speed, target, ref input);
        //MTだけペダルの踏み替えを連続化し制動と加速の細かな往復を抑える
        if (IsManualTrackDrive) { SmoothManualPedals(deltaTime); }
        //後輪制動の操舵保持が入力制限を迂回しないよう全処理の最後に滑らかにする
        driverSteer = vehicle.IsPullUp ? 0f : SmoothDriverSteering(driverSteer, input, deltaTime);
        //本番と同じアナログ操舵処理を通し入力逆算との食い違いを防ぐ
        vehicle.SetSteeringInput(driverSteer, false);
        //MTでもプレイヤーと同じ変速要求を出し安全判定を迂回しない
        UpdateDriverGear(speed);
        if (leverTest && !vehicle.IsPullUp) { UpdateLeverTest(); }
        if (!vehicle.IsPullUp && speed < 2f && now - started > 25d)
        {
            if (stuckSince < 0d) { stuckSince = now; }
            if (now - stuckSince > 15d) { Debug.LogError("[Sep11Course] 停滞を検出したため入力停止"); Stop(); return; }
        }
        else { stuckSince = -1d; }
        if (now < nextLog) { return; }
        if (leverTest)
        {
            var clutch = vehicle.GetComponentInChildren<Clutch>();
            Debug.Log($"[LeverCourse] time={Time.time:F2} gear={vehicle.ActiveGear} rpm={vehicle.EngineRPM:F0} kph={speed:F1}"
                + $" pedal={vehicle.Clutch:F2} coupling={clutch.Engagement:F2} torque={clutch.ClutchTorque:F1} manual={vehicle.ManualLeverActive}");
        }
        //ゲーム内カメラや車体へ触れずエディタ視点から接地と滑りを観察する
#if UNITY_EDITOR
        if (followScene && SceneView.lastActiveSceneView != null)
        {
            Vector3 viewDirection = vehicle.transform.forward - vehicle.transform.right * 0.5f - Vector3.up * 0.35f;
            SceneView.lastActiveSceneView.LookAt(vehicle.transform.position + Vector3.up, Quaternion.LookRotation(viewDirection), 3f, false, true);
            SceneView.lastActiveSceneView.Repaint();
        }
#endif
        WheelController2026 wheels = vehicle.WheelComtroller;
        float error = 0f;
        for (int i = 0; i < wheels.WheelCount; i++) { error = Mathf.Max(error, wheels.GetVisualPoseError(i)); }
        //音が鳴る要求とFMODの実出力を別々に記録する
        float soundRequest = 0f;
        float soundParameter = 0f;
        float soundPeak = 0f;
        foreach (TireSound sound in tireSounds)
        {
            soundRequest = Mathf.Max(soundRequest, sound.RequestedVolume);
            soundParameter = Mathf.Max(soundParameter, sound.SentVolumeParameter);
            soundPeak = Mathf.Max(soundPeak, ReadTirePeak(sound));
        }
        //道路中心からの横ずれと壁の衝撃を別々に記録して完走だけで良否を決めない
        int following = Mathf.Clamp(nearest + direction, 0, route.Length - 1);
        Vector3 roadRight = Vector3.Cross(Vector3.up, (route[following] - route[nearest]).normalized);
        float lateral = Vector3.Dot(vehicle.transform.position - route[nearest], roadRight);
        float[] values = { (float)(now - started), nearest, speed, vehicle.EngineRPM, vehicle.ActiveGear, vehicle.Accel,
            vehicle.Brake, driverSteer, vehicle.GroundedWheelCount, vehicle.ESCIntervention, vehicle.ESCSlipAngle,
            (float)vehicle.passDifferential.CurrentMode, wheels.GetWheelRPM(0), wheels.GetWheelRPM(1), wheels.GetWheelRPM(2),
            wheels.GetWheelRPM(3), error, vehicle.passDifferential.TrackHandlingBlend, vehicle.passDifferential.AppliedFrontTorqueRatio,
            soundRequest, soundParameter, soundPeak, elapsed, vehicle.IsPullUp ? 1f : 0f, vehicle.Engine.RevLimiterCut ? 1f : 0f,
            vehicle.Engine.CombustionThrottle, ReadDrivePeak(), (float)vehicle.Transmission.Type,
            analogTest ? 1f : 0f, earlyTurn ? 1f : 0f, lateral, steeringComponent.CurrentCenterAngle,
            Vector3.Dot(vehicleBody.angularVelocity, Vector3.up), contactProbe.WallContacts, contactProbe.WallImpulse,
            wheels.GetContactLoad(0), wheels.GetContactLoad(1), wheels.GetContactLoad(2), wheels.GetContactLoad(3),
            wheels.GetSuspensionCompression01(0), wheels.GetSuspensionCompression01(1),
            wheels.GetSuspensionCompression01(2), wheels.GetSuspensionCompression01(3),
            wheels.GetSidewaysSlip(0), wheels.GetSidewaysSlip(1), wheels.GetSidewaysSlip(2), wheels.GetSidewaysSlip(3),
            //世界水平に対する角度なので道路の勾配と車体の揺れを区別して評価する
            Mathf.Asin(Mathf.Clamp(vehicle.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg,
            Mathf.Asin(Mathf.Clamp(vehicle.transform.right.y, -1f, 1f)) * Mathf.Rad2Deg,
            //ゴール演出で物理を固定した後の圧縮値を走行中の底付きと混同しない
            vehicleBody.isKinematic ? 1f : 0f,
            //滑り幅と収束速度と補助の向きを実走で比較する
            ReadHandlingValue("m_trackSlipAllowanceDegrees"), ReadHandlingValue("m_trackSlipRateDegrees"),
            ReadHandlingValue("m_handlingYawAcceleration"), ReadHandlingValue("m_escBrakeWheelIndex"), driftChallenge ? 1f : 0f,
            previousTrackTuning ? 1f : 0f, target, desiredSlip, raceStarted < 0f ? 0f : Time.time - raceStarted,
            driverPhase, driverCurvature, limitingCornerDistance, driverLineOffset, driverLineError,
            hairpinBrake != null ? hairpinBrake.HandbrakeAmount : 0f, hairpinAttempts,
            wheels.GetPoweredGripScale(2), wheels.GetPoweredGripScale(3), ReadHandlingValue("m_trackHandbrakeIntent") };
        log.WriteLine(string.Join(",", Array.ConvertAll(values, value => value.ToString("F4", CultureInfo.InvariantCulture))));
        log.Flush();
        nextLog = now + 0.1d;
    }

    //本番の補助値を書き換えず検証ログへ読み出す関数
    static float ReadHandlingValue(string name)
    {
        var field = typeof(VehicleController).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return Convert.ToSingle(field.GetValue(vehicle));
    }

    //旧しきい値と減衰なしの補助を再生中だけ再現して改善前後を比較する関数
    static void ApplyPreviousTrackTuning()
    {
#if UNITY_EDITOR
        //新しいペダル別設定を旧Trackの固定許容角へそろえる
        var settings = new SerializedObject(vehicle);
        float oldSlip = settings.FindProperty("m_escSlipAngleThreshold").floatValue
            * settings.FindProperty("m_trackESCSlipThresholdScale").floatValue;
        foreach (string name in new[] { "m_trackPoweredSlipDegrees", "m_trackCoastSlipDegrees", "m_trackBrakingSlipDegrees" })
        {
            settings.FindProperty(name).floatValue = oldSlip;
        }

        //新しい減衰とカウンター緩和と補助の速度制限だけを外して旧式と同じ出力にする
        settings.FindProperty("m_trackRecoveryDamping").floatValue = 0f;
        settings.FindProperty("m_trackCountersteerESCScale").floatValue = 1f;
        settings.FindProperty("m_trackYawAccelerationRate").floatValue = 1000000f;
        settings.FindProperty("m_trackFullRecoverySlipDegrees").floatValue = oldSlip * settings.FindProperty("m_escFullSlipMultiplier").floatValue;
        settings.ApplyModifiedPropertiesWithoutUndo();

        //前輪の戻し時間も旧値を使い車体側だけを変えた比較にならないようにする
        var steeringSettings = new SerializedObject(steeringComponent);
        steeringSettings.FindProperty("m_trackDriftReturnSeconds").floatValue
            = steeringSettings.FindProperty("m_trackReturnResponseSeconds").floatValue;
        steeringSettings.ApplyModifiedPropertiesWithoutUndo();
        Debug.Log("[Sep15Comparison] 今回変更箇所を旧設定相当にして比較。保存データは変更しません。");
    #endif
    }

    //エンジン音イベントの実出力を読みカウントダウン中の無音を検出する関数
    static float ReadDrivePeak()
    {
        if (driveSound == null) { return 0f; }
        var field = typeof(DriveSound).GetField("m_driveEvent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var instance = (FMOD.Studio.EventInstance)field.GetValue(driveSound);
        if (!instance.isValid() || instance.getChannelGroup(out var group) != FMOD.RESULT.OK) { return 0f; }
        if (group.getDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, out var dsp) != FMOD.RESULT.OK) { return 0f; }
        dsp.setMeteringEnabled(false, true);
        if (dsp.getMeteringInfo(IntPtr.Zero, out FMOD.DSP_METERING_INFO output) != FMOD.RESULT.OK) { return 0f; }
        float peak = 0f;
        for (int i = 0; i < output.numchannels; i++) { peak = Mathf.Max(peak, output.peaklevel[i]); }
        return peak;
    }
}
