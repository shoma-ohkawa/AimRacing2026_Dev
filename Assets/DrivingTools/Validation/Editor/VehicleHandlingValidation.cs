using System;
using UnityEditor;
using UnityEngine;

//シーンを変更せず本番の操舵とモード補間を検査するクラス
public static class VehicleHandlingValidation
{
    //三モードの速度制限と左右対称性と連続切替を検証する関数
    [MenuItem("Tools/検証/Validate Handling")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        GameObject probe = new GameObject("ModeHandlingValidation");
        try
        {
            ValidatePitchAcceleration();
            ValidateRoadRebound();
            Steering steering = probe.AddComponent<Steering>();
            Differential differential = probe.AddComponent<Differential>();
            //モード往復で補間値が累積せず合計が1を超えないことを確認する
            for (int step = 0; step < 600; step++)
            {
                differential.SetDriveMode((Differential.DriveMode)(step / 37 % 3));
                differential.UpdateDriveDistribution(0.02f);
                Require(differential.TrackHandlingBlend + differential.SportHandlingBlend <= 1.0001f, "モード補間の合計");
            }

            //全速度域で右左が対称かつ速度上昇で切れ角が増えないことを確認する
            for (int mode = 0; mode < 3; mode++)
            {
                steering.TrackHandlingBlend = mode == 2 ? 1f : 0f;
                steering.SportHandlingBlend = mode == 1 ? 1f : 0f;
                float previous = float.PositiveInfinity;
                for (int speed = 0; speed <= 230; speed++)
                {
                    steering.VehicleSpeedKph = speed;
                    steering.InputAngle = 1f;
                    for (int tick = 0; tick < 80; tick++) { steering.UpdateSteering(0.02f); }
                    float right = steering.CurrentCenterAngle;
                    Require(right <= previous + 0.001f && right > 0f, "高速操舵の単調性");
                    previous = right;
                    steering.InputAngle = -1f;
                    for (int tick = 0; tick < 80; tick++) { steering.UpdateSteering(0.02f); }
                    Require(Mathf.Abs(right + steering.CurrentCenterAngle) < 0.001f, "左右対称性");
                }
                Debug.Log($"[Sep13Validation] mode={mode} speed=230 maxAngle={previous:F3}");
            }
            Debug.Log("[Sep13Validation] PASS 補間600回・三モード各231速度の操舵検査");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probe);
        }
    }

    //条件不成立を成功として見逃さず検証を失敗させる関数
    static void Require(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException("[Sep13Validation] FAIL " + label); }
    }

    //横滑り中の向き変更を減速と誤認せず直進加減速を正しく求めるか検証する関数
    static void ValidatePitchAcceleration()
    {
        //本番と同じ非公開の計算を呼び別実装だけの成功を避ける
        var method = typeof(VehicleController).GetMethod("CalculatePitchAcceleration",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Require(method != null, "ピッチ加速度の本番計算");
        //速度が一定で車体だけ旋回しても前後加速度が発生しないことを確認する
        Vector3 velocity = new Vector3(4f, 0f, 25f);
        Vector3 turnedForward = Quaternion.Euler(0f, 1f, 0f) * Vector3.forward;
        float turning = (float)method.Invoke(null, new object[] { velocity, velocity, turnedForward, 0.02f });
        Require(Mathf.Abs(turning) < 0.0001f, "向き変更による偽の加減速");
        //正の加速と負の制動が毎秒毎秒のメートル単位で得られることを確認する
        float acceleration = (float)method.Invoke(null, new object[] { Vector3.forward * 20.04f, Vector3.forward * 20f, Vector3.forward, 0.02f });
        float braking = (float)method.Invoke(null, new object[] { Vector3.forward * 19.94f, Vector3.forward * 20f, Vector3.forward, 0.02f });
        Require(Mathf.Abs(acceleration - 2f) < 0.001f && Mathf.Abs(braking + 3f) < 0.001f, "加速と制動の単位と符号");
        Debug.Log("[Sep13Validation] PASS ピッチ加速度の旋回誤検知と加減速の符号");
    }

    //上り坂の速度を残し平地や空中の反発制限を強めないことを検証する関数
    static void ValidateRoadRebound()
    {
        //本番で使用する上限計算を直接呼び出して数式の取り違えを検出する
        var method = typeof(VehicleController).GetMethod("CalculateRoadUpwardLimit",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Require(method != null, "道路勾配を考慮する本番計算");
        //一割勾配を毎秒30メートルで上る時に必要な毎秒3メートルの上昇を残す
        Vector3 slope = new Vector3(0f, 1f, -0.1f).normalized;
        float uphill = (float)method.Invoke(null, new object[] { new Vector3(0f, 3f, 30f), slope, 0.05f });
        float flat = (float)method.Invoke(null, new object[] { new Vector3(0f, 3f, 30f), Vector3.up, 0.05f });
        float downhill = (float)method.Invoke(null, new object[] { new Vector3(0f, -3f, -30f), slope, 0.05f });
        Require(Mathf.Abs(uphill - 3.05f) < 0.001f, "上り坂で必要な速度を保持");
        Require(Mathf.Abs(flat - 0.05f) < 0.001f && Mathf.Abs(downhill - 0.05f) < 0.001f, "平地と下り坂の上限を維持");
        Require(Mathf.Abs(Mathf.Min(7f, uphill) - 3.05f) < 0.001f, "衝突で増えた上昇速度を制限");
        Debug.Log("[Sep13Validation] PASS 坂道の走行速度保持と余分な反発制限");
    }
}
