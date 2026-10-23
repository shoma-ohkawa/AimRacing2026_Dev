using UnityEngine;

//車両制御を役割ごとに分けて管理するクラス
public partial class VehicleController
{
    //路面への自然なピッチ追従を残す角速度の範囲
    [SerializeField, Min(0f)] float m_naturalPitchSpeedDegrees = 15f;

    //安全補助の急な姿勢固定を避ける最大角減速度
    [SerializeField, Min(0f)] float m_pitchRecoveryAcceleration = 1.2f;

    //実際の加減速に応じたノーズの上下動を補助する関数
    void ApplyBodyPitchFeel()
    {
        //空中と開始待機中は演出用トルクを加えず、着地時へ古い加速度を持ち越さない
        if (m_isPullUp || GroundedWheelCount < 2)
        {
            ResetBodyPitchState();
            return;
        }
        if (!m_pitchStateInitialized)
        {
            ResetBodyPitchState();
            return;
        }

        //旧計算は横滑り中に向きが変わるだけでも見かけの減速を生むため使用しない
        //float rawAcceleration = (forwardSpeed - m_previousForwardSpeed) / Mathf.Max(Time.fixedDeltaTime, 0.001f);
        //速度ベクトルの変化から前後加速度を求め旋回だけでノーズを押し下げない
        float rawAcceleration = CalculatePitchAcceleration(m_rigidbody.linearVelocity, m_previousPitchVelocity, transform.forward, Time.fixedDeltaTime);
        m_previousPitchVelocity = m_rigidbody.linearVelocity;
        if (Mathf.Abs(rawAcceleration) > m_maxLongitudinalAcceleration * 2f)
        {
            m_filteredLongitudinalAcceleration = 0f;
            return;
        }

        //加速度の値を制限して、フィルタリングする
        rawAcceleration = Mathf.Clamp(rawAcceleration, -m_maxLongitudinalAcceleration, m_maxLongitudinalAcceleration);
        float filterAmount = 1f - Mathf.Exp(-m_accelerationFilterSpeed * Time.fixedDeltaTime);
        m_filteredLongitudinalAcceleration = Mathf.Lerp(m_filteredLongitudinalAcceleration, rawAcceleration, filterAmount);

        //加減速に応じた小さなピッチトルクを加えて自然な荷重移動を補助する
        float pitchAcceleration = -m_filteredLongitudinalAcceleration * m_pitchResponse * Mathf.Clamp01(m_additionalPitchScale);
        if (pitchAcceleration < 0f) { pitchAcceleration *= m_accelerationPitchBoost; }
        float pitchLimit = pitchAcceleration < 0f ? m_maxNoseUpAngularAcceleration : m_maxPitchAngularAcceleration;
        m_rigidbody.AddTorque(transform.right * Mathf.Clamp(pitchAcceleration, -pitchLimit, pitchLimit), ForceMode.Acceleration);

        //サスペンションの自然な荷重移動を残しながら異常なピッチ回転だけを減衰する
        float localPitchVelocity = Vector3.Dot(m_rigidbody.angularVelocity, transform.right);
        float maximumNaturalPitchSpeed = Mathf.Max(0f, m_naturalPitchSpeedDegrees) * Mathf.Deg2Rad;
        //許容範囲外だけ減衰しサスペンションが作る小さな前後運動を消さない
        float excess = Mathf.Max(0f, Mathf.Abs(localPitchVelocity) - maximumNaturalPitchSpeed);
        if (excess <= 0f) { return; }
        //補助単独で回転方向を逆転させないよう一物理ステップ分の減速量を制限する
        float step = Mathf.Max(Time.fixedDeltaTime, Mathf.Epsilon);
        float response = 1f - Mathf.Exp(-Mathf.Max(0f, m_pitchDamping) * step);
        float recovery = Mathf.Min(excess * response / step, Mathf.Max(0f, m_pitchRecoveryAcceleration));
        m_rigidbody.AddTorque(-transform.right * Mathf.Sign(localPitchVelocity) * recovery, ForceMode.Acceleration);
    }

    //世界座標で求めた加速度のうち車体の前後方向だけを取り出す関数
    static float CalculatePitchAcceleration(Vector3 _velocity, Vector3 _previousVelocity, Vector3 _forward, float _deltaTime)
    {
        return Vector3.Dot(_velocity - _previousVelocity, _forward) / Mathf.Max(_deltaTime, 0.001f);
    }

    //発進や復帰直前の速度を基準にして古い加速度を持ち越さない関数
    void ResetBodyPitchState()
    {
        m_previousPitchVelocity = m_rigidbody != null ? m_rigidbody.linearVelocity : Vector3.zero;
        m_filteredLongitudinalAcceleration = 0f;
        m_pitchStateInitialized = true;
    }
}
