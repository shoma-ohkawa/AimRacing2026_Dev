/**
 * @file    LogitechFFBBackend.cs
 * @brief   Logitech Steering Wheel SDK によるFFB出力 (G923 / G29 / G920 など)
 *
 * SDKの初期化(LogiSteeringInitialize)は従来どおり GameManager が担当する前提。
 * このクラスは LogiUpdate / 各種Play/Stop を呼ぶだけ。
 */

using UnityEngine;

public sealed class LogitechFFBBackend : IWheelFFBBackend
{
    private readonly int m_index;
    private readonly WheelFFBBackendTuning m_tuning;
    private readonly bool m_requireLogitechManufacturer;

    private bool m_connected = false;
    private bool m_sdkUpdated = false;
    private bool m_rawConnected = false;

    public LogitechFFBBackend(int deviceIndex, WheelFFBBackendTuning tuning, bool requireLogitechManufacturer)
    {
        m_index = deviceIndex;
        m_tuning = tuning ?? new WheelFFBBackendTuning();
        m_requireLogitechManufacturer = requireLogitechManufacturer;
    }

    public EWheelFFBBackend Type => EWheelFFBBackend.LogitechSDK;
    public string Name => "Logitech SDK";
    public string DeviceName => "Logitech Wheel #" + m_index;
    public bool SupportsConditionEffects => true;
    public bool SupportsOperatingRange => true;
    public bool IsConnected => m_connected;

    /// <summary>診断用の状態文字列</summary>
    public string StatusText
    {
        get
        {
            if (!m_sdkUpdated) return "LogiUpdate失敗(SDK未初期化 or 非アクティブ)";
            if (!m_rawConnected) return "デバイスなし";
            if (!m_connected) return "SDKはデバイスを認識しているがLogitech製ではない(他社製はDirectInputで扱う)";
            return "接続中";
        }
    }

    public bool Initialize()
    {
        return Update() && m_connected;
    }

    public bool Update()
    {
        m_sdkUpdated = LogitechGSDK.LogiUpdate();

        if (!m_sdkUpdated)
        {
            m_rawConnected = false;
            m_connected = false;
            return false;
        }

        bool connected = LogitechGSDK.LogiIsConnected(m_index);
        m_rawConnected = connected;

        // SDKは他社製のDirectInputデバイスも列挙することがあるため、
        // Logitech製のときだけ「このバックエンドで接続中」とみなす。
        // ※お使いのLogitechGSDK.csに LogiIsManufacturerConnected が無い場合はこのifを削除
        if (connected && m_requireLogitechManufacturer)
        {
            connected = LogitechGSDK.LogiIsManufacturerConnected(m_index, LogitechGSDK.LOGI_MANUFACTURER_LOGITECH);
        }

        m_connected = connected;
        return true;
    }

    public bool Reinitialize()
    {
        if (GameManager.Instance == null)
        {
            AppLog.LogWarning("[Wheel FFB] GameManager.Instance が見つからないため、Logitech SDK再初期化を実行できません。");
            return false;
        }

        bool result = GameManager.Instance.ForceReinitializeLogitech("LogitechFFBBackend Reinitialize");
        Update();
        return result;
    }

    public void Shutdown()
    {
        // SDKの終了処理はGameManager側に任せる。ここではFFBだけ止める
        StopAll();
        m_connected = false;
    }

    public bool ApplyOperatingRange(int degrees)
    {
        LogitechGSDK.LogiControllerPropertiesData properties = new LogitechGSDK.LogiControllerPropertiesData();

        if (!LogitechGSDK.LogiGetCurrentControllerProperties(m_index, ref properties))
        {
            return false;
        }

        properties.wheelRange = Mathf.Clamp(degrees, 90, 900);
        return LogitechGSDK.LogiSetPreferredControllerProperties(properties);
    }

    public void PlaySpring(int offset, int saturation, int coefficient)
    {
        LogitechGSDK.LogiPlaySpringForce(
            m_index,
            Mathf.Clamp(offset, -100, 100),
            m_tuning.ScaleUnsigned(saturation, m_tuning.springScale),
            m_tuning.ScaleUnsigned(coefficient, m_tuning.springScale)
        );
    }

    public void StopSpring()
    {
        LogitechGSDK.LogiStopSpringForce(m_index);
    }

    public void PlayDamper(int coefficient)
    {
        LogitechGSDK.LogiPlayDamperForce(m_index, m_tuning.ScaleUnsigned(coefficient, m_tuning.damperScale));
    }

    public void StopDamper()
    {
        LogitechGSDK.LogiStopDamperForce(m_index);
    }

    public void PlayConstant(int force)
    {
        int value = m_tuning.ScaleSigned(force, m_tuning.constantScale);

        if (m_tuning.invertConstantForce)
        {
            value = -value;
        }

        LogitechGSDK.LogiPlayConstantForce(m_index, value);
    }

    public void StopConstant()
    {
        LogitechGSDK.LogiStopConstantForce(m_index);
    }

    public void PlayRoadEffect(EWheelRoadEffect effect, int magnitude, int surfaceWaveType, int periodMs)
    {
        int value = m_tuning.ScaleUnsigned(magnitude, m_tuning.roadEffectScale);

        switch (effect)
        {
            case EWheelRoadEffect.BumpyRoad:
                LogitechGSDK.LogiPlayBumpyRoadEffect(m_index, value);
                break;

            case EWheelRoadEffect.DirtRoad:
                LogitechGSDK.LogiPlayDirtRoadEffect(m_index, value);
                break;

            case EWheelRoadEffect.Surface:
                LogitechGSDK.LogiPlaySurfaceEffect(m_index, surfaceWaveType, value, Mathf.Max(1, periodMs));
                break;
        }
    }

    public void StopRoadEffects()
    {
        LogitechGSDK.LogiStopBumpyRoadEffect(m_index);
        LogitechGSDK.LogiStopDirtRoadEffect(m_index);
        LogitechGSDK.LogiStopSurfaceEffect(m_index);
    }

    public void StopAll()
    {
        LogitechGSDK.LogiStopConstantForce(m_index);
        LogitechGSDK.LogiStopSpringForce(m_index);
        LogitechGSDK.LogiStopDamperForce(m_index);

        LogitechGSDK.LogiStopBumpyRoadEffect(m_index);
        LogitechGSDK.LogiStopDirtRoadEffect(m_index);
        LogitechGSDK.LogiStopSlipperyRoadEffect(m_index);
        LogitechGSDK.LogiStopSurfaceEffect(m_index);

        LogitechGSDK.LogiStopCarAirborne(m_index);
        LogitechGSDK.LogiStopSoftstopForce(m_index);
    }
}
