/**
 * @file ProgressBar.cs
 * @brief ProgressBarの制御
 * @author yuuha terae
 */

using UnityEngine;
using UnityEngine.UI;

public class ProgressBar : MonoBehaviour
{
    // ProgressBarのImage
    private Image m_progressBar;

    // progressBarの進み具合
    [SerializeField,/*ReadOnly*/Range(0.0f,1.0f)]
    private float m_progressLevel;

    // バーの挙動を反転(「1でバーを最大」→「0でバーを最大」に反転する)
    [SerializeField]
    private bool m_flip;

    // 初期化処理
    public void Init(){
        // Image取得
        m_progressBar = GetComponent<Image>();

        // Pivot設定
        m_progressBar.rectTransform.pivot = Vector3.zero;

        // スケール設定
        m_progressBar.rectTransform.localScale = new Vector3(1.0f, 0.0f, 1.0f);
    }

    // ProgressBarを動かす
    public void SlideProgressBar(float _progressLevel){
        float progressLevel = _progressLevel;
        if (m_flip)  progressLevel = FlipProgressLevel(_progressLevel);  // trueならバーの挙動を反転(「1でバーを最大」→「0でバーを最大」に反転する)

        m_progressLevel = Mathf.Clamp(progressLevel, 0.0f, 1.0f);
        m_progressBar.rectTransform.localScale = new Vector3(1.0f, m_progressLevel, 1.0f);
    }

    // 値を反転(0～1を1～0に反転)
    private float FlipProgressLevel(float _value) {
        float value = 1.0f - _value;
        return value;
    }
}
