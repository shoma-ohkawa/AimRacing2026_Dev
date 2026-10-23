/**
 * @file    XInputFFBBackend.cs
 * @brief   XInputによる振動出力 (Xboxモードのハンコン・ゲームパッド用)
 *
 * XInputが送れるのは「左モーター(低周波)」「右モーター(高周波)」の強さ2つだけ。
 * そのため Spring / Damper(ハンドルの重さ・センタリング)は仕様上出せない。
 *
 * 割り当て
 *  ・左モーター(低周波, ドスン系) : 路面凹凸
 *  ・右モーター(高周波, ブルブル系) : ConstantForce(左右揺れ・衝突)の絶対値
 *
 * 注意: XInputGetState を未接続スロットに毎フレーム投げると重いことがあるので、
 *       未接続時の探索は reconnectInterval ごとにしか行わない。
 */

using System;
using System.Runtime.InteropServices;
using UnityEngine;

public sealed class XInputFFBBackend : IWheelFFBBackend
{
    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll")] private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);
    [DllImport("xinput1_4.dll")] private static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

    private const uint kErrorSuccess = 0;
    private const int kMaxUsers = 4;

    private readonly WheelFFBBackendTuning m_tuning;
    private readonly int m_preferredUserIndex;
    private readonly float m_reconnectInterval;

    private int m_userIndex = -1;
    private bool m_unavailable = false;
    private float m_nextSearchTime = 0.0f;

    private float m_road01 = 0.0f;
    private float m_constant01 = 0.0f;
    private int m_lastLeft = -1;
    private int m_lastRight = -1;

    /// <param name="preferredUserIndex">0～3で固定、-1で最初に見つかったコントローラー</param>
    public XInputFFBBackend(WheelFFBBackendTuning tuning, int preferredUserIndex, float reconnectInterval)
    {
        m_tuning = tuning ?? WheelFFBBackendTuning.CreateXInputDefault();
        m_preferredUserIndex = Mathf.Clamp(preferredUserIndex, -1, kMaxUsers - 1);
        m_reconnectInterval = Mathf.Max(0.1f, reconnectInterval);
    }

    public EWheelFFBBackend Type => EWheelFFBBackend.XInput;
    public string Name => "XInput";
    public string DeviceName => m_userIndex >= 0 ? "XInput User #" + m_userIndex : string.Empty;
    public bool SupportsConditionEffects => false;
    public bool SupportsOperatingRange => false;
    public bool IsConnected => m_userIndex >= 0;

    public bool Initialize()
    {
        SearchController(true);
        return IsConnected;
    }

    public bool Update()
    {
        if (m_unavailable)
        {
            return false;
        }

        if (m_userIndex < 0)
        {
            SearchController(false);

            if (m_userIndex < 0)
            {
                return false;
            }
        }

        if (!IsUserConnected(m_userIndex))
        {
            m_userIndex = -1;
            m_lastLeft = -1;
            m_lastRight = -1;
            return false;
        }

        SendVibration();
        return true;
    }

    public bool Reinitialize()
    {
        m_userIndex = -1;
        m_lastLeft = -1;
        m_lastRight = -1;
        return Initialize();
    }

    public void Shutdown()
    {
        StopAll();
        m_userIndex = -1;
    }

    public bool ApplyOperatingRange(int degrees)
    {
        return true;
    }

    // XInputではSpring/Damperは出せない
    public void PlaySpring(int offset, int saturation, int coefficient) { }
    public void StopSpring() { }
    public void PlayDamper(int coefficient) { }
    public void StopDamper() { }

    public void PlayConstant(int force)
    {
        int value = m_tuning.ScaleUnsigned(Mathf.Abs(force), m_tuning.constantScale);
        m_constant01 = value / 100.0f;
    }

    public void StopConstant()
    {
        m_constant01 = 0.0f;
    }

    public void PlayRoadEffect(EWheelRoadEffect effect, int magnitude, int surfaceWaveType, int periodMs)
    {
        int value = m_tuning.ScaleUnsigned(magnitude, m_tuning.roadEffectScale);
        m_road01 = value / 100.0f;
    }

    public void StopRoadEffects()
    {
        m_road01 = 0.0f;
    }

    public void StopAll()
    {
        m_road01 = 0.0f;
        m_constant01 = 0.0f;
        SendVibration();
    }

    // ==============================
    // 内部処理
    // ==============================

    private void SendVibration()
    {
        if (m_userIndex < 0 || m_unavailable)
        {
            return;
        }

        int left = Mathf.RoundToInt(Mathf.Clamp01(m_road01) * ushort.MaxValue);
        int right = Mathf.RoundToInt(Mathf.Clamp01(m_constant01) * ushort.MaxValue);

        if (left == m_lastLeft && right == m_lastRight)
        {
            return;
        }

        XINPUT_VIBRATION vibration = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed = (ushort)left,
            wRightMotorSpeed = (ushort)right,
        };

        try
        {
            if (XInputSetState((uint)m_userIndex, ref vibration) == kErrorSuccess)
            {
                m_lastLeft = left;
                m_lastRight = right;
            }
        }
        catch (DllNotFoundException)
        {
            MarkUnavailable();
        }
    }

    private void SearchController(bool force)
    {
        if (m_unavailable)
        {
            return;
        }

        if (!force && Time.unscaledTime < m_nextSearchTime)
        {
            return;
        }

        m_nextSearchTime = Time.unscaledTime + m_reconnectInterval;

        if (m_preferredUserIndex >= 0)
        {
            m_userIndex = IsUserConnected(m_preferredUserIndex) ? m_preferredUserIndex : -1;
            return;
        }

        for (int i = 0; i < kMaxUsers; i++)
        {
            if (IsUserConnected(i))
            {
                m_userIndex = i;
                m_lastLeft = -1;
                m_lastRight = -1;
                return;
            }
        }

        m_userIndex = -1;
    }

    private bool IsUserConnected(int index)
    {
        if (m_unavailable)
        {
            return false;
        }

        try
        {
            return XInputGetState((uint)index, out XINPUT_STATE _) == kErrorSuccess;
        }
        catch (DllNotFoundException)
        {
            MarkUnavailable();
            return false;
        }
    }

    private void MarkUnavailable()
    {
        m_unavailable = true;
        m_userIndex = -1;
        AppLog.LogWarning("[Wheel FFB] xinput1_4.dll が読み込めません。XInput振動は無効になります。");
    }
}
