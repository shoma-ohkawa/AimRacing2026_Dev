/*━━━━━━━━━
@file    TimeRemainingCountdown.cs
@brief   Menu画面「Time remaining」のカウントダウン表示(99→0)
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/15
         最終更新日 : 2026/09/15
━━━━━━━━━*/

using UnityEngine;
using TMPro;

public class TimeRemainingCountdown : MonoBehaviour
{
    private const int k_StartSeconds = 99; //カウントダウン開始秒数

    [SerializeField] private TextMeshProUGUI m_timeText; //残り秒数を表示するText

    private float m_remainingSeconds; //残り秒数。0に達したら以降は減算しない
    private bool m_isCounting; //Menu FadeIn完了後にtrueになり、以降Updateで減算する

    private void OnEnable()
    {
        GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete;
    }

    private void OnDisable()
    {
        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
    }

    private void Start()
    {
        m_remainingSeconds = k_StartSeconds;
        UpdateText();
    }

    private void Update()
    {
        if (!m_isCounting) { return; } //MenuのFadeInが完了し画面に確実に描画されるまでは減算を始めない
        if (m_remainingSeconds <= 0f) { return; } //0秒到達後は何もせず0表示を維持する

        m_remainingSeconds = Mathf.Max(0f, m_remainingSeconds - Time.unscaledDeltaTime); //Time.timeScaleやポーズの影響を受けない実時間で減算する
        UpdateText();
    }

    /// <summary>
    /// GameFlowRunnerのFadeIn完了通知を受け取り、Menuへの遷移であればカウントダウンを開始します。
    /// </summary>
    /// <param name="_state">FadeInが完了したState</param>
    private void OnStateFadeInComplete(GameStateId _state)
    {
        if (_state != GameStateId.Menu) { return; }
        m_isCounting = true;
    }

    /// <summary>
    /// カウントダウンを停止します。以降Updateでは減算されず、表示は現在値のまま維持されます。
    /// </summary>
    public void StopCountdown()
    {
        m_isCounting = false;
    }

    /// <summary>
    /// 残り秒数をTextへ2桁表示します。
    /// </summary>
    private void UpdateText()
    {
        if (m_timeText == null) { return; }

        int seconds = Mathf.CeilToInt(m_remainingSeconds);
        m_timeText.text = seconds.ToString("00");
    }
}
