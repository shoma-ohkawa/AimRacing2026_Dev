/*━━━━━━━━━
@file    PerfAutoLapRunner.cs
@brief   計測用: 10周などの自動周回、または操作なしの放置監視を、起動引数だけで始める。DLSS・映像(Timeline)を止めて、機能ごとの影響を比べられる(起動引数 -perfautolap / -perfidle で有効)
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/19
         最終更新日 : 2026/10/22
━━━━━━━━━*/

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

//自動周回・放置監視の計測の考え方
//  1. 起動引数 -perfautolap または -perfidle があるときだけ動く。無ければ何もしない(本番の動作を変えない)
//  2. 周回(-perfautolap): 起動して Splash が見えたら、Uキーと同じ自動周回(AutoDemoDriver.StartLoop)を、指定の周回数だけ始める。記録は、-perf の PerfCsvRecorder が取る
//     放置(-perfidle): Splash が見えたら、何も操作せず、指定の分だけ Opening の繰り返し(PVのループ)を測る
//  3. 終わったら、(指定があれば)アプリを終了する。周回は、最後の Result → Load → Refresh を待ってから。無人で、何通りも続けて回せる
//  4. 基本は、固定の周回数・時間で測る。-perfuntil を付けると、VRAM が総量の99%以上、またはFPSが基準の8割未満になった時点で終える(周回数・時間は上限になる)。理由は meta.json の stop_reason に書く
//  5. 機能ごとの影響を比べるため、DLSS(全シーンのカメラ)と映像(Timeline = Intro の映像と OP)を、読み込みのたびに止められる
//  6. 実行中は、手動のデバッグキーを受け付けない(DebugKey.KeysLocked)。Esc だけ受け付ける。計測の途中でキーが押されて、周回が乱れるのを防ぐため
//
//起動引数(例: AimRacing.exe -perf -perflabel lap_nodlss -perfautolap 10 -perfnodlss -perfquit)
//  -perfautolap <周回数>  自動周回を始める。周回数は 1 以上(既定 10)
//  -perfidle <分>         放置監視を始める。分は 1 以上(既定 120)。-perfautolap があれば、そちらが優先。出力先は .../idle
//  -perfuntil             条件(VRAM 99%以上、またはFPSが基準の8割未満)を達成した時点で終える。周回数・分は上限になる
//  -perfnodlss            全シーンのカメラの DLSS を止める
//  -perfdlssmaponly       Map シーン(Intro・Racing)のカメラの DLSS だけ残し、他のシーン(Opening・Menu・Splash・Load・Result)のカメラでは止める。-perfnodlss があれば、そちらが優先
//  -perfdlssoffbeforeunload  DLSS はシーンの中では有効のまま、シーンを離れる遷移(Transition・Reset)の開始で、離れるシーンのカメラの DLSS を止める。破棄の前に止めると、DLSS の資源が解放されるかを調べる。-perfnodlss があれば、そちらが優先
//  -perfnovideo         Timeline(Intro の映像と OP)の再生を止める(アセットを外す。Introは、映像がなければ、すぐ Racing へ進む)
//  -perfquit              終わったら、アプリを終了する
//
//Player.log に [PerfAutoLap] の行で進み具合を残す(AppLog はリリースで除去されるため、生のログを使う)。周回ごとの開始・完了は [AutoDemoLoop] の行
public sealed class PerfAutoLapRunner : MonoBehaviour
{
    //起動引数から読み取った設定
    internal struct LapConfig
    {
        public int m_laps; //周回数(放置監視のときは0)
        public bool m_bIdle; //放置監視か(falseなら自動周回)
        public int m_idleMinutes; //放置監視の時間(分。自動周回のときは0)
        public bool m_bUntil; //条件を達成するまで測るか(周回数・分は上限)
        public bool m_bQuit; //終わったら終了するか
        public bool m_bNoDlss; //DLSS を止めるか
        public bool m_bDlssMapOnly; //DLSS を Map シーンのカメラだけに残すか(他のシーンのカメラでは止める)
        public bool m_bDlssOffBeforeUnload; //シーンを離れる遷移の開始で、離れるシーンのカメラの DLSS を止めるか
        public bool m_bNoVideo; //Timeline(映像)を止めるか
    }

    private const string k_ArgQuit = "-perfquit"; //終わったら終了する起動引数
    private const string k_ArgNoDlss = "-perfnodlss"; //DLSS を止める起動引数
    private const string k_ArgDlssMapOnly = "-perfdlssmaponly"; //DLSS を Map シーンのカメラだけに残す起動引数
    private const string k_ArgDlssOffBeforeUnload = "-perfdlssoffbeforeunload"; //シーンを離れる直前に DLSS を止める起動引数
    private const string k_ArgNoVideo = "-perfnovideo"; //映像を止める起動引数
    private const string k_DlssKeepSceneName = "Map"; //-perfdlssmaponly のとき、DLSS を残すカメラのシーン名(GameFlowGraph の Intro・Racing のシーン)
    private const string k_LogTag = "[PerfAutoLap] "; //Player.logで探すための印
    private const int k_DefaultLaps = 10; //周回数の既定(計測の基準)
    private const int k_DefaultIdleMinutes = 120; //放置監視の時間の既定(分)
    private const float k_SecondsPerMinute = 60f; //分から秒への換算
    private const double k_VramLimitRatio = 0.99; //VRAMの使用量がGPUの総量のこの割合以上になったら、上限に達したとみなす(-perfuntil)
    private const float k_FpsDropRatio = 0.8f; //FPSの中央値が基準のこの割合を下回ったら、崩れたとみなす(-perfuntil)
    private const int k_IdleWindowRows = 600; //放置監視でFPSの中央値を出す窓の行数(1行1秒なので10分)
    private const int k_MinFpsSamples = 10; //FPSの中央値を出すのに必要な最低の行数
    private const int k_FpsSampleCapacity = 1024; //FPSを集めるリストの初期容量
    private const string k_StopCompleted = "completed"; //指定の周回数をすべて終えた
    private const string k_StopTimeLimit = "time_limit"; //放置監視の時間に達した
    private const string k_StopVramLimit = "vram_limit"; //VRAMが上限に達した(-perfuntil)
    private const string k_StopFpsDrop = "fps_drop"; //FPSが崩れた(-perfuntil)
    private const string k_StopNoSplash = "no_splash"; //起動後、Splash が見えなかった
    private const string k_StopLoopNotStarted = "loop_not_started"; //周回を始められなかった
    private const string k_StopLoopCancelled = "loop_cancelled"; //周回が中止された(1周が終わらないなど)
    private const float k_ReadyTimeoutSeconds = 300f; //起動して Splash が見えるまでの待ちの上限(秒)
    private const float k_SettleSeconds = 5f; //Splash が見えてから、周回を始めるまでの待ち(秒)
    private const float k_PostLapSettleSeconds = 55f; //周回が終わってから、最後の Result → Load → Refresh を待つ秒数(Result は 3 秒後に Enter で抜けるので、Load → Splash まで約 10 秒。余裕を見た値)
    private const float k_QuitDelaySeconds = 5f; //終了の前に、最後の記録を書き終えるのを待つ秒数
    private const float k_ScanIntervalSeconds = 0.5f; //シーンの読み込み後に作られるカメラなどを探し直す間隔(秒)

    private LapConfig m_config; //今回の設定
    private bool m_bArrived; //待っている State が見えたか
    private GameStateId m_arrivalState; //見えるのを待っている State
    private float m_lastScanTime; //最後にカメラなどを探した時刻(Time.unscaledTime)
    private int m_dlssDisabledCount; //DLSS を止めたカメラの数(累計)
    private int m_dlssOffBeforeUnloadCount; //離れるシーンのカメラで、破棄の前に DLSS を止めた数(累計)
    private int m_videoDisabledCount; //再生を止めた Timeline の数(累計)

    private readonly List<float> m_fpsSamples = new List<float>(k_FpsSampleCapacity); //終了条件の判定に使う、今の周(または今の10分)のFPS
    private float m_fpsBaseline = -1f; //FPSの基準(周回は1周目、放置は最初の10分の中央値)。まだ無ければ負
    private int m_trackedLap; //FPSを集めている周の番号
    private string m_stopReason = string.Empty; //測るのを終えた理由。空なら、まだ終えていない

    //起動引数に -perfautolap があるときだけ、シーンをまたいで残る実行用のオブジェクトを生成し、手動のデバッグキーを止める関数
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!TryParseArguments(Environment.GetCommandLineArgs(), out LapConfig config)) { return; }

        DebugKey.KeysLocked = true;

        GameObject runnerObject = new GameObject(nameof(PerfAutoLapRunner));
        DontDestroyOnLoad(runnerObject);
        PerfAutoLapRunner runner = runnerObject.AddComponent<PerfAutoLapRunner>();
        runner.m_config = config;
    }

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete;
        FlowTiming.OnBegin += OnFlowBegin;
        PerfCsvRecorder.OnRow += HandleRow;
    }

    private void Start()
    {
        StartCoroutine(RunAutoLaps());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
        FlowTiming.OnBegin -= OnFlowBegin;
        PerfCsvRecorder.OnRow -= HandleRow;
    }

    private void Update()
    {
        if (!m_config.m_bNoDlss && !m_config.m_bDlssMapOnly && !m_config.m_bNoVideo) { return; }
        if (Time.unscaledTime - m_lastScanTime < k_ScanIntervalSeconds) { return; }

        m_lastScanTime = Time.unscaledTime;
        ApplyToggles();
    }

    // ---------------------------------------------------------------- 起動引数

    /// <summary>
    /// 起動引数から設定を読み取ります。-perfautolap も -perfidle も無ければ false を返します(両方あれば、-perfautolap が優先)。
    /// </summary>
    /// <param name="_arguments">起動引数の一覧</param>
    /// <param name="_config">読み取った設定</param>
    internal static bool TryParseArguments(string[] _arguments, out LapConfig _config)
    {
        _config = new LapConfig();

        bool bLap = HasArgument(_arguments, PerfCsvRecorder.k_ArgAutoLap); //自動周回の指定があるか
        bool bIdle = !bLap && HasArgument(_arguments, PerfCsvRecorder.k_ArgIdle); //放置監視の指定があるか
        if (!bLap && !bIdle) { return false; }

        _config.m_bIdle = bIdle;
        _config.m_bUntil = HasArgument(_arguments, PerfCsvRecorder.k_ArgUntil);

        if (bLap)
        {
            _config.m_laps = k_DefaultLaps;
            if (TryGetArgumentValue(_arguments, PerfCsvRecorder.k_ArgAutoLap, out string lapsText) && int.TryParse(lapsText, out int laps) && laps > 0) { _config.m_laps = laps; }
        }
        else
        {
            _config.m_idleMinutes = k_DefaultIdleMinutes;
            if (TryGetArgumentValue(_arguments, PerfCsvRecorder.k_ArgIdle, out string minutesText) && int.TryParse(minutesText, out int minutes) && minutes > 0) { _config.m_idleMinutes = minutes; }
        }

        _config.m_bQuit = HasArgument(_arguments, k_ArgQuit);
        _config.m_bNoDlss = HasArgument(_arguments, k_ArgNoDlss);
        _config.m_bDlssMapOnly = HasArgument(_arguments, k_ArgDlssMapOnly);
        _config.m_bDlssOffBeforeUnload = HasArgument(_arguments, k_ArgDlssOffBeforeUnload);
        _config.m_bNoVideo = HasArgument(_arguments, k_ArgNoVideo);
        return true;
    }

    //起動引数に、指定した文字列があるかを判定する関数
    private static bool HasArgument(string[] _arguments, string _name)
    {
        //起動引数を先頭から順に照合する処理
        for (int i = 0; i < _arguments.Length; ++i)
        {
            if (string.Equals(_arguments[i], _name, StringComparison.OrdinalIgnoreCase)) { return true; }
        }

        return false;
    }

    //起動引数の値(引数の次の文字列)を取得する関数。無ければ false を返す
    private static bool TryGetArgumentValue(string[] _arguments, string _name, out string _value)
    {
        _value = string.Empty;

        //起動引数を先頭から順に照合し、次の文字列を値として取る処理
        for (int i = 0; i < _arguments.Length - 1; ++i)
        {
            if (!string.Equals(_arguments[i], _name, StringComparison.OrdinalIgnoreCase)) { continue; }

            _value = _arguments[i + 1];
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- 自動周回

    //起動して Splash が見えるのを待ってから、自動周回(または放置監視)を始め、終わったら(指定があれば)終了する関数
    internal IEnumerator RunAutoLaps()
    {
        string planText = m_config.m_bIdle ? "放置監視 " + m_config.m_idleMinutes + " 分" : m_config.m_laps + " 周"; //計画の表示
        Debug.Log(k_LogTag + "開始: " + planText + " 条件達成まで=" + m_config.m_bUntil + " DLSS停止=" + m_config.m_bNoDlss + " DLSSはMapのみ=" + m_config.m_bDlssMapOnly + " DLSS離脱前停止=" + m_config.m_bDlssOffBeforeUnload + " 映像停止=" + m_config.m_bNoVideo + " 終了=" + m_config.m_bQuit);

        ApplyToggles();
        BeginWaitForArrival(GameStateId.Splash);
        yield return WaitForArrival(k_ReadyTimeoutSeconds);
        if (!m_bArrived)
        {
            Debug.LogWarning(k_LogTag + "起動後、Splash が見えませんでした。中止します");
            RequestStop(k_StopNoSplash);
            yield return FinishRun();
            yield break;
        }

        yield return new WaitForSecondsRealtime(k_SettleSeconds);

        if (m_config.m_bIdle)
        {
            yield return RunIdle();
            yield break;
        }

        AutoDemoDriver.StartLoop(true, m_config.m_laps);
        if (!AutoDemoDriver.IsLooping)
        {
            Debug.LogWarning(k_LogTag + "周回を始められませんでした");
            RequestStop(k_StopLoopNotStarted);
            yield return FinishRun();
            yield break;
        }

        //周回の繰り返しが終わる(最後の Result に着く、中止される、または終了条件に達する)まで待つ処理
        while (AutoDemoDriver.IsLooping) { yield return null; }

        //終了条件に達して止めたときは、その理由のまま終える。最後の周を終えたときは、最後の Result → Load → Refresh まで待つ
        if (m_stopReason.Length == 0 && AutoDemoDriver.LastLoopCompleted)
        {
            RequestStop(k_StopCompleted);
            Debug.Log(k_LogTag + "周回が終わりました。最後の Result → Load → Refresh を待ちます");
            yield return new WaitForSecondsRealtime(k_PostLapSettleSeconds);
        }
        else if (m_stopReason.Length == 0)
        {
            RequestStop(k_StopLoopCancelled);
        }

        yield return FinishRun();
    }

    //放置監視。何も操作せず、指定の分が経つ(または終了条件に達する)まで、Opening の繰り返しを測る関数
    private IEnumerator RunIdle()
    {
        float deadline = Time.realtimeSinceStartup + m_config.m_idleMinutes * k_SecondsPerMinute; //測り終える時刻
        Debug.Log(k_LogTag + "放置監視を始めます: " + m_config.m_idleMinutes + " 分(操作なし)");

        //上限の時間が経つか、終了条件に達するまで待つ処理
        while (m_stopReason.Length == 0 && Time.realtimeSinceStartup < deadline) { yield return null; }

        if (m_stopReason.Length == 0) { RequestStop(k_StopTimeLimit); }
        yield return FinishRun();
    }

    /// <summary>
    /// 測るのを終える理由を記録します。最初の理由だけを残します。周回中なら、周回の繰り返しも止めます。
    /// </summary>
    /// <param name="_reason">理由(meta.json の stop_reason に書く)</param>
    private void RequestStop(string _reason)
    {
        if (m_stopReason.Length > 0) { return; }

        m_stopReason = _reason;
        PerfCsvRecorder.SetStopReason(_reason);
        Debug.Log(k_LogTag + "終了の理由: " + _reason);

        if (!m_config.m_bIdle && AutoDemoDriver.IsLooping) { AutoDemoDriver.CancelLoop(); }
    }

    //終了の指定があれば、最後の記録を書き終えるのを待ってから、アプリを終了する関数
    private IEnumerator FinishRun()
    {
        Debug.Log(k_LogTag + "完了" + (m_config.m_bQuit ? "。アプリを終了します" : ""));
        if (!m_config.m_bQuit) { yield break; }

        yield return new WaitForSecondsRealtime(k_QuitDelaySeconds);
        Application.Quit();
    }

    // ---------------------------------------------------------------- 終了条件(-perfuntil)

    /// <summary>
    /// 1行(1秒)ごとに呼ばれ、-perfuntil のとき、VRAMとFPSが終了条件に達したかを判定します。
    /// </summary>
    /// <param name="_row">その行の値</param>
    private void HandleRow(PerfCsvRecorder.RowInfo _row)
    {
        if (!m_config.m_bUntil || m_stopReason.Length > 0) { return; }

        //VRAMがGPUの総量の99%以上になったら、上限に達したとみなす(VRAMはPC全体の値)
        double vramTotalMB = SystemInfo.graphicsMemorySize;
        if (_row.m_vramUsedMB >= 0.0 && vramTotalMB > 0.0 && _row.m_vramUsedMB >= vramTotalMB * k_VramLimitRatio)
        {
            Debug.LogWarning(k_LogTag + "VRAM が上限に達しました: " + _row.m_vramUsedMB.ToString("F0") + " / " + vramTotalMB.ToString("F0") + " MB");
            RequestStop(k_StopVramLimit);
            return;
        }

        if (m_config.m_bIdle) { TrackIdleFps(_row); }
        else { TrackLapFps(_row); }
    }

    //周回のFPSの判定。走行中(Racing)の行のFPSを周ごとに集め、周が替わったら中央値を出して、基準(1周目)と比べる
    private void TrackLapFps(PerfCsvRecorder.RowInfo _row)
    {
        if (_row.m_lap != m_trackedLap)
        {
            bool bStopped = m_trackedLap > 0 && EvaluateFps("周 " + m_trackedLap); //終えた周を評価する(基準の8割未満なら止める)
            m_trackedLap = _row.m_lap;
            m_fpsSamples.Clear();
            if (bStopped) { return; }
        }

        if (_row.m_lap > 0 && _row.m_state == (int)GameStateId.Racing && !_row.m_bBusy) { m_fpsSamples.Add((float)_row.m_fps); }
    }

    //放置監視のFPSの判定。Opening 中の行のFPSを10分ごとにまとめ、最初の10分の中央値(基準)と比べる
    private void TrackIdleFps(PerfCsvRecorder.RowInfo _row)
    {
        if (_row.m_state != (int)GameStateId.Opening || _row.m_bBusy) { return; }

        m_fpsSamples.Add((float)_row.m_fps);
        if (m_fpsSamples.Count < k_IdleWindowRows) { return; }

        EvaluateFps("10 分の窓");
        m_fpsSamples.Clear();
    }

    /// <summary>
    /// 集めたFPSの中央値を出し、基準があれば比べます。基準が無ければ、これを基準にします。基準の8割未満なら止めて true を返します。
    /// </summary>
    /// <param name="_label">ログに書く、対象の名前</param>
    private bool EvaluateFps(string _label)
    {
        if (m_fpsSamples.Count < k_MinFpsSamples) { return false; }

        m_fpsSamples.Sort();
        float median = m_fpsSamples[m_fpsSamples.Count / 2]; //中央値
        if (m_fpsBaseline < 0f)
        {
            m_fpsBaseline = median;
            Debug.Log(k_LogTag + "FPS の基準: " + median.ToString("F1") + "(" + _label + ")");
            return false;
        }

        if (median >= m_fpsBaseline * k_FpsDropRatio) { return false; }

        Debug.LogWarning(k_LogTag + "FPS が崩れました: " + median.ToString("F1") + "(" + _label + "、基準 " + m_fpsBaseline.ToString("F1") + " の " + (k_FpsDropRatio * 100f).ToString("F0") + "% 未満)");
        RequestStop(k_StopFpsDrop);
        return true;
    }

    // ---------------------------------------------------------------- State の到着の待ち

    //これから、指定の State が見える(FadeInが完了する)のを待つ印を立てる関数
    private void BeginWaitForArrival(GameStateId _state)
    {
        m_arrivalState = _state;
        m_bArrived = false;
    }

    //待っている State が見える、または上限の秒数が経つまで待つ関数
    private IEnumerator WaitForArrival(float _timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + _timeoutSeconds;

        //State が見える(通知が来る)まで、1フレームずつ待つ処理
        while (!m_bArrived && Time.realtimeSinceStartup < deadline) { yield return null; }
    }

    //画面が見えた State の通知を受ける関数
    private void OnStateFadeInComplete(GameStateId _state)
    {
        if (_state == m_arrivalState) { m_bArrived = true; }
    }

    // ---------------------------------------------------------------- DLSS と映像を止める

    //遷移・リセットの開始で呼ばれる関数。-perfdlssoffbeforeunload のとき、これから離れるシーンのカメラの DLSS を、破棄の前に止める(フェードアウトの間に、DLSS オフで数フレーム描画される)
    private void OnFlowBegin(FlowTimingKind _kind, GameStateId _from, GameStateId _to, float _startSeconds)
    {
        if (!m_config.m_bDlssOffBeforeUnload || m_config.m_bNoDlss) { return; }
        if (_kind != FlowTimingKind.Transition && _kind != FlowTimingKind.Reset) { return; }
        if (!GameFlowGraph.SceneGroups.TryGetValue(_from, out GameSceneGroup fromGroup)) { return; }
        if (!GameFlowGraph.SceneGroups.TryGetValue(_to, out GameSceneGroup toGroup)) { return; }

        bool bReset = _kind == FlowTimingKind.Reset; //リセットは Manager 以外の全シーンを破棄する
        HDAdditionalCameraData[] cameras = FindObjectsByType<HDAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int changed = 0;

        //離れるシーン(通常の遷移は今のグループにだけあるシーン、リセットは Manager 以外すべて)のカメラを、1台ずつ止める処理
        for (int i = 0; i < cameras.Length; ++i)
        {
            if (!cameras[i].allowDeepLearningSuperSampling) { continue; }

            string sceneName = cameras[i].gameObject.scene.name; //カメラが居るシーン名
            bool bLeaving = bReset ? sceneName != GameFlowGraph.k_ManagerSceneName : ContainsName(fromGroup.SceneNames, sceneName) && !ContainsName(toGroup.SceneNames, sceneName); //このカメラのシーンを離れるか
            if (!bLeaving) { continue; }

            cameras[i].allowDeepLearningSuperSampling = false;
            ++changed;
        }

        if (changed > 0)
        {
            m_dlssOffBeforeUnloadCount += changed;
            Debug.Log(k_LogTag + "離れるシーンの DLSS を止めました: " + changed + " 台(" + _from + " → " + _to + "、累計 " + m_dlssOffBeforeUnloadCount + ")");
        }
    }

    //シーン名の一覧に、指定の名前があるかを判定する関数
    private static bool ContainsName(IReadOnlyList<string> _names, string _name)
    {
        //一覧を先頭から順に照合する処理
        for (int i = 0; i < _names.Count; ++i)
        {
            if (_names[i] == _name) { return true; }
        }

        return false;
    }

    //シーンの読み込み直後に呼ばれる関数。作られたカメラと Timeline に、止める設定を当てる
    private void OnSceneLoaded(Scene _scene, LoadSceneMode _mode)
    {
        ApplyToggles();
    }

    //指定があれば、全シーンのカメラの DLSS と、Timeline の再生を止める関数
    internal void ApplyToggles()
    {
        if (m_config.m_bNoDlss || m_config.m_bDlssMapOnly)
        {
            HDAdditionalCameraData[] cameras = FindObjectsByType<HDAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;

            //DLSS が有効なカメラを、1台ずつ止める処理(Map だけ残す指定のときは、Map シーンのカメラを飛ばす)
            for (int i = 0; i < cameras.Length; ++i)
            {
                if (!cameras[i].allowDeepLearningSuperSampling) { continue; }
                if (!m_config.m_bNoDlss && cameras[i].gameObject.scene.name == k_DlssKeepSceneName) { continue; }

                cameras[i].allowDeepLearningSuperSampling = false;
                ++changed;
            }

            if (changed > 0)
            {
                m_dlssDisabledCount += changed;
                Debug.Log(k_LogTag + "DLSS を止めました: " + changed + " 台(累計 " + m_dlssDisabledCount + ")");
            }
        }

        if (m_config.m_bNoVideo)
        {
            PlayableDirector[] directors = FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int changed = 0;

            //アセットを持つ Timeline を、1つずつ止めてアセットを外す処理(外すと、再生を始めても何も起きず、再生の時間も長さも 0 になる)
            for (int i = 0; i < directors.Length; ++i)
            {
                if (directors[i].playableAsset == null) { continue; }

                directors[i].Stop();
                directors[i].playableAsset = null;
                ++changed;
            }

            if (changed > 0)
            {
                m_videoDisabledCount += changed;
                Debug.Log(k_LogTag + "Timeline(映像)を止めました: " + changed + " 個(累計 " + m_videoDisabledCount + ")");
            }
        }
    }
}
