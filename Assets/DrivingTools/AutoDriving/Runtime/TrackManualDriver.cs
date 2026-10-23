using UnityEngine;

//ATや手動プレイを変更せずMT自動走行の連続操作を管理するクラス
public static partial class TrackDriveSession
{
    //MTの完走結果を二分三十秒の依頼目標と比較する値で車両の時間や速度は変更しない
    const float ManualTargetRaceSeconds = 150f;
    //MTの通過速度を計画する横加速度上限で車体へ力は加えない
    const float ManualCornerAcceleration = 26f;
    //MTのカーブで維持を狙う車体と進行方向の角度差
    const float ManualDriftDegrees = 10f;
    //MTの進入で後輪を滑らせる最長時間で目標角に達したら先に解放する
    const float ManualDriftPulseSeconds = 0.14f;
    //95km/hを下回る前に駆動へ戻すため余裕を含めた巡航目標
    const float ManualCruiseMinimumKph = 100f;
    //MTの高速ドリフト進入を許す上限で車速は直接変更しない
    const float ManualDriftMaximumKph = 165f;
    //発進操作と区別して速度に余裕がある中高速カーブだけで後輪制動する条件
    const float ManualDriftEntryReserveKph = 10f;
    //直線の揺れをドリフト進入と取り違えない道路曲率の下限
    const float ManualDriftEntryCurvature = 0.014f;
    //滑り始めの短い制動を終えた後の角度増加を見越す解除角
    const float ManualDriftReleaseDegrees = 6f;
    //急カーブで車体の向きが先行しても必要な前輪角を早く戻しすぎない配分
    const float ManualCurvatureFeedforward = 0.5f;
    //滑り目標の解除時に進路追従を打ち消す逆舵を重ね過ぎない補正率
    const float ManualSlipFeedbackGain = 0.08f;
    //MTの高速旋回で進行方向の曲がり不足を操舵へ戻す補正率
    const float ManualYawFeedbackGain = 0.6f;

    //曲率由来の前輪角を進路追従へ混ぜ急カーブの操舵不足を減らす関数
    static float CalculateManualPursuit(float pursuit, float curvature)
    {
        //逆カーブへの切り返しと壁からの回避は追従操舵を優先する
        float feedforward = Mathf.Atan(DriverWheelbase * curvature) * Mathf.Rad2Deg;
        if (pursuit * feedforward <= 0f) { return pursuit; }
        float tightness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.035f, Mathf.Abs(curvature)));
        //すでに十分な舵が出ている場合は追加せず切り遅れだけを補う
        if (Mathf.Abs(pursuit) >= Mathf.Abs(feedforward)) { return pursuit; }
        return Mathf.Lerp(pursuit, feedforward, tightness * ManualCurvatureFeedforward);
    }

    //通常の曲率計画を求めた後に高速ドリフト試走の希望下限を加える関数
    static float CalculateManualCruiseSpeed(float curvature, float acceleration)
    {
        //これは入力の目標で車速やタイヤの摩擦を上書きしない
        return Mathf.Max(CalculateManualCornerSpeed(curvature, acceleration), ManualCruiseMinimumKph / 3.6f);
    }
    //小さな速度誤差でアクセルとブレーキが交互に入らない応答時間
    const float ManualPedalResponseSeconds = 0.12f;
    //前回の加減速要求を保持し次の操作へ滑らかにつなぐ値
    static float manualPedal;
    //MTの操舵速度を保持し左右の切り返しで速度が突然反転しないようにする値
    static float manualSteerVelocity;
    //入力追従の遅れを増やし過ぎず切り返しを丸める応答秒数
    const float ManualSteerResponseSeconds = 0.06f;
    //戻しと逆ハンドルを切り込みより早く追従させる応答秒数
    const float ManualRecoveryResponseSeconds = 0.03f;
    //MTの自動操舵を一物理ステップで急変させない毎秒の最大操作量
    const float ManualSteerRate = 2.25f;

    //前の試験のペダル状態を次の発進へ持ち込まない関数
    static void ResetManualDriver()
    {
        manualPedal = 0f;
        manualSteerVelocity = 0f;
    }

    //目標ラインへ戻る横移動を逸脱と取り違えず離れる分だけ先読みする関数
    static float PredictManualLineError(float lineError, float lateralVelocity)
    {
        float departure = Mathf.Sign(lineError) * lateralVelocity;
        return Mathf.Abs(lineError) + Mathf.Max(0f, departure) * RoadDeparturePredictionSeconds;
    }

    //希望下限を優先して接触することを避け曲率からMTの通過目標を求める関数
    static float CalculateManualCornerSpeed(float curvature, float acceleration)
    {
        float bend = Mathf.Max(0.0001f, Mathf.Abs(curvature));
        //急カーブで希望最低速度を優先せず指定された横加速度の範囲内で計画する
        float allowedAcceleration = Mathf.Clamp(acceleration, 0f, ManualCornerAcceleration);
        return Mathf.Min(Mathf.Sqrt(allowedAcceleration / bend), StraightSpeedKph / 3.6f);
    }

    //MTの操舵量と操舵速度を連続にして目標入力へ近づける関数
    static float SmoothManualSteering(float current, float requested, float deltaTime)
    {
        if (deltaTime <= 0f) { return current; }
        //切り返し後に直前の操舵速度が逆方向へ入力を押し続けることを防ぐ
        float target = Mathf.Clamp(requested, -1f, 1f);
        if ((target - current) * manualSteerVelocity < 0f) { manualSteerVelocity = 0f; }
        //切り込みの滑らかさは残し戻しと逆ハンドルだけ短い応答にする
        bool recovering = current * target < 0f || Mathf.Abs(target) < Mathf.Abs(current);
        float response = recovering ? ManualRecoveryResponseSeconds : ManualSteerResponseSeconds;
        float filtered = Mathf.SmoothDamp(current, target, ref manualSteerVelocity, response, ManualSteerRate, deltaTime);
        return Mathf.MoveTowards(current, filtered, ManualSteerRate * deltaTime);
    }

    //加速を正で制動を負にまとめ両方のペダルが同時に入らないようにする関数
    static void SmoothManualPedals(float deltaTime)
    {
        //待機中の空ぶかしとドリフト進入の短い後輪制動はそのまま通す
        if (vehicle.IsPullUp || driverPhase == 5)
        {
            manualPedal = vehicle.Accel;
            return;
        }
        //大きな制動要求は遅らせず小さな踏み替えだけを平滑化する
        float requested = vehicle.Brake > 0f ? -vehicle.Brake : vehicle.Accel;
        float response = requested < -0.5f ? ManualPedalResponseSeconds * 0.25f : ManualPedalResponseSeconds;
        manualPedal = Mathf.Lerp(manualPedal, requested, 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / response));
        vehicle.Accel = Mathf.Max(0f, manualPedal);
        vehicle.Brake = Mathf.Max(0f, -manualPedal);
    }
}
