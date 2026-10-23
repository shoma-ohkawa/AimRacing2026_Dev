/*━━━━━━━━━
@file    PerfTransitionRecorder.cs
@brief   計測用: 遷移・ロード時間を、FlowTimingの通知から transitions.csv へ記録する(PerfCsvRecorder が持つ)
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/19
         最終更新日 : 2026/10/19
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

//遷移エンジン(GameFlowRunner など)が FlowTiming で通知する各ステップの所要時間を、1ステップ1行で記録する
//  1. 1つの処理(Boot / Transition / Reset / Refresh)の最中は、メモリ上に溜めるだけで、ファイルには触らない(遷移の最中のI/Oを避けるため)
//  2. 処理が終わった時に、START・各ステップ・TOTAL をまとめて書き出して Flush する
//  3. 次の処理が始まった時、またはアプリの終了時に、終わっていない処理が残っていれば TOTAL なしで書き出す(中断された処理)
//  4. 通知された文字列(ステップ名・シーン名)をそのまま書く。計測のために新しい文字列を作らない
//  5. 1つの処理に溜められるステップは k_MaxSteps 件まで。超えた分は捨てる(TOTAL は書く)
public sealed class PerfTransitionRecorder : IDisposable
{
    public const string k_FileName = "transitions.csv"; //出力するファイル名
    public const string k_CsvHeader = "wall,uptime_s,seq,kind,from,to,step,detail,type,ms,gc0_d,gc2_d,ws_mb,mono_mb"; //CSVの列名

    private const int k_MaxSteps = 64; //1つの処理に溜められるステップの数
    private const int k_RowCapacity = 256; //1行分の文字数の初期容量
    private const int k_FileBufferBytes = 4096; //ファイルのバッファの大きさ(バイト)
    private const int k_GcGeneration0 = 0; //GCの世代0
    private const int k_GcGeneration2 = 2; //GCの世代2
    private const int k_DecimalsOne = 1; //小数1桁
    private const int k_DecimalsTwo = 2; //小数2桁
    private const double k_MarkMs = 0.0; //開始の印の所要時間(印なので0)
    private const int k_MarkGcCount = 0; //開始の印のGC回数(印なので0)
    private const double k_BytesPerMB = 1024.0 * 1024.0; //バイトからMBへの換算
    private const double k_MillisecondsPerSecond = 1000.0; //秒からミリ秒への換算
    private const double k_Unavailable = -1.0; //取得できなかった値
    private const float k_NoEndSeconds = 0f; //終了時刻を使わない(中断された処理の)書き出しに渡す値
    private const char k_Comma = ','; //CSVの区切り
    private const char k_NewLine = '\n'; //CSVの改行
    private const string k_StepStart = "START"; //処理の開始を表すステップ名
    private const string k_StepTotal = "TOTAL"; //処理の終了を表すステップ名
    private const string k_TypeMark = "mark"; //開始の印
    private const string k_TypeWork = "work"; //作業
    private const string k_TypeWait = "wait"; //待ち
    private const string k_TypeTotal = "total"; //処理全体

    //溜めておく1ステップ分の記録
    private struct StepRecord
    {
        public string m_step; //ステップ名
        public string m_detail; //詳細
        public FlowStepType m_type; //作業か待ちか
        public DateTime m_wall; //ステップが終わった日時
        public double m_uptime; //同じ時刻の、アプリを起動してからの秒数
        public double m_ms; //所要時間(ミリ秒)
        public int m_gc0Delta; //ステップの間に起きたGC(世代0)の回数。取得しないときは-1
        public int m_gc2Delta; //ステップの間に起きたGC(世代2)の回数。取得しないときは-1
        public double m_workingSetMB; //ステップ終了時の物理メモリ(MB)。取得しないときは-1
        public double m_monoUsedMB; //ステップ終了時のマネージドヒープの使用量(MB)。取得しないときは-1
    }

    private readonly StepRecord[] m_steps = new StepRecord[k_MaxSteps]; //溜めているステップ
    private readonly StringBuilder m_row = new StringBuilder(k_RowCapacity); //1行分の組み立て用
    private readonly string[] m_kindNames; //FlowTimingKindの名前(値の順)
    private readonly Dictionary<int, string> m_stateNames = new Dictionary<int, string>(); //GameStateIdの名前
    private readonly string m_path; //transitions.csv のパス
    private char[] m_rowChars = new char[k_RowCapacity]; //1行分をファイルへ渡すための文字配列

    private StreamWriter m_writer; //CSVの書き込み先
    private bool m_bFailureReported; //書き込みの失敗を報告済みか
    private bool m_bDisposed; //後片付けを済ませたか
    private bool m_bOpen; //開始したが、まだ終わっていない処理があるか
    private int m_stepCount; //溜めているステップの数
    private int m_seq; //処理の通し番号
    private FlowTimingKind m_kind; //今の処理の種類
    private GameStateId m_from; //今の処理の離脱元State
    private GameStateId m_to; //今の処理の遷移先State
    private float m_startSeconds; //今の処理の開始時刻(Time.realtimeSinceStartup)
    private DateTime m_beginWall; //今の処理を始めた日時
    private double m_beginUptime; //同じ時刻の、アプリを起動してからの秒数
    private int m_beginGc0; //処理の開始時点のGC回数(世代0)
    private int m_beginGc2; //処理の開始時点のGC回数(世代2)
    private double m_beginWorkingSetMB; //処理の開始時点の物理メモリ(MB)
    private double m_beginMonoUsedMB; //処理の開始時点のマネージドヒープの使用量(MB)
    private int m_markGc0; //直前のステップの終了時点のGC回数(世代0)
    private int m_markGc2; //直前のステップの終了時点のGC回数(世代2)

    /// <summary>
    /// 記録を始めます。FlowTimingの通知を購読します。
    /// </summary>
    /// <param name="_runFolder">transitions.csv を作る出力フォルダ</param>
    public PerfTransitionRecorder(string _runFolder)
    {
        m_path = Path.Combine(_runFolder, k_FileName);
        m_kindNames = Enum.GetNames(typeof(FlowTimingKind));

        //GameStateIdの名前を、計測中に文字列を作らないよう、先に集めておく処理
        foreach (GameStateId id in Enum.GetValues(typeof(GameStateId)))
        {
            m_stateNames[(int)id] = id.ToString();
        }

        FlowTiming.OnBegin += HandleBegin;
        FlowTiming.OnStep += HandleStep;
        FlowTiming.OnEnd += HandleEnd;
    }

    /// <summary>
    /// 購読を解除し、終わっていない処理が残っていれば TOTAL なしで書き出して、ファイルを閉じます。
    /// </summary>
    public void Dispose()
    {
        if (m_bDisposed) { return; }

        m_bDisposed = true;
        FlowTiming.OnBegin -= HandleBegin;
        FlowTiming.OnStep -= HandleStep;
        FlowTiming.OnEnd -= HandleEnd;

        if (m_bOpen) { FlushProcess(false, k_NoEndSeconds); }

        CloseWriter();
    }

    // ---------------------------------------------------------------- FlowTimingの通知

    //処理の開始。前の処理が終わっていなければ、中断された処理として TOTAL なしで書き出す
    private void HandleBegin(FlowTimingKind _kind, GameStateId _from, GameStateId _to, float _startSeconds)
    {
        if (m_bOpen) { FlushProcess(false, k_NoEndSeconds); }

        ++m_seq;
        m_bOpen = true;
        m_stepCount = 0;
        m_kind = _kind;
        m_from = _from;
        m_to = _to;
        m_startSeconds = _startSeconds;
        m_beginWall = DateTime.Now;
        m_beginUptime = Time.realtimeSinceStartupAsDouble;
        m_beginGc0 = GC.CollectionCount(k_GcGeneration0);
        m_beginGc2 = GC.CollectionCount(k_GcGeneration2);
        m_beginWorkingSetMB = PerfNativeMetrics.GetWorkingSetMB();
        m_beginMonoUsedMB = Profiler.GetMonoUsedSizeLong() / k_BytesPerMB;
        m_markGc0 = m_beginGc0;
        m_markGc2 = m_beginGc2;
    }

    /// <summary>
    /// ステップの終了。通知された時点のGC回数とメモリを取る(数十マイクロ秒)ので、次のステップの時間に含まれる。
    /// </summary>
    /// <param name="_step">ステップ名</param>
    /// <param name="_detail">詳細</param>
    /// <param name="_type">作業か待ちか</param>
    /// <param name="_durationSeconds">所要秒数</param>
    /// <param name="_bBeforeBegin">Beginより前に終わった作業か</param>
    private void HandleStep(string _step, string _detail, FlowStepType _type, float _durationSeconds, bool _bBeforeBegin)
    {
        if (!m_bOpen) { return; }

        int gc0 = GC.CollectionCount(k_GcGeneration0);
        int gc2 = GC.CollectionCount(k_GcGeneration2);
        int gc0Delta = gc0 - m_markGc0;
        int gc2Delta = gc2 - m_markGc2;

        //Beginより前に終わった作業は、今のGC回数やメモリと対応しないので、印は更新せず値も書かない
        if (!_bBeforeBegin)
        {
            m_markGc0 = gc0;
            m_markGc2 = gc2;
        }

        if (m_stepCount >= k_MaxSteps) { return; }

        StepRecord record = new StepRecord();
        record.m_step = _step;
        record.m_detail = _detail;
        record.m_type = _type;
        record.m_wall = DateTime.Now;
        record.m_uptime = Time.realtimeSinceStartupAsDouble;
        record.m_ms = _durationSeconds * k_MillisecondsPerSecond;
        record.m_gc0Delta = _bBeforeBegin ? -1 : gc0Delta;
        record.m_gc2Delta = _bBeforeBegin ? -1 : gc2Delta;
        record.m_workingSetMB = _bBeforeBegin ? k_Unavailable : PerfNativeMetrics.GetWorkingSetMB();
        record.m_monoUsedMB = _bBeforeBegin ? k_Unavailable : Profiler.GetMonoUsedSizeLong() / k_BytesPerMB;
        m_steps[m_stepCount] = record;
        ++m_stepCount;
    }

    //処理の終了。溜めた内容を TOTAL つきでまとめて書き出す
    private void HandleEnd(float _endSeconds)
    {
        if (!m_bOpen) { return; }

        FlushProcess(true, _endSeconds);
    }

    // ---------------------------------------------------------------- 書き出し

    /// <summary>
    /// 溜めている処理を、START・各ステップ・TOTAL の順にまとめて書き出します。
    /// </summary>
    /// <param name="_bCompleted">終わった処理か(falseなら中断された処理として、TOTAL を書かない)</param>
    /// <param name="_endSeconds">終了時刻(Time.realtimeSinceStartup。_bCompletedがtrueのときだけ使う)</param>
    private void FlushProcess(bool _bCompleted, float _endSeconds)
    {
        m_bOpen = false;

        EnsureWriter();
        if (m_writer == null) { return; }

        m_row.Clear();

        string kindName = m_kindNames[(int)m_kind];
        string fromName = ResolveStateName(m_from);
        string toName = ResolveStateName(m_to);

        AppendRow(m_beginWall, m_beginUptime, kindName, fromName, toName, k_StepStart, string.Empty, k_TypeMark, k_MarkMs, k_MarkGcCount, k_MarkGcCount, m_beginWorkingSetMB, m_beginMonoUsedMB);

        //溜めたステップを順に書き込む処理
        for (int i = 0; i < m_stepCount; ++i)
        {
            StepRecord record = m_steps[i];
            string typeName = record.m_type == FlowStepType.Work ? k_TypeWork : k_TypeWait;
            AppendRow(record.m_wall, record.m_uptime, kindName, fromName, toName, record.m_step, record.m_detail, typeName,
                record.m_ms, record.m_gc0Delta, record.m_gc2Delta, record.m_workingSetMB, record.m_monoUsedMB);
        }

        if (_bCompleted)
        {
            double totalMs = (_endSeconds - m_startSeconds) * k_MillisecondsPerSecond;
            int gc0Total = GC.CollectionCount(k_GcGeneration0) - m_beginGc0;
            int gc2Total = GC.CollectionCount(k_GcGeneration2) - m_beginGc2;
            AppendRow(DateTime.Now, Time.realtimeSinceStartupAsDouble, kindName, fromName, toName, k_StepTotal, string.Empty, k_TypeTotal,
                totalMs, gc0Total, gc2Total, PerfNativeMetrics.GetWorkingSetMB(), Profiler.GetMonoUsedSizeLong() / k_BytesPerMB);
        }

        int length = m_row.Length;
        if (length > m_rowChars.Length) { m_rowChars = new char[length * 2]; }
        m_row.CopyTo(0, m_rowChars, 0, length);

        try
        {
            m_writer.Write(m_rowChars, 0, length);
            m_writer.Flush(); //リアルタイムに読めるよう、処理ごとに書き出す
        }
        catch (Exception exception)
        {
            //ディスクの空き不足などで書けない場合は、閉じて次の処理で開き直す
            CloseWriter();
            ReportFailureOnce("transitions.csv へ書き込めません: " + exception.Message);
        }
    }

    /// <summary>
    /// CSVの1行を、組み立て用のバッファへ追加します。
    /// </summary>
    /// <param name="_wall">その行の日時</param>
    /// <param name="_uptime">同じ時刻の、アプリを起動してからの秒数</param>
    /// <param name="_kind">処理の種類の名前</param>
    /// <param name="_from">離脱元Stateの名前</param>
    /// <param name="_to">遷移先Stateの名前</param>
    /// <param name="_step">ステップ名</param>
    /// <param name="_detail">詳細</param>
    /// <param name="_type">種類(mark / work / wait / total)</param>
    /// <param name="_ms">所要時間(ミリ秒)</param>
    /// <param name="_gc0Delta">GC(世代0)の回数</param>
    /// <param name="_gc2Delta">GC(世代2)の回数</param>
    /// <param name="_workingSetMB">物理メモリ(MB)</param>
    /// <param name="_monoUsedMB">マネージドヒープの使用量(MB)</param>
    private void AppendRow(
        DateTime _wall,
        double _uptime,
        string _kind,
        string _from,
        string _to,
        string _step,
        string _detail,
        string _type,
        double _ms,
        int _gc0Delta,
        int _gc2Delta,
        double _workingSetMB,
        double _monoUsedMB)
    {
        PerfCsvFormat.AppendDateTime(m_row, _wall);
        AppendNumberCell(_uptime, k_DecimalsTwo);
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendInteger(m_row, m_seq);
        AppendTextCell(_kind);
        AppendTextCell(_from);
        AppendTextCell(_to);
        AppendTextCell(_step);
        AppendTextCell(_detail);
        AppendTextCell(_type);
        AppendNumberCell(_ms, k_DecimalsTwo);
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendInteger(m_row, _gc0Delta);
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendInteger(m_row, _gc2Delta);
        AppendNumberCell(_workingSetMB, k_DecimalsOne);
        AppendNumberCell(_monoUsedMB, k_DecimalsOne);
        m_row.Append(k_NewLine);
    }

    //区切りと文字列を、1行の組み立て用バッファへ追加する関数
    private void AppendTextCell(string _text)
    {
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendText(m_row, _text);
    }

    //区切りと小数を、1行の組み立て用バッファへ追加する関数(負の値は取得できなかったものとして -1 を書く)
    private void AppendNumberCell(double _value, int _decimals)
    {
        m_row.Append(k_Comma);
        PerfCsvFormat.AppendValue(m_row, _value, _decimals);
    }

    //GameStateIdの名前を返す関数(定義にない値は空文字)
    private string ResolveStateName(GameStateId _id)
    {
        return m_stateNames.TryGetValue((int)_id, out string name) ? name : string.Empty;
    }

    //transitions.csv を開く関数。新しいファイルの先頭にだけ列名を書く
    private void EnsureWriter()
    {
        if (m_writer != null) { return; }

        try
        {
            //別のプロセス(ビューア)が読んでいても書けるよう、共有を許可して追記で開く
            FileStream stream = new FileStream(m_path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, k_FileBufferBytes);
            m_writer = new StreamWriter(stream, new UTF8Encoding(false), k_FileBufferBytes);

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
            ReportFailureOnce("transitions.csv を開けません: " + exception.Message);
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
            //閉じられなくても、次の処理で開き直す
        }

        m_writer = null;
    }

    /// <summary>
    /// 書き込みの失敗を、最初の1回だけ生のログで知らせます(AppLog はリリースで除去されるため、Player.log に残らない)。
    /// </summary>
    /// <param name="_message">失敗の内容</param>
    private void ReportFailureOnce(string _message)
    {
        if (m_bFailureReported) { return; }

        m_bFailureReported = true;
        Debug.LogWarning("[" + nameof(PerfTransitionRecorder) + "] " + _message);
    }
}
