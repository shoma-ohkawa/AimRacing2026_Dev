/*━━━━━━━━━
@file RankingDateSorter.cs
@brief ランキング記録の並べ替え
@author 24cu0312 久場洸太
@date 2026/07/02
最終更新日 2026/07/02
━━━━━━━━━*/

using System;

namespace CrossProjectRanking
{
    /// <summary>
    /// 読み込んだランキング記録を並び替えるためのクラスです。
    /// 元配列を直接変更しないよう、コピーを作ってから並び替えます。
    /// </summary>
    public static class RankingDateSorter
    {
        public static RankingRecord[] NewestFirst(RankingRecord[] _source)
        {
            //記録日時が新しいものから順に並べる。
            //日付順で確認したい時に使用する。
            RankingRecord[] result = CloneRecords(_source);    //並べ替え用コピー
            Array.Sort(result, (_left, _right) => _right.RecordedAtUtc.CompareTo(_left.RecordedAtUtc));
            return result;
        }

        public static RankingRecord[] OldestFirst(RankingRecord[] _source)
        {
            //NewestFirstで新しい順にした後、反転して古い順にする。
            RankingRecord[] result = NewestFirst(_source);     //並べ替え用コピー
            Array.Reverse(result);
            return result;
        }

        public static RankingRecord[] FastestFirst(RankingRecord[] _source)
        {
            //走行タイムが短いものから順に並べる。
            //レースランキング表示で一番よく使う並び順。
            RankingRecord[] result = CloneRecords(_source);    //並べ替え用コピー
            Array.Sort(result, (_left, _right) => _left.m_timeMilliseconds.CompareTo(_right.m_timeMilliseconds));
            return result;
        }

        private static RankingRecord[] CloneRecords(RankingRecord[] _source)
        {
            //読み込み結果がnullの場合でも、呼び出し元が安全に扱えるよう空配列を返す。
            if (_source == null) { return Array.Empty<RankingRecord>(); }

            //元データを直接並び替えると他の処理に影響する可能性があるためコピーする。
            return (RankingRecord[])_source.Clone();
        }
    }
}
