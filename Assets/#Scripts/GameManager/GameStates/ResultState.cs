/*━━━━━━━━━
@file    ResultState.cs
@brief   結果表示Resultの新エンジン版State実装(旧End統合)
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/14
━━━━━━━━━*/

using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public class ResultState : GameStateBindingBase
{
    [SerializeField] private CanvasGroup m_resultUI; //先に表示するUI一式(ResultRoot配下のImage+Text、m_resultDisplayDuration秒後にFadeOut)
    [SerializeField] private CanvasGroup m_thankYouCanvasGroup; //m_resultDisplayDuration秒後にFadeInするCanvas
    [SerializeField] private float m_resultDisplayDuration = 30f; //ResultUI(ResultCanvas)を表示しておく秒数
    [SerializeField] private float m_fadeDuration = 1f; //ResultUIとThank youを切り替える際のFade時間

    private WIZMOController m_wizmoController;
    private readonly ChairRideOperator m_rideOff = new ChairRideOperator(); //旧GameStateManager_End.csから移植

    public override GameStateId Id => GameStateId.Result;

    public override void Enter()
    {
        base.Enter();
        EnsureDisplayCamera();
        if (!SoundManager.Instance.IsBGMPlaying()) //RacingState.WaitFadeOut()で既に先行再生済みなら再度呼ばない(二重呼び出しはSwitchBGMの副作用で無関係な曲へクロスフェードしてしまう)
        {
            SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.Result,true);
        }
        if (GameManager.Instance != null) { m_wizmoController = GameManager.Instance.WIZMO; }

        if (m_thankYouCanvasGroup != null) { m_thankYouCanvasGroup.gameObject.SetActive(true); } //Fade可能にするため先にアクティブ化(表示自体はalpha=0で隠す)
        SetCanvasGroupState(m_resultUI, 1f, true);
        SetCanvasGroupState(m_thankYouCanvasGroup, 0f, false);

        StartCoroutine(ShowThankYouAfterDelay());
    }

    /// <summary>
    /// m_resultDisplayDuration秒待ってからResultUIをFadeOutし、完了後にThank youをFadeInします。
    /// </summary>
    private IEnumerator ShowThankYouAfterDelay()
    {
        yield return new WaitForSeconds(m_resultDisplayDuration);

        if (m_resultUI != null)
        {
            m_resultUI.interactable = false; //Fade開始と同時に入力を無効化する
            m_resultUI.blocksRaycasts = false;
            m_resultUI.DOFade(0f, m_fadeDuration).OnComplete(FadeInThankYou); //ResultUIが消えきってからThank youを表示する
        }
        else
        {
            FadeInThankYou();
        }
    }

    /// <summary>
    /// ResultUIのFadeOut完了を受けてThank youをFadeInします。
    /// </summary>
    private void FadeInThankYou()
    {
        if (m_thankYouCanvasGroup == null) { return; }
        m_thankYouCanvasGroup.DOFade(1f, m_fadeDuration)
            .OnComplete(() => SetCanvasGroupState(m_thankYouCanvasGroup, 1f, true));
    }

    /// <summary>
    /// CanvasGroupの表示状態(透明度・入力可否)をまとめて設定します。
    /// </summary>
    /// <param name="_group">対象のCanvasGroup</param>
    /// <param name="_alpha">設定する透明度</param>
    /// <param name="_interactable">入力を受け付けるかどうか</param>
    private void SetCanvasGroupState(CanvasGroup _group, float _alpha, bool _interactable)
    {
        if (_group == null) { return; }
        _group.alpha = _alpha;
        _group.interactable = _interactable;
        _group.blocksRaycasts = _interactable;
    }

    public override void Tick(float _deltaTime)
    {
        if (m_wizmoController != null) //椅子を降車位置へ戻す
        {
            m_rideOff.RideOff(m_wizmoController);
        }
    }

    /// <summary>
    /// Result画面のOverlay Canvasを表示するためのCameraを用意します。
    /// </summary>
    private void EnsureDisplayCamera()
    {
        Scene resultScene = gameObject.scene; //Resultシーン
        GameObject[] rootObjects = resultScene.GetRootGameObjects(); //Result内のルートObject
        for (int i = 0; i < rootObjects.Length; ++i)
        {
            Camera[] cameras = rootObjects[i].GetComponentsInChildren<Camera>(true); //Result内のCamera
            for (int j = 0; j < cameras.Length; ++j)
            {
                if (cameras[j].enabled && cameras[j].targetTexture == null) { return; }
            }
        }

        GameObject cameraObject = new GameObject("ResultDisplayCamera");
        SceneManager.MoveGameObjectToScene(cameraObject, resultScene);
        Camera displayCamera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<HDAdditionalCameraData>();
        displayCamera.clearFlags = CameraClearFlags.SolidColor;
        displayCamera.backgroundColor = Color.black;
        displayCamera.cullingMask = 0;
    }
}
