/*━━━━━━━━━
@file    OpeningState.cs
@brief   オープニングムービーシーンOpeningの新エンジン版State実装
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/17
━━━━━━━━━*/

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class OpeningState : GameStateBindingBase
{
    private const double k_SecondSongStartTime = 113.33333333333334; //OpeningTimeline 2曲目"AimRacing2024 OP modify"の開始時刻(秒)
    private const string k_SplashSceneName = "Splash"; //曲間に一時的に追加ロードするシーン名
    private const float k_InterludeFadeDuration = 0.3f; //曲間Splash切替時のフェード秒数(GameFlowRunnerの遷移フェードと同じ長さ)

    private const double k_TimelineFrameRate = 60d; //OpeningTimelineのm_Framerateに合わせる
    private const double k_ReturnToLoadTimelineFrame = 5540d; //この少し手前からFadeOutを開始しLoadへ戻る基準フレーム(Timeline上の位置)
    private const double k_ReturnToLoadLeadFrames = 90d; //基準フレームに対して何フレーム手前でFadeOutを開始するか(60fpsで1.5秒)

    [SerializeField]
    float m_BGMFadeTime = 0f;   //BGMのフェードにかかる時間
    [SerializeField]
    float m_loopInterval = 1.0f;    //BGM同士の感覚

    [SerializeField] private PlayableDirector m_director; //OPムービー本体
    [SerializeField] private AudioSource m_opAudio; //Timeline駆動のOP音声出力
    [SerializeField] private float m_fadeOutDuration = 1f; //入力確定時にOP音声をフェードアウトする秒数

    private bool m_triger; //ムービー側からのスキップ許可
    private bool m_changed; //遷移開始済みか(多重発火防止、ペダルスキップ・Loadへの自動復帰の両方で共有する)
    private bool m_bSplashInterludeTriggered; //曲間Splash挿入の多重発火防止
    private bool m_bIsPlayingSplashInterlude; //曲間Splash再生中か(true の間は入力を無視する)
    private bool m_isReady; //OpeningのFadeIn完了(=確実に画面へ描画された)までtrueにならない。Loading画面中の誤入力を防ぐ

    private readonly AxisPressdOnce m_pressedOnce = new AxisPressdOnce();
    private readonly ChairRideOperator m_gettingOn = new ChairRideOperator(); //チェアを乗車位置へ移行

    private WIZMOController m_wizmoController;

    public override GameStateId Id => GameStateId.Opening;

    private void OnEnable()
    {
        GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete;
    }

    private void OnDisable()
    {
        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
    }

    /// <summary>
    /// GameFlowRunnerのFadeIn完了通知を受け取り、Openingへの遷移であれば入力受付を開始します。
    /// </summary>
    /// <param name="_state">FadeInが完了したState</param>
    private void OnStateFadeInComplete(GameStateId _state)
    {
        if (_state != GameStateId.Opening) { return; }
        m_isReady = true;
    }

    public override void Enter()
    {
        base.Enter();
        MenuInputDeviceSharing.Ensure(GetComponent<PlayerInput>()); //Manager常駐のPlayerInputに機器を先取りされていても入力が空にならないようにする
        AppLog.Log("[SceneFlow] OpeningState.Enter()");
        m_director.Play();
        if (!SoundManager.Instance.IsBGMPlaying()) //Loading画面側で先行再生済みなら再度呼ばない(二重呼び出しはSwitchBGMの副作用で無関係な曲へクロスフェードしてしまう)
        {
            AppLog.Log("[SceneFlow] BGM未再生のためEnter()内で新規再生");
            SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.OP);
        }
        else
        {
            AppLog.Log("[SceneFlow] BGM再生中のためEnter()内での再生をスキップ(Loading画面で先行再生済み)");
        }
        m_wizmoController = GameManager.Instance.WIZMO;
    }

    public override void Tick(float _deltaTime)
    {
        //タイムラインがOPの再生時間を過ぎたらSplashシーンに移る
        if (!m_bSplashInterludeTriggered && m_director.time >= SoundManager.Instance.GetBGMLen() + m_loopInterval)
        {
            AppLog.Log("[SceneFlow] PlaySplashInterlude発火: director.time=" + m_director.time.ToString("F2") + " GetBGMLen()=" + SoundManager.Instance.GetBGMLen().ToString("F2"));
            m_bSplashInterludeTriggered = true;
            m_bIsPlayingSplashInterlude = true;
            StartCoroutine(PlaySplashInterlude());
        }

        if (m_bIsPlayingSplashInterlude) { return; } //曲間Splash再生中はムービー側の入力判定を止める

        //Timelineが規定フレームの少し手前まで進んだら、入力を待たずFadeOutしてLoadへ戻る(PV/デモ用の折り返し地点)
        if (!m_changed && m_director.time >= (k_ReturnToLoadTimelineFrame - k_ReturnToLoadLeadFrames) / k_TimelineFrameRate)
        {
            m_changed = true;
            ReturnToLoad();
            return;
        }

        if (!m_pressedOnce.PressedOnce && !m_triger) { return; }

        if (!m_changed)
        {
            //// BGMフェードアウト開始
            SoundManager.Instance.FadeOutBGM(m_BGMFadeTime);
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.OPPushSE);
            m_opAudio.DOFade(0, m_fadeOutDuration).SetEase(Ease.Linear);
            m_changed = true;
            GameFlowRunner.Instance.AdvanceToNext();
        }

        if (m_wizmoController != null) //椅子を乗車位置にセッティング
        {
            m_gettingOn.Ride(m_wizmoController);
        }
    }

    /// <summary>
    /// OpeningTimelineが規定フレームまで進んだ際に、BGM/OP音声をフェードアウトしつつLoadへ強制リセットします。
    /// PV/デモとして繰り返し再生する用途のため、通常のMenuへの遷移は行いません。
    /// </summary>
    private void ReturnToLoad()
    {
        AppLog.Log("[SceneFlow] OpeningTimelineが規定フレーム(" + k_ReturnToLoadTimelineFrame + ")に到達したためLoadへ戻ります");
        //画面のFadeOut(GameFlowRunner.EmergencyResetがTransitionFadeController経由で行う)と同じ秒数でBGM/OP音声も合わせてフェードアウトする
        float screenFadeOutSeconds = TransitionFadeController.Instance.ResolveFadeOutDuration(GameStateId.Opening) ?? m_fadeOutDuration;
        SoundManager.Instance.FadeOutBGM(screenFadeOutSeconds);
        m_opAudio.DOFade(0, screenFadeOutSeconds).SetEase(Ease.Linear);
        GameFlowRunner.Instance.EmergencyReset(GameStateId.Load); //Load→Manager/MapEnvironment安定待ち→Splashの正規フローで最初から繰り返す
    }

    public void OnPedal(InputAction.CallbackContext _context)
    {
        if (!m_isReady) { return; } //Loading画面中などOpeningがまだ画面に描画されていない間は入力を無視する

        float value = _context.ReadValue<float>();
        value = 1 - (value + 1) / 2;
        m_pressedOnce.AxisCheck(value);
    }

    public void OnTriger()
    {
        if (!m_isReady) { return; } //Loading画面中などOpeningがまだ画面に描画されていない間は入力を無視する

        m_triger = true;
    }

    //OpeningTimelineを1曲目終了時点で一時停止し、Splashシーンを追加ロードして再生した後、2曲目から再開する処理
    private IEnumerator PlaySplashInterlude()
    {
        m_director.Stop();
        SoundManager.Instance.FadeOutBGM(1.0f);
        Camera openingCamera = Camera.main; //Splashロード前なのでOpening自身のカメラを取得できる

        yield return TransitionFadeController.Instance.FadeOut(k_InterludeFadeDuration, Color.white); //Splashへ切り替える前の白フェード

        yield return SceneManager.LoadSceneAsync(k_SplashSceneName, LoadSceneMode.Additive);

        if (openingCamera != null) //Splash側のカメラへ完全に切り替える
        {
            openingCamera.gameObject.SetActive(false);
        }

        yield return TransitionFadeController.Instance.FadeIn(k_InterludeFadeDuration); //Splash側の映像を表示

        Scene splashScene = SceneManager.GetSceneByName(k_SplashSceneName);
        SplashFade splashFade = FindSplashFade(splashScene);
        while (splashFade != null && !splashFade.IsEndAnimation)
        {
            yield return null;
        }

        yield return TransitionFadeController.Instance.FadeOut(k_InterludeFadeDuration, Color.white); //Openingへ戻る前の白フェード

        if (openingCamera != null) //Openingのカメラへ戻す
        {
            openingCamera.gameObject.SetActive(true);
        }

        yield return SceneManager.UnloadSceneAsync(splashScene);

        m_director.time = 0.0f;
        m_director.Play();
        SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.OP);

        yield return TransitionFadeController.Instance.FadeIn(k_InterludeFadeDuration); //Openingの続きを表示
        m_bSplashInterludeTriggered = false;
        m_bIsPlayingSplashInterlude = false;
    }

    //追加ロードしたSplashシーンのルートオブジェクトからSplashFadeを検索する関数
    private SplashFade FindSplashFade(Scene _scene)
    {
        foreach (GameObject rootObject in _scene.GetRootGameObjects())
        {
            SplashFade splashFade = rootObject.GetComponentInChildren<SplashFade>(true);
            if (splashFade != null)
            {
                return splashFade;
            }
        }
        return null;
    }
}
