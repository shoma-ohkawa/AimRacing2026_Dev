/**
 * @file    DirectInputFFBBackend.cs
 * @brief   DirectInput8 ネイティブプラグイン(WheelFFB_DI.dll)によるFFB出力
 *
 * Thrustmaster など、Logitech SDKが使えないFFBホイール用。
 * Logitech製でもDirectInput FFBは出せるので、skipLogitech=false にすればG923でも動作確認できる。
 * (その場合はLogitech SDKを初期化しないこと。同じデバイスを排他で取り合うため)
 *
 * 回転角はDirectInputに標準の設定方法がないため、各メーカーのコントロールパネルで設定する。
 */

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

public sealed class DirectInputFFBBackend : IWheelFFBBackend
{
    private const string kDll = "WheelFFB_DI";

    [DllImport(kDll)] private static extern int WFFB_Initialize(IntPtr hwnd, int skipLogitech);
    [DllImport(kDll)] private static extern void WFFB_Shutdown();
    [DllImport(kDll)] private static extern int WFFB_Connect();
    [DllImport(kDll)] private static extern int WFFB_Update();
    [DllImport(kDll)] private static extern int WFFB_IsConnected();
    [DllImport(kDll)] private static extern int WFFB_GetVendorId();
    [DllImport(kDll)] private static extern int WFFB_GetProductId();
    [DllImport(kDll, CharSet = CharSet.Unicode)] private static extern int WFFB_GetDeviceName(StringBuilder buffer, int length);
    [DllImport(kDll)] private static extern int WFFB_GetLastError();
    [DllImport(kDll)] private static extern void WFFB_SetGain(int percent);
    [DllImport(kDll)] private static extern void WFFB_PlayConstant(int force);
    [DllImport(kDll)] private static extern void WFFB_StopConstant();
    [DllImport(kDll)] private static extern void WFFB_PlaySpring(int offset, int saturation, int coefficient);
    [DllImport(kDll)] private static extern void WFFB_StopSpring();
    [DllImport(kDll)] private static extern void WFFB_PlayDamper(int coefficient);
    [DllImport(kDll)] private static extern void WFFB_StopDamper();
    [DllImport(kDll)] private static extern void WFFB_PlayPeriodic(int waveType, int magnitude, int periodMs);
    [DllImport(kDll)] private static extern void WFFB_StopPeriodic();
    [DllImport(kDll)] private static extern void WFFB_StopAll();

    [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();

    // Logitech SDKのBumpy/Dirtに近い感触になるよう仮で割り当て。実機で要調整
    private const int kBumpyWaveType = 1;   // Square
    private const int kBumpyPeriodMs = 120;
    private const int kDirtWaveType = 0;    // Sine
    private const int kDirtPeriodMs = 35;

    private readonly WheelFFBBackendTuning m_tuning;
    private readonly bool m_skipLogitech;
    private readonly float m_reconnectInterval;

    private bool m_initialized = false;
    private bool m_pluginUnavailable = false;
    private float m_nextConnectTime = 0.0f;
    private string m_deviceName = string.Empty;

    // G923_FFBControllerは毎フレーム「StopConstant → PlayConstant」の順で呼ぶため、
    // そのままだとエフェクトが毎フレーム停止・再開してガタつく。
    // Stopは次のUpdateまで保留し、同じフレーム内にPlayが来たら取り消す。
    private bool m_constantStopPending = false;

    public DirectInputFFBBackend(WheelFFBBackendTuning tuning, bool skipLogitechDevices, float reconnectInterval)
    {
        m_tuning = tuning ?? new WheelFFBBackendTuning();
        m_skipLogitech = skipLogitechDevices;
        m_reconnectInterval = Mathf.Max(0.1f, reconnectInterval);
    }

    public EWheelFFBBackend Type => EWheelFFBBackend.DirectInput;
    public string Name => "DirectInput";
    public string DeviceName => m_deviceName;
    public bool SupportsConditionEffects => true;
    public bool SupportsOperatingRange => false;
    public bool IsConnected => m_initialized && WFFB_IsConnected() != 0;

    /// <summary>診断用の状態文字列</summary>
    public string StatusText
    {
        get
        {
            if (m_pluginUnavailable) return "WheelFFB_DI.dll が読み込めない(Plugins/x86_64 と Inspector設定を確認)";
            if (!m_initialized) return "未初期化(ゲームウィンドウ未取得)";
            if (IsConnected) return "接続中 " + m_deviceName;

            int hr = WFFB_GetLastError();
            string reason;

            switch ((uint)hr)
            {
                case 0x80070005: reason = "他(Logitech SDK・Control Panel・他ゲーム)が排他で掴んでいる"; break;
                case 0x80070002: reason = "FFB対応デバイスが見つからない(ドライバ・PS3スイッチ・USBを確認)"; break;
                case 0x80070057: reason = "引数エラー(ウィンドウハンドル不正の可能性)"; break;
                default: reason = "接続失敗"; break;
            }

            return reason + " / HRESULT:" + LastErrorHex;
        }
    }

    /// <summary>直近のDirectInputエラー(HRESULT)。接続できない原因調査用</summary>
    public string LastErrorHex => m_initialized ? "0x" + WFFB_GetLastError().ToString("X8") : "-";

    public bool Initialize()
    {
        if (!EnsureInitialized())
        {
            return false;
        }

        TryConnect(true);
        return IsConnected;
    }

    public bool Update()
    {
        if (!EnsureInitialized())
        {
            return false;
        }

        if (WFFB_IsConnected() == 0)
        {
            m_constantStopPending = false;
            TryConnect(false);
            return WFFB_IsConnected() != 0;
        }

        if (m_constantStopPending)
        {
            WFFB_StopConstant();
            m_constantStopPending = false;
        }

        return WFFB_Update() != 0;
    }

    public bool Reinitialize()
    {
        Shutdown();
        return Initialize();
    }

    public void Shutdown()
    {
        if (!m_initialized)
        {
            return;
        }

        WFFB_StopAll();
        WFFB_Shutdown();
        m_initialized = false;
        m_deviceName = string.Empty;
    }

    public bool ApplyOperatingRange(int degrees)
    {
        // DirectInputには回転角の標準設定がない。メーカーのコントロールパネル側で設定する
        return true;
    }

    public void PlaySpring(int offset, int saturation, int coefficient)
    {
        if (!IsConnected) return;

        WFFB_PlaySpring(
            Mathf.Clamp(offset, -100, 100),
            m_tuning.ScaleUnsigned(saturation, m_tuning.springScale),
            m_tuning.ScaleUnsigned(coefficient, m_tuning.springScale)
        );
    }

    public void StopSpring()
    {
        if (m_initialized) WFFB_StopSpring();
    }

    public void PlayDamper(int coefficient)
    {
        if (!IsConnected) return;
        WFFB_PlayDamper(m_tuning.ScaleUnsigned(coefficient, m_tuning.damperScale));
    }

    public void StopDamper()
    {
        if (m_initialized) WFFB_StopDamper();
    }

    public void PlayConstant(int force)
    {
        if (!IsConnected) return;

        int value = m_tuning.ScaleSigned(force, m_tuning.constantScale);

        if (m_tuning.invertConstantForce)
        {
            value = -value;
        }

        m_constantStopPending = false;
        WFFB_PlayConstant(value);
    }

    public void StopConstant()
    {
        if (m_initialized) m_constantStopPending = true;
    }

    public void PlayRoadEffect(EWheelRoadEffect effect, int magnitude, int surfaceWaveType, int periodMs)
    {
        if (!IsConnected) return;

        int value = m_tuning.ScaleUnsigned(magnitude, m_tuning.roadEffectScale);

        switch (effect)
        {
            case EWheelRoadEffect.BumpyRoad:
                WFFB_PlayPeriodic(kBumpyWaveType, value, kBumpyPeriodMs);
                break;

            case EWheelRoadEffect.DirtRoad:
                WFFB_PlayPeriodic(kDirtWaveType, value, kDirtPeriodMs);
                break;

            case EWheelRoadEffect.Surface:
                WFFB_PlayPeriodic(Mathf.Clamp(surfaceWaveType, 0, 2), value, Mathf.Max(1, periodMs));
                break;
        }
    }

    public void StopRoadEffects()
    {
        if (m_initialized) WFFB_StopPeriodic();
    }

    public void StopAll()
    {
        m_constantStopPending = false;
        if (m_initialized) WFFB_StopAll();
    }

    // ==============================
    // 内部処理
    // ==============================

    private bool EnsureInitialized()
    {
        if (m_initialized)
        {
            return true;
        }

        if (m_pluginUnavailable)
        {
            return false;
        }

        IntPtr hwnd = FindGameWindow();

        // ウィンドウがまだ取れない(起動直後・非アクティブ)なら次の機会に再試行
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            m_initialized = WFFB_Initialize(hwnd, m_skipLogitech ? 1 : 0) != 0;

            if (m_initialized)
            {
                WFFB_SetGain(100);
            }
        }
        catch (DllNotFoundException)
        {
            m_pluginUnavailable = true;
            AppLog.LogWarning("[Wheel FFB] WheelFFB_DI.dll が見つかりません。Assets/Plugins/x86_64/ に配置してください。DirectInput FFBは無効になります。");
        }
        catch (EntryPointNotFoundException e)
        {
            m_pluginUnavailable = true;
            AppLog.LogWarning("[Wheel FFB] WheelFFB_DI.dll の関数が見つかりません。DLLのバージョンを確認してください : " + e.Message);
        }

        return m_initialized;
    }

    private void TryConnect(bool force)
    {
        if (!force && Time.unscaledTime < m_nextConnectTime)
        {
            return;
        }

        m_nextConnectTime = Time.unscaledTime + m_reconnectInterval;

        if (WFFB_Connect() == 0)
        {
            return;
        }

        StringBuilder sb = new StringBuilder(260);
        WFFB_GetDeviceName(sb, sb.Capacity);
        m_deviceName = sb.ToString() +
            " (VID:" + WFFB_GetVendorId().ToString("X4") +
            " PID:" + WFFB_GetProductId().ToString("X4") + ")";
    }

    private static IntPtr FindGameWindow()
    {
        IntPtr hwnd = IntPtr.Zero;

        try
        {
            hwnd = GetActiveWindow();

            if (hwnd == IntPtr.Zero)
            {
                hwnd = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            }
        }
        catch (Exception)
        {
            hwnd = IntPtr.Zero;
        }

        return hwnd;
    }
}
