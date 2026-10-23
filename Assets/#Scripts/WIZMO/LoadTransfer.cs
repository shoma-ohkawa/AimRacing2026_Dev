/**
 * @file    LoadTransfer.cs
 * @brief   重心にかかる力を計算する機能        
 * @author  22CU0225豊田達也
 * @date    2024/09/01 作成

 */
using UnityEngine;

public class LoadTransfer
{
    #region 変数

    [Header("Vehicle")]
    [SerializeField] private GameObject m_vehicle = null;
    // VehicleController
    private VehicleController m_vehicleController;

    // 車の剛体
    private Rigidbody m_vehicleRigitbody;   // 車の剛体

    [Space]
#pragma warning disable CS0414 // 加減速/遠心力の計算式に未適用のInspector係数(適用すると挙動が変わるため削除せず保持)
    [SerializeField] private float m_AccleCoefficient = 1f;             // 加減速用係数
    [SerializeField] private float m_CentrifugeForceCoefficient = 1f;   // 遠心力用係数
#pragma warning restore CS0414

    // 速度
    private Vector3 m_velocity;                 // 速度
    private Vector3 m_prevVelocity;             // 前回の速度

    private float m_forwardSpeed;               // 前方加速度

    // 重心にかかる力
    private float m_accel;          // 加減速
    private float m_centrifugal;    // 遠心力
    private float m_YawRate;        // ヨーレート


    #endregion

    #region プロパティ
    public GameObject Vehicle
    {
        set { m_vehicle = value; }
    }

    public float ForwardSpeed => m_forwardSpeed;
    public float Accel => m_accel;
    public float Centrifugal => m_centrifugal;
    public float YawRate => m_YawRate;
    #endregion


    // 初期設定
    public void Initialize()
    {
        // 初期化
        m_prevVelocity = Vector3.zero;
        m_vehicleController = m_vehicle.GetComponent<VehicleController>();
        m_vehicleRigitbody = m_vehicle.GetComponent<Rigidbody>();
    }

    // 更新
    public void UpdateProcess()
    {
        #region 車が正しく設定されているか確認
        if (m_vehicle.tag == null)  // Nullチェック
        {
            AppLog.LogError("荷重移動で使う車が設定されていません/Load2024.cs/");
            return;
        }
		#endregion

		// 速度の取得
		m_velocity = m_vehicleRigitbody.linearVelocity;

        // 計算
        LongitudinalLoadTransfer();
        LateralLoadTransfer();

        // 取得
        YawRateTransfar();

		// 速度の保存
		m_prevVelocity = m_velocity;
    }


    // 加速力計算
    private void LongitudinalLoadTransfer()
    {
        // 正面方向ベクトル
        Vector3 forward = m_vehicle.transform.forward;
        // 正面方向ベクトルと速度ベクトルの内積を正面方向ベクトルとかけて前方速度ベクトルを取得
        Vector3 forwardVelocity = Vector3.Dot(m_velocity, forward) * forward;
        m_forwardSpeed = forwardVelocity.magnitude;

        Vector3 prevForwardVelocity = Vector3.Dot(m_prevVelocity, forward) * forward;
        // 加速度 [G]　[Vf - V0f / t / g ]
        float acceleration = (forwardVelocity.magnitude - prevForwardVelocity.magnitude) / Time.deltaTime / Physics.gravity.magnitude;
        m_accel = acceleration; // 値保持
        //AppLog.Log("加速度" + acceleration);
    }

    // 遠心力計算
    private void LateralLoadTransfer()
    {
        // 右方向ベクトル
        Vector3 sideway = m_vehicle.transform.right;
        // 右方向ベクトルと速度ベクトルの内積を右方向ベクトルとかけて右速度ベクトルを取得
        Vector3 sidewayVelocity = Vector3.Dot(m_velocity, sideway) * sideway;
        Vector3 prevSidewayVelocity = Vector3.Dot(m_prevVelocity, sideway) * sideway;

        // 遠心力（向心加速度） [G] [Vs - V0s / t / g ]
        // アングルベロシティYで左右判断 また　遠心力なので反転
        float centrifugalForce = -1 * Mathf.Sign(m_vehicleRigitbody.angularVelocity.y) * (sidewayVelocity.magnitude - prevSidewayVelocity.magnitude) / Time.deltaTime / Physics.gravity.magnitude;
        m_centrifugal = centrifugalForce;   // 値保持
        //AppLog.Log("遠心力" +  centrifugalForce);

        // F(遠心力) = mass * (現在のベクトルの大きさ　- 前のベクトルの大きさ)^2 / r(最小回転半径(5m))
        //float centrifugalForce = 0.0f;
        //float minRadius = 5.0f;
        //float v0 = sidewayVelocity.magnitude - prevSidewayVelocity.magnitude;
        //      Vector3 sideVec = m_vehicle.transform.right;
        //sideVec.Normalize();
        //centrifugalForce = m_vehicleRigitbody.mass * v0 /** Mathf.Abs(v0)*/ * 2.0f / minRadius * sideVec.y;
		//m_centrifugal = centrifugalForce * Mathf.Abs(centrifugalForce) * 2.0f;
	}

    // ヨーレート取得
    private void YawRateTransfar()
    {
        m_YawRate = m_vehicleRigitbody.angularVelocity.y;
    }
}
