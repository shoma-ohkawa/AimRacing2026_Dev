/*━━━━━━━━━
@file RankingRecord.cs
@brief ランキング1件分の記録データ
@author 24cu0312 久場洸太
@date 2026/07/02
最終更新日 2026/07/02
━━━━━━━━━*/

using System;
using System.Globalization;

namespace CrossProjectRanking
{
    /// <summary>
    /// CSVの1行に対応するデータクラスです。
    /// 保存処理ではこのクラスに値を入れてCSVへ書き込み、
    /// 読み込み処理ではCSVの各列をこのクラスへ戻します。
    /// </summary>
    [Serializable]
    public sealed class RankingRecord
    {
        private const double SecondsPerMillisecond = 1000.0;   //ミリ秒を秒へ変換するための値。TimeSecondsプロパティで表示用秒数を作る時に使う

        public string m_id;                 //同じ名前・同じタイムの記録でも別データとして識別するためのID。表示には使わない
        public string m_playerName;         //ランキングに表示するプレイヤー名。CSVのPlayerName列に保存される
        public long m_timeMilliseconds;     //走行タイムをミリ秒で保存した値。小数誤差を避けるためランキング比較はこの値を使う
        public string m_recordedAtUtc;      //その日のランキング判定に使う記録日時。表示には出さないが日付フィルタに必要

        public double TimeSeconds
        {
            get
            {
                return m_timeMilliseconds / SecondsPerMillisecond;
            }
        }

        public DateTime RecordedAtUtc
        {
            get
            {
                DateTime value;             //変換後の日時

                //CSVに保存された文字列をDateTimeへ変換する。
                //失敗した場合は並べ替えで扱いやすいように最小日時を返す。
                if (!DateTime.TryParse(m_recordedAtUtc, null, DateTimeStyles.RoundtripKind, out value))
                {
                    return DateTime.MinValue;
                }

                return value.ToUniversalTime();
            }
        }
    }
}
