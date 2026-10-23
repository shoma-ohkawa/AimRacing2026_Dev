using System.Collections.Generic;
using UnityEngine;
using VehiclePhysics;

public class DebugInputVehicles : MonoBehaviour
{
    //G923の中央から片側までの実際の操作角度
    private const float G923HalfRangeDegrees = 180f;
    private VehicleController vc;  // 速度参照用
    // インスペクタで代入
    [SerializeField] WheelController2024 m_wheelController;

    // デバッグ手順（例: Startメソッドにnullチェックを追加）
    private void Start()
    {
		// コンポーネント取得
		vc = GetComponent<VehicleController>();
		// nullチェック
		if (vc == null)
            AppLog.LogError("VehicleController2024 がアタッチされていません");
    }

	// 土田
	// 追記 06/10
	// 分離 07/31
	// 追加 09/08
	// GUIの表示
	private void OnGUI()
    {
        //GUI
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24, 
            normal = { textColor = Color.white }
        };

		//G923の入力値を左右180度の実操作角度へ変換する処理
		float steerInput = vc.Steering;
		float handleAngle = steerInput * G923HalfRangeDegrees;

        // ギアのトルク計算格納用
        float DriveTorque = vc.passClutch.ClutchTorque * vc.passTransmission.CurrentGearRatio;

        style.fontSize = 24;    // 文字の大きさ
        style.normal.textColor = Color.white; // 文字の色
        int y = 10; // Y座標を加算して表示位置をずらす

        GUI.Label(new Rect(10, y, 500, 50), "Speed: " + vc.KPH.ToString("F2") + " km/h", style);
        y += 30;    // 速度
        GUI.Label(new Rect(10, y, 400, 50), "Engine RPM: " + vc.Engine.RPM.ToString("F2") + " rpm", style);
        y += 30;    // エンジン回転数
        
        GUI.Label(new Rect(10, y, 400, 30), $"Accel Input: {vc.Accel:F2}", style);
        y += 30;    // アクセル入力
        GUI.Label(new Rect(10, y, 400, 30), $"Brake Input: {vc.Brake:F2}", style);
        y += 30;    // ブレーキ入力

        GUI.Label(new Rect(10, y, 400, 30), $"Clutch Input: {vc.Clutch:F2}", style);
        y += 30;    // クラッチ入力
        GUI.Label(new Rect(10, y, 400, 30), $"Clutch Torque: {vc.passClutch.ClutchTorque:F2}", style);
        y += 30;    // クラッチトルク (N・m)

        GUI.Label(new Rect(10, y, 400, 30), $"Current Gear: {vc.passTransmission.ActiveGear:F0}", style);
        y += 30;    // 現在のギア
        GUI.Label(new Rect(10, y, 400, 30), $"Current Gear Ratio: {vc.passTransmission.CurrentGearRatio:F2}", style);
        y += 30;    // 現在のギア比
        GUI.Label(new Rect(10, y, 400, 30), $"Torque After Gear: {DriveTorque:F2}", style);
        y += 30;    // 計算後ギアトルク (N・m)
        //GUI.Label(new Rect(10, y, 400, 30), $"Torque After FinalGear: {TotalDriveTorque:F2}", style);
        //y += 30;    // 計算後ファイナルギアトルク (N・m)
        
        //GUI.Label(new Rect(10, y, 400, 30), $"Wheel Torque: {vc.passDifferential.GetDriveTorque(TotalDriveTorque,m_wheelController.IsRightSide):F2}", style);
        //y += 30;    // ホイールトルク

        GUI.Label(new Rect(10, y, 400, 30), $"Steer Input: {vc.Steering:F2}", style);
        y += 30;    // ハンドル入力(0~1)
        GUI.Label(new Rect(10, y, 400, 30), $"Steer Angle: {handleAngle:F2}", style);
		y += 30;    // ハンドル入力の角度(-450~450)
        //GUI.Label(new Rect(10, y, 400, 30), $"Wheel Speed: {RpmToSpeed():F2}", style);
        //y += 30;    // ホイール回転速度


        GUI.Label(new Rect(10, y, 400, 30), $"Long Friction: {m_wheelController.m_fx:F2}", style);
        y += 30;    // 縦力
		GUI.Label(new Rect(10, y, 400, 30), $"Lateral Friction: {m_wheelController.m_fy:F2}", style);
        y += 30;    // 横力
		GUI.Label(new Rect(10, y, 400, 30), $"Lateral Speeed: {m_wheelController.LatSpeed:F2}", style);
        y += 30;    // 横力速度
        GUI.Label(new Rect(10, y, 400, 30), $"Long Speed: {m_wheelController.LongSpeed:F2}", style);
        y += 30;    // 縦力速度

        GUI.Label(new Rect(10, y, 400, 30), $"Slip Ratio: {m_wheelController.SlipRatio:F2}", style);
        y += 30;    // スリップ率
        GUI.Label(new Rect(10, y, 400, 30), $"Slip Angle: {m_wheelController.SlipAngle:F2}", style);
        y += 30;    // スリップ角

        GUI.Label(new Rect(10, y, 400, 30), $"WheelAngleVelo: {m_wheelController.WheelAngularVelocity:F2}", style);
        y += 30;    // ホイール角速度
        GUI.Label(new Rect(10, y, 400, 30), $"Wheel rpm: {m_wheelController.WheelRPM:F2}", style);
        y += 30;    // ホイール回転数

        // ステアリング角の表示
        foreach (WheelController2024 wheel in vc.WheelComtrollers)
        {
            if (wheel.IsFrontSide)
            {
                string side = wheel.IsRightSide ? "Right" : "Left";
                GUI.Label(new Rect(10, y, 400, 30), $"{side} Front Steer Angle: {wheel.SteerAngle:F2} deg", style);
                y += 30;
            }
        }

        //GUI.Label(new Rect(10, y, 500, 50), "RoadTorque : " + m_wheelController.RoadTorque.ToString("F2") + " km/h", style);
        //y += 30;    // 駆動トルク
        //GUI.Label(new Rect(10, y, 500, 50), "MaxDriveTorque : " + m_wheelController.MaxDriveTorque.ToString("F2") + " km/h", style);
        //y += 30;    // 最大駆動トルク
        
    }

    // 09/09 追加
    // RPM → 車速 (km/h) 計算する関数
    // 09/23 ファイナルギア修正のためコメントアウト
    //public float RpmToSpeed()
    //{
    //    float gearRatio = vc.Transmission.CurrentGearRatio;
    //    // Nで速度計算できないため0と表示する
    //    if(gearRatio <= 0.01f)
    //        return 0f;

    //    float totalRatio = gearRatio * vc.Transmission.m_finalGearRatio;   // 合計のギア比
    //    float wheelRpm = vc.EngineRPM / totalRatio;                    // タイヤの回転数(分)
    //    float wheelRps = wheelRpm / 60f;                     // タイヤの回転数(秒) 
    //    float circumference = Mathf.PI * 0.645f;            // タイヤの外周(外径645mm)
    //    return wheelRps * circumference * 3.6f;            // km/h
    //}
}
