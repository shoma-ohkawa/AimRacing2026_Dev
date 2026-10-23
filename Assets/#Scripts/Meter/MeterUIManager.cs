/**
 * @file    MeterUIManager.cs
 * @brief   車両メーターの管理
 * @author  中田樹希
 * @data    2026/09/02
 */

using UnityEngine;

//車両メーターの表示、更新、タイマーを管理するクラス
public class MeterUIManager : MonoBehaviour
{
    //表示対象のメーター本体を保持する変数
    [SerializeField] GameObject m_meter;
    //速度や回転数を画面へ反映する制御を保持する変数
    [SerializeField] MeterController m_meterController;
    //走行時間を計測するタイマーを保持する変数
    [SerializeField] Timer m_timer;
    //メーターを表示するUnityディスプレイ番号
    [SerializeField, Min(0)] int m_targetDisplay;
    //表示中だけメーター更新を行うための変数
    bool m_isActiveMeter = true;
    //メーターが所属するCanvasを保持する変数
    Canvas m_meterCanvas;

    //参照とCanvas出力先をゲーム開始前に整える関数
    void Awake()
    {
        ResolveReferences();
        ConfigureCanvas();
    }

    //メーターの初期表示値を設定する関数
    void Start()
    {
        if (m_meterController != null) { m_meterController.Init(); }
    }

    //速度、回転数、ギア、ペダル表示を毎フレーム更新する関数
    void Update()
    {
        if (!m_isActiveMeter || m_meterController == null) { return; }
        m_meterController.UpdateMeter();
    }

    //表示中の走行時間を物理更新に合わせて進める関数
    void FixedUpdate()
    {
        if (!m_isActiveMeter || m_meterController == null) { return; }
        m_meterController.FixedUpdateMeter();
    }

    //メーター本体と制御参照をシーン内から補完する関数
    void ResolveReferences()
    {
        if (m_meterController == null) { m_meterController = FindFirstObjectByType<MeterController>(FindObjectsInactive.Include); }
        if (m_timer == null) { m_timer = FindFirstObjectByType<Timer>(FindObjectsInactive.Include); }
        if (m_meter == null && m_meterController != null) { m_meter = m_meterController.gameObject; }
        if (m_meter != null) { m_meterCanvas = m_meter.GetComponentInParent<Canvas>(true); }
    }

    //メーターCanvasをゲーム映像と同じ出力先へ設定する関数
    void ConfigureCanvas()
    {
        if (m_meterCanvas == null && m_meter != null) { m_meterCanvas = m_meter.GetComponentInParent<Canvas>(true); }
        if (m_meterCanvas != null) { m_meterCanvas.targetDisplay = Mathf.Clamp(m_targetDisplay, 0, 7); }
    }

    //インゲーム開始時にメーターを表示する関数
    public void ShowMeter()
    {
        ResolveReferences();
        ConfigureCanvas();
        if (m_meter == null)
        {
            AppLog.LogError("MeterUIManager: 表示するメーターが見つかりません。", this);
            return;
        }
        m_meter.SetActive(true);
        m_isActiveMeter = true;
    }

    //イントロ中にメーターを非表示にする関数
    public void HideMeter()
    {
        if (m_meter == null) { ResolveReferences(); }
        if (m_meter == null) { return; }
        m_meter.SetActive(false);
        m_isActiveMeter = false;
    }

    //外部のタイム表示へ走行タイマーを渡す関数
    public Timer GetTimer()
    {
        if (m_timer == null) { ResolveReferences(); }
        return m_timer;
    }

    //カウントダウン終了後に走行時間の計測を開始する関数
    public void StartTimer()
    {
        if (m_timer == null) { ResolveReferences(); }
        if (m_timer != null) { m_timer.StartTimer(); }
    }

    //ゴール時に走行時間の計測を停止する関数
    public void StopTimer()
    {
        if (m_timer == null) { ResolveReferences(); }
        if (m_timer != null) { m_timer.StopTimer(); }
    }
}
