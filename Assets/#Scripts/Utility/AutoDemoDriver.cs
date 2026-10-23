/*━━━━━━━━━
@file    AutoDemoDriver.cs
@brief   F9・Yキーの自動走行をEditorと共通の自動走行へ接続し、Uキーでは指定の周回数(既定10周)を自動で繰り返す
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/19
         最終更新日 : 2026/10/22
━━━━━━━━━*/

using UnityEngine;

//周回の自動繰り返し(Uキー)の流れ
//  1. Uキーで、Yキーと同じく始める(BGMとFFBを止めて Load へ緊急リセット → Opening・Menuの自動入力 → Racingで自動走行)
//  2. ゴールして Result へ着いたら、1周完了。Load を経由して Splash へ戻った所で、リセットなしで次の周を予約する
//  3. 指定の周回数に達したら止める(アプリは終了しない。Escで終了する)
//  4. 次のときは、繰り返しを中止する: もう一度Uを押した / F9・Yを押した / 1周が k_LapTimeoutSeconds で終わらなかった(走行が詰まったなど)
//リリースビルドでも Player.log に残すため、AppLog ではなく生のログを使う(AppLog はリリースで除去される)
public static class AutoDemoDriver
{
    private const int k_DefaultLoopLaps = 10; //Uキーで繰り返す周回数(計測の基準)
    private const float k_LapTimeoutSeconds = 900f; //1周がこの秒数で終わらなければ中止する(実測の1周は約4〜5分。走行が詰まったときの保険)
    private const string k_LogTag = "[AutoDemoLoop] "; //Player.logで探すための印
    private const string k_ModeManual = "MT"; //MT走行の表記
    private const string k_ModeAutomatic = "AT"; //AT走行の表記

    private static bool s_bLooping; //周回の繰り返し中か
    private static bool s_bManual; //MTで走るか(falseならAT)
    private static bool s_bResultReached; //今の周がResultへ着いたか(次にSplashへ戻った所で次の周を始める合図)
    private static bool s_bSubscribed; //GameFlowRunnerの通知を購読済みか
    private static int s_totalLaps; //繰り返す周回数
    private static int s_lapNumber; //今の周(1から)
    private static float s_lapDeadline; //今の周を終えるべき時刻(Time.realtimeSinceStartup)
    private static bool s_bCompleted; //直前の繰り返しが、指定の周回数をすべて終えて止まったか(中止や詰まりなら false)

    //周回の繰り返し中か
    public static bool IsLooping { get { return s_bLooping; } }

    //今の周の番号(繰り返し中は1から。繰り返し中でなければ0)。計測の記録に使う
    public static int CurrentLap { get { return s_bLooping ? s_lapNumber : 0; } }

    //直前の繰り返しが、指定の周回数をすべて終えて止まったか(中止や詰まりなら false)
    public static bool LastLoopCompleted { get { return s_bCompleted; } }

    //既存のF9呼び出しをTrack ATへ接続する関数
    public static void Start() { Start(false); }

    /// <summary>
    /// 選択された変速方式で、最新の共通走行処理を1周だけ開始します(周回の繰り返しが残っていれば、先に中止します)。
    /// </summary>
    /// <param name="_bManual">MTで走るか(falseならAT)</param>
    public static void Start(bool _bManual)
    {
        CancelLoop();
        TrackDriveSession.BeginRuntime(_bManual);
    }

    /// <summary>
    /// 既定の周回数(k_DefaultLoopLaps)で、周回の自動繰り返しを開始します。
    /// </summary>
    /// <param name="_bManual">MTで走るか(falseならAT)</param>
    public static void StartLoop(bool _bManual)
    {
        StartLoop(_bManual, k_DefaultLoopLaps);
    }

    /// <summary>
    /// 周回の自動繰り返しを開始します。1周目はYキーと同じ流れで始まり、以降は各周のResult到達後、Splashへ戻った所で自動で始まります。
    /// </summary>
    /// <param name="_bManual">MTで走るか(falseならAT)</param>
    /// <param name="_laps">繰り返す周回数</param>
    public static void StartLoop(bool _bManual, int _laps)
    {
        if (_laps <= 0) { return; }
        if (GameFlowRunner.Instance == null || GameManager.Instance == null) { return; } //始められない状態では、繰り返しの状態も作らない

        CancelLoop();
        SubscribeFlowEvent();

        s_bLooping = true;
        s_bCompleted = false;
        s_bManual = _bManual;
        s_bResultReached = false;
        s_totalLaps = _laps;
        s_lapNumber = 1;
        s_lapDeadline = Time.realtimeSinceStartup + k_LapTimeoutSeconds;

        TrackDriveSession.BeginRuntime(_bManual);
        Debug.Log(k_LogTag + "開始 " + s_lapNumber + "/" + s_totalLaps + "周目(" + (s_bManual ? k_ModeManual : k_ModeAutomatic) + ")");
    }

    //周回の繰り返しを中止し、今の周の自動入力も止める関数(繰り返し中でなければ何もしない)
    public static void CancelLoop()
    {
        if (!s_bLooping) { return; }

        s_bLooping = false;
        TrackDriveSession.Stop();
        TrackDriveSession.CancelResultReturn(); //Resultからの自動復帰の予約も取り消す
        Debug.Log(k_LogTag + "中止 " + s_lapNumber + "/" + s_totalLaps + "周目");
    }

    //毎フレーム呼ぶ関数。自動走行の更新を進め、1周が終わらないとき(走行が詰まったなど)は繰り返しを中止する
    public static void Tick()
    {
        TrackDriveSession.RuntimeTick();

        if (!s_bLooping) { return; }
        if (Time.realtimeSinceStartup <= s_lapDeadline) { return; }

        Debug.LogWarning(k_LogTag + s_lapNumber + "/" + s_totalLaps + "周目が " + k_LapTimeoutSeconds + " 秒で終わりませんでした(走行が詰まった可能性)。繰り返しを中止します");
        CancelLoop();
    }

    //GameFlowRunnerの通知を、一度だけ購読する関数
    private static void SubscribeFlowEvent()
    {
        if (s_bSubscribed) { return; }

        GameFlowRunner.OnStateFadeInComplete += HandleStateFadeInComplete;
        s_bSubscribed = true;
    }

    /// <summary>
    /// 画面が見えた(FadeIn完了)State の通知を受けて、周回を進めます。
    /// Resultへ着いたら1周完了とし、Splashへ戻った所で、リセットなしで次の周を予約します。
    /// </summary>
    /// <param name="_state">FadeInが完了したState</param>
    private static void HandleStateFadeInComplete(GameStateId _state)
    {
        if (!s_bLooping) { return; }

        if (_state == GameStateId.Result)
        {
            s_bResultReached = true;
            Debug.Log(k_LogTag + "完了 " + s_lapNumber + "/" + s_totalLaps + "周目(Result)");

            //最後の周なら、ここで終わる(アプリは終了しない)
            if (s_lapNumber >= s_totalLaps)
            {
                s_bLooping = false;
                s_bCompleted = true;
                Debug.Log(k_LogTag + "全 " + s_totalLaps + " 周が完了しました。Load を経由して Splash へ戻ります。Esc で終了してください");
            }
            return;
        }

        //前の周がResultへ着いたあと、Loadを経由してSplashへ戻った所で、次の周を始める
        if (_state == GameStateId.Splash && s_bResultReached)
        {
            s_bResultReached = false;
            ++s_lapNumber;
            s_lapDeadline = Time.realtimeSinceStartup + k_LapTimeoutSeconds;

            TrackDriveSession.ArmRuntime(s_bManual);
            Debug.Log(k_LogTag + "開始 " + s_lapNumber + "/" + s_totalLaps + "周目(" + (s_bManual ? k_ModeManual : k_ModeAutomatic) + ")");
        }
    }
}
