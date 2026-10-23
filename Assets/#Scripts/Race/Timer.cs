/**
 * @file Timer.cs
 * @brief 時間を計測するタイマー
 * @author yuuha terae
 */

using UnityEngine;

/// <summary>
/// 構造体名：TimerInfo<br/>
/// 概要：タイマー情報構造体
/// </summary>
public struct TimerInfo{
    public float total;               // 時間を計測(合計)
    public float minutes;           // 分
    public float seconds;           // 秒
    public float milliSecond;      // ミリ秒

    // 初期化処理
    public void Init(){
        total = 0.0f;
        minutes = 0.0f;
        seconds = 0.0f;
        milliSecond = 0.0f;
    }
}

public class Timer : MonoBehaviour{
    // タイマー情報
    [SerializeField]
    private TimerInfo m_time;

    // 時間を計測するか
    bool m_isMeasureTime;

    #region プロパティ
    public float TotalTime
    {
        get => m_time.total;
    }
    public float Minutes 
    {
        get => m_time.minutes;
    }
    public float Seconds
    {
        get => m_time.seconds;
    }
    public float MiliSeconds
    {
        get => m_time.milliSecond;
    }
    public bool IsMeasureTime
    {
        get => m_isMeasureTime;
        set => m_isMeasureTime = value;
    }
    #endregion

    private void Start(){
        // 初期化
        Init();
    }

    /// <summary>
    /// メソッド名：Init()<br/>
    /// 概要：初期化処理
    /// </summary>
    public void Init(){
        m_time.Init();
        StopTimer();
    }

    /// <summary>
    /// メソッド名：Run()<br/>
    /// 概要：更新処理
    /// </summary>
    public void Run(){
        if (!m_isMeasureTime) return;

            // 時間を計測
            MeasureTime();

            // 時間の表記を変換
            m_time = ConvertTimeNotation(m_time);
    }

    /// <summary>
    /// メソッド名：MeasureTime()
    /// 概要：時間を計測
    /// </summary>
    private void MeasureTime(){
        // 時間計測
        m_time.total += Time.deltaTime;
    }

    /// <summary>
    /// メソッド名：ConvertTimeNotation(TimerInfo _time)<br/>
    /// 概要：時間を秒表記から[0:00.000]の表記に変換する<br/>
    /// 引数：TimeInfo型<br/>
    /// 戻り値：TimeInfo型
    /// </summary>
    public TimerInfo ConvertTimeNotation(TimerInfo _time){
        // 0秒以下になっていたら補正してreturn
        if (_time.total <= 0.0f) {
            TimerInfo _tmpTime = new TimerInfo();
            _tmpTime.Init();
            return _tmpTime;
        }

        float _totalSeconds = _time.total;     // 合計秒
        float _minutes;                                 // 分
        float _seconds;                                 // 秒
        float _milliSecond;                            // ミリ秒

        // 分を算出
        _minutes = _totalSeconds / 60.0f;
        _minutes = Mathf.FloorToInt(_minutes);

        // 秒を算出
        _seconds = _totalSeconds - (_minutes * 60.0f);
        _seconds = Mathf.FloorToInt(_seconds);

        // ミリ秒を算出
        _milliSecond = Mathf.FloorToInt(_totalSeconds % 1.0f * 1000.0f);

        // 合計秒、分、秒、ミリ秒を格納して返す
        TimerInfo time = new TimerInfo();
        time.Init();
        time.total = _time.total;
        time.minutes = _minutes;
        time.seconds = _seconds;
        time.milliSecond = _milliSecond;
        return time;
    }

    /// <summary>
    /// メソッド名：StartTimer()<br/>
    /// 概要：タイマースタート
    /// </summary>
    public void StartTimer(){
        m_isMeasureTime = true;
    }

    /// <summary>
    /// メソッド名：StopTimer()<br/>
    /// 概要：タイマーストップ
    /// </summary>
    public void StopTimer(){
        m_isMeasureTime = false;
    }

    /// <summary>
    /// メソッド名：ResetTimer()<br/>
    /// 概要：タイマーをリセットする
    /// </summary>
    public void ResetTimer(){
        m_time.Init();
    }
}
