/*
 * @file    WIZMODebug.cs
 * @brief   椅子のDebugシステム     
 * @author  22CU0225 豊田達也
 * @date    2024/10/03 作成
 */
using UnityEngine;
using UnityEngine.SceneManagement;

public class WIZMODebug : MonoBehaviour
{
    // WIZMO
    [SerializeField]
    WIZMOController m_WIZMOController;

    [SerializeField, ShowInInspector]
    private bool m_isEmergencyShutdown = false;   // 緊急停止

    public bool isEmergencyShutdown => m_isEmergencyShutdown;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (EmergencyShutdown()) return;
    }

    //━━━━━━━━━━━━━━━━━━━━━
    // 椅子との接続確認
    //━━━━━━━━━━━━━━━━━━━━━
    private void CheckChairConnect()
    {
        // コントローラーが見つからない場合はこの後の処理をしない
        if (m_WIZMOController == null) return;

        // 椅子が正常に動いているかをチェック
        if (m_WIZMOController.GetState() != WIZMOController.Running || m_WIZMOController.GetState() != WIZMOController.Initial )
        {
            // 正常に動いていない場合強制停止
            m_WIZMOController.CloseSIMVR();
        }
    }

    //━━━━━━━━━━━━━━━━━━━━━
    // 椅子の緊急停止の関数
    // 戻り値：使っていい場合はtrue、
    // 　　　　それ以外はfalse
    //━━━━━━━━━━━━━━━━━━━━━
    public bool EmergencyShutdown()
    {
        // 緊急停止
        // キーが押されたら、緊急停止します
        if (Input.GetKeyDown(KeyCode.F11))
        {
            m_isEmergencyShutdown = true;

            m_WIZMOController.CloseSIMVR();
            AppLog.LogWarning("[WIZMODebug] ShutDown");
            return true;
        }

        // 通常状態
        // キーが押されたら、再接続します
        if (Input.GetKeyDown(KeyCode.F12))
        {
            m_isEmergencyShutdown = false;

            // 値を戻す
            m_WIZMOController.OpenSIMVR();
            AppLog.LogWarning("[WIZMODebug] Active");
            return false;
        }

        if (m_isEmergencyShutdown) return true;
        return false;
    }

    // WIZMO LOG
    public string GetState_WIZMO()
    {
        string log;
        int state = m_WIZMOController.GetState();

        switch (state)
        {
            case WIZMOController.CanNotFindUsb:
                log = "USBが接続されていません [0:MachineNotDetected]";
                break;
            case WIZMOController.CanNotFindSimvr:
                log = "SIMVRが見つかりません [1:InaccessibleToTheMachine]";
                break;
            case WIZMOController.CanNotCalibration:
                log = "SIMVRの原点復帰に失敗しました [2:ZeroReturnFailure]";
                break;
            case WIZMOController.TimeoutCalibration:
                log = "SIMVRの原点復帰中にエラーが発生しました[3:ErrorReturningToZero]";
                break;
            case WIZMOController.ShutDownActuator:
                log = "アクチュエータが停止してエラーが発生しました [4:ShutDown]";
                break;
            case WIZMOController.CanNotCertificate:
                log = "SIMVRの認証に失敗しました [5:AuthenticationFailure]";
                break;
            case WIZMOController.Initial:
                log = "初期化しています [6:Intialize]";
                break;
            case WIZMOController.Running:
                log = "正常動作中です [7:Runnning]";
                break;
            case WIZMOController.StopActuator:
                log = "一部のアクチュエータが停止してます [8:StopActuator]";
                break;
            case WIZMOController.CalibrationRetry:
                log = "SIMVRの再度原点復帰中です [9:InternalDisconnectionError]";
                break;
            default:
                log = "何もしていません [-1:ー]";
                break;
        }

        return log;
    }
}
