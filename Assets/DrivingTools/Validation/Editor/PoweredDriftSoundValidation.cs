using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

//加速中の横グリップと上限音の本番計算を再検査するクラス
public static class PoweredDriftSoundValidation
{
    //Unityを閉じずにTrackのグリップ継続と安全条件を検証する関数
    [MenuItem("Tools/検証/挙動/Powered Drift")]
    public static void RunInEditor() { Validate(false); }

    //摩擦の境界と更新間隔とレブ音の解除条件をまとめて検査する関数
    public static void Run() { Validate(true); }

    //対話中はEditorを終了せずバッチ実行だけ終了コードを返す関数
    static void Validate(bool exitAfter)
    {
        try
        {
            TrackDriftValidation.Run();
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var usage = typeof(WheelController2026).GetMethod("CalculatePoweredGripUsage", flags);
            //トルク切れと残留空転を区別する本番条件を直接検査する
            var demand = typeof(WheelController2026).GetMethod("HasPoweredGripDemand", flags);
            bool Demand(float torque, float spin, float brake) => (bool)demand.Invoke(null, new object[] { torque, spin, brake });
            Require(Demand(0f, 0.14f, 0f), "変速中の残留空転を維持");
            Require(!Demand(0f, 0f, 0f), "空転終了後は通常グリップへ復帰");
            Require(!Demand(0f, 0.14f, 100f), "制動中の残留空転で緩和を開始しない");
            Require(!Demand(0f, -0.14f, 0f), "制動スリップで緩和を開始しない");
            var grip = typeof(WheelController2026).GetMethod("CalculatePoweredGripScale", flags);
            var smooth = typeof(WheelController2026).GetMethod("SmoothPoweredGrip", flags);
            var rev = typeof(DriveSound).GetMethod("ShouldSoloCountdownRev", flags);
            float Usage(float torque, float spin) => (float)usage.Invoke(null, new object[] { torque, spin, 0.14f });
            float Grip(float load, float blend) => (float)grip.Invoke(null, new object[] { load, blend, 0.82f });
            float Step(float value, float target, bool recovery, float dt) =>
                (float)smooth.Invoke(null, new object[] { value, target, 0.18f, 0.32f, recovery, dt });
            bool Rev(bool waiting, float throttle, bool held) => (bool)rev.Invoke(null, new object[] { waiting, throttle, held });

            //空転が残る場合はトルク制限だけで横グリップが復帰しないことを確認する
            Require(Usage(0.2f, 0.14f) == 1f, "空転中のグリップ復帰抑制");
            Require(Mathf.Abs(Usage(0.2f, 0f) - 0.2f) < 0.001f, "通常駆動の維持");
            Require(Usage(0f, -0.2f) == 0f, "制動滑りを加速と混同しない");
            for (int i = 0; i <= 100; i++)
            {
                float scale = Grip(i / 100f, 1f);
                Require(scale >= 0.82f && scale <= 1f, "横グリップの安全範囲");
                Require(Grip(i / 100f, 0f) == 1f, "直進と他モードの維持");
            }

            //グリップ回復を滑らかにしても危険角度では復帰を遅らせないことを確認する
            Require(Step(0.82f, 1f, false, 0.02f) < Step(0.82f, 1f, true, 0.02f), "危険時の回復優先");
            float split = Step(Step(0.82f, 1f, false, 0.01f), 1f, false, 0.01f);
            Require(Mathf.Abs(split - Step(0.82f, 1f, false, 0.02f)) < 0.00001f, "更新間隔の非依存");
            Require(Rev(true, 1f, true) && Rev(true, 0.99f, true), "カウント中の全開上限でレブ音を保持");
            Require(!Rev(false, 1f, true), "走行中は上限でもエンジン音を強制消音しない");
            Require(!Rev(true, 0f, true) && !Rev(true, 1f, false), "アクセルオフと上限解除で通常音へ戻る");
            Debug.Log("[PoweredDriftSoundValidation] PASS grip bounds / spin continuity / recovery / timestep / limiter sound policy");
            if (exitAfter) { EditorApplication.Exit(0); }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            if (exitAfter) { EditorApplication.Exit(1); }
        }
    }

    //期待を満たさない条件を成功扱いにせず検査を中断する関数
    static void Require(bool condition, string message)
    {
        if (!condition) { throw new InvalidOperationException(message); }
    }
}
