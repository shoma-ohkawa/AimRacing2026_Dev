using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

//急カーブで短い後輪制動から滑り維持と出口加速へつなぐ自動走行クラス
public static partial class TrackDriveSession
{
    //再生時の再読み込みをまたいで急カーブ試験の選択だけを保持するキー
    const string HairpinTestKey = "TrackHairpinDriftTest";
    //速度計画がこの値以下のカーブだけを進入試験の対象にする
    const float HairpinTargetKph = 115f;
    //姿勢制御と組み合わせて試す急カーブの計画横加速度で実車の値ではない
    const float HairpinPlannedAcceleration = 22f;
    //高速や発進直後に後輪制動を入れない許容車速
    const float HairpinMinimumKph = 105f;
    const float HairpinMaximumKph = 125f;
    //100km/hを割り込む前に後輪制動を解放する速度
    const float HairpinReleaseKph = 102f;
    //同じカーブで何度も後輪をロックしない最短間隔
    const float HairpinCooldownSeconds = 3f;
    //姿勢を作った後は素早く駆動へ戻す押下時間
    const float HairpinPulseSeconds = 0.12f;
    //十分な向き変えが生じたら押下を途中で解除する滑り角
    const float HairpinReleaseSlipDegrees = 7f;
    //直線と急カーブを区別する曲率で単位は一メートル当たり
    const float HairpinCurvature = 0.026f;
    //本番の後輪制動へ入力する参照で車両設定は変更しない
    static Brake hairpinBrake;
    //押し始めと再試行可能時刻を管理する値
    static float hairpinStarted = -1f;
    static float nextHairpinTime;
    //進入直後に追従操舵が戻り過ぎないよう短時間保持する入力
    static float hairpinEntrySteer;
    //カーブを抜けるまで同じ進入操作を繰り返さない状態
    static bool hairpinArmed = true;
    //ログで進入操作と実際の横滑りを照合する累計回数
    static int hairpinAttempts;

    //同方向のカーブで滑り維持を許す時間上限
    const float HairpinSustainSeconds = 2.5f;
    //余分な減速を避けるため維持中だけ許す速度計画との差
    const float HairpinSpeedToleranceKph = 2f;
    //滑り角の増大で駆動を緩め始める維持限界
    const float HairpinSustainSlipDegrees = 11f;
    //進入後に同じ向きのカーブかを判断する曲率の符号
    static float hairpinDirection;
    //解除後に同じカーブで制動をかけ直さない状態
    static bool hairpinReleased;

    //滑り角の追従で急な旋回を要求しない応答時間
    const float HairpinSlipFollowSeconds = 0.5f;
    //滑り維持のために上乗せする毎秒ラジアンの上限
    const float HairpinYawCorrectionLimit = 0.35f;
    //姿勢作りをカーブの頂点より前へ移す先読み時間
    const float HairpinEntryPreviewSeconds = 0.4f;
    //後輪制動が抜けるまでに増える滑り角を見越す時間
    const float HairpinReleasePredictionSeconds = 0.12f;

    //移動方向の旋回率と横滑り角の変化から車体の目標旋回率を求める関数
    static float CalculateDriftYawTarget(float speed, float curvature, float slip, float targetSlip)
    {
        //横滑り角の変化率は移動方向の旋回率から車体の旋回率を引いた値になる
        float correction = (slip - targetSlip) * Mathf.Deg2Rad / HairpinSlipFollowSeconds;
        return Mathf.Max(0f, speed) * curvature + Mathf.Clamp(correction, -HairpinYawCorrectionLimit, HairpinYawCorrectionLimit);
    }

    //通常自動走行と分けてATの急カーブ後輪制動を比較する関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/急カーブ進入/Track AT")]
    #endif
    public static void RunHairpinAutomatic() { RunFromWindow(2, false, true, false, false, true); }

    //通常自動走行と分けてMTの急カーブ後輪制動を比較する関数
    #if UNITY_EDITOR
    [MenuItem("Tools/検証/急カーブ進入/Track MT")]
    #endif
    public static void RunHairpinManual() { RunFromWindow(3, false, true, false, false, true); }

    //速度だけでなく曲率と路肩余裕と接地を満たした進入を選ぶ関数
    static bool CanInitiateHairpin(float speed, float target, float curvature, float error, float slip, float brake, int grounded)
    {
        //MTは中高速カーブも対象にし接地と横方向の余裕がある時だけ姿勢を作る
        if (IsManualTrackDrive)
        {
            return speed >= ManualCruiseMinimumKph + ManualDriftEntryReserveKph && speed <= ManualDriftMaximumKph
                && Mathf.Abs(curvature) >= ManualDriftEntryCurvature
                && Mathf.Abs(error) < 0.8f && Mathf.Abs(slip) < 4f && brake < 0.2f && grounded == 4;
        }
        return speed >= HairpinMinimumKph && speed <= HairpinMaximumKph && target <= HairpinTargetKph
            && Mathf.Abs(curvature) >= HairpinCurvature && Mathf.Abs(error) < 0.8f
            && Mathf.Abs(slip) < 5f && brake < 0.55f && grounded == 4;
    }

    //再生開始と終了で検証専用入力を必ず解放する関数
    static void ResetHairpinDrift()
    {
        if (hairpinBrake != null) { hairpinBrake.SetEditorHandbrakeInput(false); }
        hairpinBrake = null;
        hairpinStarted = -1f;
        nextHairpinTime = 0f;
        hairpinEntrySteer = 0f;
        hairpinArmed = true;
        hairpinAttempts = 0;
        hairpinDirection = 0f;
        hairpinReleased = false;
    }

    //短い後輪制動と解除を通常の物理計算へ送り滑りが増えたら即座に解除する関数
    static void UpdateHairpinDrift(float speed, float target, ref float input)
    {
        //比較用の通常走行では後輪制動を追加せず選択した走行だけに適用する
        if (!TrackDrivePreferences.GetBool(HairpinTestKey, false)) { return; }
        if (hairpinBrake == null) { hairpinBrake = vehicle.GetComponentInChildren<Brake>(); }
        if (hairpinBrake == null) { return; }
        float slip = Mathf.Abs(vehicle.ESCSlipAngle);
        //現在地点と同方向の急カーブだけを少し手前から判定する
        float entryCurve = RoadCurvature(Mathf.Clamp(speed / 3.6f * HairpinEntryPreviewSeconds, 5f, 12f));
        if (entryCurve * driverCurvature < 0f) { entryCurve = driverCurvature; }
        float maximumSpeed = IsManualTrackDrive ? ManualDriftMaximumKph : HairpinMaximumKph;
        bool safe = IsHighSpeedTrackDrive && !vehicle.IsPullUp && vehicle.GroundedWheelCount == 4
            && Mathf.Abs(driverLineError) < 1.2f && speed >= HairpinReleaseKph && speed <= maximumSpeed;
        //十分に直線へ戻った時だけ次のカーブの進入を許可する
        if (Mathf.Abs(driverCurvature) < 0.015f && Time.time >= nextHairpinTime) { hairpinArmed = true; }
        //速度と接地と進路余裕を満たしたカーブだけで短い後輪制動を許可する
        if (safe && hairpinStarted < 0f && hairpinArmed && Time.time >= nextHairpinTime
            && CanInitiateHairpin(speed, target, entryCurve, driverLineError, slip, vehicle.Brake, vehicle.GroundedWheelCount))
        {
            hairpinStarted = Time.time;
            hairpinEntrySteer = input;
            hairpinDirection = Mathf.Sign(entryCurve);
            hairpinReleased = false;
            nextHairpinTime = Time.time + HairpinCooldownSeconds;
            hairpinArmed = false;
            hairpinAttempts++;
            Debug.Log($"[HairpinDrift] entry index={nearest} speed={speed:F1} target={target:F1} slip={slip:F1}");
        }
        float elapsed = Time.time - hairpinStarted;
        float sustainSlip = IsManualTrackDrive ? ManualDriftDegrees + 4f : HairpinSustainSlipDegrees;
        bool active = safe && hairpinStarted >= 0f && elapsed < HairpinSustainSeconds
            && driverCurvature * hairpinDirection > 0f && slip < sustainSlip;
        //解除の遅れで目標角を超えないよう拡大中の滑りだけを先読みする
        float outwardRate = Mathf.Max(0f, Mathf.Sign(vehicle.ESCSlipAngle) * ReadHandlingValue("m_trackSlipRateDegrees"));
        float predictedSlip = slip + outwardRate * HairpinReleasePredictionSeconds;
        //MTは滑り角による早期解除を残して姿勢作りに使える時間だけを延ばす
        float pulseSeconds = IsManualTrackDrive ? ManualDriftPulseSeconds : HairpinPulseSeconds;
        float releaseSlip = IsManualTrackDrive ? ManualDriftReleaseDegrees : HairpinReleaseSlipDegrees;
        bool pressed = active && !hairpinReleased && elapsed < pulseSeconds && predictedSlip < releaseSlip;
        if (!pressed && hairpinStarted >= 0f) { hairpinReleased = true; }
        hairpinBrake.SetEditorHandbrakeInput(pressed);
        if (pressed)
        {
            //後輪制動中は空ぶかしを抑え解除後に通常のアクセル維持へ戻す
            vehicle.Accel = 0f;
            //目標付近では四輪制動を重ねず後輪の短い制動だけで向きを作る
            if (speed <= target + HairpinSpeedToleranceKph) { vehicle.Brake = 0f; }
            //後輪を滑らせる間だけ進入時の舵を保ち解除後は通常の追従へ戻す
            if (input * hairpinEntrySteer > 0f && Mathf.Abs(input) < Mathf.Abs(hairpinEntrySteer)) { input = hairpinEntrySteer; }
            driverPhase = 5;
        }
        else if (active)
        {
            //小さな速度超過で四輪制動を繰り返さず滑りを残したまま出口の駆動を戻す
            if (speed <= target + HairpinSpeedToleranceKph)
            {
                vehicle.Brake = 0f;
                float holding = Mathf.Clamp01(0.55f + (target - speed) * DriverThrottleGain);
                vehicle.Accel = Mathf.Max(vehicle.Accel, holding);
            }
            driverPhase = 6;
        }
        else { hairpinStarted = -1f; }
    }
}
