using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 概要　：スタート/ゴールを通過したときにアクションを起こすプログラム
/// 担当者：22CU0235 諸星大和
/// 変更日：2024/05/17 作成
/// 　　　：2024/09/04 名称変更
/// </summary>
public class Line_StartFinish : MonoBehaviour
{
    private const double k_DebugLapSeconds = 999.99; //デバッグ完走時の各ラップ値
    private const int k_MillisecondsPerSecond = 1000; //秒からミリ秒への換算値
    private const int k_MillisecondsPerMinute = 60000; //分からミリ秒への換算値

    [System.Serializable]
    private enum LineMode
    {
        Start,
        Finish
    }
    [FormerlySerializedAs("_lineMode")]
    [SerializeField] private LineMode m_lineMode;

    private BoxCollider m_boxCollider;

    [FormerlySerializedAs("_timeKeeper")]
    [SerializeField]
    private TimeKeeper m_timeKeeper;

    //[SerializeField]
    //private GameObject _goalText;

    [FormerlySerializedAs("_isChecked")]
    [SerializeField]
    private bool m_bIsChecked = false; //ラインを通過済みか

    [FormerlySerializedAs("_debugKeyCode")]
    [SerializeField]
    private KeyCode m_debugKeyCode = KeyCode.None;

    [SerializeField, Min(1)]
    private int m_debugLapCount = 3; //デバッグ完走時に作成するラップ数

    #region
    public bool IsChecked => m_bIsChecked;
	#endregion

	private void Start()
    {
        m_boxCollider = GetComponent<BoxCollider>();

        m_bIsChecked = false;

		//if (_goalText != null)
		//{
		//	_goalText.SetActive(false);
		//}
	}

    private void Update()
    {
        if (m_lineMode == LineMode.Finish && m_debugKeyCode == KeyCode.F3)
        {
            if (DebugKey.GetKeyDown(m_debugKeyCode))
            {
                AppLog.Log("[ResultRouteDebug] 元MasterのFinish用F3を検出。デバッグ完走を実行します。");
                CompleteRaceForDebug();
            }
            return;
        }

        if (DebugKey.IsCompleteRaceKey(m_debugKeyCode)) { return; }

        if (DebugKey.GetKeyDown(m_debugKeyCode))
        {
            if (m_bIsChecked == false)
            {
                switch (m_lineMode)
                {
                    case LineMode.Start:
                        m_timeKeeper?.ControlActiveFlag(true);

                        break;

                    case LineMode.Finish:
                        m_timeKeeper?.ControlActiveFlag(false);
                        GameManager.Instance.ResultTime = m_timeKeeper.RetrieveSavedTotalTime();
                        GameManager.Instance.SetLapTimes(m_timeKeeper.RetrieveSavedLapTimes());
                        break;
                }

                m_bIsChecked = true;
            }
        }
    }

    private void OnTriggerEnter(Collider _other)
    {
        if (m_bIsChecked == false)
        {
            switch (m_lineMode)
            {
                case LineMode.Start:
                    m_timeKeeper?.ControlActiveFlag(true);

                    break;

                case LineMode.Finish:
                    m_timeKeeper?.ControlActiveFlag(false);
					GameManager.Instance.ResultTime = m_timeKeeper.RetrieveSavedTotalTime();
					GameManager.Instance.SetLapTimes(m_timeKeeper.RetrieveSavedLapTimes());
					//_goalText?.SetActive(true);
					break;
            }

            m_bIsChecked = true;
        }
    }

    public void CompleteRaceForDebug()
    {
        List<string> lapTimes = new List<string>(m_debugLapCount); //Resultへ渡すデバッグラップ
        for (int i = 0; i < m_debugLapCount; ++i)
        {
            lapTimes.Add(k_DebugLapSeconds.ToString("0.00", CultureInfo.InvariantCulture));
        }

        long totalMilliseconds = (long)Math.Round(
            k_DebugLapSeconds * k_MillisecondsPerSecond * m_debugLapCount);
        long minutes = totalMilliseconds / k_MillisecondsPerMinute;
        long seconds = totalMilliseconds % k_MillisecondsPerMinute / k_MillisecondsPerSecond;
        long milliseconds = totalMilliseconds % k_MillisecondsPerSecond;

        m_timeKeeper?.ControlActiveFlag(false);
        GameManager.Instance.ResultTime = string.Format(
            CultureInfo.InvariantCulture,
            "{0:0}:{1:00}.{2:000}",
            minutes,
            seconds,
            milliseconds);
        GameManager.Instance.SetLapTimes(lapTimes);
        m_bIsChecked = true;
    }

   
}
