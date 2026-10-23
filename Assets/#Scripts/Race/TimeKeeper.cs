using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 概要　：時間を計測し、テキストとして表示するプログラム
/// 担当者：22CU0235 諸星大和
/// 変更日：2024/05/17 作成
/// 　　　：2024/09/04 名称変更、セクタータイム用の処理を追加
/// 　　　：2026/09/01 GC削減対応
/// 　　　：2026/09/12 タイマー参照統一(Meter側Timer参照を廃止しTimeKeeper自身の値を単一ソース化)
/// </summary>
public class TimeKeeper : MonoBehaviour
{
    private const int k_MillisecondsPerMinute = 60000;
    private const int k_MillisecondsPerSecond = 1000;
    private const int k_SecondsPerMinute = 60;

    /// <summary>
    /// 時間保存用クラス
    /// </summary>
    [System.Serializable]
    private class SavedTimeInfo
    {
        [FormerlySerializedAs("minutes")] public int m_minutes;
        [FormerlySerializedAs("seconds")] public int m_seconds;
        [FormerlySerializedAs("milliSecond")] public float m_milliseconds;
        [FormerlySerializedAs("totalMilliSecond")] public float m_totalMilliseconds;

        public SavedTimeInfo(int _minutes, int _seconds, float _milliseconds)
        {
            m_minutes = _minutes;
            m_seconds = _seconds;
            m_milliseconds = _milliseconds;

            m_totalMilliseconds =
                (m_minutes * k_MillisecondsPerMinute) +
                (m_seconds * k_MillisecondsPerSecond) +
                m_milliseconds;
        }
    }

    [FormerlySerializedAs("_savedTimeInfo")]
    [SerializeField] private List<SavedTimeInfo> m_savedTimeInfo = new List<SavedTimeInfo>();

    [FormerlySerializedAs("_minutes")]
    [SerializeField] private int m_minutes = 0;

    [FormerlySerializedAs("_seconds")]
    [SerializeField] private int m_seconds = 0;

    [FormerlySerializedAs("_milliSecond")]
    [SerializeField] private float m_milliseconds = 0;

    [FormerlySerializedAs("_isActive")]
    [SerializeField] private bool m_bIsActive = false;

    [FormerlySerializedAs("_totalTimeText")]
    [SerializeField] private TextMeshProUGUI m_totalTimeText = null;

    [SerializeField] private SectorTimeManager m_sectorTimeManager;

    // 表示済みの値を保持して、同じ値ならUI更新しない
    private int m_lastDisplayMinutes = int.MinValue;
    private int m_lastDisplaySeconds = int.MinValue;
    private int m_lastDisplayMilliseconds = int.MinValue;

    private void Start()
    {
        m_bIsActive = false;
        ResetDisplayCache();
    }

    private void FixedUpdate()
    {
        if (!m_bIsActive)
        {
            return;
        }

        CountUpTimer();
    }

    public int GetSavedTimeInfoCount()
    {
        return m_savedTimeInfo.Count;
    }

    /// <summary>
    /// 時間を加算する関数
    /// </summary>
    private void CountUpTimer()
    {
        // FixedUpdate内なので fixedDeltaTime を使用
        m_milliseconds += Time.fixedDeltaTime * k_MillisecondsPerSecond;

        if (m_milliseconds >= k_MillisecondsPerSecond)
        {
            m_seconds++;
            m_milliseconds -= k_MillisecondsPerSecond;

            if (m_seconds >= k_SecondsPerMinute)
            {
                m_minutes++;
                m_seconds -= k_SecondsPerMinute;
            }
        }

        UpdateTotalTimeText();
    }

    /// <summary>
    /// TotalTime表示を更新する。
    /// 同じ値ならTextMeshProへ再代入しない。
    /// </summary>
    private void UpdateTotalTimeText()
    {
        if (m_totalTimeText == null)
        {
            return;
        }

        int minutes = Mathf.Clamp(m_minutes, 0, 99);
        int seconds = Mathf.Clamp(m_seconds, 0, 99);
        int milliseconds = Mathf.Clamp(Mathf.FloorToInt(m_milliseconds), 0, 999);

        if (minutes == m_lastDisplayMinutes &&
            seconds == m_lastDisplaySeconds &&
            milliseconds == m_lastDisplayMilliseconds)
        {
            return;
        }

        m_totalTimeText.SetText("{0:0}:{1:00}.{2:000}", minutes, seconds, milliseconds);

        m_lastDisplayMinutes = minutes;
        m_lastDisplaySeconds = seconds;
        m_lastDisplayMilliseconds = milliseconds;
    }

    /// <summary>
    /// 稼働させるかを管理する関数
    /// </summary>
    public void ControlActiveFlag(bool _flag)
    {
        m_bIsActive = _flag;
    }

    /// <summary>
    /// 他クラスから現在の計測値を読めるようにする公開プロパティ群
    /// </summary>
    public int CurrentMinutes => Mathf.Clamp(m_minutes, 0, 99);
    public int CurrentSeconds => Mathf.Clamp(m_seconds, 0, 99);
    public int CurrentMilliseconds => Mathf.Clamp(Mathf.FloorToInt(m_milliseconds), 0, 999);
    public float CurrentTotalSeconds => (m_minutes * k_SecondsPerMinute) + m_seconds + (m_milliseconds / k_MillisecondsPerSecond);

    /// <summary>
    /// 呼び出したときに、時間を保存する関数
    /// </summary>
    public void SaveTime()
    {
        m_savedTimeInfo.Add(new SavedTimeInfo(m_minutes, m_seconds, m_milliseconds));
    }

    /// <summary>
    /// 保存した時間を取得する関数
    /// </summary>
    public string RetrieveSavedTime(int _value)
    {
        if (m_sectorTimeManager == null)
        {
            return "0:00.000";
        }

        // Sector1のとき
        if (_value == 0)
        {
            TimerInfo sector1 = m_sectorTimeManager.GetSectorTime(SectorNumber.Sector1);

            return "{" + FormatTime(
                Mathf.FloorToInt(sector1.minutes),
                Mathf.FloorToInt(sector1.seconds),
                Mathf.FloorToInt(sector1.milliSecond)
            ) + "}";
        }

        // Sector2以降のとき
        TimerInfo sector1Time = m_sectorTimeManager.GetSectorTime(SectorNumber.Sector1);
        TimerInfo sector2Time = m_sectorTimeManager.GetSectorTime(SectorNumber.Sector2);

        int sectorMinutes = Mathf.FloorToInt(sector2Time.minutes - sector1Time.minutes);
        int sectorSeconds = Mathf.FloorToInt(sector2Time.seconds - sector1Time.seconds);
        int sectorMilliseconds = Mathf.FloorToInt(sector2Time.milliSecond - sector1Time.milliSecond);

        return "{" + FormatTime(sectorMinutes, sectorSeconds, sectorMilliseconds) + "}";
    }

    /// <summary>
    /// 保存した時間を取得する関数
    /// </summary>
    public string RetrieveSavedTimeToMeter(int _value)
    {
        if (_value < 0 || _value >= m_savedTimeInfo.Count)
        {
            return "0:00.000";
        }

        // Sector1のとき
        if (_value == 0)
        {
            SavedTimeInfo savedTime = m_savedTimeInfo[_value];

            return FormatTime(
                savedTime.m_minutes,
                savedTime.m_seconds,
                Mathf.FloorToInt(savedTime.m_milliseconds)
            );
        }

        // Sector2以降のとき
        float totalMilliseconds =
            m_savedTimeInfo[_value].m_totalMilliseconds -
            m_savedTimeInfo[_value - 1].m_totalMilliseconds;

        return FormatMilliseconds(totalMilliseconds);
    }

    /// <summary>
    /// ゴールした時間を取得する関数
    /// </summary>
    public string RetrieveSavedTotalTime()
    {
        return FormatTime(
            m_minutes,
            m_seconds,
            Mathf.FloorToInt(m_milliseconds)
        );
    }

    public List<string> RetrieveSavedLapTimes()
    {
        List<string> lapTimes = new List<string>();
        float previousTotalMilliseconds = 0.0f;

        for (int i = 0; i < m_savedTimeInfo.Count; ++i)
        {
            float lapMilliseconds = m_savedTimeInfo[i].m_totalMilliseconds - previousTotalMilliseconds;
            lapTimes.Add(FormatMilliseconds(lapMilliseconds));
            previousTotalMilliseconds = m_savedTimeInfo[i].m_totalMilliseconds;
        }

        float totalMilliseconds =
            (m_minutes * k_MillisecondsPerMinute) +
            (m_seconds * k_MillisecondsPerSecond) +
            m_milliseconds;

        float finalLapMilliseconds = totalMilliseconds - previousTotalMilliseconds;

        if (finalLapMilliseconds >= 0.0f)
        {
            lapTimes.Add(FormatMilliseconds(finalLapMilliseconds));
        }

        return lapTimes;
    }

    private static string FormatMilliseconds(float _milliseconds)
    {
        int totalMilliseconds = Mathf.Max(0, Mathf.RoundToInt(_milliseconds));

        int minutes = totalMilliseconds / k_MillisecondsPerMinute;
        int seconds = totalMilliseconds % k_MillisecondsPerMinute / k_MillisecondsPerSecond;
        int milliseconds = totalMilliseconds % k_MillisecondsPerSecond;

        return FormatTime(minutes, seconds, milliseconds);
    }

    private static string FormatTime(int _minutes, int _seconds, int _milliseconds)
    {
        _minutes = Mathf.Clamp(_minutes, 0, 99);
        _seconds = Mathf.Clamp(_seconds, 0, 99);
        _milliseconds = Mathf.Clamp(_milliseconds, 0, 999);

        return _minutes.ToString("0") + ":" +
               _seconds.ToString("00") + "." +
               _milliseconds.ToString("000");
    }

    private void ResetDisplayCache()
    {
        m_lastDisplayMinutes = int.MinValue;
        m_lastDisplaySeconds = int.MinValue;
        m_lastDisplayMilliseconds = int.MinValue;
    }
}