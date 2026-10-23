using UnityEngine;

public class MenuMotionManager : MonoBehaviour
{
    [SerializeField] private WIZMOController m_wizmoController;
    [SerializeField] private VehicleController m_vehicleController;

    [Header("Motion Settings")]
    [SerializeField] private float m_menuHeave = 1f;   // メニュー時の目標高さ
    [SerializeField] private float m_smoothTime = 2.0f; // 昇降にかかる時間（秒）

    // メニュー（-1f）状態からスタート
    private float m_currentHeave = 0f;
    private float m_heaveVelocity = 0f;
    private float m_currentAxis = 0f;
    private float m_axisVelocity = 0f;

    // インゲーム開始時の「0への復帰移動」が完了したかのフラグ
    private bool m_isInGameTransitionComplete = false;

    private void Update()
    {
        if (m_wizmoController == null) return;

        bool inGame = IsVehiclePresent();

        if (inGame)
        {
            // ------------------------------------------
            // インゲーム時：0に向かって1度だけ復帰移動
            // ------------------------------------------
            if (!m_isInGameTransitionComplete)
            {
                m_currentHeave = Mathf.SmoothDamp(
                    m_currentHeave,
                    0f,
                    ref m_heaveVelocity,
                    m_smoothTime
                );

                m_wizmoController.SetChairMotion(0f, 0f, 0f, m_currentHeave, 0f, 0f, 0.2f, 0.2f);

                // ほぼ 0 に到達したら移動完了とし、以降は車両挙動の制御に任せる
                if (Mathf.Abs(m_currentHeave) <= 0.01f)
                {
                    m_currentHeave = 0f;
                    m_wizmoController.SetChairMotion(0f, 0f, 0f, 0f, 0f, 0f, 0.2f, 0.2f);
                    m_isInGameTransitionComplete = true;
                }
            }
            // ※ TransitionComplete が true の間は SetChairMotion を呼ばず、車両側のモーション入力を阻害しません
        }
        else
        {
            // ------------------------------------------
            // メニュー時（またはインゲーム終了時）：-1へ移動
            // ------------------------------------------
            m_isInGameTransitionComplete = false; // 次回のインゲーム用にフラグをリセット

            m_currentHeave = Mathf.SmoothDamp(
                m_currentHeave,
                m_menuHeave,
                ref m_heaveVelocity,
                m_smoothTime
            );

           // m_currentAxis = Mathf.SmoothDamp(
           //    m_currentAxis,
           //    0,
           //    ref m_axisVelocity,
           //    m_smoothTime
           //);

            m_wizmoController.SetChairMotion(0, 0, 0, m_currentHeave, 0, 0, 0.2f, 0.2f);
        }
    }

    /// <summary>
    /// VehicleController の存在チェック（自動検索付き）
    /// </summary>
    private bool IsVehiclePresent()
    {
        if (m_vehicleController != null) return true;

#if UNITY_2023_1_OR_NEWER
        m_vehicleController = FindFirstObjectByType<VehicleController>();
#else
        m_vehicleController = FindObjectOfType<VehicleController>();
#endif

        return m_vehicleController != null;
    }
}