/**
 * @file    ChairController.cs
 * @brief   椅子を制御する
 *          車両Rigidbodyの実挙動を元に、CSV設定値を反映してWIZMOControllerへ値を渡す
 * @author  yuuha terae / edited by 中田樹希
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UniGLTF;
using Unity.VisualScripting;
using UnityEngine;
using VehiclePhysics;

public class ChairController : MonoBehaviour
{
    // ==============================
    // Reference
    // ==============================

    [Header("---Reference---")]

    [SerializeField]
    private WIZMOController m_wizmoController;

    [SerializeField]
    private GameObject m_vehicle;

    [SerializeField]
    private VehicleController m_vehicleController;


    private Rigidbody m_vehicleRigitbody;

    private Car_Engine m_engine;

    // ==============================
    // Active
    // ==============================

    [Header("---Active---")]

    [SerializeField]
    private bool m_isChairActive = false;

    // ==============================
    // Axis
    // ==============================

    [Header("---各軸の値---")]

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_roll = 0.0f;

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_pitch = 0.0f;

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_yaw = 0.0f;

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_heave = 0.0f;

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_sway = 0.0f;

    [SerializeField, Range(-1.0f, 1.0f)]
    private float m_surge = 0.0f;

    [SerializeField, Range(0.0f, 1.0f)]
    private float m_speed = 0.0f;

    [SerializeField, Range(0.0f, 1.0f)]
    private float m_accel = 0.0f;

    [SerializeField]
    private float m_maxForce = 3.0f;

    // ==============================
    // Ratio
    // ==============================

    [Header("---Ratio---")]

    [SerializeField]
    private float m_accelRatio = 1.0f;

    [SerializeField]
    private float m_centrifugalRatio = 1.0f;

    [SerializeField]
    private float m_yawRateRatio = 1.0f;

    [SerializeField]
    private float m_brakePedalInputRatio = 1.0f;

    [SerializeField]
    private float m_shiftShockRatio = 0.5f;
    [SerializeField]
    private float m_pitchMagnification = 0.0f;

    [SerializeField, Range(0.01f, 1f)]
    private float m_accelSmoothing = 0f;

    [SerializeField]
    private float m_forceSmoothTime = 0.1f;

    private float m_CurrentVelocityAccelForce;

    //private float m_VibrationMagnification = 0.0f;

    [SerializeField]
    private float m_VibrationMagnificationMainas = 0.0f;

    // ==============================
    // Shift
    // ==============================

    [Header("---Shift---")]

    [SerializeField]
    private float m_finalReductionRatio = 3.462f;

    [SerializeField]
    private float m_driveWheelRadius = 0.3343f;

    [SerializeField]
    private float m_wheelCircumference;

    [SerializeField]
    private int m_beforeGear;

    // ==============================
    // Engine
    // ==============================

    [Header("---Engine---")]

    [SerializeField]
    private float m_frequency = 0.16f;

    [SerializeField]
    private float m_engineRatio = 1.0f;

    [SerializeField]
    private float m_engineIdleRPM = 1000.0f;

    [SerializeField]
    private float m_engineLimitRPM = 10000.0f;

    // ==============================
    // Smooth
    // ==============================

    [Header("---SmoothDamp---")]
    [Header("---roll---")]

    [SerializeField]
    private float m_setrollSmoothTime = 1.0f;

    [SerializeField]
    private float m_rollSmoothTime = 0f;

    [Header("---yaw---")]

    [SerializeField]
    private float m_setYawSmoothTime = 1.0f;

    [SerializeField]
    private float m_yawSmoothTime = 0f;

    //private float m_rollSmoothTime = 1.0f;


    [Header("---pitch---")]

    [SerializeField]
    private float m_setPitchSmoothTime = 1f;

    [SerializeField]
    private float m_pitchSmoothTime = 0f;




    private float m_smoothTimePram = 1f;

    [SerializeField]
    private float m_smoothTime = 0f;



    [SerializeField]
    private float m_smoothMaxSpeed = 5000.0f;

    [SerializeField]
    private float m_brakeSmoothTime = 0.2f;


    private float m_currentVelocityRoll;
    private float m_currentVelocityPitch;
    private float m_currentVelocityYaw;

    // ==============================
    // Threshold
    // ==============================

    [Header("---Threshold---")]

    [SerializeField, Range(0, 9000)]
    private float m_minShiftShockRPM = 500.0f;

    [SerializeField, Range(0.0f, 1.0f)]
    private float m_thresholdRoll = 0.2f;

    [SerializeField, Range(0.0f, 1.0f)]
    private float m_thresholdPitch = 0.002f;

    [SerializeField, Range(0.0f, 0.2f)]
    private float m_thresholdYaw = 0.02f;

    [SerializeField, Range(0.0f, 1.0f)]
    private float m_thresholdClutchInput = 0.8f;

    // ==============================
    // Stop correction
    // ==============================

    [Header("---Stop Correction---")]

    [SerializeField]
    private bool m_zeroChairMotionWhenStopped = true;

    [SerializeField, Range(0.0f, 5.0f)]
    private float m_stopMotionKPH = 1f;

    // ==============================
    // WIZMO output
    // ==============================

    [Header("---WIZMO Output---")]

    [SerializeField]
    private bool m_enableWizmoOutput = true;

    [SerializeField]
    private bool m_allowNoWizmoForDebug = true;

    [SerializeField]
    private bool m_resetWizmoOutputOnDisable = true;

    // ==============================
    // Debug
    // ==============================

    [Header("---Debug---")]

    [SerializeField]
    private bool m_showChairDebugLog = true;

    [SerializeField, Range(0.1f, 5.0f)]
    private float m_chairDebugLogInterval = 0.5f;

    private float m_chairDebugTimer = 0.0f;

    private bool m_hasWarnedVehicleMissing = false;
    private bool m_hasWarnedRigidbodyMissing = false;
    private bool m_hasWarnedWizmoMissing = false;



    // ==============================
    // CSV
    // ==============================

    private readonly List<string[]> csvChairData = new List<string[]>();

    // ==============================
    // Force values
    // ==============================

    private Vector3 m_velocity;
    private Vector3 m_prevVelocity = Vector3.zero;
    private float m_forwardSpeed;

    private float m_accelForce;
    private float m_centrifugal;
    private float m_yawRate;

    [SerializeField]
    private float m_AccleCoefficient = 1.0f;

    [SerializeField]
    private float m_CentrifugeForceCoefficient = 1.0f;

    // ==============================
    // Road vibration
    // ==============================

    [Header("---Road Vibration---")]

    [SerializeField]
    private float m_startAmplitude = 0.1f;

    [SerializeField]
    private float m_decaySpeed = 3.0f;

    [SerializeField]
    private float m_Hz = 2.0f;

    [SerializeField]
    private float m_phase = 0.0f;

    private float m_startTime;
    private bool m_changeVib = false;

    public float EngineRPM => m_engine.RPM;

    // 後で変更します---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // 後で変更します---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    // 後で変更します---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
    private float m_VibrationMagnificationUp = 0;
    private float m_VibrationMagnificationDown = 0;
    private float m_speedRatio = 0;


    private bool m_pitchVib = false;


    //[SerializeField, Range(-1.0f, 1.0f)]
    private float m_pitchVibrationImpulse = 0.7f;

    [SerializeField, Range(-10.0f, 10.0f)]
    private float m_ShiftShockimpulse = 2f;


    [SerializeField]
    private float m_pitchVibrationTime = 0.0f;


    private bool m_hasAppliedShiftShock = false;

    private float vibrationPitch = 0;

    private float KPH = 0;


    private float m_heaveVelocity = 0.0f;
    private float m_lastNoiseValue = 0.0f;




    private float m_vibrationCooldownTimer = 0.0f;
    //[SerializeField, Range(-1.0f, 1.0f)]

    [Header("---振動調整---")]
    [SerializeField, Range(0.01f, 10.0f)]
    private float m_heaveVib = 2f;


    //[SerializeField, Range(0.01f, 1.0f)]
    private float m_pitchVibMin = 0.1f;

    [SerializeField, Range(0.01f, 1.0f)]
    private float m_heaveVibRatio = 0.03f;


    [SerializeField, Range(0.01f, 10.0f)]
    private float m_minCooldownMin = 0.08f;

    [SerializeField, Range(0.01f, 10.0f)]
    private float m_minCooldownMax = 0.5f;

    [SerializeField, Range(0.01f, 10.0f)]
    private float maxCooldownMin = 0.20f;

    [SerializeField, Range(0.01f, 10.0f)]
    private float maxCooldownmax = 1.5f;




    // ==============================
    // Properties
    // ==============================

    public bool InGameMove
    {
        get => m_isChairActive;
        set => m_isChairActive = value;
    }

    public float Speed
    {
        get => m_speed;
        set => m_speed = value;
    }

    public float Accel
    {
        get => m_accel;
        set => m_accel = value;
    }

    public float SmoothTime
    {
        get => m_smoothTime;
        set => m_smoothTime = value;
    }

    public float AccelRatio
    {
        get => m_accelRatio;
        set => m_accelRatio = value;
    }

    public float CentrifugalRatio
    {
        get => m_centrifugalRatio;
        set => m_centrifugalRatio = value;
    }

    public float YawRateRatio
    {
        get => m_yawRateRatio;
        set => m_yawRateRatio = value;
    }

    public float BrakePedalRatio
    {
        get => m_brakePedalInputRatio;
        set => m_brakePedalInputRatio = value;
    }

    public float ShiftShockRatio
    {
        get => m_shiftShockRatio;
        set => m_shiftShockRatio = value;
    }

    public float EngineFrequency
    {
        get => m_frequency;
        set => m_frequency = value;
    }

    public float EngineRatio
    {
        get => m_engineRatio;
        set => m_engineRatio = value;
    }

    public float DebugRoll => m_roll;
    public float DebugPitch => m_pitch;
    public float DebugYaw => m_yaw;
    public float DebugHeave => m_heave;
    public float DebugAccelForce => m_accelForce;
    public float DebugCentrifugal => m_centrifugal;
    public float DebugYawRate => m_yawRate;

    // ==============================
    // Unity events
    // ==============================

    private void Start()
    {
        Initialize();
        LoadCSV_ChairSettings_Low();
    }

    private void FixedUpdate()
    {
        if (!m_isChairActive)
        {
            return;
        }

        if (!EnsureRequiredReferences())
        {
            return;
        }

        // 一定以下の速度なら止まっているとみなす
        float currentKPH = m_vehicleController.KPH;

        if (currentKPH <= m_stopMotionKPH)
        {
            KPH = 0;
        }

        else
        {
            KPH = currentKPH;
        }

        // 椅子の値更新
        UpdateChairValue();
        UpdateCenterOfGravityForce();
    }

    private void Update()
    {
        // F6で倍率を弱に
        if (Input.GetKeyDown(KeyCode.F6))
        {
            LoadCSV_ChairSettings_Low();
            Debug.Log("[ChairController] 弱設定をCSVから読み込みました。");
        }

        //// F7で倍率を中に
        //if (Input.GetKeyDown(KeyCode.F7))
        //{
        //    LoadCSV_ChairSettings_Medium();
        //    Debug.Log("[ChairController] 中設定をCSVから読み込みました。");
        //}

        //// F8で倍率を強に
        //if (Input.GetKeyDown(KeyCode.F8))
        //{
        //    LoadCSV_ChairSettings_High();
        //    Debug.Log("[ChairController] 強設定をCSVから読み込みました。");
        //}


        if (!m_isChairActive)
        {
            DebugChairValues();
            return;
        }

        if (!EnsureRequiredReferences())
        {
            DebugChairValues();
            return;
        }



        DebugChairValues();
    }

    private void OnDisable()
    {
        if (m_resetWizmoOutputOnDisable)
        {
            ForceStopWizmoOutput();
        }
    }

    private void OnDestroy()
    {
        if (m_resetWizmoOutputOnDisable)
        {
            ForceStopWizmoOutput();
        }
    }

    // ==============================
    // Initialize / references
    // ==============================

    private void Initialize()
    {
        ResolveReferences();

        if (m_vehicleController != null && m_vehicleController.Transmission != null)
        {
            m_beforeGear = m_vehicleController.Transmission.ActiveGear;
        }


        m_wheelCircumference = m_driveWheelRadius * 2.0f * Mathf.PI;

        if (m_vehicleRigitbody != null)
        {
            m_prevVelocity = m_vehicleRigitbody.linearVelocity;
        }
    }

    private void ResolveReferences()
    {
        if (m_vehicleController == null && m_vehicle != null)
        {
            m_vehicleController = m_vehicle.GetComponent<VehicleController>();
        }

        if (m_vehicleController == null)
        {
            m_vehicleController = GetComponentInParent<VehicleController>();
        }

        if (m_vehicleController == null)
        {
#if UNITY_2023_1_OR_NEWER

            m_vehicleController = FindFirstObjectByType<VehicleController>();
#else
            m_vehicleController = FindObjectOfType<VehicleController>();
#endif
        }

        if (m_vehicle == null && m_vehicleController != null)
        {
            m_vehicle = m_vehicleController.gameObject;
        }

        if (m_vehicleRigitbody == null && m_vehicle != null)
        {
            m_vehicleRigitbody = m_vehicle.GetComponent<Rigidbody>();
        }

        if (m_wizmoController == null)
        {
            if (GameManager.Instance != null)
            {
                m_wizmoController = GameManager.Instance.WIZMO;
            }
        }

        if (m_wizmoController == null)
        {
#if UNITY_2023_1_OR_NEWER
            m_wizmoController = FindFirstObjectByType<WIZMOController>();
#else
            m_wizmoController = FindObjectOfType<WIZMOController>();
#endif
        }


        if (m_engine == null)
        {
            m_engine = FindFirstObjectByType<Car_Engine>();
        }
    }

    private bool EnsureRequiredReferences()
    {
        ResolveReferences();

        if (m_vehicleController == null)
        {
            if (!m_hasWarnedVehicleMissing)
            {
                Debug.LogWarning("[ChairController] VehicleController が取得できません。Vehicle参照を確認してください。");
                m_hasWarnedVehicleMissing = true;
            }

            return false;
        }

        m_hasWarnedVehicleMissing = false;

        if (m_vehicleRigitbody == null)
        {
            if (!m_hasWarnedRigidbodyMissing)
            {
                Debug.LogWarning("[ChairController] Vehicle Rigidbody が取得できません。VehicleにRigidbodyが付いているか確認してください。");
                m_hasWarnedRigidbodyMissing = true;
            }

            return false;
        }

        m_hasWarnedRigidbodyMissing = false;

        if (m_wizmoController == null && !m_allowNoWizmoForDebug)
        {
            if (!m_hasWarnedWizmoMissing)
            {
                Debug.LogWarning("[ChairController] WIZMOController が取得できません。GameManagerのWIZMO欄を確認してください。");
                m_hasWarnedWizmoMissing = true;
            }
        }

        if (m_wizmoController != null)
        {
            m_hasWarnedWizmoMissing = false;
        }

        return true;
    }

    // ==============================
    // Main chair process

    // ==============================

    private float m_lastKPH;
    private void UpdateChairValue()
    {

        VibrationMagnification();

        //
        //m_smoothTime = m_setSmoothTime * m_smoothTimePram;

        m_rollSmoothTime = m_setrollSmoothTime * m_smoothTimePram;

        m_yawSmoothTime = m_setYawSmoothTime * m_smoothTimePram;

        m_pitchSmoothTime = m_setPitchSmoothTime * m_smoothTimePram;

        // 停止する速度かどうかのフラグ
        bool isStopped = m_zeroChairMotionWhenStopped && (KPH <= m_stopMotionKPH);


        // 坂道の角度を格納
        float slopeFactor = SlopeAangle();

        // 停止なら速度を0になるように目標値を決める。
        float targetRool = isStopped ? 0.01f : ChairRoll();
        float rawPitch = isStopped ? /*slopeFactor / 10*/0.01f : ChairPitch(slopeFactor);
        float targetPitch = Mathf.Clamp(rawPitch, -0.3f, 0.8f);

        float targetYaw = isStopped ? 0.01f : ChiarYaw();


        // 停止していない場合、pitchのバイブレーション処理を実行
        if (!isStopped)
        {
            // 振動の周期
            if (CheckRoadVibrationTrigger(out float noiseDelta, out bool isPitchTriggered))
            {
                //heaveの振動
                ApplyHeaveVibration(noiseDelta);
                //AutoPitchVibrationImpulse(noiseDelta, isPitchTriggered);
            }
        }

        else
        {
            m_hasAppliedShiftShock = false;
        }



        //CheckAccelerationEnd();


        float decelRate = (m_lastKPH - KPH) / Time.deltaTime;
        m_lastKPH = KPH;


        // 止まろうしているかのフラグ
        bool isRapidDeceleration = decelRate > 50.0f || (m_vehicleController.Brake > 0.2f);

        float dynamicPitchSmoothTime = isRapidDeceleration ? m_brakeSmoothTime : m_pitchSmoothTime;

        //Debug.Log("isStopped" + isStopped);



        //// シフトショック(シフトチェンジ時の振動)
        //if (CanApplyShiftShock())
        //{
        //    if (!m_hasAppliedShiftShock)
        //    {
        //        m_currentVelocityPitch += CalculateShiftShock() * m_shiftShockRatio;



        //        m_hasAppliedShiftShock = true;
        //    }
        //}

        //else
        //{
        //    m_hasAppliedShiftShock = false;
        //    m_beforeGear = GetActiveGear();
        //}

        m_roll = Mathf.SmoothDamp(
            m_roll,
            targetRool,
            ref m_currentVelocityRoll,
            m_rollSmoothTime,
            m_smoothMaxSpeed,
            Time.deltaTime
        );

        m_pitch = Mathf.SmoothDamp(
            m_pitch,
            targetPitch,
            ref m_currentVelocityPitch,
            dynamicPitchSmoothTime,
            m_smoothMaxSpeed,
            Time.deltaTime
        );

        m_yaw = Mathf.SmoothDamp(
            m_yaw,
            targetYaw,
            ref m_currentVelocityYaw,
            m_yawSmoothTime,
            m_smoothMaxSpeed,
            Time.deltaTime
        );

        //m_heaveVelocity *= 2;


        m_heave = Mathf.SmoothDamp(
            m_heave,
            0.001f,
            ref m_heaveVelocity,
            dynamicPitchSmoothTime,
            10.0f,
            Time.deltaTime
        );

        if (m_pitch <= -0.3f && m_currentVelocityPitch < 0.0f) m_currentVelocityPitch = 0.0f;
        if (m_pitch >= 0.8f && m_currentVelocityPitch > 0.0f) m_currentVelocityPitch = 0.0f;

        // 椅子の値を丸め込む
        ClampChairValues();
        SendWizmoValues();
    }

    /// <summary>
    /// 椅子の値を丸め込む
    /// </summary>
    private void ClampChairValues()
    {
        m_roll = Mathf.Clamp(m_roll, -0.7f, 0.7f);
        m_pitch = Mathf.Clamp(m_pitch, -0.3f, 0.8f);
        m_yaw = Mathf.Clamp(m_yaw, -1.0f, 1.0f);
        m_heave = Mathf.Clamp(m_heave, -1.0f, 1.0f);
        m_sway = Mathf.Clamp(m_sway, -1.0f, 1.0f);
        m_surge = Mathf.Clamp(m_surge, -1.0f, 1.0f);

        m_speed = Mathf.Clamp01(m_speed);
        m_accel = Mathf.Clamp01(m_accel);
    }

    /// <summary>
    /// 坂道の角度を取得
    /// </summary>
    private float SlopeAangle()
    {
        float pitchAngle = m_vehicle.transform.eulerAngles.x;
        if (pitchAngle > 180) pitchAngle -= 360f;

        float slopeFactor = Mathf.Clamp(pitchAngle / 90f, -1f, 1);

        return -slopeFactor;
    }
    private void SendWizmoValues()
    {
        if (!m_enableWizmoOutput)
        {
            return;
        }

        if (m_wizmoController == null)
        {
            if (!m_allowNoWizmoForDebug && !m_hasWarnedWizmoMissing)
            {
                Debug.LogWarning("[ChairController] WIZMOController が未設定のため、椅子へ値を送れません。");
                m_hasWarnedWizmoMissing = true;
            }

            return;
        }

        // WIZMOControllerから関数を呼び出して渡す
        m_wizmoController.SetChairMotion(m_roll, m_pitch, m_yaw, m_heave, m_sway, m_surge, m_speed, m_accel);

    }

    private void ForceStopWizmoOutput()
    {
        if (m_wizmoController == null)
        {
            return;
        }

        // WIZMOControllerから関数を呼び出して渡す
        m_wizmoController.SetChairMotion(0, 0, 0, 0, 0, 0, 0, 0);

    }

    // ==============================
    // Chair axis calculations
    // ==============================

    private float ChairRoll()
    {
        float centrifugal = ScaleValue(
            m_centrifugal * m_centrifugalRatio,
            -m_maxForce,
            m_maxForce,
            -1.0f,
            1.0f
        );

        centrifugal = Mathf.Clamp(centrifugal, -1.0f, 1.0f);

        if (Mathf.Abs(centrifugal) < m_thresholdRoll)
        {
            centrifugal = 0.0f;
        }

        return -centrifugal;
    }
    private float m_prevAccelForce = 0f;
    public bool IsAccelerationEnding { get; private set; }



    private float ChairPitch(float sloopeFactor)
    {
        float acceleration = ScaleValue(
            m_accelForce * m_accelRatio,
            -m_maxForce,
            m_maxForce,
            -1.0f,
            1.0f
        );

        //  acceleration += sloopeFactor;

        acceleration = Mathf.Clamp(acceleration, -1.0f, 1.0f);

        if (Mathf.Abs(acceleration) < m_thresholdPitch)
        {
            return 0.0f;
        }

        // 坂の角度を入れる
        acceleration += SlopeAangle();

        if (acceleration < 0.0f)
        {
            acceleration *= (1.0f + m_vehicleController.Brake * m_brakePedalInputRatio);
        }

        // 振動があるなら

        acceleration += vibrationPitch;

        return acceleration;
    }

    private float ChiarYaw()
    {
        float yawDegPerSec = m_yawRate * Mathf.Rad2Deg;

        float adjustedYawRate = yawDegPerSec * m_yawRateRatio;

        float yawRate = ScaleValue(
            m_yawRate * m_yawRateRatio,
            //-45f,
            //45f,
            -m_maxForce,
            m_maxForce,
            -1.0f,
            1.0f
        );

        yawRate = Mathf.Clamp(yawRate, -1.0f, 1.0f);

        if (Mathf.Abs(yawRate) < m_thresholdYaw)
        {
            yawRate = 0.0f;
        }

        int currentGear = GetActiveGear();

        return yawRate;
    }

    private bool CanApplyShiftShock()
    {
        if (m_vehicleController == null)
        {
            return false;
        }

        if (m_vehicleController.Transmission == null)
        {
            return false;
        }

        return m_beforeGear != m_vehicleController.ActiveGear &&
            m_vehicleController.Clutch > m_thresholdClutchInput;
    }

    private int GetActiveGear()
    {
        if (m_vehicleController == null) return 0;
        if (m_vehicleController.Transmission != null) return m_vehicleController.Transmission.ActiveGear;
        return m_vehicleController.ActiveGear;
    }


    /// <summary>
    /// シフトショック(ベロシティに一瞬値を加える。処理が簡単なうえ負担も少ない。はず)
    /// </summary>
    private float CalculateShiftShock()
    {

        //バウンドの強さを求める
        return m_ShiftShockimpulse;// UnityEngine.Random.Range(0.3f, 0.5f) * m_VibrationMagnificationDown;

    }

    // ==============================
    // Vehicle force calculations
    // ==============================

    private void UpdateCenterOfGravityForce()
    {
        m_velocity = m_vehicleRigitbody.linearVelocity;

        LongitudinalLoadTransfer();
        LateralLoadTransfer();
        YawRateTransfar();

        m_prevVelocity = m_velocity;
    }

    private float m_smoothedRawAccel = 0f;
    private float m_smoothedRawAccelVelocity = 0f;
    private void LongitudinalLoadTransfer()
    {
        Vector3 forward = m_vehicle.transform.forward;

        float forwardSpeed = Vector3.Dot(m_velocity, forward);
        float prevForwardSpeed = Vector3.Dot(m_prevVelocity, forward);

        m_forwardSpeed = Mathf.Abs(forwardSpeed); // デバッグ用等の絶対値

        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);

        float rawAcceleration = (forwardSpeed - prevForwardSpeed) / deltaTime / Physics.gravity.magnitude;

        m_smoothedRawAccel = Mathf.SmoothDamp(
        m_smoothedRawAccel,
        rawAcceleration,
        ref m_smoothedRawAccelVelocity,
        0.08f
    );

        if (Mathf.Abs(m_smoothedRawAccel) < 0.05f)
        {
            rawAcceleration = 0;
        }

        float targetAccelForce = m_smoothedRawAccel * m_AccleCoefficient;

        m_accelForce = Mathf.SmoothDamp(
            m_accelForce,
            targetAccelForce,
            ref m_CurrentVelocityAccelForce,
            m_forceSmoothTime
            );
    }

    private void LateralLoadTransfer()
    {
        Vector3 sideway = m_vehicle.transform.right;

        float sidewayVelocity = Mathf.Abs(Vector3.Dot(m_velocity, sideway));
        float prevSidewayVelocity = Mathf.Abs(Vector3.Dot(m_prevVelocity, sideway));

        Vector3 localAngularVelocity =
            m_vehicle.transform.InverseTransformDirection(m_vehicleRigitbody.angularVelocity);

        float horizontalDecision = -Mathf.Sign(localAngularVelocity.y);

        if (Mathf.Approximately(horizontalDecision, 0.0f))
        {
            horizontalDecision = -Mathf.Sign(Vector3.Dot(m_velocity, sideway));
        }

        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);

        float lateralAcceleration =
            (sidewayVelocity - prevSidewayVelocity) /
            deltaTime /
            Physics.gravity.magnitude;

        if (lateralAcceleration > 0.0f)
        {
            lateralAcceleration *= -1.0f;
        }

        float centrifugalForce = horizontalDecision * lateralAcceleration;

        m_centrifugal = centrifugalForce * m_CentrifugeForceCoefficient;
    }

    private void YawRateTransfar()
    {
        m_yawRate = m_vehicleRigitbody.angularVelocity.y;
    }

    // ==============================
    // Road vibration
    // ==============================

    private float EngineVibration()
    {


        float frequency = Mathf.Max(m_frequency, 0.1f);
        //float f = 1.0f / frequency;

        float sin = Mathf.Sin(2.0f * Mathf.PI * frequency * Time.fixedTime);

        //if (sin > -0.001f && sin < 0.001f)
        if (Mathf.Abs(sin) < 0.001f)
        {
            sin = sin > 0.0f ? 0.001f : -0.001f;
        }

        return Mathf.Lerp(0.05f, 0.05f, sin);
    }

    public void SetVibrationParam(
        float startAmplitude,
        float decaySpeed,
        float frequency,
        float phase
    )
    {
        m_startAmplitude = startAmplitude;
        m_decaySpeed = decaySpeed;
        m_Hz = frequency;
        m_phase = phase;
        m_startTime = Time.time;
    }

    /// <summary>
    /// バウンド
    /// </summary>
    /// 



    private void AutoPitchVibrationImpulse(float noiseDelta, bool isPitchTriggered)
    {
        if (isPitchTriggered)
        {
            //バウンドの強さを求める
            m_currentVelocityPitch = noiseDelta * m_pitchVibrationImpulse * m_VibrationMagnificationDown;// UnityEngine.Random.Range(0.3f, 0.5f) * m_VibrationMagnificationDown;

        }
    }

    private Coroutine m_vibrationCoroutine = null;

    // 振動の倍率
    private void VibrationMagnification()
    {
        // これを基準
        m_VibrationMagnificationUp = 1;
        m_VibrationMagnificationDown = 1;

        m_speedRatio = Mathf.InverseLerp(1.0f, 150.0f, KPH);    // 速度を0.0から1に正規化

        // 速度が高くなるほど倍率を上げる
        m_VibrationMagnificationUp = Mathf.Lerp(0.1f, 1.0f, m_speedRatio);
        //速度が速くなるほど倍率を下げる
        m_VibrationMagnificationDown = Mathf.Lerp(1.0f, 0.1f, m_speedRatio);

        // 一定以下にしない
        m_VibrationMagnificationUp = Mathf.Clamp(m_VibrationMagnificationUp, 0.1f, 1.0f);
        m_VibrationMagnificationDown = Mathf.Clamp(m_VibrationMagnificationDown, 0.1f, 1.0f);
    }



    // 路面振動のトリガー
    private bool CheckRoadVibrationTrigger(out float noiseDelta, out bool isPitchTriggered)
    {
        noiseDelta = 0.0f;
        isPitchTriggered = false;

        // 振動倍率の決定
        VibrationMagnification();

        // クールダウンタイマー更新
        if (m_vibrationCooldownTimer > 0.0f)
        {
            m_vibrationCooldownTimer -= Time.deltaTime;
        }

        // ノイズ
        float slowNoise = Mathf.PerlinNoise(Time.time * 0.5f, 99.0f);
        // 速度に応じて周波数補完
        float baseFrequency = Mathf.Lerp(5.0f, 20.0f, m_speedRatio);
        // 周波数に変動
        float noiseFrequency = baseFrequency * Mathf.Lerp(0.7f, 1.3f, slowNoise);

        // ノイズでなんかやかんや
        float currentNoise = Mathf.PerlinNoise(Time.time * noiseFrequency, 0.0f);
        noiseDelta = currentNoise - m_lastNoiseValue;
        m_lastNoiseValue = currentNoise;

        // ノイズが一定を超え、クールダウン終了時に
        if (noiseDelta > m_heaveVibRatio && m_vibrationCooldownTimer <= 0.0f)
        {

            // 一定の確率で
            if (UnityEngine.Random.value > 0.4f)
            {
                isPitchTriggered = true;
            }

            // 符号を返す
            noiseDelta = Mathf.Sign(noiseDelta);

            // 速度に応じてクールダウンの最小と最大を決め、そこからさらにランダムレンジ
            float minCooldown = Mathf.Lerp(m_minCooldownMax, m_minCooldownMin, m_speedRatio);
            float maxCooldown = Mathf.Lerp(maxCooldownmax, maxCooldownMin, m_speedRatio);
            m_vibrationCooldownTimer = UnityEngine.Random.Range(minCooldown, maxCooldown);

            return true;
        }

        return false;
    }

    private void ApplyHeaveVibration(float noiseDelta)
    {
        float randomFactor = UnityEngine.Random.Range(0.8f, 1.2f);
        float amplitudeScale = Mathf.Lerp(1.0f, 0.2f, m_speedRatio);
        float impulse = noiseDelta * m_heaveVib * randomFactor * amplitudeScale * m_VibrationMagnificationDown;

        m_heaveVelocity = Mathf.Clamp(m_heaveVelocity + impulse, -7f, 7f);

        if (Mathf.Abs(m_heaveVelocity) <= 0.1f)
        {
            m_heaveVelocity = Mathf.Sign(m_heaveVelocity) * 0.08f;
        }

        else
        {
            m_heaveVelocity = Mathf.Sign(m_heaveVelocity);
        }

    }



    // ==============================
    // Utility
    // ==============================

    private float ScaleValue(
        float value,
        float nowMin,
        float nowMax,
        float scaleMin,
        float scaleMax
    )
    {
        if (Mathf.Approximately(nowMin, nowMax))
        {
            return scaleMin;
        }

        return scaleMin + (value - nowMin) * (scaleMax - scaleMin) / (nowMax - nowMin);
    }

    private void DebugChairValues()
    {
        if (!m_showChairDebugLog)
        {
            return;
        }

        m_chairDebugTimer -= Time.unscaledDeltaTime;

        if (m_chairDebugTimer > 0.0f)
        {
            return;
        }

        m_chairDebugTimer = m_chairDebugLogInterval;

        float kph = -1.0f;
        float steering = 0.0f;
        float accelInput = 0.0f;
        float brakeInput = 0.0f;
        float rigidbodySpeed = -1.0f;
        float angularY = 0.0f;
        bool wizmoOpened = false;

        if (m_vehicleController != null)
        {
            kph = m_vehicleController.KPH;
            steering = m_vehicleController.Steering;
            accelInput = m_vehicleController.Accel;
            brakeInput = m_vehicleController.Brake;
        }

        if (m_vehicleRigitbody != null)
        {
            rigidbodySpeed = m_vehicleRigitbody.linearVelocity.magnitude;
            angularY = m_vehicleRigitbody.angularVelocity.y;
        }

        if (m_wizmoController != null)
        {
            wizmoOpened = m_wizmoController.isOpened();
        }

        Debug.Log(
            "[ChairController] " +
            "Active:" + m_isChairActive +
            " WIZMO:" + (m_wizmoController != null) +
            " WIZMOOpened:" + wizmoOpened +
            " KPH:" + kph.ToString("F2") +
            " RbSpeed:" + rigidbodySpeed.ToString("F2") +
            " Steering:" + steering.ToString("F3") +
            " AccelInput:" + accelInput.ToString("F3") +
            " BrakeInput:" + brakeInput.ToString("F3") +
            " AccelForce:" + m_accelForce.ToString("F3") +
            " Centrifugal:" + m_centrifugal.ToString("F3") +
            " YawRate:" + m_yawRate.ToString("F3") +
            " AngularY:" + angularY.ToString("F3") +
            " | Roll:" + m_roll.ToString("F3") +
            " Pitch:" + m_pitch.ToString("F3") +
            " Yaw:" + m_yaw.ToString("F3") +
            " Heave:" + m_heave.ToString("F3") +
            " Speed:" + m_speed.ToString("F3") +
            " ChairAccel:" + m_accel.ToString("F3")
        );
    }

    // ==============================
    // CSV
    // ==============================

    public void LoadCSV_ChairSettings_Low()
    {
        LoadCSVRow(1, "Low");
    }

    public void LoadCSV_ChairSettings_Medium()
    {
        LoadCSVRow(2, "Medium");
    }

    public void LoadCSV_ChairSettings_High()
    {
        LoadCSVRow(3, "High");
    }

    private void LoadCSVRow(int rowIndex, string label)
    {
        string dataPath = Path.Combine(Application.streamingAssetsPath, "ChairSettings.csv");

        if (!File.Exists(dataPath))
        {
            Debug.LogError("[ChairController] CSVファイルが存在しません: " + dataPath);
            return;
        }

        csvChairData.Clear();

        using (StreamReader dataReader = new StreamReader(dataPath))
        {
            while (!dataReader.EndOfStream)
            {
                string lineData = dataReader.ReadLine();

                if (string.IsNullOrWhiteSpace(lineData))
                {
                    continue;
                }

                string[] lineSplit = lineData.Split(',');
                csvChairData.Add(lineSplit);
            }
        }

        if (csvChairData.Count <= rowIndex)
        {
            Debug.LogError(
                "[ChairController] CSVに必要な行が足りません。必要行 index:" +
                rowIndex +
                " 現在行数:" +
                csvChairData.Count
            );
            return;
        }

        string[] row = csvChairData[rowIndex];

        if (row.Length < 13)
        {
            Debug.LogError(
                "[ChairController] CSVの列数が足りません。必要列数:13 現在列数:" +
                row.Length
            );
            return;
        }

        try
        {
            m_speed = ParseCSVFloat(row[0]);
            m_accel = ParseCSVFloat(row[1]);
            m_accelRatio = ParseCSVFloat(row[2]);
            m_centrifugalRatio = ParseCSVFloat(row[3]);
            m_yawRateRatio = ParseCSVFloat(row[4]);
            m_brakePedalInputRatio = ParseCSVFloat(row[5]);
            m_shiftShockRatio = ParseCSVFloat(row[6]);
            m_smoothTimePram = ParseCSVFloat(row[7]);
            m_minShiftShockRPM = ParseCSVFloat(row[8]);
            m_thresholdRoll = ParseCSVFloat(row[9]);
            m_thresholdPitch = ParseCSVFloat(row[10]);
            m_thresholdYaw = ParseCSVFloat(row[11]);
            m_thresholdClutchInput = ParseCSVFloat(row[12]);

            ClampChairValues();

            Debug.Log(
                "[ChairController] CSV " + label + " Loaded " +
                "Speed:" + m_speed +
                " Accel:" + m_accel +
                " AccelRatio:" + m_accelRatio +
                " CentrifugalRatio:" + m_centrifugalRatio +
                " YawRateRatio:" + m_yawRateRatio +
                " BrakePedalInputRatio:" + m_brakePedalInputRatio +
                " ShiftShockRatio:" + m_shiftShockRatio +
                " SmoothTime:" + m_smoothTime +
                " ThresholdRoll:" + m_thresholdRoll +
                " ThresholdPitch:" + m_thresholdPitch +
                " ThresholdYaw:" + m_thresholdYaw
            );
        }
        catch (Exception e)
        {
            Debug.LogError("[ChairController] CSVのパースに失敗しました: " + e.Message);
        }
    }

    private float ParseCSVFloat(string value)
    {
        return float.Parse(value.Trim(), CultureInfo.InvariantCulture);
    }
}