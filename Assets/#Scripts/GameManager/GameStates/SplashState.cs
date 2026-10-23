/*━━━━━━━━━
@file    SplashState.cs
@brief   起動シーンSplashの新エンジン版State実装
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/01
━━━━━━━━━*/

using UnityEngine;

public class SplashState : GameStateBindingBase
{
    [SerializeField] private SplashFade m_splashFade; //終了検知に使うフェード演出

    private ChairRideOperator m_rideOff = new ChairRideOperator(); //チェアを降車姿勢へ戻す
    private WIZMOController m_wizmoController;

    public override GameStateId Id => GameStateId.Splash;

    public override void Enter()
    {
        base.Enter();
        m_wizmoController = GameManager.Instance.WIZMO;
    }

    public override void Tick(float _deltaTime)
    {
        if (m_wizmoController != null) //降車姿勢を毎フレーム維持する(旧実装踏襲)
        {
            m_rideOff.RideOff(m_wizmoController);
        }

        if (m_splashFade != null && m_splashFade.IsEndAnimation)
        {
            GameFlowRunner.Instance.AdvanceToNext();
        }
    }
}
