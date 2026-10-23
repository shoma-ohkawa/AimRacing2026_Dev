/*━━━━━━━━━
@file    RacingState.cs
@brief   走行中Racing(旧Ingame)の新エンジン版State実装
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/10/15
━━━━━━━━━*/

using System.Collections;
using UnityEngine;

public class RacingState : GameStateBindingBase
{
    private const int k_InitialState = 0; //ゴール処理前の状態
    private const int k_GoalState = 1; //ゴール処理開始後の状態
    private const float k_GoalFadeDelaySeconds = 0f; //ゴール表示からフェードまでの待機秒数(余分な走行を見せないため即座にFadeOutする)

    [SerializeField] private VehicleController m_vehicle;
    [SerializeField] private Line_StartFinish m_finishLine;
    [SerializeField] private ChairController m_chairController;
    [SerializeField] private MeterUIManager m_meterUIManager;
    [SerializeField] private GameObject m_okutamaNameDisplay; //IntroStateと同一オブジェクトを参照。EmergencyReset(F2)でIntroを飛ばした場合の非表示安全網
    [SerializeField] private RacingEnterEffectPattern m_enterEffectPattern = RacingEnterEffectPattern.LaunchCombo; //Racingに入った時の演出パターン。Noneで演出なし

    private Coroutine m_coroutine; //ゴール後フェード処理
    private int m_state; //現在のゴール処理状態
    private RacingEnterEffect m_pEnterEffect; //Racingに入った時の演出の再生役。初回のEnterで同じGameObjectに追加する

    public override GameStateId Id => GameStateId.Racing;

    public override void Enter()
    {
        base.Enter();
        m_state = k_InitialState;
        m_coroutine = null;

        m_chairController.InGameMove = true;
        m_meterUIManager.ShowMeter();
        if (m_okutamaNameDisplay != null) { m_okutamaNameDisplay.SetActive(false); } //Intro専用表示。通常はIntroState側で既に隠れているが、直接Racingへ入った場合に備えて強制非表示にする

        m_vehicle.PullUp(true);
        if (GameManager.Instance.DrivingSettings.isAT)
        {
            m_vehicle.Transmission.Type = Transmission.TransmissionType.Automatic;
        }
        else
        {
            m_vehicle.Transmission.Type = Transmission.TransmissionType.Manual;
        }

        PlayEnterEffect();
    }

    public override void Exit()
    {
        base.Exit();
        if (m_pEnterEffect != null) { m_pEnterEffect.Stop(); } //演出の途中でゴールした場合も、カメラとVolumeを元に戻す
    }

    //Racingに入った時の演出を再生する。Noneのときは何もしない
    private void PlayEnterEffect()
    {
        if (m_enterEffectPattern == RacingEnterEffectPattern.None) { return; }
        if (m_pEnterEffect == null) { m_pEnterEffect = gameObject.AddComponent<RacingEnterEffect>(); }

        m_pEnterEffect.Play(m_enterEffectPattern);
    }

    public override void Tick(float _deltaTime)
    {
        if (DebugKey.GetKeyDown(DebugKeyAction.ChangeTransmission)) //AT/MT切り替え用デバッグキー F5
        {
            m_vehicle.ChangeMissionType();
        }

        if (DebugKey.GetKeyDown(DebugKeyAction.CompleteRace)) //デバッグ操作でも通常のゴール後処理を通す
        {
            if (Time.timeScale <= 0.0f) { Time.timeScale = 1.0f; }
            m_finishLine.CompleteRaceForDebug();
        }

        if (m_finishLine.IsChecked) //ゴール時
        {
            if (m_state == k_InitialState)
            {
                Music.Resume();
                SoundManager.Instance.PlaySE(SoundManager.SE_Type.Goal);

                m_state = k_GoalState;

                //FFB停止に失敗しても(ハンドル切断・SDK未接続等)ゴール処理自体は必ず進める
                try
                {
                    if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(0))
                    {
                        LogitechGSDK.LogiPlayConstantForce(0, 0);
                        LogitechGSDK.LogiStopConstantForce(0);
                        LogitechGSDK.LogiPlayDamperForce(0, 10);
                        LogitechGSDK.LogiStopDamperForce(0);
                    }
                }
                catch (System.Exception e)
                {
                    AppLog.LogWarning("[RacingState] ゴール時のFFB停止に失敗しましたが、ゴール処理は継続します: " + e.Message);
                }

                if (m_coroutine == null)
                {
                    m_coroutine = StartCoroutine(WaitFadeOut(k_GoalFadeDelaySeconds));
                }
            }

            m_chairController.InGameMove = false; //椅子停止
        }
    }

    private IEnumerator WaitFadeOut(float _waittime)
    {
        yield return new WaitForSeconds(_waittime);

        SoundManager.Instance.PlayAndSwitchBGM(SoundManager.BGM_Type.Result, true); //ResultState.Enter()側もloop:trueで参照するため揃える(異なるとPlayBGMの早期returnでloop更新がスキップされた時に無限ループしなくなる)
        GameFlowRunner.Instance.AdvanceToNext();
    }
}
