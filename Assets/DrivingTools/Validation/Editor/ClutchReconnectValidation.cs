using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

//実機を接続せずクラッチの切断と踏み戻しを回帰検査するクラス
public static class ClutchReconnectValidation
{
    //シーンへ保存せず検査対象の内部状態を組み立てる検索条件
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    //失敗時にどの確認まで通ったか残す検査数
    static int checks;

    //本番のクラスへ模擬入力を渡して接続率と出力を検査する関数
    [MenuItem("Tools/検証/挙動/Clutch reconnect")]
    public static void Run()
    {
        //Awakeや通常走行を起動せず物理部品だけを組み立てる検査用オブジェクト
        GameObject root = new GameObject("ClutchReconnectTest");
        root.SetActive(false);
        Gamepad pad = null;
        InputAction action = null;
        checks = 0;
        try
        {
            VehicleController vehicle = root.AddComponent<VehicleController>();
            Clutch clutch = root.AddComponent<Clutch>();
            Transmission transmission = root.AddComponent<Transmission>();
            Differential differential = root.AddComponent<Differential>();
            Set(vehicle, "m_clutch", clutch);
            Set(vehicle, "m_mission", transmission);
            Set(vehicle, "m_differential", differential);
            Set(differential, "m_driveMode", Differential.DriveMode.Track);
            transmission.Type = Transmission.TransmissionType.Manual;
            transmission.ConfigureShiftSafety(0.33f, 8200f);
            Set(transmission, "m_currentGear", 1);
            Set(transmission, "m_lastShiftChangeTime", -100f);

            vehicle.SetPhysicalClutchPedal(1f);
            Check(vehicle.ManualLeverActive && !clutch.AutoClutch, "最初の変速前に実クラッチを選択");
            Check(clutch.Engagement == 0f, "踏み切りで切断");
            Check(vehicle.ManualDriveAssistRatio == 0f, "踏み切り中は加速補助も零");
            vehicle.SelectPaddleClutch();
            Check(vehicle.ManualLeverActive && clutch.Engagement == 0f, "パドル操作で踏み込みを無視しない");
            Check(vehicle.RequestLeverShift(true) && transmission.ActiveGear == 2, "踏み切りで一段変速");
            clutch.DrivetrainUpdate(100f, 120f, 100f, transmission.CurrentGearRatio, 0.24f);
            Check(clutch.ClutchTorque == 0f, "踏み切り中の伝達は零");

            vehicle.SetPhysicalClutchPedal(0.45f);
            Check(Mathf.Abs(clutch.Engagement - 0.5f) < 0.001f, "半クラッチの接続率");
            Set(transmission, "m_lastShiftChangeTime", -100f);
            Check(!vehicle.RequestLeverShift(true) && transmission.ActiveGear == 2, "半踏みでレバー経路の変速を拒否");
            vehicle.SetPhysicalClutchPedal(0.03f);
            Check(clutch.Engagement == 1f, "踏み戻しの残留値でも完全接続");
            Check(vehicle.ManualDriveAssistRatio == 1f, "完全接続後は通常MTと同じ加速補助");
            clutch.DrivetrainUpdate(100f, 120f, 100f, transmission.CurrentGearRatio, 0.24f);
            Check(clutch.ClutchTorque > 0f, "変速後の踏み戻しで正の駆動トルクが復帰");
            vehicle.ClearPhysicalClutchPedal();
            Check(clutch.Engagement == 0f && clutch.ClutchTorque == 0f, "入力切断で古いトルクを残さない");
            vehicle.SetPhysicalClutchPedal(0f);
            Check(clutch.Engagement == 1f, "再受信した離し入力で再接続");
            vehicle.SetPhysicalClutchPedal(float.NaN);
            Check(clutch.Engagement == 0f, "不正入力で切断");
            vehicle.SetPhysicalClutchPedal(0f);
            vehicle.SelectPaddleClutch();
            Check(!vehicle.ManualLeverActive && clutch.AutoClutch, "未踏みの通常パドルは自動クラッチへ戻る");

            //実機の代わりに符号付き軸を使いイベントに依存しない踏み戻しを検査する
            pad = InputSystem.AddDevice<Gamepad>();
            action = new InputAction("ClutchTest", InputActionType.Value, "<Gamepad>/leftStick/y");
            action.Enable();
            VehicleUnityEvent input = root.AddComponent<VehicleUnityEvent>();
            Set(input, "m_vehicleController", vehicle);
            Set(input, "m_clutchAxis", pad.leftStick.y);
            Set(input, "m_clutchAction", action);
            Set(input, "m_clutchInputDevice", pad);
            Set(input, "m_invertClutchPedal", false);
            InputSystem.QueueDeltaStateEvent(pad.leftStick.y, -1f);
            InputSystem.Update();
            Invoke(input, "RefreshPhysicalClutch");
            Check(vehicle.Clutch == 1f && clutch.Engagement == 0f, "符号付き軸の全踏み");
            InputSystem.QueueDeltaStateEvent(pad.leftStick.y, 1f);
            InputSystem.Update();
            Invoke(input, "RefreshPhysicalClutch");
            Check(vehicle.Clutch == 0f && clutch.Engagement == 1f, "通知なしの現在値読取でも踏み戻しを反映");
            //報告された逆向きの機器も踏むと切断し離すと接続することを確認する
            Set(input, "m_invertClutchPedal", true);
            Invoke(input, "RefreshPhysicalClutch");
            Check(vehicle.Clutch == 1f && clutch.Engagement == 0f, "反転設定でプラス入力の踏み切りを切断へ変換");
            clutch.DrivetrainUpdate(100f, 120f, 100f, transmission.CurrentGearRatio, 0.24f);
            Check(clutch.ClutchTorque == 0f, "反転後も踏み込み中は駆動を切断");
            InputSystem.QueueDeltaStateEvent(pad.leftStick.y, -1f);
            InputSystem.Update();
            Invoke(input, "RefreshPhysicalClutch");
            Check(vehicle.Clutch == 0f && clutch.Engagement == 1f, "反転設定でマイナス入力の踏み戻しを接続へ変換");
            clutch.DrivetrainUpdate(100f, 120f, 100f, transmission.CurrentGearRatio, 0.24f);
            Check(clutch.ClutchTorque > 0f, "反転後は離した状態で駆動トルクが復帰");
            action.Disable();
            Invoke(input, "RefreshPhysicalClutch");
            Check(clutch.Engagement == 0f, "アクション停止を未踏みと取り違えない");
            //低速補助は両方のMTで同じ条件になりNとRへ介入しないことを確認する
            foreach (bool manual in new[] { false, true })
            {
                transmission.ManualClutchControlled = manual;
                foreach (int gear in new[] { -1, 0, 1, 2, 3, 6 })
                {
                    foreach (float speed in new[] { 0f, 29.9f, 30f, 30.1f })
                    {
                        Set(transmission, "m_currentGear", gear);
                        Set(transmission, "m_lastShiftChangeTime", -100f);
                        transmission.TransmissionUpdate(4000f, speed, 0f);
                        int expected = gear >= 2 && speed <= 30f ? 1 : gear;
                        Check(transmission.ActiveGear == expected, "低速1速復帰 manual=" + manual + " gear=" + gear + " speed=" + speed);
                    }
                }
            }
            Debug.Log("[ClutchReconnectValidation] PASS " + checks + " checks. 実車輪走行とG923実機は別途必要。");
        }
        finally
        {
            action?.Dispose();
            if (pad != null) { InputSystem.RemoveDevice(pad); }
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    //検査名を記録して不一致では成功扱いせず停止する関数
    static void Check(bool condition, string label)
    {
        if (!condition) { throw new InvalidOperationException("[ClutchReconnectValidation] FAIL " + label); }
        checks++;
        Debug.Log("[ClutchReconnectValidation] OK " + label);
    }

    //検査用オブジェクトの参照だけを設定する関数
    static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, Flags);
        if (field == null) { throw new MissingFieldException(target.GetType().Name, name); }
        field.SetValue(target, value);
    }

    //本番のクラッチ読取処理を検査から直接呼ぶ関数
    static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, Flags).Invoke(target, null);
    }
}
