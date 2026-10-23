/*━━━━━━━━━
@file    IntroState.cs
@brief   イントロ演出Intro(旧Movie)の新エンジン版State実装
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/14
━━━━━━━━━*/

using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

public class IntroState : GameStateBindingBase
{
    private const float k_MoviePlayDelaySeconds = 0.5f; //IntroStateがStart(Enter)してから映像再生を始めるまでの待機秒数

    [SerializeField] private PlayableDirector m_moviePlay; //イントロ映像の再生と終了判定に使う
    [SerializeField] private VehicleController m_vehicle; //イントロ中の車両固定に使う
    [SerializeField] private UI_StarCountDown m_startAction; //インゲーム開始前のカウントダウン
    [SerializeField] private DriveSound m_driveSound; //インゲーム開始後の走行音
    [SerializeField] private MeterUIManager m_meterUIManager; //イントロ中にメーターを隠す
    [SerializeField] private GameObject m_okutamaNameDisplay; //Intro専用の「OkutamaName」表示。Timelineが表示制御するがIntro終了時に強制的に隠す

    private WIZMOController m_wizmoController;
    private readonly ChairRideOperator m_drive = new ChairRideOperator(); //イントロ中の椅子駆動
    private bool m_isSkipped; //イントロをスキップしたか
    private bool m_hasFinished; //インゲームへ複数回切り替えないためのフラグ

    public override GameStateId Id => GameStateId.Intro;

    public override void Enter()
    {
        base.Enter();
        m_isSkipped = false;
        m_hasFinished = false;

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.StopBGM();
            SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.InGame,true);
        }
        if (GameManager.Instance != null) { m_wizmoController = GameManager.Instance.WIZMO; }

        if (m_vehicle != null) { m_vehicle.PrepareIntroGrounding(); }
        if (m_startAction != null) { m_startAction.enabled = false; }
        if (m_driveSound != null) { m_driveSound.enabled = false; }
        if (m_wizmoController != null) { m_drive.Drive(m_wizmoController); }

        if (m_moviePlay != null) { StartCoroutine(PlayMovieAfterDelay()); } //Enter()からk_MoviePlayDelaySeconds待ってから映像再生を開始する
    }

    //Enter()からk_MoviePlayDelaySeconds待ってから映像再生を開始するコルーチン
    private IEnumerator PlayMovieAfterDelay()
    {
        yield return new WaitForSecondsRealtime(k_MoviePlayDelaySeconds); //Time.timeScaleの影響を受けないよう実時間で待機
        m_moviePlay.Play();
    }

    public override void Tick(float _deltaTime)
    {
        if (m_meterUIManager != null) { m_meterUIManager.HideMeter(); }
        if (m_hasFinished) { return; }

        if (m_moviePlay == null)
        {
            ChangeToRacing();
            return;
        }

        if (Input.GetKeyDown(KeyCode.PageDown) && !m_isSkipped)
        {
            m_moviePlay.time = Mathf.Max(0f, (float)m_moviePlay.duration - 0.1f);
            m_isSkipped = true;
        }

        if (m_moviePlay.time >= m_moviePlay.duration) { ChangeToRacing(); }
    }

    //カウントダウンを有効化してRacingへ一度だけ切り替える
    private void ChangeToRacing()
    {
        if (m_hasFinished) { return; }
        m_hasFinished = true;

        if (m_meterUIManager != null) { m_meterUIManager.ShowMeter(); }
        if (m_startAction != null)
        {
            m_startAction.enabled = true;
            StartGateKeyboardFallback.Attach(m_startAction, m_vehicle);
        }
        if (m_driveSound != null) { m_driveSound.enabled = true; }
        if (m_okutamaNameDisplay != null) { m_okutamaNameDisplay.SetActive(false); } //TimelineがどこまでOkutamaNameを表示していても、Intro終了と同時に強制的に隠す

        GameFlowRunner.Instance.AdvanceToNext();
    }
}
