using UnityEngine;

//駆動中の後輪グリップを配分してTrackの滑りを急に消さないクラス
public partial class WheelController2026
{
    //空転悪化では直ちに駆動を制限し回復中は現在の要求まで徐々に戻す関数
    static float UpdateTractionTorqueRatio(float current, float target, float recoveryRate, float deltaTime)
    {
        //不正な入力でも駆動倍率を零から一の範囲に収める
        target = Mathf.Clamp01(target);
        current = Mathf.Clamp01(current);
        if (target <= current) { return target; }

        //滑りのしきい値を下回る前でも回復量に応じてトルクを戻す
        return Mathf.MoveTowards(current, target, Mathf.Max(0f, recoveryRate) * Mathf.Max(0f, deltaTime));
    }

    //比較検証と調整で加速ドリフトの横グリップ配分を切り替える設定
    [Header("Track Powered Grip")]
    [SerializeField] bool m_trackPoweredGripEnabled = true;

    //横滑りを残しても操縦に必要な後輪の横グリップを確保する下限
    [SerializeField, Range(0.75f, 1f)] float m_trackPoweredGripMinimum = 0.82f;

    //通常のコーナーではグリップを減らさないための滑り開始角
    [SerializeField, Min(0f)] float m_trackPoweredGripStartDegrees = 3f;

    //十分に滑った状態でグリップ配分を全量適用する角度
    [SerializeField, Min(1f)] float m_trackPoweredGripFullDegrees = 8f;

    //スピンに近付いたときに通常グリップへ戻し始める角度
    [SerializeField, Min(1f)] float m_trackPoweredGripRecoveryDegrees = 12f;

    //大きく姿勢が崩れた場合にグリップ緩和を終了する角度
    [SerializeField, Min(1f)] float m_trackPoweredGripStopDegrees = 18f;

    //踏み直しで後輪の横力を急変させない応答時間
    [SerializeField, Range(0.05f, 0.5f)] float m_trackPoweredGripResponseSeconds = 0.18f;

    //空転が収まる途中で急に横グリップを戻さずカウンターの修正回数を減らす時間
    [SerializeField, Range(0.05f, 0.6f)] float m_trackPoweredGripReturnSeconds = 0.32f;

    //加速ドリフトの速度補間が全量になる速度で実車の最低ドリフト速度ではない
    [SerializeField, Min(0f)] float m_trackPoweredGripMinimumKph = 25f;

    //停車付近の安定を残して低速ヘアピンへ段階的につなぐ速度幅
    [SerializeField, Min(1f)] float m_trackPoweredGripSpeedFadeKph = 10f;

    //後輪ロック中も操縦できる横力を残すゲーム用の下限
    [SerializeField, Range(0.5f, 1f)] float m_trackHandbrakeGripMinimum = 0.65f;

    //後輪制動と実際の縦滑りがそろった時だけ横力の余裕を減らす関数
    static float CalculateHandbrakeGrip(float amount, float slip, float peakSlip, float blend, float minimum)
    {
        float locking = Mathf.InverseLerp(0f, Mathf.Max(0.01f, peakSlip), Mathf.Abs(slip));
        return CalculatePoweredGripScale(locking, Mathf.Clamp01(amount) * Mathf.Clamp01(blend), minimum);
    }

    //境界速度をまたいでも横グリップの要求が急変しない割合を求める関数
    static float CalculatePoweredGripSpeedBlend(float speed, float fullSpeed, float fadeWidth)
    {
        //後退と停止では追加緩和を行わず前進速度だけを補間する
        float start = Mathf.Max(0f, fullSpeed - Mathf.Max(1f, fadeWidth));
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, Mathf.Max(start + 1f, fullSpeed), speed));
    }

    //各輪の配分を独立して平滑化する横グリップ倍率
    readonly float[] m_poweredGripScale = { 1f, 1f, 1f, 1f };

    //走行ログで後輪の横グリップ緩和量を確認する関数
    public float GetPoweredGripScale(int _index)
    {
        if (_index < 0 || _index >= m_poweredGripScale.Length) { return 1f; }
        return m_poweredGripScale[_index];
    }

    //縦方向の駆動負荷から残す横グリップを摩擦円の形で求める関数
    static float CalculatePoweredGripScale(float _usage, float _blend, float _minimum)
    {
        //駆動需要が増えても横グリップを零へ落とさず制御余地を残す
        float longitudinal = Mathf.Clamp01(_usage);
        float remaining = Mathf.Sqrt(Mathf.Max(0f, 1f - longitudinal * longitudinal));
        return Mathf.Lerp(1f, Mathf.Max(Mathf.Clamp01(_minimum), remaining), Mathf.Clamp01(_blend));
    }

    //空転抑制後のトルクだけでグリップ回復を判断せず実際の加速空転も含める関数
    static float CalculatePoweredGripUsage(float _torqueUsage, float _accelerationSlip, float _peakSlip)
    {
        //摩擦曲線のピークまでの空転を正規化し制動滑りは加速空転に含めない
        float spinUsage = Mathf.Clamp01(Mathf.Max(0f, _accelerationSlip) / Mathf.Max(0.01f, _peakSlip));
        return Mathf.Clamp01(Mathf.Max(_torqueUsage, spinUsage));
    }

    //変速中も残る加速空転を維持し制動中には追加のグリップ緩和を始めない関数
    static bool HasPoweredGripDemand(float _netDrive, float _accelerationSlip, float _brakeTorque)
    {
        return _netDrive > 0f || (_accelerationSlip > 0f && _brakeTorque <= 0f);
    }

    //横グリップの復帰だけゆっくり行い危険な姿勢では元の応答速度を使う関数
    static float SmoothPoweredGrip(float _current, float _target, float _attack, float _release, bool _recovery, float _deltaTime)
    {
        float seconds = _target > _current && !_recovery ? Mathf.Max(_attack, _release) : _attack;
        float response = 1f - Mathf.Exp(-Mathf.Max(0f, _deltaTime) / Mathf.Max(0.01f, seconds));
        return Mathf.Lerp(_current, _target, response);
    }

    //速度を直接書き換えず後輪の摩擦だけで加速中の滑りを残す関数
    void ApplyTrackPoweredGrip(int _index, WheelCollider _wheel, bool _grounded, WheelHit _hit)
    {
        //NormalとSportと前輪の接地特性は変更しない
        float track = m_differential.TrackHandlingBlend;
        if (!m_trackPoweredGripEnabled || m_isFront[_index] || !_grounded || track <= 0f)
        {
            m_poweredGripScale[_index] = 1f;
            return;
        }

        //停止中や制動中や後退中には加速ドリフトの緩和を持ち込まない
        Vector3 velocity = m_vehicleRigidbody.transform.InverseTransformDirection(m_vehicleRigidbody.linearVelocity);
        float target = 1f;
        float slip = Mathf.Abs(Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg);
        float netDrive = Mathf.Max(0f, _wheel.motorTorque - _wheel.brakeTorque);
        //変速や駆動制限でトルクが一瞬消えても実際の空転が残る間は滑りを急に消さない
        float accelerationSlip = GetAccelerationSlip(_hit.forwardSlip);
        bool driveDemand = HasPoweredGripDemand(netDrive, accelerationSlip, _wheel.brakeTorque);
        float speedBlend = CalculatePoweredGripSpeedBlend(velocity.z * 3.6f, m_trackPoweredGripMinimumKph, m_trackPoweredGripSpeedFadeKph);
        if (speedBlend > 0f && driveDemand)
        {
            //横速度が既にあるときだけ作用し直進から勝手に滑りを作らない
            float drift = Mathf.InverseLerp(m_trackPoweredGripStartDegrees, m_trackPoweredGripFullDegrees, slip);
            float recovery = Mathf.InverseLerp(m_trackPoweredGripRecoveryDegrees, m_trackPoweredGripStopDegrees, slip);
            //Nmをタイヤ半径と接地荷重で割り縦方向のグリップ使用率へ換算する
            float peak = _wheel.forwardFriction.extremumValue * _wheel.forwardFriction.stiffness;
            float capacity = Mathf.Max(1f, _hit.force * _wheel.radius * peak);
            //ESCの小さな制動で緩和を突然解除せず実際の駆動と空転に合わせる
            float usage = CalculatePoweredGripUsage(netDrive / capacity, accelerationSlip, _wheel.forwardFriction.extremumSlip);
            target = CalculatePoweredGripScale(usage, track * drift * (1f - recovery) * speedBlend, m_trackPoweredGripMinimum);
        }

        //後輪ロックで縦滑りが生じた時だけ横グリップを配分し解除後は駆動側へ連続して戻す
        float handbrake = m_brake != null ? m_brake.HandbrakeAmount : 0f;
        float protection = 1f - Mathf.InverseLerp(m_trackPoweredGripRecoveryDegrees, m_trackPoweredGripStopDegrees, slip);
        float lockedGrip = CalculateHandbrakeGrip(handbrake, _hit.forwardSlip, _wheel.forwardFriction.extremumSlip,
            track * speedBlend * protection, m_trackHandbrakeGripMinimum);
        target = Mathf.Min(target, lockedGrip);
        //通常ブレーキと過大な姿勢では横グリップを速く復帰する
        bool normalBraking = handbrake <= 0f && _wheel.brakeTorque > 0f && _wheel.brakeTorque >= _wheel.motorTorque;
        bool recovering = slip >= m_trackPoweredGripRecoveryDegrees || normalBraking;
        m_poweredGripScale[_index] = SmoothPoweredGrip(m_poweredGripScale[_index], target, m_trackPoweredGripResponseSeconds,
            m_trackPoweredGripReturnSeconds, recovering, Time.fixedDeltaTime);
        WheelFrictionCurve lateral = _wheel.sidewaysFriction;
        lateral.stiffness *= m_poweredGripScale[_index];
        _wheel.sidewaysFriction = lateral;
    }
}
