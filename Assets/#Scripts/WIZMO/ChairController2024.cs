/**
 * @file    ChairController2024.cs
 * @brief   椅子の制御用のクラス     
 * @author  22CU0225 豊田達也
 * @date    2024/07/24 作成
 *          2024/08/28 ShiftShockの作成
 *          2024/09/20 Yawの動き作成
 * 
 * メモ
 * 
 * クラス作成者に関して
 * ━━━━━━ ←　ゴゴケンさん？
 * ------       ←　21cu0203_池田柊太さん？
 * ======       ←　22CU0225 豊田達也
 * 
 */

/* WIZMOController の　変数順番
 * 並び順はこちらに準ずる          2024使用関数
 * Roll                                 〇
 * Pitch                                〇
 * Yaw                                  〇
 * Heave                                〇
 * Sway                                 ×
 * Surge                                ×
 * Speed                                〇
 * Accel                                〇s
 */

using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public class ChairController2024 : MonoBehaviour
{
    #region 参照するクラス
    // WIZMO
    [SerializeField]
    private WIZMOController m_controller;
    // Vehicle
    private GameObject m_vehicleObject;
    // VehicleController
    private VehicleController m_vehiclecontroller;
    // Load
    private LoadTransfer m_load = new LoadTransfer();
	#endregion

	//Axis Processing
	[SerializeField,Range(-1.0f, 1.0f)]
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
    [SerializeField, Range(0f, 1.0f)]
	private float m_speed = 0.0f;
    [SerializeField, Range(0f, 1.0f)]
	private float m_accel = 0.0f;

    private float m_maxForce = 3f;              // かかる力の最大値 Debugの値からおおよその値
    
    // ボタン
    [Space]
    [SerializeField]
    private bool m_isChairOperatingKey = false;  // 起動



    [Header("Shift")]
    // shift
    [SerializeField]
    private float m_finalReductionRatio = 3.462f;   // 最終減速比
    [SerializeField]
    private float m_driveWheelRadius = 0.334f;      // 駆動タイヤの半径
    private float m_wheelCircumference;             // タイヤの円周
    private int m_beforeGear;                       // 保存したギア
	
    [SerializeField]
	private float m_smoothTime = 0.7f;          // 時間
    private float m_smoothMaxSpeed = 5000;      // スピード
    private float m_currentVelocityRoll;        // ベロシティRoll
    private float m_currentVelocityPitch;       // ベロシティPitch
    private float m_currentVelocityYaw;         // ベロシティYaw

    // それぞれを適応する割合
    [Header("Ratio")]
    [SerializeField]
    private float m_accelRatio = 1f;            // 加速力適応割合
    [SerializeField]
    private float m_centrifugalRatio = 1f;      // 遠心力適応割合
    [SerializeField]
    private float m_yawRateRatio = 1f;          // ヨーレート適応割合  
    [Space]
    //[SerializeField]
    //private float m_gasPedalInputRatio = 1f;    // アクセル入力適応割合
    [SerializeField]
    private float m_brakePedalInputRatio = 1f;  // ブレーキ入力適応割合
    [SerializeField]
    private float m_shiftShockRatio = 1f;       // シフトショック適応割合

    [Header("Engine")]
    [SerializeField]
    private float m_frequency = 0.16f;          // 振動の周波数

    [SerializeField]
    private float m_engineRatio = 1f;           // 振動の適応割合

    private float m_engineIdleRPM = 1000f;      // 下限RPM
    private float m_engineLimitRPM = 10000f;    // 最高RPM

    // 閾値
    [Header("Threshold")]
    [SerializeField]
    [Range(0, 9000)]
    private float m_minShiftShockRPM = 500.0f;      // RPM閾値
    [Space]
    [SerializeField, Range(0, 1)]
    private float m_thresholdRoll = 0.2f;          // Roll閾値
    [SerializeField, Range(0, 1)]
    private float m_thresholdPitch = 0.2f;          // Pitch閾値
    [SerializeField, Range(0, 0.2f)]
    private float m_thresholdYaw = 0.02f;            // Yaw閾値
    [Space]
    [SerializeField, Range(0, 1)]
    private float m_thresholdClutchInput = 0.8f;    // Clutch閾値

    #region プロパティ
    public bool InGameMove
    {
        set => m_isChairOperatingKey = value;
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

    //public float GasPedalRatio
    //{
    //    get => m_gasPedalInputRatio;
    //    set => m_gasPedalInputRatio = value;
    //}

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

    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Initialize();

    }

    void Initialize()
    {
        m_vehicleObject = transform.root.gameObject;
        m_vehiclecontroller = m_vehicleObject.GetComponent<VehicleController>();
        if (m_controller == null)
        {
			m_controller = GameManager.Instance.WIZMO;
        }
        // ギア取得
        m_beforeGear = m_vehiclecontroller.Transmission.ActiveGear;
        // タイヤの円周
        m_wheelCircumference = m_driveWheelRadius * 2 * Mathf.PI;

        m_load.Vehicle = m_vehicleObject;
        m_load.Initialize();
  //      m_speed = 0.5f;
  //      m_accel = 0.5f;
		////m_speed = 0.75f;
		////m_accel = 0.65f;
		//m_accelRatio = 0.9f;
  //      m_centrifugalRatio = 0.75f;
    }

	private void FixedUpdate()
	{
		if (m_isChairOperatingKey)
		{
			m_load.UpdateProcess();
		}
	}

	// Update is called once per frame
	void Update()
    {
        if (m_isChairOperatingKey)
        {
            UpdateChairValue();
        }
    }

    // ===========================
    //   椅子の値を更新する関数
    // ===========================
    private void UpdateChairValue()
    {
		// 荷重による回転反映
		m_roll = Mathf.SmoothDamp(m_roll, ChairRoll(), ref m_currentVelocityRoll, m_smoothTime, m_smoothMaxSpeed, Time.deltaTime);
        //AppLog.Log("Roll:" + m_roll);
        m_pitch = Mathf.SmoothDamp(m_pitch, ChairPitch(), ref m_currentVelocityPitch, m_smoothTime, m_smoothMaxSpeed, Time.deltaTime);
        m_yaw = Mathf.SmoothDamp(m_yaw, ChiarYaw(), ref m_currentVelocityYaw, m_smoothTime, m_smoothMaxSpeed, Time.deltaTime);

        // シフトショック
        // ギアチェンジが発生
        if (m_beforeGear != m_vehiclecontroller.ActiveGear)
        {
            // クラッチを踏んでいない
            if (m_vehiclecontroller.Clutch > m_thresholdClutchInput)
            {
                // シフトショックの計算結果に割合をかけてピッチに入力
                m_pitch += ShiftShock() * m_shiftShockRatio;
                //AppLog.Log("shift");
            }
        }
        // エンジン
        //EngineRatioChanger();
        m_heave = EngineVibration() * m_engineRatio;

        // 値更新
        ToWIZMOController();
	}

	// ===========================
	// WIZMOControllerに値を渡す
	// =========================== 
    private void ToWIZMOController()
    {
        // 保険の為WIZMOの範囲に制限
        m_roll  = Mathf.Clamp(m_roll,  -1f,1f);
        m_pitch = Mathf.Clamp(m_pitch, -1f,1f);
        m_yaw   = Mathf.Clamp(m_yaw,   -1f,1f);
        m_heave = Mathf.Clamp(m_heave, -1f,1f);
        m_sway  = Mathf.Clamp(m_sway,  -1f,1f);
        m_surge = Mathf.Clamp(m_surge, -1f,1f);
        m_speed = Mathf.Clamp(m_speed,  0f,1f);
        m_accel = Mathf.Clamp(m_accel,  0f,1f);


		m_controller.roll  = m_roll;
		m_controller.pitch = m_pitch;
		m_controller.yaw   = m_yaw;
		m_controller.heave = m_heave;
		m_controller.sway  = m_sway;
		m_controller.surge = m_surge;
        m_controller.speed1_all = m_speed;
		m_controller.accel      = m_accel;
	}

	// ===========================
	// 遠心力の処理(Roll)
	// ===========================
	private float ChairRoll()
    {
        float Centrifugal = 0.0f;

		// 遠心力量と割合を掛けてm_maxForceの範囲からWIZMOの範囲にスケーリングする
		Centrifugal = ScaleValue(m_load.Centrifugal * m_centrifugalRatio, -m_maxForce, m_maxForce, -1f, 1f);
		// 遠心力量をWIZMOの範囲に制限
		Centrifugal = Mathf.Clamp(Centrifugal, -1f, 1f);

		// 閾値以下の場合傾きをなくす
		if (Mathf.Abs(Centrifugal) < m_thresholdRoll) Centrifugal = 0;

        return Centrifugal;
    }

    // ===========================
    // 加速減速時の処理(Pitch)
    // ===========================
    private float ChairPitch()
    {
        float Accelaration = 0.0f;

		// 加速量と割合を掛けてm_maxForceの範囲からWIZMOの範囲にスケーリングする
		Accelaration = ScaleValue(m_load.Accel * m_accelRatio, -m_maxForce, m_maxForce, -1f, 1f);
		// 加速量をWIZMOの範囲に制限
		Accelaration = Mathf.Clamp(Accelaration, -1, 1);

		// 閾値以下の場合傾きをなくす
		if (Mathf.Abs(Accelaration) < m_thresholdPitch) return 0;

        // アクセルの入力量によって傾きを加算
        //if(Accel > 0)
        //{
        //    Accel += m_vehiclecontroller.Accel/ m_gasPedalRatio;
        //}
		// ブレーキの入力量と適応割合を掛ける
		if (Accelaration < 0)
        {
            Accelaration *= m_vehiclecontroller.Brake * m_brakePedalInputRatio;
        }

        return Accelaration;
    }

    // ===========================
    // 車体の回転の処理(Yaw)
    // ===========================
    private float ChiarYaw()
    {
        float YawRate = 0;
		// ヨーレート量と割合を掛けてm_maxForceの範囲からWIZMOの範囲にスケーリングする
		YawRate = ScaleValue(m_load.YawRate * m_yawRateRatio, -m_maxForce, m_maxForce, -1f, 1f);
		// ヨーレート量をWIZMOの範囲に制限
		YawRate = Mathf.Clamp(YawRate, -1, 1);
        // 閾値以下の場合傾きをなくす
        if (Mathf.Abs(YawRate) < m_thresholdYaw) YawRate = 0;

        return YawRate;
    }

    // ==================================
    //  シフトチェンジ時の反動  
    // ==================================
    private float ShiftShock()
    {
        float result = 0.0f;
        float nowRPM = m_vehiclecontroller.EngineRPM;
        float targetRPM = 0.0f;

        // シフト後のギアのRPM計算    RPM = 速度/((タイヤの円周*時速変換用60/1000)/(ギア比* 最終減速比))
        targetRPM = m_vehiclecontroller.KPH / ((m_wheelCircumference * 0.06f) / (m_vehiclecontroller.Transmission.CurrentGearRatio * m_finalReductionRatio));
        result = nowRPM - targetRPM;

        // RPMの差が小さい場合振動を無くす
        if (Mathf.Abs(result) < m_minShiftShockRPM)
        {
            result = 0.0f;
        }
        // 正負取得
        float sign = Mathf.Sign(result);
        // 0～1の割合にする
        result = Mathf.InverseLerp(m_engineIdleRPM, m_engineLimitRPM, Mathf.Abs(result));
        //正負戻す
        result *= sign;

        // ギアの値を更新
        m_beforeGear = m_vehiclecontroller.ActiveGear;

        //AppLog.Log("RPM" + nowRPM);
        //AppLog.Log("目標RPM" + targetRPM);
        //AppLog.Log("シフトショック" + result);

        // 値を返す
        return result;
    }

    #region エンジン

    // ----------------------------------
    // sin波を使用した上下の振動量
    // ----------------------------------
    float EngineVibration()
    {
        // 振幅数を計算する
        float f = 1.0f / m_frequency;

        // sin波を作成する
        float sin = Mathf.Sin(2 * Mathf.PI * f * Time.fixedTime);

        // 値が小さすぎる場合
        if (sin > -0.001f && sin < 0.001f)
        {
            if (sin > 0.0f)
            {
                // 0に補正する
                sin = 0.001f;
            }
            else
            {
                // 0に補正する
                sin = -0.001f;
            }
        }

        // 値を返す
        return sin;
    }
	#endregion

	// 加速に応じて振動比率を変化させる
    private void EngineRatioChanger()
    {
        float Ratio = Mathf.Clamp(m_load.Accel, 0, 1f);

        m_engineRatio = ScaleValue(Ratio, 0, 1, 0, 0.016f);
    }

	// -----------------------------------------
	// 値を指定した範囲にスケーリングする関数
	// 引数1 : スケーリングを行う値
	// 引数2 : 現在の最小値
	// 引数3 : 現在の最大値
	// 引数4 : スケーリングする最小値
	// 引数5 : スケーリングする最大値
	// -----------------------------------------
	private float ScaleValue(float value, float NowMin, float NowMax, float ScaleMin, float ScaleMax)
    {
        // 値を新しい範囲に変換
        return ScaleMin + (value - NowMin) * (ScaleMax - ScaleMin) / (NowMax - NowMin);
    }
}