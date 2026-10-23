using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[DefaultExecutionOrder(-100)]

public class G29 : MonoBehaviour
{
    public LogitechGSDK.DIJOYSTATE2ENGINES rec;
    //public AIM./*DesktopInputManager.*/InputController inputController;

    public float accel;

    // Start is called before the first frame update
    void Start()
    {
        //ReConnect();

        //AppLog.Log("SteeringInit:" + LogitechGSDK.LogiSteeringInitialize(false));
    }

    private void Update()
    {
        if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(0))
        {
            //CONTROLLER STATE
            //入力確認
            rec = LogitechGSDK.LogiGetStateUnity(0);
        }
        
        InputPedal();
    }
    public void ReConnect()
    {
        rec = LogitechGSDK.LogiGetStateUnity(0);
    }
    void InputPedal()
    {
        // アクセルとブレーキに振り分ける
        //AIM.InputHandleController.GetHandleAccelAndBrake(ref newAccel, ref newBrake);

        //// ハンドル使用
        //if (isConnectHandle)
        //{
        //    // LogicoolSDK使用
        //    if (vc.wheelInput.isG29)
        //    {
        //        newAccel = vc.wheelInput.inputAxel;
        //        newBrake = vc.wheelInput.inputBrake;
        //    }
        //    else
        //    {
        //        AIM.InputHandleController.GetHandleAccelAndBrake(ref newAccel, ref newBrake);
        //    }
        //    newAccel = accelCurve.Evaluate(newAccel).Round(inputDigit);
        //    newBrake = brakeCurve.Evaluate(newBrake).Round(inputDigit);
        //}
        // コントローラー使用
        //else if (isConectController)
        //{
        //    newBrake = Input.GetAxis("L_Trigger").Round(inputDigit);
        //    newAccel = Input.GetAxis("R_Trigger").Round(inputDigit);
        //}
        //// キーボード使用
        //else
        //{
        //    newAccel = Input.GetAxis("KeyboardAccel").Round(inputDigit);
        //    newBrake = Input.GetAxis("KeyboardBrake").Round(inputDigit);
        //}
        //// クラッチ
        //if (!vc.transmission.automaticClutch)
        //{
        //    try
        //    {
        //        vc.input.Clutch = Input.GetAxis("Clutch").Round(inputDigit);
        //    }
        //    catch
        //    {
        //        vc.transmission.automaticClutch = true;
        //        vc.input.Clutch = 0.0f;
        //    }
        //}

        //// ゴールでブレーキをかける
        //if (GoalJudgment.isGoal)
        //{
        //    newAccel = 0;
        //    newBrake = 1;
        //}

        //// 代入
        //vc.input.Vertical = newAccel - newBrake;
        //brakeValue = newBrake;

        //AppLog.Log(Input.GetAxis("Accel"));

        accel = Input.GetAxis("Accel");
    }
}
