/*━━━━━━━━━
@file    TitleLogoOverlay.cs
@brief   Splash→Opening〜Opening→Menuの区間でタイトルロゴを表示し続ける常駐オーバーレイ
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/13
         最終更新日 : 2026/09/14
━━━━━━━━━*/

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TitleLogoOverlay : SingletonMonoBehaviour<TitleLogoOverlay>
{
    [SerializeField] private Image m_logoImage; //中央モニター用のロゴ画像(Manager.unity側で配置・サイズ調整する)
    [SerializeField] private float m_fadeInDuration = 0.3f; //FadeInにかける秒数
    [SerializeField] private float m_fadeOutDuration = 1f; //FadeOutにかける秒数

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) { return; } //重複破棄された場合は初期化処理を行わない

        if (m_logoImage != null)
        {
            Color color = m_logoImage.color;
            color.a = 0f; //実行開始時は非表示にする(シーン上はEditorで見やすいよう不透明のまま保存されている)
            m_logoImage.color = color;
        }
    }

    /// <summary>
    /// タイトルロゴをm_fadeInDurationかけてFadeInで表示します。
    /// </summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(1f, m_fadeInDuration);
    }

    /// <summary>
    /// タイトルロゴをm_fadeOutDurationかけてFadeOutで隠します。
    /// </summary>
    public IEnumerator FadeOut()
    {
        yield return Fade(0f, m_fadeOutDuration);
    }

    /// <summary>
    /// 進行中のFadeIn/FadeOutを中断し、ロゴを即座に非表示にします。
    /// Opening以外のStateへ入る際、GameFlowRunnerから安全網として呼ばれます。
    /// </summary>
    public void ForceHide()
    {
        if (m_logoImage == null) { return; }

        m_logoImage.DOKill(); //進行中のFadeIn/FadeOutコルーチンのトゥイーンを止める
        Color color = m_logoImage.color;
        color.a = 0f; //即座に非表示化する
        m_logoImage.color = color;
    }

    /// <summary>
    /// ロゴ画像を目標アルファ値まで変化させます。
    /// </summary>
    /// <param name="_targetAlpha">目標アルファ値(0:透明 1:不透明)</param>
    /// <param name="_duration">フェードにかける秒数</param>
    private IEnumerator Fade(float _targetAlpha, float _duration)
    {
        if (m_logoImage == null) { yield break; } //m_logoImage未割り当てなら何もしない

        bool isDone = false;
        m_logoImage.DOFade(_targetAlpha, _duration).SetUpdate(true).OnComplete(() => { isDone = true; }); //Time.timeScale=0(ポーズ)中でも動作させる
        yield return new WaitUntil(() => isDone);
    }
}
