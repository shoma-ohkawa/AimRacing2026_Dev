/*━━━━━━━━━
@file    FlowTiming.cs
@brief   遷移エンジンの各ステップの所要時間を、計測側へ通知するだけの部品(購読者がいなければ何もしない)
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/19
         最終更新日 : 2026/10/19
━━━━━━━━━*/

using System;

//通知する処理の種類
public enum FlowTimingKind
{
    Boot,       //起動(最初のシーンのAwake後のBootstrapから、最初のFadeInの完了まで)
    Transition, //通常の遷移(AdvanceToNextから、遷移先のFadeInの完了まで)
    Reset,      //緊急リセット(EmergencyResetから、FadeInの完了まで)
    Refresh     //Loadでのセッションのリフレッシュ(SessionCacheRefresher.Refresh)
}

//ステップの種類。エンジンの速さの比較は、Workの合計で行う(Waitは設定で決まる待ちのため)
public enum FlowStepType
{
    Work, //ロード・Exit・Enter・描画の安定待ち(StableFrames。最初の描画の重さで時間が決まる)など、実装で変わる作業
    Wait  //フェード・保持秒数など、設定で決まる待ち(フレームが止まると、実時間で伸びることがある)
}

//遷移エンジン側の計測点。購読者(PerfTransitionRecorder)がいなければ、null判定だけで何もしない
//呼び出す側は Begin → Step(複数回) → End の順に通知する。Beginを呼ばずに Step / End を呼んでも、受け取る側は無視する
public static class FlowTiming
{
    /// <summary>
    /// 処理の開始。引数は、処理の種類・離脱元State・遷移先State・開始時刻(Time.realtimeSinceStartup)の順です。
    /// </summary>
    public static event Action<FlowTimingKind, GameStateId, GameStateId, float> OnBegin;

    /// <summary>
    /// ステップの終了。引数は、ステップ名・詳細・種類・所要秒数・Beginより前に終わった作業かの順です。
    /// </summary>
    public static event Action<string, string, FlowStepType, float, bool> OnStep;

    /// <summary>
    /// 処理の終了。引数は、終了時刻(Time.realtimeSinceStartup)です。
    /// </summary>
    public static event Action<float> OnEnd;

    /// <summary>
    /// 処理の開始を通知します。前の処理が終了していなければ、受け取る側は中断された処理として扱います。
    /// </summary>
    /// <param name="_kind">処理の種類</param>
    /// <param name="_from">離脱元State(BootとRefreshは、入るStateを渡す)</param>
    /// <param name="_to">遷移先State</param>
    /// <param name="_startSeconds">開始時刻(Time.realtimeSinceStartup。Bootは起動時の0を渡す)</param>
    public static void Begin(FlowTimingKind _kind, GameStateId _from, GameStateId _to, float _startSeconds)
    {
        OnBegin?.Invoke(_kind, _from, _to, _startSeconds);
    }

    /// <summary>
    /// ステップの終了を通知します。通知する文字列は、受け取る側がそのまま書き出します(新しい文字列を作らないため)。
    /// </summary>
    /// <param name="_step">ステップ名</param>
    /// <param name="_detail">詳細(シーン名など。無ければ空文字)</param>
    /// <param name="_type">作業か待ちか</param>
    /// <param name="_durationSeconds">ステップの所要秒数</param>
    /// <param name="_bBeforeBegin">Beginより前に終わった作業か(trueなら、GCとメモリの値は取らない)</param>
    public static void Step(string _step, string _detail, FlowStepType _type, float _durationSeconds, bool _bBeforeBegin = false)
    {
        OnStep?.Invoke(_step, _detail, _type, _durationSeconds, _bBeforeBegin);
    }

    /// <summary>
    /// 処理の終了を通知します。OnStateFadeInCompleteを発火する前に呼び、購読者の処理を所要時間に含めないようにします。
    /// </summary>
    /// <param name="_endSeconds">終了時刻(Time.realtimeSinceStartup)</param>
    public static void End(float _endSeconds)
    {
        OnEnd?.Invoke(_endSeconds);
    }
}
