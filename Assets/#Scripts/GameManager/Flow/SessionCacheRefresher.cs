/*━━━━━━━━━
@file    SessionCacheRefresher.cs
@brief   1セッション分のプレイで溜まる状態・キャッシュをリフレッシュする共通処理
         (元はLoadingScreenController.RefreshSessionCachesに実装されていたものをLoadStateと共用するために抜き出した)
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/16
         最終更新日 : 2026/10/22
━━━━━━━━━*/

using System.Collections;
using AimRacing.Ranking;
using UnityEngine;

public static class SessionCacheRefresher
{
    //FlowTimingで通知するステップ名(PerfTransitionRecorderがそのまま書き出す。docs/Perf.mdのtransitions.csvの節と一致させる)
    private const string k_StepResetState = "ResetState"; //名前・結果・サウンド・ポーズのリセット
    private const string k_StepGc1 = "GC1"; //1回目のGC(未使用アセットの判定の前処理)
    private const string k_StepUnloadUnusedAssets = "UnloadUnusedAssets"; //未使用アセットの解放
    private const string k_StepGc2 = "GC2"; //2回目のGC(ファイナライザ待ちを含む)
    private const string k_NoDetail = ""; //詳細が無いステップに渡す値

    /// <summary>
    /// AimRacingPlayerName(前回のプレイヤー名)、GameManagerの計測タイム、SoundManagerのフェード状態、
    /// DebugKeyのポーズ状態をリセットしたうえで、Resources.UnloadUnusedAssets()とGC.Collect()で
    /// 未使用アセット・ゴミを回収します。ファイナライザ待ちのオブジェクトも含めて確実に回収し、
    /// セッションを繰り返すごとにガベージが積み重なっていかないようにします。
    /// </summary>
    public static IEnumerator Refresh()
    {
        float startTime = Time.realtimeSinceStartup; //計測用の基準時刻
        FlowTiming.Begin(FlowTimingKind.Refresh, GameStateId.Load, GameStateId.Load, startTime); //LoadStateだけが呼ぶので、入るStateはLoad

        AimRacingPlayerName.Clear(); //前のプレイヤーの名前を次セッションへ引き継がない
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResultTime = string.Empty; //前回の計測タイムを消去
            GameManager.Instance.SetLapTimes(null); //前回のラップタイムを消去
        }
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ResetFadeState(); //フェード関連フラグ・コルーチンをリセット
        }
        DebugKey debugKey = UnityEngine.Object.FindFirstObjectByType<DebugKey>();
        if (debugKey != null)
        {
            debugKey.ResetPauseState(); //m_bPauseとTime.timeScaleの不整合を防ぐ
        }
        float prevTime = NotifyStep(k_StepResetState, startTime);

        System.GC.Collect(); //UnloadUnusedAssetsが未参照アセットを正しく判定できるよう、先にマネージド側の参照を解放しておく
        prevTime = NotifyStep(k_StepGc1, prevTime);

        yield return Resources.UnloadUnusedAssets(); //直前のプレイで不要になったアセットを解放
        prevTime = NotifyStep(k_StepUnloadUnusedAssets, prevTime);

        //ファイナライザ待ちのオブジェクトは1回のCollectだけでは回収されずキャッシュとして残ることがあるため、
        //WaitForPendingFinalizers()を挟んで2回Collectし、蓄積したガベージを確実に回収する
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();
        prevTime = NotifyStep(k_StepGc2, prevTime);

        FlowTiming.End(prevTime);
    }

    /// <summary>
    /// 直前の計測ポイントからの経過時間を、計測側(FlowTiming)へ作業として通知します。
    /// </summary>
    /// <param name="_step">ステップ名(定数のk_Step〜を渡す)</param>
    /// <param name="_prevTime">直前の計測時刻(Time.realtimeSinceStartup)</param>
    private static float NotifyStep(string _step, float _prevTime)
    {
        float now = Time.realtimeSinceStartup;
        FlowTiming.Step(_step, k_NoDetail, FlowStepType.Work, now - _prevTime);
        return now;
    }
}
