using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

//最新マスターの指定出力とモード切替をシーン保存なしで確認するクラス
public static class VehicleConfigurationValidation
{
    //保存済みデータを汚さず検査後に起動シーンへ戻す関数
    [MenuItem("Tools/検証/Validate and Open Splash")]
    public static void ValidateFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
        try
        {
            Run();
        }
        finally
        {
            EditorSceneManager.OpenScene("Assets/!Scenes/Splash.unity");
        }
    }

    //保存済み車両と本番の計算を使って設定の不一致を検出する関数
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/!Scenes/Map.unity");
        Car_Engine engine = UnityEngine.Object.FindFirstObjectByType<Car_Engine>(FindObjectsInactive.Include);
        Transmission transmission = UnityEngine.Object.FindFirstObjectByType<Transmission>(FindObjectsInactive.Include);
        Differential differential = UnityEngine.Object.FindFirstObjectByType<Differential>(FindObjectsInactive.Include);
        VehicleController vehicle = UnityEngine.Object.FindFirstObjectByType<VehicleController>(FindObjectsInactive.Include);
        Require(engine != null && transmission != null && differential != null && vehicle != null, "車両参照");
        engine.Initialize();
        CheckCountdownFreeRev(engine);
        CheckG923ModeToggle(differential);
        transmission.ConfigureShiftSafety(vehicle.WheelComtroller.WheelRadius, engine.OverRevRPM);
        float peakPS = 0f;
        float peakTorque = 0f;
        //レブ上限まで全負荷カーブを走査してPSとNmを別々に検証する
        for (float rpm = 600f; rpm <= engine.OverRevRPM; rpm += 1f)
        {
            float torque = engine.EvaluateEngineTorque(rpm);
            float ps = torque * rpm * CarPhysics.m_rpmToRadians / 735.49875f;
            Require(torque >= 0f && torque <= 500.01f && ps <= 500.01f, "出力上限");
            peakPS = Mathf.Max(peakPS, ps);
            peakTorque = Mathf.Max(peakTorque, torque);
        }
        Require(peakPS > 499.9f && peakTorque > 499.9f, "指定ピーク");
        transmission.Type = Transmission.TransmissionType.Manual;
        float[] ratios = { 0f, 2.8f, 2.4f, 2f, 1.6f, 1.2f, 1f };
        FieldInfo gear = typeof(Transmission).GetField("m_currentGear", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int index = 0; index < ratios.Length; index++)
        {
            gear.SetValue(transmission, index);
            Require(Mathf.Abs(transmission.CurrentGearRatio - ratios[index]) < 0.001f, "MT比率" + index);
            Require(Mathf.Abs(transmission.FinalDriveRatio - 4.8f) < 0.001f, "最終減速比");
        }
        //各段の低回転では駆動不足となり十分回すと通常トルクへ戻る
        for (int index = 3; index <= 6; index++)
        {
            float threshold = index == 3 ? 1000f : index == 4 ? 2000f : 3000f;
            gear.SetValue(transmission, index);
            transmission.TransmissionUpdate(threshold, 60f, 1f);
            Require(transmission.LowRPMDriveRatio < 0.001f, "低回転駆動不足" + index);
            transmission.TransmissionUpdate(threshold + 800f, 60f, 1f);
            Require(transmission.LowRPMDriveRatio > 0.999f, "駆動復帰" + index);
        }
        //Trackへ切り替えた補間がNormalへ戻した時も残留しない
        differential.SetDriveMode(Differential.DriveMode.Track);
        for (int i = 0; i < 150; i++) { differential.UpdateDriveDistribution(0.02f); }
        Require(differential.TrackHandlingBlend > 0.999f, "Track補間");
        differential.SetDriveMode(Differential.DriveMode.Normal);
        for (int i = 0; i < 150; i++) { differential.UpdateDriveDistribution(0.02f); }
        Require(differential.TrackHandlingBlend < 0.001f, "Normal復帰");
        Require(Mathf.Abs(vehicle.CountdownMaximumRPM - 6000f) < 0.1f, "待機上限");
        CheckGearProtection(transmission, engine);
        CheckLaunch(vehicle, engine);
        CheckTrackAndTireSound();
        CheckImpactYawDamping();
        Debug.Log($"[Sep11] PASS peakPS={peakPS:F2} peakNm={peakTorque:F2} gears=6 countdown={vehicle.CountdownMaximumRPM:F0}");
    }

    //実際のエンジン積分で燃料カットの幅と踏み戻しの応答を検証する関数
    static void CheckCountdownFreeRev(Car_Engine engine)
    {
        engine.ResetToIdle();
        engine.TemporaryRevLimitRPM = 6000f;
        int cuts = 0;
        bool previousCut = false;
        float cutStart = 0f;
        float maximumCutSeconds = 0f;
        for (int i = 0; i < Mathf.CeilToInt(5f / Time.fixedDeltaTime); i++)
        {
            engine.EngineUpdate(1f, 0f);
            Require(engine.RPM >= 1199f && engine.RPM <= 6000.01f, "空ぶかし回転範囲");
            if (engine.RevLimiterCut && !previousCut) { cuts++; cutStart = i * Time.fixedDeltaTime; }
            if (!engine.RevLimiterCut && previousCut)
            {
                maximumCutSeconds = Mathf.Max(maximumCutSeconds, i * Time.fixedDeltaTime - cutStart);
                Require(engine.RPM >= 5400f && engine.RPM <= 5550f, "待機リミッター復帰回転");
            }
            if (engine.RevLimiterCut) { Require(engine.CombustionThrottle == 0f, "燃料カットと音入力同期"); }
            previousCut = engine.RevLimiterCut;
        }
        Require(cuts >= 5 && maximumCutSeconds < 0.4f, "待機リミッター周期");
        float releasedRPM = engine.RPM;
        for (int i = 0; i < Mathf.CeilToInt(0.5f / Time.fixedDeltaTime); i++) { engine.EngineUpdate(0f, 0f); }
        Require(engine.RPM < releasedRPM - 300f && engine.CombustionThrottle == 0f, "空ぶかしペダル解除");
        Debug.Log($"[Sep11] FREEREV cuts={cuts} maxCutSeconds={maximumCutSeconds:F3} releaseRPM={engine.RPM:F0}");
        engine.ResetToIdle();
    }

    //編集モードの入力バッファを更新する検証専用関数で製品の入力には使用しない
    static void UpdateEditorInput()
    {
        typeof(UnityEngine.InputSystem.InputSystem).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(UnityEngine.InputSystem.LowLevel.InputUpdateType) }, null)
            .Invoke(null, new object[] { UnityEngine.InputSystem.LowLevel.InputUpdateType.Editor });
    }

    //模擬G923の通常ボタン入力で押下切替と長押しを検証する関数
    static void CheckG923ModeToggle(Differential differential)
    {
        UnityEngine.InputSystem.InputSystem.RegisterLayout("{\"name\":\"G923ModeVerification\",\"extend\":\"Gamepad\","
            + "\"controls\":[{\"name\":\"button4\",\"layout\":\"Button\",\"offset\":32,\"bit\":0}]}");
        var device = UnityEngine.InputSystem.InputSystem.AddDevice("G923ModeVerification");
        var button = device.GetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("button4");
        var method = typeof(Differential).GetMethod("ReadG923ModeToggle", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            differential.SetDriveMode(Differential.DriveMode.Normal);
            //初回照会前の編集用フレーム番号に依存しないよう押下監視を準備する
            bool initialPressed = button.wasPressedThisFrame;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(button, (byte)1);
            UpdateEditorInput();
            Debug.Log($"[Sep11] G923 test value={button.ReadValue()} pressed={button.wasPressedThisFrame} enabled={device.enabled}");
            method.Invoke(differential, null);
            Require(differential.CurrentMode == Differential.DriveMode.Track, "G923 NormalからTrack");
            UpdateEditorInput();
            method.Invoke(differential, null);
            Require(differential.CurrentMode == Differential.DriveMode.Track, "G923長押し連打防止");
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(button, (byte)0);
            UpdateEditorInput();
            method.Invoke(differential, null);
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(button, (byte)1);
            UpdateEditorInput();
            method.Invoke(differential, null);
            Require(differential.CurrentMode == Differential.DriveMode.Normal, "G923 TrackからNormal");
            //他担当の有効な割り当てが増えた時は既存操作を優先することを確認する
            using (var reserved = new UnityEngine.InputSystem.InputAction("ExistingG923Button", binding: "<G923ModeVerification>/button4"))
            {
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(button, (byte)0);
                UpdateEditorInput();
                method.Invoke(differential, null);
                reserved.Enable();
                UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(button, (byte)1);
                UpdateEditorInput();
                method.Invoke(differential, null);
                Require(differential.CurrentMode == Differential.DriveMode.Normal, "G923既存割り当て優先");
            }
            Debug.Log("[Sep11] G923 synthetic button4 Normal-Track-Normal PASS");
        }
        finally
        {
            UnityEngine.InputSystem.InputSystem.RemoveDevice(device);
            UnityEngine.InputSystem.InputSystem.RemoveLayout("G923ModeVerification");
            differential.SetDriveMode(Differential.DriveMode.Normal);
        }
    }

    //低速復帰の境界と過回転保護を実際の変速関数で確認する関数
    static void CheckGearProtection(Transmission transmission, Car_Engine engine)
    {
        transmission.Type = Transmission.TransmissionType.Manual;
        transmission.IsPullUp = false;
        foreach (float speed in new[] { 29f, 30f, 31f })
        {
            for (int gear = -1; gear <= 6; gear++)
            {
                SetField(transmission, "m_currentGear", gear);
                SetField(transmission, "m_lastShiftChangeTime", Time.time - 10f);
                transmission.TransmissionUpdate(4000f, speed, 1f);
                int expected = gear >= 3 && speed <= 30f ? 1 : gear;
                Require(transmission.ActiveGear == expected, $"低速復帰 {gear} {speed}");
            }
        }
        //3速から2速へ下げると上限を超える車速を作る
        SetField(transmission, "m_currentGear", 3);
        SetField(transmission, "m_lastShiftChangeTime", Time.time - 10f);
        float unsafeSpeed = engine.OverRevRPM * 1.05f / transmission.GetSynchronizedRPM(2, 1f);
        transmission.TransmissionUpdate(4000f, unsafeSpeed, 0f);
        transmission.ShiftDown();
        Require(transmission.ActiveGear == 3, "逐次過回転拒否");
        transmission.ShiftTo(1);
        Require(transmission.ActiveGear == 3, "直接過回転拒否");
        //低回転でもシフトアップを許可し直後の連打だけを拒否する
        SetField(transmission, "m_currentGear", 1);
        SetField(transmission, "m_lastShiftChangeTime", Time.time - 10f);
        transmission.TransmissionUpdate(1800f, 15f, 0f);
        transmission.ShiftUp();
        Require(transmission.ActiveGear == 2, "低回転アップ許可");
        transmission.ShiftUp();
        Require(transmission.ActiveGear == 2, "連打拒否");
        //同じ車速なら変速後RPMがギア比の比率になることを確認する
        float[] ratios = { 0f, 2.8f, 2.4f, 2f, 1.6f, 1.2f, 1f };
        for (int gear = 1; gear < 6; gear++)
        {
            float speed = 7000f / transmission.GetSynchronizedRPM(gear, 1f);
            float target = transmission.GetSynchronizedRPM(gear + 1, speed);
            Require(Mathf.Abs(target - 7000f * ratios[gear + 1] / ratios[gear]) < 0.1f, "変速比率");
            engine.SetLaunchRPM(7000f);
            int synchronizationSteps = Mathf.CeilToInt(1f / Time.fixedDeltaTime);
            for (int step = 0; step < synchronizationSteps; step++) { engine.SynchronizeToDrivetrain(target * CarPhysics.m_rpmToRadians, 1f); }
            Require(Mathf.Abs(engine.RPM - target) < 2f, "回転同期");
            Debug.Log($"[Sep11] RPM gear={gear}->{gear + 1} start=7000 target={target:F1} sync1s={engine.RPM:F1}");
        }
    }

    //上限から下がったフレームの開始判定と発進回転の引き継ぎを確認する関数
    static void CheckLaunch(VehicleController vehicle, Car_Engine engine)
    {
        SetField(vehicle, "m_isPullUp", true);
        vehicle.Transmission.IsPullUp = true;
        vehicle.Accel = 1f;
        engine.TemporaryRevLimitRPM = vehicle.CountdownMaximumRPM;
        engine.SetLaunchRPM(5900f);
        SetField(engine, "m_revLimiterCut", true);
        Require(vehicle.CountdownReady, "リミッター下降中の開始");
        vehicle.BeginCountdownLaunch();
        Require(vehicle.ActiveGear == 1, "カウント1速");
        //カウント中に1速選択の待ち時間が経過した状態を再現する
        SetField(vehicle.Transmission, "m_lastShiftChangeTime", Time.time - 10f);
        Invoke(vehicle, "ReleaseCountdownLaunch");
        SetField(vehicle, "m_isPullUp", false);
        float minimumRPM = float.PositiveInfinity;
        int launchSteps = Mathf.CeilToInt(1f / Time.fixedDeltaTime);
        for (int step = 0; step < launchSteps; step++)
        {
            Invoke(vehicle, "UpdateLaunchRPM");
            minimumRPM = Mathf.Min(minimumRPM, engine.RPM);
        }
        Require(Mathf.Abs(engine.RPM - 4000f) < 1f && minimumRPM >= 3999f, "発進4000回転");
        Debug.Log($"[Sep11] LAUNCH firstGear=1 retained={engine.RPM:F1} minimum={minimumRPM:F1}");
    }

    //音量変換の単調性とNormalの即応性とTrackの短い応答遅れを本番コードで確認する関数
    static void CheckTrackAndTireSound()
    {
        TireSound sound = UnityEngine.Object.FindFirstObjectByType<TireSound>(FindObjectsInactive.Include);
        Require(sound != null, "タイヤ音源参照");
        MethodInfo convert = typeof(TireSound).GetMethod("CalculateVolumeParameter", BindingFlags.Instance | BindingFlags.NonPublic);
        float silent = (float)convert.Invoke(sound, new object[] { 0f });
        float quarter = (float)convert.Invoke(sound, new object[] { 0.25f });
        float full = (float)convert.Invoke(sound, new object[] { 1f });
        Require(silent == 0f && quarter > 0.83f && quarter < 0.85f && full == 1f, "音量デシベル変換");
        Steering steering = UnityEngine.Object.FindFirstObjectByType<Steering>(FindObjectsInactive.Include);
        Require(steering != null, "操舵参照");
        steering.VehicleSpeedKph = 100f;
        steering.SmoothInput = false;
        steering.TrackHandlingBlend = 0f;
        steering.InputAngle = 0.5f;
        steering.UpdateSteering(0.005f);
        float normal = steering.CurrentCenterAngle;
        steering.InputAngle = 0f;
        steering.UpdateSteering(0.005f);
        steering.TrackHandlingBlend = 1f;
        steering.InputAngle = 0.5f;
        steering.UpdateSteering(0.005f);
        float trackFirst = steering.CurrentCenterAngle;
        Require(trackFirst > 0f && trackFirst < normal * 0.1f, "Track切り込み応答");
        for (int i = 0; i < 200; i++) { steering.UpdateSteering(0.005f); }
        Require(Mathf.Abs(steering.CurrentCenterAngle - normal) < 0.01f, "Track定常舵角維持");
        steering.TrackHandlingBlend = 0f;
        steering.InputAngle = -0.5f;
        steering.UpdateSteering(0.005f);
        Require(Mathf.Abs(steering.CurrentCenterAngle + normal) < 0.01f, "Normal復帰");
        Debug.Log($"[Sep11] AUDIO quarterParameter={quarter:F4} TRACK first={trackFirst:F3} normal={normal:F3} restore=OK");
    }

    //壁接触後の回転減衰を更新間隔と左右の回転を変えて本番の式で検証する関数
    static void CheckImpactYawDamping()
    {
        MethodInfo method = typeof(VehicleController).GetMethod("CalculateWallSpinDecay", BindingFlags.Static | BindingFlags.NonPublic);
        Require(method != null, "壁擦り回転補助の参照");
        //左右の回転と零から最大までの補助量を均等に検証する
        for (int yawIndex = -20; yawIndex <= 20; yawIndex++)
        {
            for (int stepIndex = 1; stepIndex <= 20; stepIndex++)
            {
                for (int amountIndex = 0; amountIndex <= 10; amountIndex++)
                {
                    float yaw = yawIndex * 0.1f;
                    float step = stepIndex * 0.001f;
                    float amount = amountIndex * 0.1f;
                    float result = (float)method.Invoke(null, new object[] { yaw, amount, step, 0.12f });
                    Require(Mathf.Abs(result) <= Mathf.Abs(yaw) + 0.0001f, "ヨー回転量の増加なし");
                    Require(result * yaw >= -0.0001f, "逆向き回転の追加なし");
                    if (amountIndex == 0) { Require(Mathf.Abs(result - yaw) < 0.0001f, "補助零で無変更"); }
                    float half = (float)method.Invoke(null, new object[] { yaw, amount, step * 0.5f, 0.12f });
                    float twice = (float)method.Invoke(null, new object[] { half, amount, step * 0.5f, 0.12f });
                    Require(Mathf.Abs(result - twice) < 0.0001f, "更新間隔を半分にしても同じ減衰");
                }
            }
        }
        Debug.Log("[Sep11] WALL_SPIN_DECAY PASS cases=9020");
    }

    //本番の公開入力で作れない境界状態だけを検証用に設定する関数
    static void SetField(object instance, string name, object value)
    {
        instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }

    //本番の計算手順をコピーせず同じ処理を検証から呼び出す関数
    static void Invoke(object instance, string name)
    {
        instance.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
    }

    //不一致を成功ログに混ぜず検証失敗として通知する関数
    static void Require(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException(label); }
    }
}
