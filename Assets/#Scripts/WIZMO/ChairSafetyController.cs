using UnityEngine;

/// <summary>
/// ChairController / WIZMOController 本体を改造せず、外側から緊急停止を行う安全制御。
/// 
/// F12        : 緊急停止
/// Shift+F12 : 緊急停止解除
/// 
/// マスター版ChairController対応。
/// 2026/09/01 GC削減対応
/// </summary>
public class ChairSafetyController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChairController m_chairController;
    [SerializeField] private WIZMOController m_wizmoController;

    [Header("Keys")]
    [SerializeField] private KeyCode m_emergencyStopKey = KeyCode.F12;
    [SerializeField] private KeyCode m_clearEmergencyStopKey = KeyCode.F12;

    [SerializeField] private bool m_clearEmergencyNeedsShift = true;

    [Header("Settings")]
    [SerializeField] private bool m_findReferencesAutomatically = true;
    [SerializeField] private bool m_logSafetyAction = true;

    [SerializeField, Range(0.1f, 5.0f)]
    private float m_referenceResolveInterval = 1.0f;

    private float m_referenceResolveTimer = 0.0f;
    private bool m_isEmergencyStopped = false;

    public bool IsEmergencyStopped => m_isEmergencyStopped;

    private bool HasRequiredReferences
    {
        get
        {
            return m_chairController != null &&
                   m_wizmoController != null;
        }
    }

    private void Start()
    {
        ResolveReferencesIfNeeded();
    }

    private void Update()
    {
        ResolveReferencesByInterval();

        bool shiftPressed =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        // Shift + F12：緊急停止解除
        if (Input.GetKeyDown(m_clearEmergencyStopKey))
        {
            if (m_clearEmergencyNeedsShift && shiftPressed)
            {
                ClearEmergencyStopAndResume();
                return;
            }
        }

        // F12単体：緊急停止
        if (Input.GetKeyDown(m_emergencyStopKey))
        {
            if (!shiftPressed)
            {
                EmergencyStopChair();
                return;
            }
        }

        // 緊急停止中は毎フレームWIZMO出力を0固定する
        if (m_isEmergencyStopped)
        {
            ForceZeroWizmoOutput();
        }
    }

    /// <summary>
    /// 緊急停止。
    /// ChairControllerを停止し、WIZMOControllerへ渡る値を0固定する。
    /// </summary>
    public void EmergencyStopChair()
    {
        m_isEmergencyStopped = true;

        ResolveReferencesIfNeeded();

        if (m_chairController != null)
        {
            m_chairController.InGameMove = false;
        }

        ForceZeroWizmoOutput();

        if (m_logSafetyAction)
        {
            AppLog.Log("[ChairSafety] Emergency Stop. ChairController stopped and WIZMO output forced to zero.");
        }
    }

    /// <summary>
    /// 緊急停止解除。
    /// ChairControllerを再開する。
    /// </summary>
    public void ClearEmergencyStopAndResume()
    {
        m_isEmergencyStopped = false;

        ResolveReferencesIfNeeded();

        if (m_chairController != null)
        {
            m_chairController.InGameMove = true;
        }

        if (m_logSafetyAction)
        {
            AppLog.Log("[ChairSafety] Emergency Stop cleared. ChairController resumed.");
        }
    }

    /// <summary>
    /// WIZMOControllerへ送る値を0にする。
    /// </summary>
    private void ForceZeroWizmoOutput()
    {
        if (m_wizmoController == null)
        {
            return;
        }

        m_wizmoController.roll = 0.0f;
        m_wizmoController.pitch = 0.0f;
        m_wizmoController.yaw = 0.0f;
        m_wizmoController.heave = 0.0f;
        m_wizmoController.sway = 0.0f;
        m_wizmoController.surge = 0.0f;

        m_wizmoController.speed1_all = 0.0f;
        m_wizmoController.accel = 0.0f;
    }

    private void ResolveReferencesByInterval()
    {
        if (!m_findReferencesAutomatically)
        {
            return;
        }

        // 参照が揃っているなら検索しない
        if (HasRequiredReferences)
        {
            return;
        }

        m_referenceResolveTimer -= Time.unscaledDeltaTime;

        if (m_referenceResolveTimer > 0.0f)
        {
            return;
        }

        m_referenceResolveTimer = m_referenceResolveInterval;
        ResolveReferencesIfNeeded();
    }

    private void ResolveReferencesIfNeeded()
    {
        if (!m_findReferencesAutomatically)
        {
            return;
        }

        if (m_chairController == null)
        {
#if UNITY_2023_1_OR_NEWER
            m_chairController = FindFirstObjectByType<ChairController>();
#else
            m_chairController = FindObjectOfType<ChairController>();
#endif
        }

        if (m_wizmoController == null)
        {
#if UNITY_2023_1_OR_NEWER
            m_wizmoController = FindFirstObjectByType<WIZMOController>();
#else
            m_wizmoController = FindObjectOfType<WIZMOController>();
#endif
        }
    }
}