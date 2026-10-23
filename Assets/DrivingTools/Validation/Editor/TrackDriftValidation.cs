using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

//Trackの復帰補助を本番の計算式で検査するクラス
public static class TrackDriftValidation
{
    //滑りの増大と収束とカウンターの保護条件を左右両方向で検証する関数
    [MenuItem("Tools/検証/Validate Track Drift")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        //既存三モードの左右対称性と速度別舵角も回帰検査する
        VehicleHandlingValidation.Run();
        //別の検算式だけで成功させず本番の非公開関数を直接呼び出す
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        //希望速度より曲率による通過限界を優先し左右で同じ速度になることを検査する
        var cornerSpeed = typeof(TrackDriveSession).GetMethod("CalculateCornerSpeed", flags);
        Require(cornerSpeed != null, "急カーブの速度計画");
        float Corner(float curvature) => (float)cornerSpeed.Invoke(null, new object[] { curvature, 22f, 105f });
        Require(Mathf.Abs(Corner(0.028f) * 3.6f - 105f) < 0.001f, "旋回余裕があるカーブは105km/hを目標にする");
        Require(Corner(0.04f) * Corner(0.04f) * 0.04f <= 24.001f, "急カーブでは速度下限より接触回避を優先する");
        Require(Corner(0.01f) * 3.6f > 110f, "通常カーブでは過剰に減速しない");
        Require(Corner(0.04f) == Corner(-0.04f), "速度計画の左右対称性");
        //進入処理で上書きされた入力も左右対称で一定速度以下に丸めることを検査する
        var steering = typeof(TrackDriveSession).GetMethod("SmoothDriverSteering", flags);
        Require(steering != null, "最終操舵の平滑化");
        float Steer(float current, float requested, float dt) => (float)steering.Invoke(null, new object[] { current, requested, dt });
        Require(Steer(0f, 1f, 0f) == 0f, "時間停止時は操舵維持");
        Require(Steer(0f, 1f, 0.02f) <= 0.03001f, "進入上書きも操舵速度上限を維持");
        Require(Mathf.Abs(Steer(0.4f, -1f, 0.02f) + Steer(-0.4f, 1f, 0.02f)) < 0.00001f, "操舵の左右対称性");
        Require(Steer(0.4f, 1f, 0.02f) > 0.4f && Steer(0.4f, -1f, 0.02f) < 0.4f, "操作方向を反転しない");
        //滑りが目標内なら道路の旋回率を維持し誤差だけを補正することを検査する
        var driftYaw = typeof(TrackDriveSession).GetMethod("CalculateDriftYawTarget", flags);
        Require(driftYaw != null, "滑り維持の旋回計算");
        float Yaw(float curve, float slip, float target)
            => (float)driftYaw.Invoke(null, new object[] { 25f, curve, slip, target });
        Require(Mathf.Abs(Yaw(0.03f, -8f, -8f) - 0.75f) < 0.00001f, "目標滑り時の旋回維持");
        Require(Yaw(0.03f, -3f, -8f) > 0.75f && Yaw(0.03f, -12f, -8f) < 0.75f, "不足と過大の補正方向");
        Require(Mathf.Abs(Yaw(0.03f, -3f, -8f) + Yaw(-0.03f, 3f, 8f)) < 0.00001f, "滑り維持の左右対称性");
        //後輪制動の横グリップが入力だけで減らず実際の縦滑りと連動することを検査する
        var lockGrip = typeof(WheelController2026).GetMethod("CalculateHandbrakeGrip", flags);
        Require(lockGrip != null, "後輪ロック配分の本番関数");
        float LockGrip(float amount, float slip, float blend)
            => (float)lockGrip.Invoke(null, new object[] { amount, slip, 0.2f, blend, 0.65f });
        Require(LockGrip(0f, 1f, 1f) == 1f && LockGrip(1f, 0f, 1f) == 1f, "未操作と縦滑りなしではグリップ維持");
        Require(LockGrip(1f, 1f, 0f) == 1f, "Track以外は横グリップ維持");
        Require(Mathf.Abs(LockGrip(1f, 1f, 1f) - 0.65f) < 0.00001f, "後輪ロックでも横力下限を保持");
        Require(LockGrip(1f, -0.1f, 1f) == LockGrip(1f, 0.1f, 1f), "縦滑り符号への非依存");
        //後輪制動中でもスピン予測と逆旋回では通常ESCへ戻ることを検査する
        var entryESC = typeof(VehicleController).GetMethod("CalculateHandbrakeESCWeight", flags);
        Require(entryESC != null, "進入ESCの本番関数");
        float Entry(float intent, float slip, float rate, float yaw)
            => (float)entryESC.Invoke(null, new object[] { intent, slip, rate, yaw, 9f, 16f, 0.2f });
        Require(Entry(1f, -5f, 0f, 0.5f) == 1f, "制御内の意図的進入を許可");
        Require(Entry(0f, -5f, 0f, 0.5f) == 0f, "通常旋回のESCを維持");
        Require(Entry(1f, -16f, 0f, 0.5f) == 0f && Entry(1f, -12f, -20f, 0.5f) == 0f, "限界と急拡大は保護");
        Require(Entry(1f, -5f, 0f, -0.5f) == 0f, "異常な逆旋回を緩和しない");
        Require(Entry(1f, -5f, 0f, 0.5f) == Entry(1f, 5f, 0f, -0.5f), "進入ESCの左右対称性");
        Debug.Log("[TrackDriftValidation] PASS 後輪ロック配分・横力下限・進入ESC・限界保護");
        //後輪制動の進入条件が急カーブだけを選び高速や未接地を除外することを検査する
        var hairpin = typeof(TrackDriveSession).GetMethod("CanInitiateHairpin", flags);
        Require(hairpin != null, "急カーブ進入の本番関数");
        bool Hairpin(float speed, float target, float curve, float error, float slip, float brake, int grounded)
            => (bool)hairpin.Invoke(null, new object[] { speed, target, curve, error, slip, brake, grounded });
        Require(Hairpin(112f, 110f, 0.03f, 0.2f, 2f, 0.1f, 4), "目標付近の急カーブを選択");
        Require(Hairpin(112f, 110f, -0.03f, -0.2f, -2f, 0.1f, 4), "左右の急カーブを選択");
        Require(!Hairpin(130f, 110f, 0.03f, 0f, 0f, 0f, 4), "高速の後輪制動を禁止");
        Require(Hairpin(105f, 105f, 0.03f, 0f, 0f, 0f, 4), "105km/hの急カーブで進入できる");
        Require(!Hairpin(104f, 105f, 0.03f, 0f, 0f, 0f, 4), "進入速度の下限未満で追加減速を開始しない");
        Require(!Hairpin(112f, 120f, 0.03f, 0f, 0f, 0f, 4), "高速通過カーブは通常走行");
        Require(!Hairpin(112f, 110f, 0.01f, 0f, 0f, 0f, 4), "直線で後輪制動しない");
        Require(!Hairpin(112f, 110f, 0.03f, 1f, 0f, 0f, 4), "路肩で開始しない");
        Require(!Hairpin(112f, 110f, 0.03f, 0f, 6f, 0f, 4), "既に滑っている時は追加しない");
        Require(!Hairpin(112f, 110f, 0.03f, 0f, 0f, 0.8f, 4), "強い制動中は追加しない");
        Require(!Hairpin(112f, 110f, 0.03f, 0f, 0f, 0f, 3), "未接地では開始しない");
        Debug.Log("[TrackDriftValidation] PASS 急カーブ進入・左右・高速・路肩・接地の保護");
        //走行ラインが外側進入と内側通過を連続的に結び上限を超えないことを検査する
        var apex = typeof(TrackDriveSession).GetMethod("CalculateApexOffset", flags);
        Require(apex != null, "走行ラインの本番関数");
        float Apex(float progress) => (float)apex.Invoke(null, new object[] { progress });
        Require(Apex(0f) < 0f && Apex(0.6f) > 0f && Apex(1f) < 0f, "外側進入と遅い内側通過と脱出");
        for (int i = -10; i <= 110; i++)
        {
            Require(Mathf.Abs(Apex(i / 100f)) <= 1.151f, "走行ラインの横移動上限");
            //横移動幅2.3mと脱出区間40%の滑らかな補間から1%刻みの差が9cm未満であることを確認する
            Require(Mathf.Abs(Apex(i / 100f) - Apex((i + 1) / 100f)) < 0.09f, "走行ラインの連続性");
        }
        Debug.Log("[TrackDriftValidation] PASS 走行ラインの通過順序・上限・連続性");
        //自動走行の変速で上げ下げを往復せず出口の再加速を遅らせないことを検査する
        var downshift = typeof(TrackDriveSession).GetMethod("ShouldDriverDownshift", flags);
        var throttle = typeof(TrackDriveSession).GetMethod("CalculateDriverThrottle", flags);
        Require(downshift != null && throttle != null, "自動走行の本番関数");
        bool Down(float current, float lower) => (bool)downshift.Invoke(null, new object[] { current, lower, 8200f });
        float Throttle(float speed, float target) => (float)throttle.Invoke(null, new object[] { speed, target });
        Require(!Down(5200f, 7400f), "アップ後の低い回転だけでは下げ直さない");
        Require(!Down(4700f, 7500f), "ダウン後の即アップを防ぐ");
        Require(Down(4500f, 6000f), "減速後は再加速に適した段へ下げる");
        Require(!Down(4700f, 8500f), "過回転するダウン要求を出さない");
        Require(Throttle(100f, 110f) == 1f && Throttle(100f, 100f) > 0.7f, "出口の駆動復帰");
        Require(Throttle(120f, 100f) == 0f, "目標超過時の加速抑制");
        Debug.Log("[TrackDriftValidation] PASS 自動走行の往復変速防止・安全なダウン・出口加速");
        //低速境界の連続性と後退時に追加グリップ緩和がないことを検査する
        var speedBlend = typeof(WheelController2026).GetMethod("CalculatePoweredGripSpeedBlend", flags);
        Require(speedBlend != null, "速度補間の本番関数");
        float SpeedBlend(float speed) => (float)speedBlend.Invoke(null, new object[] { speed, 25f, 10f });
        Require(SpeedBlend(-30f) == 0f && SpeedBlend(0f) == 0f && SpeedBlend(15f) == 0f, "後退と停車では追加緩和なし");
        Require(Mathf.Abs(SpeedBlend(20f) - 0.5f) < 0.00001f, "低速ヘアピンでは段階的な緩和");
        Require(SpeedBlend(25f) == 1f && SpeedBlend(120f) == 1f, "高速域の既存配分を維持");
        Require(Mathf.Abs(SpeedBlend(25.001f) - SpeedBlend(24.999f)) < 0.00001f, "速度境界の連続性");
        var grip = typeof(WheelController2026).GetMethod("CalculatePoweredGripScale", flags);
        Require(grip != null, "横グリップの本番関数");
        float Grip(float usage, float blend) => (float)grip.Invoke(null, new object[] { usage, blend, 0.82f });
        Require(Grip(1f, 0f) == 1f && Grip(0f, 1f) == 1f, "直進または駆動負荷なしでは緩和しない");
        Require(Grip(1f, SpeedBlend(20f)) > Grip(1f, SpeedBlend(30f)), "低速では高速よりグリップを残す");
        Debug.Log("[TrackDriftValidation] PASS 低速補間・境界連続・停車後退・直進保護");
        //空転のしきい値をまたがなくても駆動制限が回復することを確認する
        var traction = typeof(WheelController2026).GetMethod("UpdateTractionTorqueRatio", flags);
        Require(traction != null, "駆動制限の本番関数");
        float Traction(float current, float target, float dt) => (float)traction.Invoke(null, new object[] { current, target, 1.2f, dt });
        Require(Mathf.Abs(Traction(0.35f, 0.8f, 0.02f) - 0.374f) < 0.00001f, "空転収束中の段階的な駆動回復");
        Require(Traction(0.8f, 0.35f, 0.02f) == 0.35f, "空転悪化時の即時制限");
        Require(Traction(0.79f, 0.8f, 0.02f) == 0.8f, "駆動回復の要求上限");
        Require(Traction(0.35f, 0.8f, 0f) == 0.35f, "停止時間では駆動回復しない");
        Require(Mathf.Abs(Traction(Traction(0.35f, 0.8f, 0.01f), 0.8f, 0.01f) - Traction(0.35f, 0.8f, 0.02f)) < 0.00001f,
            "駆動回復の時間刻みへの非依存");
        Debug.Log("[TrackDriftValidation] PASS 駆動回復・即時制限・要求上限・時間刻み");
        var recovery = typeof(VehicleController).GetMethod("CalculateTrackRecovery", flags);
        var counter = typeof(VehicleController).GetMethod("CalculateTrackCountersteerWeight", flags);
        var limiter = typeof(VehicleController).GetMethod("LimitTrackRecovery", flags);
        Require(recovery != null && counter != null && limiter != null, "本番関数の存在");
        float Limit(float prior, float demand) => (float)limiter.Invoke(null, new object[] { prior, demand, 2.5f, 0.02f });
        float Recover(float slip, float rate, float allowance) => (float)recovery.Invoke(null, new object[] { slip, rate, allowance, 0.65f, 0.8f });
        float Counter(float slip, float yaw, float target) => (float)counter.Invoke(null, new object[] { slip, yaw, target, 11f, 16f });

        //右旋回と左旋回で符号だけが反転し介入量が一致することを確認する
        for (int slip = 0; slip <= 25; slip++)
        {
            for (int rate = -40; rate <= 40; rate++)
            {
                Require(Mathf.Abs(Recover(slip, rate, 11f) + Recover(-slip, -rate, 11f)) < 0.00001f, "復帰補助の左右対称性");
            }
        }

        //許容幅の内側では滑りを強制せず回復中は過剰な押し戻しを止める
        Require(Recover(8f, 10f, 11f) == 0f, "制御可能な滑りを維持");
        Require(Recover(12f, -10f, 11f) == 0f, "収束中の押し戻しを停止");
        Require(Recover(12f, 10f, 11f) > Recover(12f, 0f, 11f), "発散時の回復補助");
        Require(Recover(10f, 0f, 7f) > Recover(10f, 0f, 11f), "制動時は姿勢を整える");

        //カウンターだけ比較誤差を緩和し同方向操舵と限界時のESCを維持する
        Require(Counter(-10f, 0.4f, -0.3f) > 0f, "制御中のカウンターを許可");
        Require(Counter(-10f, 0.4f, 0.3f) == 0f, "同方向操舵のESCを維持");
        Require(Counter(-16f, 0.4f, -0.3f) == 0f, "限界角でESCを全量復帰");
        Require(Counter(-10f, -0.4f, 0.3f) == 0f, "異常な逆旋回のESCを維持");
        //回復後や左右の連続切り返しで前の補助が逆効果にならないことを検査する
        Require(Limit(0.8f, 0f) == 0f, "回復後の残留補助を解除");
        Require(Limit(0.8f, -0.4f) < 0f, "右から左への切り返しで古い補助を解除");
        Require(Limit(-0.8f, 0.4f) > 0f, "左から右への切り返しで古い補助を解除");
        Require(Mathf.Abs(Limit(0.8f, 0.1f) - 0.1f) < 0.00001f, "収束中の要求以上に補助しない");
        Require(Mathf.Abs(Limit(0f, 0.8f) - 0.05f) < 0.00001f, "復帰補助の立ち上がり上限");
        Debug.Log("[Sep15Validation] PASS 復帰計算2106条件・滑り維持・収束・制動・カウンター・限界保護");
        Debug.Log("[Sep15Validation] PASS 残留補助解除・左右切り返し・収束上限・立ち上がり上限");
        //滑り維持中だけESCを緩和し限界角と急拡大では保護が戻ることを検査する
        var powered = typeof(VehicleController).GetMethod("CalculateTrackPoweredESCScale", flags);
        Require(powered != null, "アクセル時ESCの本番関数");
        float Scale(float slip, float rate) => (float)powered.Invoke(null, new object[] { slip, rate, 11f, 16f, 0.2f, 0.75f });
        Require(Mathf.Abs(Scale(8f, 0f) - 0.75f) < 0.00001f, "安定した滑りでESCを限定緩和");
        Require(Scale(16f, 0f) == 1f && Scale(12f, 20f) == 1f, "限界と急拡大の保護");
        Require(Scale(12f, -20f) < Scale(12f, 20f), "収束と拡大を区別");
        Require(Scale(-12f, -20f) == Scale(12f, 20f), "アクセル時ESCの左右対称性");
        Debug.Log("[Sep15Validation] PASS アクセル中ESC・限界保護・拡大予測・左右対称性");
        //MTの全開で惰性制動が消え更新間隔を分けても同じ解除量になることを検査する
        var coast = typeof(VehicleController).GetMethod("ReleaseManualCoastTorque", flags);
        Require(coast != null, "MT惰性制動の本番関数");
        float Coast(float previous, float throttle, float dt) => (float)coast.Invoke(null, new object[] { previous, 0f, throttle, 0.08f, dt });
        Require(Coast(100f, 1f, 0.02f) == 0f, "全開で追加惰性制動を解除");
        Require(Coast(100f, 0.5f, 0.02f) > 0f && Coast(100f, 0.5f, 0.02f) < 100f, "踏み直しは滑らかに解除");
        Require(Mathf.Abs(Coast(Coast(100f, 0.5f, 0.01f), 0.5f, 0.01f) - Coast(100f, 0.5f, 0.02f)) < 0.001f, "更新間隔への非依存");
        Debug.Log("[Sep15Validation] PASS MT踏み直し・全開解除・更新間隔");
    }

    //期待と違う計算結果を検証失敗として知らせる関数
    static void Require(bool condition, string description)
    {
        if (!condition) { throw new InvalidOperationException("[Sep15Validation] FAIL " + description); }
    }
}
