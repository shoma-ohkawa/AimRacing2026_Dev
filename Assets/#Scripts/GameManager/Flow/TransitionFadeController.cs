/*━━━━━━━━━
@file    TransitionFadeController.cs
@brief   シーン遷移中の画面を単色で覆うフェード演出を管理するクラス
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/10
━━━━━━━━━*/

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TransitionFadeController : SingletonMonoBehaviour<TransitionFadeController>
{
    /// <summary>
    /// 特定StateのみFadeOut/FadeIn秒数とFadeOut色を個別に上書きするためのInspector設定用構造体。
    /// </summary>
    [System.Serializable]
    private struct StateFadeSettings
    {
        [SerializeField] public GameStateId m_state; //上書き対象のState
        [SerializeField] public float m_fadeOutDuration; //このStateから離れる際のFadeOut秒数
        [SerializeField] public float m_fadeInDuration; //このStateに入った際のFadeIn秒数
        [SerializeField] public Color m_fadeOutColor; //このStateから離れる際のFadeOut色
    }

    private const int k_EditorDisplayCount = 1; //Editor上で生成するCanvas枚数(常にDisplay 0のみ)
    private const int k_SortOrder = 1000; //他の全UIより手前に描画するためのCanvas優先度

    [SerializeField] private StateFadeSettings[] m_stateFadeSettings = new StateFadeSettings[0]; //State別のFadeOut/FadeIn秒数・FadeOut色の登録

    private readonly List<Image> m_fadeImages = new List<Image>(); //モニターごとのフェード用黒画像

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) { return; } //重複破棄された場合は生成処理を行わない

        CreateFadeCanvases();
        SetAlphaImmediate(1f); //起動直後は黒画面から開始する(最初のFadeInはGameFlowRunner.Bootstrapが呼ぶ)
    }

    /// <summary>
    /// 画面を指定色へフェードアウトします。_durationがnull(=State未登録)の場合はアニメーションせず
    /// 即座に透明へスナップし(=覆わずに素通しする)、シーン遷移自体は止めません。
    /// </summary>
    /// <param name="_duration">フェードにかける秒数(nullなら覆わず即座に透明化)</param>
    /// <param name="_color">フェード先の色(省略時は黒)</param>
    public IEnumerator FadeOut(float? _duration, Color? _color = null)
    {
        if (_duration == null) //登録の無いStateはフェードで覆わない
        {
            SetAlphaImmediate(0f);
            yield break;
        }

        SetColorImmediate(_color ?? Color.black);
        yield return Fade(1f, _duration.Value);
    }

    /// <summary>
    /// 直前のFadeOutで指定した色から通常表示へフェードインします。_durationがnull(=State未登録)の場合は
    /// アニメーションせず即座に透明へスナップし、画面が黒いまま固定される事故を防ぎます。
    /// </summary>
    /// <param name="_duration">フェードにかける秒数(nullなら即座に透明化)</param>
    public IEnumerator FadeIn(float? _duration)
    {
        if (_duration == null) //登録の無いStateはフェードで覆わない
        {
            SetAlphaImmediate(0f);
            yield break;
        }

        yield return Fade(0f, _duration.Value);
    }

    /// <summary>
    /// 指定Stateから離れる際のFadeOut秒数を取得します。m_stateFadeSettingsに登録が無ければnullを返します。
    /// </summary>
    /// <param name="_state">離れようとしているState</param>
    public float? ResolveFadeOutDuration(GameStateId _state)
    {
        for (int i = 0; i < m_stateFadeSettings.Length; ++i) //State別登録を探索するループ
        {
            if (m_stateFadeSettings[i].m_state == _state) { return m_stateFadeSettings[i].m_fadeOutDuration; }
        }
        return null;
    }

    /// <summary>
    /// 指定Stateに入る際のFadeIn秒数を取得します。m_stateFadeSettingsに登録が無ければnullを返します。
    /// </summary>
    /// <param name="_state">入ろうとしているState</param>
    public float? ResolveFadeInDuration(GameStateId _state)
    {
        for (int i = 0; i < m_stateFadeSettings.Length; ++i) //State別登録を探索するループ
        {
            if (m_stateFadeSettings[i].m_state == _state) { return m_stateFadeSettings[i].m_fadeInDuration; }
        }
        return null;
    }

    /// <summary>
    /// 指定Stateから離れる際のFadeOut色を取得します。m_stateFadeSettingsに登録が無ければ黒を返します。
    /// </summary>
    /// <param name="_state">離れようとしているState</param>
    public Color ResolveFadeOutColor(GameStateId _state)
    {
        for (int i = 0; i < m_stateFadeSettings.Length; ++i) //State別登録を探索するループ
        {
            if (m_stateFadeSettings[i].m_state == _state) { return m_stateFadeSettings[i].m_fadeOutColor; }
        }
        return Color.black;
    }

    /// <summary>
    /// 全モニター分のフェード画像を、目標アルファ値まで同時に変化させます。
    /// </summary>
    /// <param name="_targetAlpha">目標アルファ値(0:透明 1:黒不透明)</param>
    /// <param name="_duration">フェードにかける秒数</param>
    private IEnumerator Fade(float _targetAlpha, float _duration)
    {
        if (m_fadeImages.Count == 0) { yield break; }

        int remaining = m_fadeImages.Count; //完了待ちのフェード数

        for (int i = 0; i < m_fadeImages.Count; ++i)
        {
            m_fadeImages[i].DOFade(_targetAlpha, _duration)
                .SetUpdate(true) //Time.timeScale=0(ポーズ)中でも動作させる
                .OnComplete(() => { --remaining; });
        }

        yield return new WaitUntil(() => remaining <= 0);
    }

    //即座にアルファ値を設定する関数(アニメーションなし)
    private void SetAlphaImmediate(float _alpha)
    {
        for (int i = 0; i < m_fadeImages.Count; ++i)
        {
            Color color = m_fadeImages[i].color;
            color.a = _alpha;
            m_fadeImages[i].color = color;
        }
    }

    //RGBのみ即座に変更する関数(アルファ値は維持する)
    private void SetColorImmediate(Color _color)
    {
        for (int i = 0; i < m_fadeImages.Count; ++i)
        {
            Color color = _color;
            color.a = m_fadeImages[i].color.a;
            m_fadeImages[i].color = color;
        }
    }

    //接続モニター数に応じたフェード用Canvasを生成する関数
    private void CreateFadeCanvases()
    {
        int displayCount = Application.isEditor ? k_EditorDisplayCount : Mathf.Max(1, Display.displays.Length);

        for (int i = 0; i < displayCount; ++i)
        {
            CreateFadeCanvas(i);
        }
    }

    /// <summary>
    /// 指定したモニターへ黒画像を表示するCanvasを1枚生成します。
    /// </summary>
    /// <param name="_displayIndex">対象モニターのディスプレイ番号</param>
    private void CreateFadeCanvas(int _displayIndex)
    {
        GameObject canvasObject = new GameObject($"TransitionFadeCanvas_{_displayIndex}");
        canvasObject.transform.SetParent(transform);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = _displayIndex;
        canvas.sortingOrder = k_SortOrder;

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(canvasObject.transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true; //フェード中に裏側の操作を受け付けないようにする

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        m_fadeImages.Add(image);
    }
}
