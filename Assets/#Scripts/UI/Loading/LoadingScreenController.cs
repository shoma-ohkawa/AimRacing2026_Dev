/*━━━━━━━━━
@file    LoadingScreenController.cs
@brief   シーン遷移の裏で一時的に表示するLoading画面(黒背景)を管理するクラス
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/13
         最終更新日 : 2026/09/17
━━━━━━━━━*/

using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenController : MonoBehaviour
{
    private const int k_EditorDisplayCount = 1; //Editor上で生成するCanvas枚数(常にDisplay 0のみ)
    private const int k_SortOrder = 1100; //TransitionFadeControllerの黒フェードより手前に描画するための優先度

    /// <summary>
    /// 離脱元State別にLoading画面のFadeIn/保持/FadeOut秒数を上書きするInspector設定用構造体。
    /// </summary>
    [System.Serializable]
    private struct LoadingFadeSettings
    {
        [SerializeField] public GameStateId m_leavingState; //このLoading画面が挟まる離脱元State
        [SerializeField] public float m_fadeInDuration; //Loading画面のFadeIn秒数
        [SerializeField] public float m_keepDuration; //FadeIn後にLoading画面を保持する秒数
        [SerializeField] public float m_fadeOutDuration; //Loading画面のFadeOut秒数
    }

    [SerializeField] private LoadingFadeSettings[] m_fadeSettings = new LoadingFadeSettings[0]; //離脱元State別のFadeIn/保持/FadeOut秒数の登録
    [SerializeField] private float m_titleLogoFadeInDelay = 0.3f; //Loading画面安定後、タイトルロゴのFadeInを開始するまでの追加待機秒数(Splash→Openingのみ)

    /// <summary>
    /// Splash→Opening限定で、再生中のOpening BGMクリップ名別にKeep秒数を上書きする設定用構造体。
    /// BGM再生開始からOpening本編が見えるまでの合計時間を曲ごとに揃えたい場合に使う。
    /// </summary>
    [System.Serializable]
    private struct BgmKeepOverride
    {
        [SerializeField] public string m_bgmClipName; //対象のBGMクリップ名(AudioClip.name)
        [SerializeField] public float m_keepDuration; //このBGMが再生されている場合のKeep秒数
    }

    [SerializeField] private BgmKeepOverride[] m_splashToOpeningBgmKeepOverrides = new BgmKeepOverride[0]; //再生中のOpening BGMクリップ名別のKeep秒数上書き

    private readonly List<Image> m_backgroundImages = new List<Image>(); //モニターごとの黒背景画像

    private GameStateId m_leavingState; //Configure()で通知された離脱元State
    private GameStateId m_enteringState; //Configure()で通知された遷移先State
    private float m_bgmStartedAtRealtime = -1f; //TriggerOpeningTitleRevealでOpening BGMを再生開始した実時間(未設定なら-1)

    private void Awake()
    {
        CreateLoadingCanvases();
    }

    /// <summary>
    /// GameFlowRunnerから遷移元/遷移先のStateを通知する関数。
    /// セッションキャッシュのリフレッシュは(Result→Splashの特殊分岐ではなく)LoadStateへ一本化したため、
    /// このクラスはFadeIn/保持/FadeOutの秒数解決のみを担う汎用コンポーネントに戻している。
    /// </summary>
    /// <param name="_fromState">離脱するState</param>
    /// <param name="_toState">遷移先のState</param>
    public void Configure(GameStateId _fromState, GameStateId _toState)
    {
        m_leavingState = _fromState;
        m_enteringState = _toState;
        AppLog.Log("[SceneFlow] LoadingScreenController.Configure: " + _fromState + " → " + _toState);
    }

    /// <summary>
    /// Loading画面をFadeInで表示します。GameFlowRunnerがこれをyieldし、完了してから次シーンの
    /// ロードを開始することで、ロード負荷とFadeInのタイミング重複による表示不具合を防ぎます。
    /// Splash→Openingの遷移ではLoading画面が安定した後、さらに一呼吸置いてからシーン切替(Exit/ロード/アンロード)を開始します。
    /// タイトルロゴ(TitleLogoOverlay)のFadeInとOpening BGMの先行再生は、Splashアンロード後にTriggerOpeningTitleReveal()で行います。
    /// Opening→Menuの遷移ではEntry BGMを本編のロード開始前に先行再生します。
    /// </summary>
    public IEnumerator PlayFadeIn()
    {
        float? duration = ResolveFadeInDuration();
        AppLog.Log("[SceneFlow] PlayFadeIn: duration=" + (duration?.ToString("F2") ?? "null(即時スナップ)"));

        if (IsOpeningToMenu() && SoundManager.Instance != null)
        {
            AppLog.Log("[SceneFlow] Entry BGMを先行再生開始");
            SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.Entry, true); //Menu本編のロード開始前にBGMを先行再生開始
        }

        yield return Fade(1f, duration); //Loading画面(黒背景)が安定するまで待つ

        if (IsSplashToOpening())
        {
            yield return new WaitForSecondsRealtime(m_titleLogoFadeInDelay); //シーン切替(Exit/ロード/アンロード)を始める前に、安定後さらに一呼吸置く
        }
    }

    /// <summary>
    /// タイトルロゴ(TitleLogoOverlay)のFadeInとOpening BGMの先行再生を同時に開始します。
    /// Splash→Opening限定で、GameFlowRunnerがSplashのアンロード完了後・SetActiveScene前に呼びます
    /// (以前はPlayFadeIn内でLoading画面FadeIn直後に呼んでいたが、Splashが実際に消えるタイミングへ合わせるため移動した)。
    /// </summary>
    public void TriggerOpeningTitleReveal()
    {
        if (!IsSplashToOpening()) { return; }

        if (SoundManager.Instance != null)
        {
            AppLog.Log("[SceneFlow] Opening BGMを先行再生開始");
            SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.OP); //タイトルロゴのFadeIn開始と同時にBGMを先行再生開始
            m_bgmStartedAtRealtime = Time.realtimeSinceStartup; //WaitForKeepでBGM再生開始からの経過秒数を数える基準点
        }
        if (TitleLogoOverlay.Instance != null)
        {
            AppLog.Log("[SceneFlow] TitleLogoOverlay.FadeIn開始");
            StartCoroutine(TitleLogoOverlay.Instance.FadeIn()); //秒数はTitleLogoOverlay自身が持つ
        }
        else
        {
            AppLog.LogWarning("[SceneFlow] TitleLogoOverlay.Instanceが見つからないためタイトルロゴを表示できません");
        }
    }

    /// <summary>
    /// FadeIn後の保持秒数だけ待機します。Splash→Openingは再生中のBGMクリップ名によって秒数が変わり、
    /// Opening→Menuは2.312秒、他は0秒です。
    /// Splash→Openingの秒数は「Opening BGM再生開始からOpening本編が見えるまでの合計時間」を曲ごとに揃えるためのものなので、
    /// TriggerOpeningTitleRevealでのBGM再生開始からここに来るまでに経過した時間(Exit/シーンロード/アンロード等)を差し引いた残りだけ待つ。
    /// </summary>
    public IEnumerator WaitForKeep()
    {
        float? keepDuration = ResolveKeepDuration();
        AppLog.Log("[SceneFlow] WaitForKeep: duration=" + (keepDuration?.ToString("F2") ?? "0"));
        if (keepDuration == null || keepDuration.Value <= 0f) { yield break; }

        float waitSeconds = keepDuration.Value;
        if (IsSplashToOpening() && m_bgmStartedAtRealtime >= 0f)
        {
            float elapsedSinceBgmStart = Time.realtimeSinceStartup - m_bgmStartedAtRealtime; //BGM再生開始からここまでに既に経過した秒数
            waitSeconds = Mathf.Max(0f, keepDuration.Value - elapsedSinceBgmStart);
            AppLog.Log("[SceneFlow] WaitForKeep: BGM再生開始から" + elapsedSinceBgmStart.ToString("F2") + "s経過済みのため、残り" + waitSeconds.ToString("F2") + "s待機");
        }

        yield return new WaitForSecondsRealtime(waitSeconds); //Time.timeScale=0(ポーズ)の影響を受けないよう実時間で待機
    }

    /// <summary>
    /// Loading画面をFadeOutで隠します。完了後にGameFlowRunnerがLoadingシーンをアンロードします。
    /// Opening→Menuの遷移では、Splash→Openingから表示し続けていたタイトルロゴもここで一緒に隠します。
    /// </summary>
    public IEnumerator PlayFadeOut()
    {
        float? duration = ResolveFadeOutDuration();
        AppLog.Log("[SceneFlow] PlayFadeOut: duration=" + (duration?.ToString("F2") ?? "null(即時スナップ)"));

        if (IsOpeningToMenu() && TitleLogoOverlay.Instance != null)
        {
            AppLog.Log("[SceneFlow] TitleLogoOverlay.FadeOut開始");
            StartCoroutine(TitleLogoOverlay.Instance.FadeOut()); //Loading画面の黒背景と同時にタイトルロゴも隠す(秒数はTitleLogoOverlay自身が持つ)
        }

        yield return Fade(0f, duration);
    }

    /// <summary>
    /// 背景を目標アルファ値まで変化させます。_durationがnull(=State未登録)の場合は
    /// アニメーションせず即座にスナップします。
    /// </summary>
    /// <param name="_targetAlpha">目標アルファ値(0:透明 1:不透明)</param>
    /// <param name="_duration">フェードにかける秒数(nullなら即座にスナップ)</param>
    private IEnumerator Fade(float _targetAlpha, float? _duration)
    {
        if (_duration == null)
        {
            SetAlphaImmediate(_targetAlpha);
            yield break;
        }

        int remaining = m_backgroundImages.Count; //完了待ちの枚数
        if (remaining == 0) { yield break; }

        for (int i = 0; i < m_backgroundImages.Count; ++i)
        {
            m_backgroundImages[i].DOFade(_targetAlpha, _duration.Value).SetUpdate(true).OnComplete(() => { --remaining; });
        }

        yield return new WaitUntil(() => remaining <= 0);
    }

    //現在の離脱元StateがSplash、遷移先StateがOpeningかどうかを判定する関数
    private bool IsSplashToOpening()
    {
        return m_leavingState == GameStateId.Splash && m_enteringState == GameStateId.Opening;
    }

    //現在の離脱元StateがOpening、遷移先StateがMenuかどうかを判定する関数
    private bool IsOpeningToMenu()
    {
        return m_leavingState == GameStateId.Opening && m_enteringState == GameStateId.Menu;
    }

    /// <summary>
    /// 現在の離脱元StateのFadeIn秒数を取得します。m_fadeSettingsに登録が無ければnullを返します。
    /// </summary>
    private float? ResolveFadeInDuration()
    {
        return ResolveDuration((_settings) => _settings.m_fadeInDuration);
    }

    /// <summary>
    /// 現在の離脱元Stateの保持秒数を取得します。Splash→Openingでは再生中のBGMクリップ名に
    /// 一致する上書き設定(m_splashToOpeningBgmKeepOverrides)があればそちらを優先します。
    /// どちらにも登録が無ければnullを返します。
    /// </summary>
    private float? ResolveKeepDuration()
    {
        if (IsSplashToOpening())
        {
            float? bgmOverride = ResolveSplashToOpeningKeepDurationByBGM();
            if (bgmOverride != null) { return bgmOverride; }
        }
        return ResolveDuration((_settings) => _settings.m_keepDuration);
    }

    /// <summary>
    /// 再生中のOpening BGMクリップ名からm_splashToOpeningBgmKeepOverridesを検索し、
    /// 一致するKeep秒数を返します。SoundManager未初期化や未登録の曲なら null です。
    /// </summary>
    private float? ResolveSplashToOpeningKeepDurationByBGM()
    {
        if (SoundManager.Instance == null) { return null; }

        string clipName = SoundManager.Instance.GetCurrentBGMClipName();
        if (string.IsNullOrEmpty(clipName)) { return null; }

        for (int i = 0; i < m_splashToOpeningBgmKeepOverrides.Length; ++i) //登録済みクリップ名を探索するループ
        {
            if (m_splashToOpeningBgmKeepOverrides[i].m_bgmClipName == clipName) { return m_splashToOpeningBgmKeepOverrides[i].m_keepDuration; }
        }
        return null;
    }

    /// <summary>
    /// 現在の離脱元StateのFadeOut秒数を取得します。m_fadeSettingsに登録が無ければnullを返します。
    /// </summary>
    private float? ResolveFadeOutDuration()
    {
        return ResolveDuration((_settings) => _settings.m_fadeOutDuration);
    }

    /// <summary>
    /// m_fadeSettingsから現在の離脱元Stateに一致する設定を探し、指定した秒数を取り出します。
    /// </summary>
    /// <param name="_selector">見つかった設定から秒数を取り出す関数</param>
    private float? ResolveDuration(Func<LoadingFadeSettings, float> _selector)
    {
        for (int i = 0; i < m_fadeSettings.Length; ++i) //離脱元State別登録を探索するループ
        {
            if (m_fadeSettings[i].m_leavingState == m_leavingState) { return _selector(m_fadeSettings[i]); }
        }
        return null;
    }

    //即座に背景のアルファ値を設定する関数(アニメーションなし)
    private void SetAlphaImmediate(float _alpha)
    {
        for (int i = 0; i < m_backgroundImages.Count; ++i)
        {
            Color color = m_backgroundImages[i].color;
            color.a = _alpha;
            m_backgroundImages[i].color = color;
        }
    }

    //接続モニター数に応じたLoading用Canvasを生成する関数
    private void CreateLoadingCanvases()
    {
        int displayCount = Application.isEditor ? k_EditorDisplayCount : Mathf.Max(1, Display.displays.Length);

        for (int i = 0; i < displayCount; ++i) //モニター1台につきCanvasを1枚生成するループ
        {
            CreateLoadingCanvas(i);
        }
    }

    /// <summary>
    /// 指定したモニターへ黒背景を表示するCanvasを1枚生成します。
    /// </summary>
    /// <param name="_displayIndex">対象モニターのディスプレイ番号</param>
    private void CreateLoadingCanvas(int _displayIndex)
    {
        GameObject canvasObject = new GameObject($"LoadingCanvas_{_displayIndex}");
        canvasObject.transform.SetParent(transform);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = _displayIndex;
        canvas.sortingOrder = k_SortOrder;

        canvasObject.AddComponent<CanvasScaler>();

        CreateBackgroundImage(canvasObject.transform);
    }

    /// <summary>
    /// 全画面を覆う黒背景を生成します(初期状態はalpha 0、PlayFadeInが表示制御する)。
    /// </summary>
    /// <param name="_parent">生成先のCanvas</param>
    private void CreateBackgroundImage(Transform _parent)
    {
        GameObject backgroundObject = new GameObject("Background");
        backgroundObject.transform.SetParent(_parent, false);

        Image image = backgroundObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f); //PlayFadeIn開始まで非表示(裏側はTransitionFadeControllerの不透明フェードで既に覆われている)

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        m_backgroundImages.Add(image);
    }
}
