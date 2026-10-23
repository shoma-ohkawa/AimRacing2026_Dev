/**
 * @file SectorTimeManager.cs
 * @brief セクタータイムを管理する
 * @author yuuha terae
 */

using UnityEngine;

public class SectorTimeManager : MonoBehaviour
{
    // タイマー(TimeKeeperへ一本化)
    [SerializeField] TimeKeeper m_timeKeeper;

    // セクタータイム格納用
    [SerializeField] private TimerInfo[] m_sectorTimes;

    // セクターポイントの数
    [SerializeField] private int m_numOfSectorPorints;

    void Start(){
        // 初期化処理
        Init();
    }

    /// <summary>
    /// メソッド名:Init()<br />
    /// 概要:初期化処理
    /// </summary>
    public void Init(){
        m_sectorTimes = new TimerInfo[m_numOfSectorPorints];
        for (int i = 0; i < m_numOfSectorPorints; ++i){
            m_sectorTimes[i].Init();
        }
    }

    /// <summary>
    /// メソッド名:RegisterSectorTime(int _sectorNumber)<br />
    /// 概要:セクタータイムを保存用配列に登録<br />
    /// 引数:SectorNumber型 セクタータイムの番号
    /// </summary>
    public void RegisterSectorTime(SectorNumber _sectorNumber){
        m_sectorTimes[(int)_sectorNumber].total = m_timeKeeper.CurrentTotalSeconds;
        m_sectorTimes[(int)_sectorNumber].minutes = m_timeKeeper.CurrentMinutes;
        m_sectorTimes[(int)_sectorNumber].seconds = m_timeKeeper.CurrentSeconds;
        m_sectorTimes[(int)_sectorNumber].milliSecond = m_timeKeeper.CurrentMilliseconds;

        if (_sectorNumber == SectorNumber.Sector2){
            float _sector1TotalTime = m_sectorTimes[(int)SectorNumber.Sector1].total;   // セクター1通過時の時間
            float _sector2TotalTime = m_sectorTimes[(int)SectorNumber.Sector2].total;   // セクター2通過時の時間
            float _sector2Time = _sector2TotalTime - _sector1TotalTime;                        // セクター2の区間タイム

            m_sectorTimes[(int)_sectorNumber] = ConvertSecondsToTimerInfo(_sector2Time);
        }

        if (_sectorNumber == SectorNumber.Sector3){
            float _sector2TotalTime = m_sectorTimes[(int)SectorNumber.Sector2].total;   // セクター2通過時の時間
            float _sector3TotalTime = m_sectorTimes[(int)SectorNumber.Sector3].total;   // セクター3通過時の時間
            float _sector3Time = _sector3TotalTime - _sector2TotalTime;                        // セクター3の区間タイム

            m_sectorTimes[(int)_sectorNumber] = ConvertSecondsToTimerInfo(_sector3Time);
        }
    }

    /// <summary>
    /// メソッド名:ConvertSecondsToTimerInfo(float _totalSeconds)<br />
    /// 概要:合計秒数を分・秒・ミリ秒表記のTimerInfoへ変換する<br />
    /// 引数:float型 変換対象の合計秒数<br />
    /// 戻り値:TimerInfo型 分・秒・ミリ秒に変換した結果
    /// </summary>
    private TimerInfo ConvertSecondsToTimerInfo(float _totalSeconds){
        TimerInfo _time = new TimerInfo();
        _time.Init();

        if (_totalSeconds <= 0.0f){
            return _time;
        }

        _time.total = _totalSeconds;
        _time.minutes = Mathf.FloorToInt(_totalSeconds / 60.0f);
        _time.seconds = Mathf.FloorToInt(_totalSeconds - (_time.minutes * 60.0f));
        _time.milliSecond = Mathf.FloorToInt(_totalSeconds % 1.0f * 1000.0f);
        return _time;
    }

    /// <summary>
    /// メソッド名:GetSectorTime(SectorNumber _sectorNumber)<br />
    /// 概要:セクタータイムを取得<br />
    /// 引数:SectorNumber型 セクタータイムの番号<br />
    /// 戻り値:TimerInfo型 引数で指定したセクター間のタイム
    /// </summary>
    public TimerInfo GetSectorTime(SectorNumber _sectorNumber){
        return m_sectorTimes[(int)_sectorNumber];
    }
}
