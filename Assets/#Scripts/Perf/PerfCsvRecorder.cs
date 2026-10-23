/*━━━━━━━━━
@file    PerfCsvRecorder.cs
@brief   リリースビルドで、約1秒ごとにFPS・CPU・GPU・メモリ・GCをCSVへ記録する計測ツール(起動引数 -perf で有効)
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/16
         最終更新日 : 2026/10/22
━━━━━━━━━*/

using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

//計測の考え方
//  1. 起動引数 -perf があるときだけ動く。無ければ何もしない(本番の動作を変えない)
//  2. フレームごとの値は保存せず、約1秒ごとに集約した1行をCSVへ追記する。複数日の連続稼働でも、記録が溜まらない
//  3. 1行ごとに書き出す(Flush)ので、リアルタイムに読めて、強制終了しても直前の行まで残る
//  4. 計測中に文字列やオブジェクトを作らない(GCの計測を汚さないため)。数値は PerfCsvFormat で直接書く
//  5. 取得できなかった値は -1 を書く(列は残す)
//
//起動引数(例: AimRacing.exe -perf -perflabel baseline)
//  -perf                計測を有効にする(これが無ければ何もしない)
//  -perflabel <名前>     計測の名前。出力フォルダ名に使う(既定: run)
//  -perfout <フォルダ>   出力先(既定: Application.persistentDataPath/perf。-perfidle のときは .../idle)
//  -perfautolap / -perfidle / -perfuntil は PerfAutoLapRunner が扱う。ここでは、モードと計画を meta.json に書くために読む
//
//出力: <出力先>/<名前>_<起動日時>/ に、条件を書いた meta.json と、日ごとの perf_yyyyMMdd.csv と、遷移・ロード時間の transitions.csv
//CSVの state(最後にFadeInが完了したStateの番号。GameStateId の値)・lap(周回の番号)・trans(遷移などの処理中なら1)は、State別・周回別の集計に使う
public class PerfCsvRecorder : MonoBehaviour
{
    /// <summary>
    /// 1行を書き出したときに通知する、その行の値です(終了条件の判定に使う)。
    /// </summary>
    public struct RowInfo
    {
        public int m_state; //最後にFadeInが完了したStateの番号(まだ無ければ-1)
        public int m_lap; //周回の番号(周回の繰り返し中でなければ0)
        public bool m_bBusy; //遷移・リセット・起動・リフレッシュの処理中か
        public double m_fps; //今の1秒のFPS
        public double m_vramUsedMB; //VRAMの使用量(PC全体。取得できなければ-1)
        public double m_uptimeSeconds; //アプリを起動してからの秒数
    }

    /// <summary>
    /// 1行を書き出すたびに通知します(1秒に1回)。
    /// </summary>
    public static event Action<RowInfo> OnRow;

    internal const string k_ArgAutoLap = "-perfautolap"; //自動周回の起動引数(値は周回数)
    internal const string k_ArgIdle = "-perfidle"; //放置監視の起動引数(値は分)
    internal const string k_ArgUntil = "-perfuntil"; //条件を達成するまで測る起動引数(周回数・時間は上限になる)

    private const string k_ArgEnable = "-perf"; //計測を有効にする起動引数
    private const string k_ArgLabel = "-perflabel"; //計測の名前の起動引数
    private const string k_ArgOut = "-perfout"; //出力先の起動引数
    private const string k_DefaultLabel = "run"; //計測の名前の既定値
    private const string k_OutputFolderName = "perf"; //出力先の既定のフォルダ名(周回と通常の計測)
    private const string k_IdleOutputFolderName = "idle"; //出力先の既定のフォルダ名(放置監視)
    private const string k_ModeManual = "manual"; //手動で遊ぶ計測のモード名
    private const string k_ModeLap = "lap"; //自動周回のモード名
    private const string k_ModeIdle = "idle"; //放置監視のモード名
    private const string k_StopReasonQuit = "quit"; //停止の理由が設定されないまま終了したときの値
    private const int k_StateNone = -1; //まだStateが分からないときの値
    private const string k_MetaFileName = "meta.json"; //条件を書くファイル名
    private const string k_CsvFilePrefix = "perf_"; //CSVのファイル名の先頭
    private const string k_CsvFileExtension = ".csv"; //CSVの拡張子
    private const string k_FolderDateFormat = "yyyyMMdd_HHmmss"; //出力フォルダ名に付ける起動日時の書式
    private const string k_FileDateFormat = "yyyyMMdd"; //CSVのファイル名に付ける日付の書式
    private const string k_MetaDateFormat = "yyyy-MM-ddTHH:mm:ss"; //meta.json の日時の書式
    private const string k_CsvHeader = "wall,uptime_s,fps,frame_ms_avg,frame_ms_max,cpu_main_ms,cpu_render_ms,cpu_process_pct,"
        + "gpu_ms,gpu_pct,vram_mb,gpu_temp_c,ws_mb,private_mb,unity_alloc_mb,unity_reserved_mb,gc0,mono_used_mb,mono_heap_mb,state,lap,trans"; //CSVの列名
    private const int k_SchemaVersion = 2; //CSVの列の構成の版数(列を変えたら上げる。2: gc1・gc2 を削除し、state・lap・trans を追加)
    private const float k_RowIntervalSeconds = 1f; //CSVへ1行出力する間隔(秒)
    private const float k_MillisecondsPerSecond = 1000f; //秒からミリ秒への換算
    private const double k_BytesPerMB = 1024.0 * 1024.0; //バイトからMBへの換算
    private const int k_TimingBatch = 4; //FrameTimingManagerから1回に取得する最大数
    private const int k_RowCapacity = 512; //1行分の文字数の初期容量
    private const int k_FileBufferBytes = 4096; //ファイルのバッファの大きさ(バイト)
    private const int k_GcGeneration0 = 0; //GCの世代0
    private const int k_DecimalsNone = 0; //小数なし
    private const int k_DecimalsOne = 1; //小数1桁
    private const int k_DecimalsTwo = 2; //小数2桁
    private const char k_Comma = ','; //CSVの区切り
    private const char k_NewLine = '\n'; //CSVの改行

#if ENABLE_IL2CPP
    private const string k_ScriptingBackend = "IL2CPP"; //スクリプトバックエンド
#else
    private const string k_ScriptingBackend = "Mono"; //スクリプトバックエンド
#endif

    private readonly StringBuilder m_row = new StringBuilder(k_RowCapacity); //1行分の組み立て用
    private readonly FrameTiming[] m_timingBuffer = new FrameTiming[k_TimingBatch]; //FrameTimingManager取得用バッファ
    private char[] m_rowChars = new char[k_RowCapacity]; //1行分をファイルへ渡すための文字配列

    private PerfTransitionRecorder m_pTransitionRecorder; //遷移・ロード時間の記録(transitions.csv)。FlowTimingの通知を購読する
    private StreamWriter m_writer; //CSVの書き込み先(日が変わるたびに開き直す)
    private string m_runFolder = string.Empty; //今回の出力フォルダ
    private string m_label = k_DefaultLabel; //計測の名前
    private string m_outputRoot = string.Empty; //出力先
    private string m_commandLine = string.Empty; //起動引数の全文
    private string m_mode = k_ModeManual; //計測のモード(manual / lap / idle)
    private int m_planned; //計画した周回数(lapのとき)または分(idleのとき)。それ以外は0
    private bool m_bUntil; //条件を達成するまで測る指定か
    private bool m_bEditorRunning; //起動時に Unity Editor が動いていたか(動いていても中断しない。記録だけ)
    private double m_startVramMB = PerfNativeMetrics.k_Unavailable; //起動時のVRAM使用量(PC全体)
    private int m_state = k_StateNone; //最後にFadeInが完了したStateの番号(GameStateId の値)
    private bool m_bBusy; //遷移・リセット・起動・リフレッシュの処理中か
    private static string s_stopReason = string.Empty; //計測を終えた理由(PerfAutoLapRunner が設定する)
    private DateTime m_startTime; //記録を始めた日時
    private DateTime m_openDate; //今開いているCSVの日付
    private bool m_bTimingEnabled; //FrameTimingManagerが有効か
    private bool m_bFailureReported; //書き込みの失敗を報告済みか
    private bool m_bFinished; //終了処理を済ませたか

    private int m_frames; //今の1秒のフレーム数
    private float m_elapsedSeconds; //今の1秒の経過時間(秒)
    private float m_frameMsSum; //今の1秒のフレーム時間の合計(ミリ秒)
    private float m_frameMsMax; //今の1秒の最大フレーム時間(ミリ秒)
    private ulong m_lastTimingStart; //最後に集計したFrameTimingのフレーム開始時刻
    private double m_cpuMainSum; //メインスレッドのCPU時間の合計(ミリ秒)
    private double m_cpuRenderSum; //レンダースレッドのCPU時間の合計(ミリ秒)
    private double m_gpuSum; //GPU時間の合計(ミリ秒)
    private int m_cpuSamples; //CPU時間を集計したフレーム数
    private int m_gpuSamples; //GPU時間を集計したフレーム数

    //起動引数に -perf があるときだけ、シーンをまたいで残る計測用オブジェクトを生成する関数
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!HasArgument(Environment.GetCommandLineArgs(), k_ArgEnable)) { return; }

        GameObject recorderObject = new GameObject(nameof(PerfCsvRecorder));
        DontDestroyOnLoad(recorderObject);
        recorderObject.AddComponent<PerfCsvRecorder>();
    }

    private void Awake()
    {
        m_startTime = DateTime.Now;
        ParseArguments();

        PerfNativeMetrics.Initialize();
        PerfNativeSample startSample = PerfNativeMetrics.Sample(); //CPU使用率は前回との差で求めるため、1回目は基準にする。VRAMは起動時の値として記録する
        m_startVramMB = startSample.m_gpuMemoryUsedMB;
        m_bEditorRunning = PerfNativeMetrics.IsUnityEditorRunning();
        m_bTimingEnabled = FrameTimingManager.IsFeatureEnabled();

        if (!PrepareFolder())
        {
            enabled = false;
            return;
        }

        m_pTransitionRecorder = new PerfTransitionRecorder(m_runFolder); //最初のシーンのAwakeより前(BeforeSceneLoad)に購読を始めるので、起動の記録も取れる
        GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete;
        FlowTiming.OnBegin += OnFlowBegin;
        FlowTiming.OnEnd += OnFlowEnd;
        WriteMeta(false);

        //AppLog はリリースビルドで除去されるため、開始の確認用に1回だけ生のログを出す(Player.log で出力先が分かる)
        Debug.Log("[" + nameof(PerfCsvRecorder) + "] 記録を開始しました: " + m_runFolder + "(モード " + m_mode + "、Unity Editor " + (m_bEditorRunning ? "起動中" : "なし") + ")");
    }

    private void Update()
    {
        float deltaSeconds = Time.unscaledDeltaTime;
        float deltaMilliseconds = deltaSeconds * k_MillisecondsPerSecond;

        ++m_frames;
        m_elapsedSeconds += deltaSeconds;
        m_frameMsSum += deltaMilliseconds;
        if (deltaMilliseconds > m_frameMsMax) { m_frameMsMax = deltaMilliseconds; }

        CollectFrameTimings();

        if (m_elapsedSeconds < k_RowIntervalSeconds) { return; }

        WriteRow(DateTime.Now);
        ResetAccumulators();
    }

    //アプリの終了時に、記録を閉じて meta.json に正常終了を書く関数
    private void OnApplicationQuit()
    {
        Finish();
    }

    private void OnDestroy()
    {
        Finish();
    }

    // ---------------------------------------------------------------- 起動引数

    /// <summary>
    /// 起動引数に、指定した文字列があるかを判定します。
    /// </summary>
    /// <param name="_arguments">起動引数の一覧</param>
    /// <param name="_name">調べる引数の名前</param>
    private static bool HasArgument(string[] _arguments, string _name)
    {
        //起動引数を先頭から順に照合する処理
        for (int i = 0; i < _arguments.Length; ++i)
        {
            if (string.Equals(_arguments[i], _name, StringComparison.OrdinalIgnoreCase)) { return true; }
        }

        return false;
    }

    /// <summary>
    /// 起動引数の値(引数の次の文字列)を取得します。無ければ false を返します。
    /// </summary>
    /// <param name="_arguments">起動引数の一覧</param>
    /// <param name="_name">引数の名前</param>
    /// <param name="_value">取得した値</param>
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

    //起動引数を読み取り、計測の名前と出力先を決める関数
    private void ParseArguments()
    {
        string[] arguments = Environment.GetCommandLineArgs();
        m_commandLine = string.Join(" ", arguments);

        //自動周回・放置監視の指定があれば、モードと計画(周回数または分)を読む。放置監視の出力先は、周回と分ける
        bool bIdle = false;
        if (PerfAutoLapRunner.TryParseArguments(arguments, out PerfAutoLapRunner.LapConfig config))
        {
            bIdle = config.m_bIdle;
            m_mode = bIdle ? k_ModeIdle : k_ModeLap;
            m_planned = bIdle ? config.m_idleMinutes : config.m_laps;
            m_bUntil = config.m_bUntil;
        }

        m_outputRoot = Path.Combine(Application.persistentDataPath, bIdle ? k_IdleOutputFolderName : k_OutputFolderName);

        if (TryGetArgumentValue(arguments, k_ArgLabel, out string label) && label.Length > 0)
        {
            m_label = PerfCsvFormat.SanitizeFileName(label);
        }

        if (TryGetArgumentValue(arguments, k_ArgOut, out string outputRoot) && outputRoot.Length > 0)
        {
            m_outputRoot = outputRoot;
        }
    }

    // ---------------------------------------------------------------- 集計

    //描画が完了したフレームのCPU/GPU時間を、FrameTimingManagerから集める関数(配列へ値を入れるだけで、メモリを確保しない)
    private void CollectFrameTimings()
    {
        if (!m_bTimingEnabled) { return; }

        FrameTimingManager.CaptureFrameTimings();
        int count = (int)FrameTimingManager.GetLatestTimings(k_TimingBatch, m_timingBuffer);
        ulong newestStart = m_lastTimingStart;

        //まだ集計していないフレームだけを足し込む処理
        for (int i = 0; i < count; ++i)
        {
            FrameTiming timing = m_timingBuffer[i];
            if (timing.frameStartTimestamp == 0 || timing.frameStartTimestamp <= m_lastTimingStart) { continue; }

            //取得できなかったフレーム(0)は、平均に含めない
            if (timing.cpuMainThreadFrameTime > 0.0)
            {
                m_cpuMainSum += timing.cpuMainThreadFrameTime;
                m_cpuRenderSum += timing.cpuRenderThreadFrameTime;
                ++m_cpuSamples;
            }

            if (timing.gpuFrameTime > 0.0)
            {
                m_gpuSum += timing.gpuFrameTime;
                ++m_gpuSamples;
            }

            if (timing.frameStartTimestamp > newestStart) { newestStart = timing.frameStartTimestamp; }
        }

        m_lastTimingStart = newestStart;
    }

    //1秒分の集計を初期状態に戻す関数
    private void ResetAccumulators()
    {
        m_frames = 0;
        m_elapsedSeconds = 0f;
        m_frameMsSum = 0f;
        m_frameMsMax = 0f;
        m_cpuMainSum = 0.0;
        m_cpuRenderSum = 0.0;
        m_gpuSum = 0.0;
        m_cpuSamples = 0;
        m_gpuSamples = 0;
    }

    // ---------------------------------------------------------------- CSVへの書き出し

    /// <summary>
    /// 1秒分の集計とその時点の状態を、CSVの1行にして書き出します。
    /// </summary>
    /// <param name="_now">今の日時</param>
    private void WriteRow(DateTime _now)
    {
        EnsureWriter(_now);
        if (m_writer == null) { return; }

        PerfNativeSample native = PerfNativeMetrics.Sample();
        double fps = m_frames / (double)m_elapsedSeconds;
        double frameMsAverage = m_frameMsSum / m_frames;
        double cpuMainAverage = m_cpuSamples > 0 ? m_cpuMainSum / m_cpuSamples : PerfNativeMetrics.k_Unavailable;
        double cpuRenderAverage = m_cpuSamples > 0 ? m_cpuRenderSum / m_cpuSamples : PerfNativeMetrics.k_Unavailable;
        double gpuAverage = m_gpuSamples > 0 ? m_gpuSum / m_gpuSamples : PerfNativeMetrics.k_Unavailable;

        m_row.Clear();
        PerfCsvFormat.AppendDateTime(m_row, _now);
        AppendCell(Time.realtimeSinceStartupAsDouble, k_DecimalsTwo);
        AppendCell(fps, k_DecimalsOne);
        AppendCell(frameMsAverage, k_DecimalsTwo);
        AppendCell(m_frameMsMax, k_DecimalsTwo);
        AppendCell(cpuMainAverage, k_DecimalsTwo);
        AppendCell(cpuRenderAverage, k_DecimalsTwo);
        AppendCell(native.m_processCpuPercent, k_DecimalsOne);
        AppendCell(gpuAverage, k_DecimalsTwo);
        AppendCell(native.m_gpuPercent, k_DecimalsOne);
        AppendCell(native.m_gpuMemoryUsedMB, k_DecimalsNone);
        AppendCell(native.m_gpuTemperatureC, k_DecimalsNone);
        AppendCell(native.m_workingSetMB, k_DecimalsOne);
        AppendCell(native.m_privateMB, k_DecimalsOne);
        AppendCell(Profiler.GetTotalAllocatedMemoryLong() / k_BytesPerMB, k_DecimalsOne);
        AppendCell(Profiler.GetTotalReservedMemoryLong() / k_BytesPerMB, k_DecimalsOne);
        AppendIntegerCell(GC.CollectionCount(k_GcGeneration0));
        AppendCell(Profiler.GetMonoUsedSizeLong() / k_BytesPerMB, k_DecimalsOne);
        AppendCell(Profiler.GetMonoHeapSizeLong() / k_BytesPerMB, k_DecimalsOne);
        int lap = AutoDemoDriver.CurrentLap; //周回の繰り返し中でなければ0
        AppendIntegerCell(m_state);
        AppendIntegerCell(lap);
        AppendIntegerCell(m_bBusy ? 1 : 0);
        m_row.Append(k_NewLine);

        int length = m_row.Length;
        if (length > m_rowChars.Length) { m_rowChars = new char[length * 2]; }
        m_row.CopyTo(0, m_rowChars, 0, length);

        try
        {
            m_writer.Write(m_rowChars, 0, length);
            m_writer.Flush(); //リアルタイムに読めるよう、1行ごとに書き出す
        }
        catch (Exception exception)
        {
            //ディスクの空き不足などで書けない場合は、閉じて次の行で開き直す
            CloseWriter();
            ReportFailureOnce("CSVへ書き込めません: " + exception.Message);
        }

        //終了条件の判定に使う購読者へ、この行の値を通知する(構造体を渡すだけで、メモリを確保しない)
        OnRow?.Invoke(new RowInfo
        {
            m_state = m_state,
            m_lap = lap,
            m_bBusy = m_bBusy,
            m_fps = fps,
            m_vramUsedMB = native.m_gpuMemoryUsedMB,
            m_uptimeSeconds = Time.realtimeSinceStartupAsDouble
        });
    }

    // ---------------------------------------------------------------- State と処理中の把握

    //画面が見えたStateの通知を受けて、CSVのstate列に使う番号を更新する関数
    private void OnStateFadeInComplete(GameStateId _state)
    {
        m_state = (int)_state;
    }

    //遷移・リセット・起動・リフレッシュの開始で呼ばれる関数。CSVのtrans列を1にする
    private void OnFlowBegin(FlowTimingKind _kind, GameStateId _from, GameStateId _to, float _startSeconds)
    {
        m_bBusy = true;
    }

    //処理の終了で呼ばれる関数。CSVのtrans列を0に戻す
    private void OnFlowEnd(float _endSeconds)
    {
        m_bBusy = false;
    }

    /// <summary>
    /// 計測を終えた理由を設定します。最初に設定した理由だけを残し、meta.json の stop_reason に書きます。
    /// </summary>
    /// <param name="_reason">理由(completed / time_limit / vram_limit / fps_drop など)</param>
    internal static void SetStopReason(string _reason)
    {
        if (s_stopReason.Length == 0) { s_stopReason = _reason; }
    }

    /// <summary>
    /// 区切りと小数の値を、1行の組み立て用バッファへ追加します。
    /// </summary>
    /// <param name="_value">値(負の値は取得できなかったものとして -1 を書く)</param>
    /// <param name="_decimals">小数の桁数</param>
    private void AppendCell(double _value, int _decimals)
    {
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendValue(m_row, _value, _decimals);
    }

    /// <summary>
    /// 区切りと整数の値を、1行の組み立て用バッファへ追加します。
    /// </summary>
    /// <param name="_value">値</param>
    private void AppendIntegerCell(long _value)
    {
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendInteger(m_row, _value);
    }

    /// <summary>
    /// 今の日付のCSVを開きます。日が変わっていたら、新しい日のファイルへ切り替えます。
    /// </summary>
    /// <param name="_now">今の日時</param>
    private void EnsureWriter(DateTime _now)
    {
        if (m_writer != null && _now.Date == m_openDate) { return; }

        CloseWriter();

        try
        {
            string fileName = k_CsvFilePrefix + _now.ToString(k_FileDateFormat, CultureInfo.InvariantCulture) + k_CsvFileExtension;
            string path = Path.Combine(m_runFolder, fileName);

            //別のプロセス(ビューア)が読んでいても書けるよう、共有を許可して追記で開く
            FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, k_FileBufferBytes);
            m_writer = new StreamWriter(stream, new UTF8Encoding(false), k_FileBufferBytes);
            m_openDate = _now.Date;

            //新しいファイルの先頭にだけ、列名を書く
            if (stream.Length == 0)
            {
                m_writer.Write(k_CsvHeader);
                m_writer.Write(k_NewLine);
                m_writer.Flush();
            }
        }
        catch (Exception exception)
        {
            CloseWriter();
            ReportFailureOnce("CSVを開けません: " + exception.Message);
        }
    }

    //開いているCSVを閉じる関数
    private void CloseWriter()
    {
        if (m_writer == null) { return; }

        try
        {
            m_writer.Flush();
            m_writer.Dispose();
        }
        catch (Exception)
        {
            //閉じられなくても、次の行で開き直す
        }

        m_writer = null;
    }

    /// <summary>
    /// 書き込みの失敗を、最初の1回だけ生のログで知らせます(リリースでも Player.log に残す)。
    /// </summary>
    /// <param name="_message">失敗の内容</param>
    private void ReportFailureOnce(string _message)
    {
        if (m_bFailureReported) { return; }

        m_bFailureReported = true;
        Debug.LogWarning("[" + nameof(PerfCsvRecorder) + "] " + _message);
    }

    // ---------------------------------------------------------------- フォルダと meta.json

    //今回の出力フォルダを作る関数。作れなければ false を返す
    private bool PrepareFolder()
    {
        try
        {
            string folderName = m_label + "_" + m_startTime.ToString(k_FolderDateFormat, CultureInfo.InvariantCulture);
            m_runFolder = Path.Combine(m_outputRoot, folderName);
            Directory.CreateDirectory(m_runFolder);
            return true;
        }
        catch (Exception exception)
        {
            ReportFailureOnce("出力フォルダを作れません: " + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// 条件を書いた meta.json を書き出します。比べる計測の環境が同じかを、後から確かめるために使います。
    /// </summary>
    /// <param name="_bCleanExit">正常に終了したか(起動時は false、終了時に true で書き直す)</param>
    private void WriteMeta(bool _bCleanExit)
    {
        try
        {
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"schema\": " + k_SchemaVersion + ",");
            json.AppendLine("  \"label\": " + PerfCsvFormat.JsonString(m_label) + ",");
            json.AppendLine("  \"start\": " + PerfCsvFormat.JsonString(m_startTime.ToString(k_MetaDateFormat, CultureInfo.InvariantCulture)) + ",");
            json.AppendLine("  \"interval_seconds\": " + k_RowIntervalSeconds.ToString(CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"columns\": " + PerfCsvFormat.JsonString(k_CsvHeader) + ",");
            json.AppendLine("  \"transition_file\": " + PerfCsvFormat.JsonString(PerfTransitionRecorder.k_FileName) + ",");
            json.AppendLine("  \"transition_columns\": " + PerfCsvFormat.JsonString(PerfTransitionRecorder.k_CsvHeader) + ",");
            json.AppendLine("  \"arguments\": " + PerfCsvFormat.JsonString(m_commandLine) + ",");
            json.AppendLine("  \"app_version\": " + PerfCsvFormat.JsonString(Application.version) + ",");
            json.AppendLine("  \"unity_version\": " + PerfCsvFormat.JsonString(Application.unityVersion) + ",");
            json.AppendLine("  \"development_build\": " + (Debug.isDebugBuild ? "true" : "false") + ",");
            json.AppendLine("  \"is_editor\": " + (Application.isEditor ? "true" : "false") + ",");
            json.AppendLine("  \"scripting_backend\": " + PerfCsvFormat.JsonString(k_ScriptingBackend) + ",");
            json.AppendLine("  \"os\": " + PerfCsvFormat.JsonString(SystemInfo.operatingSystem) + ",");
            json.AppendLine("  \"cpu\": " + PerfCsvFormat.JsonString(SystemInfo.processorType) + ",");
            json.AppendLine("  \"cpu_threads\": " + SystemInfo.processorCount + ",");
            json.AppendLine("  \"system_memory_mb\": " + SystemInfo.systemMemorySize + ",");
            json.AppendLine("  \"gpu\": " + PerfCsvFormat.JsonString(SystemInfo.graphicsDeviceName) + ",");
            json.AppendLine("  \"gpu_api\": " + PerfCsvFormat.JsonString(SystemInfo.graphicsDeviceType.ToString()) + ",");
            json.AppendLine("  \"gpu_memory_mb\": " + SystemInfo.graphicsMemorySize + ",");
            json.AppendLine("  \"nvml_available\": " + (PerfNativeMetrics.IsGpuAvailable ? "true" : "false") + ",");
            json.AppendLine("  \"nvml_device\": " + PerfCsvFormat.JsonString(PerfNativeMetrics.GpuName) + ",");
            json.AppendLine("  \"frame_timing_enabled\": " + (m_bTimingEnabled ? "true" : "false") + ",");
            json.AppendLine("  \"render_pipeline\": " + PerfCsvFormat.JsonString(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "Built-in") + ",");
            json.AppendLine("  \"quality_level\": " + PerfCsvFormat.JsonString(QualitySettings.names[QualitySettings.GetQualityLevel()]) + ",");
            json.AppendLine("  \"vsync_count\": " + QualitySettings.vSyncCount + ",");
            json.AppendLine("  \"target_frame_rate\": " + Application.targetFrameRate + ",");
            json.AppendLine("  \"screen\": " + PerfCsvFormat.JsonString(Screen.width + "x" + Screen.height) + ",");
            json.AppendLine("  \"fullscreen_mode\": " + PerfCsvFormat.JsonString(Screen.fullScreenMode.ToString()) + ",");
            json.AppendLine("  \"displays\": " + Display.displays.Length + ",");
            json.AppendLine("  \"display_sizes\": " + PerfCsvFormat.JsonString(BuildDisplaySizes()) + ",");
            json.AppendLine("  \"mode\": " + PerfCsvFormat.JsonString(m_mode) + ",");
            json.AppendLine("  \"planned\": " + m_planned + ",");
            json.AppendLine("  \"until\": " + (m_bUntil ? "true" : "false") + ",");
            json.AppendLine("  \"start_vram_mb\": " + m_startVramMB.ToString("F0", CultureInfo.InvariantCulture) + ",");
            json.AppendLine("  \"editor_running\": " + (m_bEditorRunning ? "true" : "false") + ",");
            json.AppendLine("  \"state_names\": " + PerfCsvFormat.JsonString(BuildStateNames()) + ",");

            //停止の理由。設定されないまま終了したら quit。記録の途中(clean_exit が false)は空
            string stopReason = s_stopReason.Length > 0 ? s_stopReason : (_bCleanExit ? k_StopReasonQuit : string.Empty);
            json.AppendLine("  \"stop_reason\": " + PerfCsvFormat.JsonString(stopReason) + ",");
            json.AppendLine("  \"clean_exit\": " + (_bCleanExit ? "true" : "false"));
            json.AppendLine("}");

            File.WriteAllText(Path.Combine(m_runFolder, k_MetaFileName), json.ToString(), new UTF8Encoding(false));
        }
        catch (Exception exception)
        {
            ReportFailureOnce("meta.json を書けません: " + exception.Message);
        }
    }

    //接続されている画面の大きさを、"2560x1440,2560x1440" の形にして返す関数
    private static string BuildDisplaySizes()
    {
        StringBuilder builder = new StringBuilder();

        //画面を1つずつ書き足す処理
        for (int i = 0; i < Display.displays.Length; ++i)
        {
            if (i > 0) { builder.Append(k_Comma); }
            builder.Append(Display.displays[i].systemWidth).Append('x').Append(Display.displays[i].systemHeight);
        }

        return builder.ToString();
    }

    //CSVのstate列の番号と名前の対応を、"0:Splash,1:Opening,..." の形にして返す関数(meta.json 用。集計側が番号を名前に直すのに使う)
    private static string BuildStateNames()
    {
        StringBuilder builder = new StringBuilder();
        Array values = Enum.GetValues(typeof(GameStateId));

        //Stateを1つずつ「番号:名前」で書き足す処理
        for (int i = 0; i < values.Length; ++i)
        {
            GameStateId state = (GameStateId)values.GetValue(i);
            if (i > 0) { builder.Append(k_Comma); }
            builder.Append((int)state).Append(':').Append(state.ToString());
        }

        return builder.ToString();
    }

    //終了処理。meta.json に正常終了を書き、CSVを閉じ、NVMLを終了する関数(何度呼んでも1回だけ行う)
    private void Finish()
    {
        if (m_bFinished) { return; }

        m_bFinished = true;

        GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
        FlowTiming.OnBegin -= OnFlowBegin;
        FlowTiming.OnEnd -= OnFlowEnd;

        if (m_runFolder.Length > 0) { WriteMeta(true); }

        //終わっていない処理が残っていれば、TOTAL なしで書き出してから閉じる
        if (m_pTransitionRecorder != null)
        {
            m_pTransitionRecorder.Dispose();
            m_pTransitionRecorder = null;
        }

        CloseWriter();
        PerfNativeMetrics.Shutdown();
    }
}
