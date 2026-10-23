using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

//シーンを保存せず走行上限と待機上限とAT変速条件を検査するクラス
public static class EngineRevLimitValidation
{
    //無負荷エンジンと仮の車速で回転制限の境界を確認する関数
    [MenuItem("Tools/検証/Validate RPM")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        GameObject probe = new GameObject("RPMValidation");
        try
        {
            Car_Engine engine = probe.AddComponent<Car_Engine>();
            engine.Initialize();
            Require(engine.OverRevRPM == 8200f && engine.RedzoneRPM == 7800f, "走行上限の初期値");
            engine.TemporaryRevLimitRPM = 6000f;
            float countdownPeak = Simulate(engine);
            Require(countdownPeak > 5990f && countdownPeak <= 6001f, "待機6000回転");
            engine.TemporaryRevLimitRPM = 0f;
            float drivingPeak = Simulate(engine);
            Require(drivingPeak > 8190f && drivingPeak <= 8201f, "解除後8200回転");
            Transmission transmission = probe.AddComponent<Transmission>();
            transmission.Type = Transmission.TransmissionType.Automatic;
            transmission.ConfigureShiftSafety(0.34f, engine.OverRevRPM);
            transmission.PrepareForwardStart();
            typeof(Transmission).GetField("m_lastShiftChangeTime", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(transmission, -100f);
            transmission.TransmissionUpdate(6200f, 70f, 1f);
            Require(transmission.ActiveGear == 1, "全開6200回転の早上がり防止");
            transmission.TransmissionUpdate(8000f, 70f, 1f);
            Require(transmission.ActiveGear == 2, "全開8000回転の変速");
            Debug.Log($"[Sep13RPM] PASS countdownPeak={countdownPeak:F1} drivingPeak={drivingPeak:F1} AT=6200維持/8000変速");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    //一定アクセルで積分を繰り返し上限と燃料カットの音用入力を確認する関数
    static float Simulate(Car_Engine engine)
    {
        float peak = 0f;
        bool observedCut = false;
        for (int step = 0; step < 1500; step++)
        {
            engine.EngineUpdate(1f, 0f);
            peak = Mathf.Max(peak, engine.RPM);
            if (!engine.RevLimiterCut) { continue; }
            observedCut = true;
            Require(engine.CombustionThrottle == 0f, "燃料カットと音入力の同期");
        }
        Require(observedCut, "燃料カット発生");
        return peak;
    }

    //失敗した検査を成功として扱わず原因をログへ出す関数
    static void Require(bool passed, string condition)
    {
        if (!passed) { throw new InvalidOperationException("[Sep13RPM] FAIL " + condition); }
    }
}
