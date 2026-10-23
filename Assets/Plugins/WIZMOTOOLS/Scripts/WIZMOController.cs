using UnityEngine;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[HelpURL("https://github.com/Wizapply/SIMVR-Tools/wiki")]
[DefaultExecutionOrder(999)]
[AddComponentMenu("WIZMO/WIZMOController")]
public class WIZMOController : MonoBehaviour
{

    [System.Serializable]
    public class WIZMOSystemEventObject : UnityEngine.Events.UnityEvent<string> { }

    //Axis position controls
    [Range(0.0f, 1.0f)]
    public float axis1 = 0.5f;
    [Range(0.0f, 1.0f)]
    public float axis2 = 0.5f;
    [Range(0.0f, 1.0f)]
    public float axis3 = 0.5f;
    [Range(0.0f, 1.0f)]
    public float axis4 = 0.5f;
    [Range(0.0f, 1.0f)]
    public float axis5 = 0.5f;
    [Range(0.0f, 1.0f)]
    public float axis6 = 0.5f;

    //Axis speed/accel controls
    [Range(0.0f, 1.0f)]
    public float speed1_all = 0.667f;
    [Range(0.0f, 1.0f)]
    public float speed2 = 0.667f;
    [Range(0.0f, 1.0f)]
    public float speed3 = 0.667f;
    [Range(0.0f, 1.0f)]
    public float speed4 = 0.667f;
    [Range(0.0f, 1.0f)]
    public float speed5 = 0.667f;
    [Range(0.0f, 1.0f)]
    public float speed6 = 0.667f;
    [Range(0.0f, 1.0f)]
    public float accel = 0.5f;

    //Axis Processing
    [Range(-1.0f, 1.0f)]
    public float roll = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float pitch = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float yaw = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float heave = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float sway = 0.0f;
    [Range(-1.0f, 1.0f)]
    public float surge = 0.0f;

    [Range(0.0f, 1.0f)]
    public float rotationMotionRatio = 0.8f;
    [Range(0.0f, 1.0f)]
    public float gravityMotionRatio = 0.8f;

    [Range(0, 9)]
    public int fanSpeed = 0;
    private int fanSpeedPrev = 0;

    private const int WIZMOHANDLE_ERROR = -1;
    private int wizmoHandle = WIZMOHANDLE_ERROR;

    [SerializeField]
    private bool _axisProcesser = true;

    private CancellationTokenSource m_cts;
    private float m_latestRool;

    // ロックのためのダミーオブジェクト
    private readonly object m_lockObject = new object();

    // デバッグ用カウンタ
    private int m_sendCount = 0;
    // 送信衝突の検出用
    private bool m_isWriting = false;



    private float DebugTime = 0;

    public void Start()
    {
        //axisProcesser = false;

        //Application.targetFrameRate = 10;
        //QualitySettings.vSyncCount = 0; // VSyncをオフにしないとtargetFrameRateが効かない場合があります

        m_cts = new CancellationTokenSource();

        Task.Run(() => SendLoop(m_cts.Token));
    }
    public bool axisProcesser
    {
        get
        {
            return _axisProcesser;
        }
        set
        {
            if (_axisProcesser != value)
            {
                wizmoSetAxisProcessingMode(wizmoHandle, value);
            }
            _axisProcesser = value;
        }
    }
    [SerializeField]
    private bool _isOrigin = false;
    public bool isOrigin
    {
        get
        {
            return _isOrigin;
        }
        set
        {
            if (_isOrigin != value)
            {
                wizmoSetOriginMode(wizmoHandle, value);
            }
            _isOrigin = value;
        }
    }
    public enum SpeedGainMode
    {
        NORMAL = 0,
        VARIABLE,
        MANUAL,
    }
    [SerializeField]
    private SpeedGainMode _speedGainMode = SpeedGainMode.NORMAL;
    public SpeedGainMode speedGainMode
    {
        get
        {
            return _speedGainMode;
        }
        set
        {
            if (_speedGainMode != value)
            {
                wizmoSetSpeedGainMode(wizmoHandle, (int)value);

            }
            _speedGainMode = value;
        }
    }
    [SerializeField]
    private bool _useOpenAtStart = true;
    public bool useOpenAtStart
    {
        get
        {
            return _useOpenAtStart;
        }
        set
        {
            _useOpenAtStart = value;
        }
    }

    //inside var
#pragma warning disable 0414
    [SerializeField]
    private bool appcodePrivateMode = false;
#pragma warning restore 0414

    //Wizmo System

    //Errorイベント
    public WIZMOSystemEventObject wizmoSystemEventObject = new WIZMOSystemEventObject();

    //Serial
    public string AppPassCode = "";
    public string SerialNumberAssign = "";

    #region WIZMO DLL IMPORTER
    [DllImport("wizmo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoOpen(string serialNo);
    [DllImport("wizmo", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoOpenSerialAssign(string appCode, string assign);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoClose(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoGetState(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoGetDevice(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern void wizmoWrite(int handle, Packet packet);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern void wizmoSetAxisProcessingMode(int handle, bool flag);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern void wizmoSetSpeedGainMode(int handle, int value);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern void wizmoSetOriginMode(int handle, bool flag);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool wizmoGetOriginMode(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool wizmoGetAxisProcessingMode(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern int wizmoGetSpeedGainMode(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern System.IntPtr wizmoGetAppCode(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoGetStatusEXT4(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern System.IntPtr wizmoGetVersion(int handle);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    static extern bool wizmoIsRunning(int handle);

    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoGetBackLog(byte[] str, int str_size);
    [DllImport("wizmo", CallingConvention = CallingConvention.Cdecl)]
    static extern int wizmoBackLogDataAvailable();
    #endregion

    static string wizmoGetAppCadeString(int handle)
    {
        // Receive the pointer to Unicde character array
        System.IntPtr pStr = wizmoGetAppCode(handle);
        // Construct a string from the pointer.
        return Marshal.PtrToStringAnsi(pStr);
    }

    //Status
    public const int CanNotFindUsb = 0;
    public const int CanNotFindSimvr = 1;
    public const int CanNotCalibration = 2;
    public const int TimeoutCalibration = 3;
    public const int ShutDownActuator = 4;
    public const int CanNotCertificate = 5;
    public const int Initial = 6;
    public const int Running = 7;
    public const int StopActuator = 8;
    public const int CalibrationRetry = 9;
    private bool stopActuatorTrigger = false;

    [StructLayout(LayoutKind.Sequential)]
    public class Packet
    {
        //Axis position controls
        public float axis1;
        public float axis2;
        public float axis3;
        public float axis4;
        public float axis5;
        public float axis6;

        //Axis speed/accel controls
        public float speed1_all;
        public float speed2;
        public float speed3;
        public float speed4;
        public float speed5;
        public float speed6;
        public float accel;

        //Axis Processing
        public float roll;
        public float pitch;
        public float yaw;
        public float heave;
        public float sway;
        public float surge;

        public float rotationMotionRatio;
        public float gravityMotionRatio;

        public int commandSendCount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string command;
    }
    public Packet packet;

    public void SafeWizmoWirte(Packet packet_data, string callerName)
    {
        lock (m_lockObject)
        {
            if (m_isWriting)
            {
                UnityEngine.Debug.LogWarning($"[WIZMO WARN] 競合検知！ 別のスレッドから同時に呼び出されました！ ({callerName})");
            }

            m_isWriting = true;
            m_sendCount++;

            int currentThreadId = Thread.CurrentThread.ManagedThreadId;
            bool isMainThread = Thread.CurrentThread.ManagedThreadId == 1; // 通常1がメインスレッド

            //UnityEngine.Debug.Log(
            //    $"[WIZMO Send #{m_sendCount}] CallBy: {callerName} | ThreadID: {currentThreadId} (IsMainThread: {isMainThread}) | " +
            //    $"P: {packet_data.pitch:F2}, R: {packet_data.roll:F2}, Y: {packet_data.yaw:F2}"
            //);

            if (isOpened())
            {
                wizmoWrite(wizmoHandle, packet_data);
            }

            m_isWriting = false;
        }
    }

    //Main Program

    // Use this for initialization
    void OnEnable()
    {
        if (_useOpenAtStart)
        {
            OpenSIMVR();
        }
    }

    void OnDestroy()
    {
        if (m_cts != null)
        {
            m_cts.Cancel();
            m_cts.Dispose();
        }
        CloseSIMVR();
    }

    void UpdateBackLog()
    {
        var size = wizmoBackLogDataAvailable();

        if (size > 0)
        {
            byte[] dataArray = new byte[size];
            int iRet = wizmoGetBackLog(dataArray, size);

            if (iRet <= 0 || dataArray == null) //Error
                return;

            //for debug Log
            var backLogSplit = System.Text.Encoding.UTF8.GetString(dataArray, 0, iRet).Split('\n');

            for (int i = 0; i < backLogSplit.Length - 1; i++)
                Debug.Log(backLogSplit[i]);
        }
    }

    //ステート
    void UpdateState()
    {
        var state = wizmoGetState(wizmoHandle);
        // Error if less than Initial
        if (state >= 0 && state <= Initial)
        {
            var output = "";
            switch (state)
            {
                case Initial:
                    output = "Initialize";
                    break;
                case CanNotFindUsb:
                    output = "MachineNotDetected";
                    break;
                case CanNotFindSimvr:
                    output = "InaccessibleToTheMachine";
                    break;
                case CanNotCalibration:
                    output = "ZeroReturnFailure";
                    break;
                case TimeoutCalibration:
                    output = "ErrorReturningToZero";
                    break;
                case ShutDownActuator:
                    output = "ShutDown";
                    break;
                case CanNotCertificate:
                    output = "AuthenticationFailure";
                    break;
                case CalibrationRetry:
                    output = "InternalDisconnectionError";
                    break;
            }
            wizmoSystemEventObject.Invoke(output);
        }
        if (state == StopActuator)
        {
            if (!stopActuatorTrigger)
            {
                wizmoSystemEventObject.Invoke("OverloadError");
                stopActuatorTrigger = true;
            }
        }
    }

    //Update
    Packet UpdateSIMVR()
    {

        //メインスレッドと別スレッドで同時に処理した時に値が破壊されないためにロック
        lock (m_lockObject)
        {



            var packet = new Packet();
            packet.axis1 = axis1;
            packet.axis2 = axis2;
            packet.axis3 = axis3;
            packet.axis4 = axis4;
            packet.axis5 = axis5;
            packet.axis6 = axis6;

            //Axis speed/accel controls
            packet.speed1_all = speed1_all;
            packet.speed2 = speed2;
            packet.speed3 = speed3;
            packet.speed4 = speed4;
            packet.speed5 = speed5;
            packet.speed6 = speed6;
            packet.accel = accel;

            //Axis Processing
            packet.roll = roll;
            packet.pitch = pitch;
            packet.yaw = yaw;
            packet.heave = heave;
            packet.sway = sway;
            packet.surge = surge;

            packet.rotationMotionRatio = rotationMotionRatio;
            packet.gravityMotionRatio = gravityMotionRatio;

            //FAN
            if (fanSpeedPrev != fanSpeed)
            {
                if (fanSpeed < 0) fanSpeed = 0;
                if (fanSpeed > 9) fanSpeed = 9;

                packet.commandSendCount = 1;
                packet.command = "fn" + fanSpeed.ToString() + ";";

                fanSpeedPrev = fanSpeed;
            }
            else
                packet.commandSendCount = 0;
            return packet;
        }
        //wizmoWrite(wizmoHandle, packet);
    }

    void FixedUpdate()
    {
        if (!isOpened())
            return;

        //Update default 50ms
        UpdateState();
        //UpdateSIMVR();
        UpdateBackLog();

    }

    private System.Diagnostics.Stopwatch m_stopwatch;

    private async Task SendLoop(CancellationToken token)
    {
        m_stopwatch = System.Diagnostics.Stopwatch.StartNew();
        long lastTime = m_stopwatch.ElapsedMilliseconds;
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (isOpened())
                {


                    Packet currentPacket = UpdateSIMVR();


                    SafeWizmoWirte(currentPacket, " SendLoop (Task.Run)");
                    // --- デバッグ用時間ログ ---
                    long currentTime = m_stopwatch.ElapsedMilliseconds;
                    long delta = currentTime - lastTime;
                    lastTime = currentTime;

                    if (delta > 20)
                    {
                        UnityEngine.Debug.LogWarning($"[WIZMO Delay] Send Loop Delta delayed: {delta} ms");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"WIZMO Send Exception: {ex.Message}");
            }
            await Task.Delay(1, token);
        }
    }

    //Open/Close
    public void OpenSIMVR()
    {
        if (isOpened())
            return;

        wizmoHandle = wizmoOpenSerialAssign(AppPassCode, SerialNumberAssign);
        if (wizmoHandle >= 0)
        {
            wizmoSetOriginMode(wizmoHandle, _isOrigin);
            wizmoSetAxisProcessingMode(wizmoHandle, _axisProcesser);
            wizmoSetSpeedGainMode(wizmoHandle, (int)_speedGainMode);

            stopActuatorTrigger = false;
        }
        else
        {
            // Connecting Error
            Debug.LogError("The WIZMO plugin is in error.");
            wizmoHandle = WIZMOHANDLE_ERROR;
        }
    }

    public void CloseSIMVR()
    {
        if (!isOpened())
            return;

        if (wizmoClose(wizmoHandle) < 0)
        {
            Debug.LogError("The WIZMO plugin is shutdown error.");
            return;
        }

        wizmoHandle = WIZMOHANDLE_ERROR;
        UpdateBackLog();
    }

    //EXT4
    public int GetStatusEXT4()
    {
        return wizmoGetStatusEXT4(wizmoHandle);
    }

    // 追加 22cu0219鈴木
    public int GetState()
    {
        return wizmoGetState(wizmoHandle);
    }

    public bool isOpened()
    {
        return wizmoHandle >= 0;
    }

    public bool isRunning()
    {
        return wizmoGetState(wizmoHandle) == Running;
    }

    public string GetDeviceName()
    {
        //wizmo_state.h -> WIZMODevice
        string res = "NONE";
        switch (wizmoGetDevice(wizmoHandle))
        {
            case 1:
                res = "SIMVR4DOF";
                break;
            case 2:
                res = "SIMVR6DOF";
                break;
            case 3:
                res = "SIMVR6DOF_MASSIVE";
                break;
            case 4:
                res = "ANTSEAT";
                break;
            case 0:
            default:
                res = "NONE";
                break;
        }

        return res;
    }

    public void SetChairMotion(float targetRoll, float targetPitch, float targetYaw, float targetHeave, float targetSway, float targetSurge, float targetSpeed, float targetAccel)
    {
        lock (m_lockObject)
        {
            this.roll = Mathf.Clamp(targetRoll, -1.0f, 1.0f);
            this.pitch = Mathf.Clamp(targetPitch, -1.0f, 1.0f);
            this.yaw = Mathf.Clamp(targetYaw, -1.0f, 1.0f);
            this.heave = Mathf.Clamp(targetHeave, -1.0f, 1.0f);
            this.sway = Mathf.Clamp(targetSway, -1.0f, 1.0f);
            this.surge = Mathf.Clamp(targetSurge, -1.0f, 1.0f);

            this.speed1_all = Mathf.Clamp01(targetSpeed);
            this.accel = Mathf.Clamp01(targetAccel);
        }
    }
}
