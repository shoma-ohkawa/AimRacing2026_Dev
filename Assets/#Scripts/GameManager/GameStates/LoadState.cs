/*━━━━━━━━━
@file    LoadState.cs
@brief   起動時とF1(ReturnToTitle)リセット時に必ず経由する中継StateのLoad実装。
         自前のカメラを持つLoadシーンでManager/MapEnvironmentの読込安定を待ち、
         セッションキャッシュをリフレッシュしたのちSplashへ進む。
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/16
         最終更新日 : 2026/09/16
━━━━━━━━━*/

using System.Collections;

public class LoadState : GameStateBindingBase
{
    public override GameStateId Id => GameStateId.Load;

    private bool m_isWaitingForFadeIn; //自身のFadeIn完了通知を待っている間だけtrue(多重発火防止)

    public override void Enter()
    {
        base.Enter();
        m_isWaitingForFadeIn = true;
        GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete; //自身のFadeInが完了する(=画面に確実に映る)まで待つ
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
    }

    //GameFlowRunnerからのFadeIn完了通知を受け、自身宛てのものだけ処理する関数
    private void OnStateFadeInComplete(GameStateId _completedId)
    {
        if (_completedId != Id || !m_isWaitingForFadeIn) { return; }
        m_isWaitingForFadeIn = false;
        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
        StartCoroutine(RefreshAndAdvance());
    }

    //セッションキャッシュをリフレッシュしたのち、Splashへ進む関数
    private IEnumerator RefreshAndAdvance()
    {
        yield return SessionCacheRefresher.Refresh();
        GameFlowRunner.Instance.AdvanceToNext();
    }
}
