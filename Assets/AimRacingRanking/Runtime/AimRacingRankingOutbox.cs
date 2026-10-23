/*━━━━━━━━━
@file AimRacingRankingOutbox.cs
@brief 未送信ランキングとLAN同期状態をCSVへ永続化
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/08/06
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CrossProjectRanking;
using UnityEngine;

namespace AimRacing.Ranking
{
    /// <summary>入力側CSVに記録する1件ごとの送信状態です。</summary>
    public enum AimRacingOutboxStatus
    {
        // まだACKを受け取っていないため送信対象です。
        Pending,
        // 出力側で保存済みのACKを受け取りました。
        Acknowledged,
        // 2回失敗し、操作者のF12/F11判断を待っています。
        ManualRequired,
        // F12によりLAN送信を止め、入力側CSVだけで保持しています。
        OfflineLocal
    }

    /// <summary>
    /// LAN同期状態をCSVで保持する送信コンテナです。ゲーム終了後も未送信を復元できます。
    /// </summary>
    public sealed class AimRacingRankingOutbox
    {
        // 列順はLoadとSaveで共通です。変更時は両方を同時に更新してください。
        private const string k_Header =
            "MessageId,PlayerName,TimeMilliseconds,RecordedAtJst,SourceProject,Status,Attempts,LastError";
        private const string k_FileName = "ranking-outbox.csv"; //未送信ランキングを保持するCSV名
        private const int k_ManualRequiredAttemptCount = 2; //手動判断へ移行する送信失敗回数
        private const int k_MinimumColumnCount = 8; //保存済みデータとして必要なCSV列数
        private const int k_FirstDataLineIndex = 1; //ヘッダー直後にある最初のデータ行
        private const int k_MessageIdColumn = 0; //送信データを識別するID列
        private const int k_PlayerNameColumn = 1; //ランキングへ表示する名前列
        private const int k_TimeMillisecondsColumn = 2; //順位判定に使用するタイム列
        private const int k_RecordedAtColumn = 3; //レース結果を記録した日時列
        private const int k_SourceProjectColumn = 4; //送信元ゲームを識別する列
        private const int k_StatusColumn = 5; //現在の送信状態を保持する列
        private const int k_AttemptsColumn = 6; //送信試行回数を保持する列
        private const int k_LastErrorColumn = 7; //直近の送信失敗理由を保持する列
        private readonly List<Row> m_rows = new List<Row>();

        public string FilePath { get; private set; }

        /// <summary>保存先を確保し、前回終了時の送信待ちデータを復元します。</summary>
        public AimRacingRankingOutbox(string _directory)
        {
            Directory.CreateDirectory(_directory);
            FilePath = Path.Combine(_directory, k_FileName);
            Load();
        }

        /// <summary>MessageIdをキーに追加または更新し、直ちにCSVへ確定します。</summary>
        public void Upsert(
            RankingUdpPacket _packet,
            AimRacingOutboxStatus _status,
            int _attempts,
            string _error)
        {
            Row row = Find(_packet.messageId);
            if (row == null)
            {
                row = new Row();
                m_rows.Add(row);
            }
            row.Packet = _packet;
            row.Status = _status;
            row.Attempts = _attempts;
            row.LastError = _error ?? string.Empty;
            Save();
        }

        /// <summary>ACK・失敗・手動切替に応じて対象レコードの状態だけを更新します。</summary>
        public void SetStatus(
            string _messageid,
            AimRacingOutboxStatus _status,
            string _error)
        {
            Row row = Find(_messageid);
            if (row == null) { return; }

            row.Status = _status;
            if (_status == AimRacingOutboxStatus.ManualRequired)
            {
                row.Attempts = k_ManualRequiredAttemptCount;
            }
            row.LastError = _error ?? string.Empty;
            Save();
        }

        /// <summary>操作者がLAN切離しを選んだとき、判断待ちレコードをローカル保持へ移します。</summary>
        public void MarkManualRequiredAsOffline()
        {
            bool changed = false;
            for (int i = 0; i < m_rows.Count; ++i)
            {
                if (m_rows[i].Status == AimRacingOutboxStatus.ManualRequired)
                {
                    m_rows[i].Status = AimRacingOutboxStatus.OfflineLocal;
                    changed = true;
                }
            }
            if (changed)
            {
                Save();
            }
        }

        /// <summary>再接続時に送る最古の未送信データを1件返します。</summary>
        public RankingUdpPacket GetOldestUnsent()
        {
            for (int i = 0; i < m_rows.Count; ++i)
            {
                if (m_rows[i].Status == AimRacingOutboxStatus.Pending ||
                    m_rows[i].Status == AimRacingOutboxStatus.OfflineLocal)
                {
                    return m_rows[i].Packet;
                }
            }
            return null;
        }

        /// <summary>送信結果を同じ記録へ反映するため、MessageIdが一致する行を探します。</summary>
        private Row Find(string _messageid)
        {
            return m_rows.Find(row => row.Packet != null &&
                string.Equals(row.Packet.messageId, _messageid, StringComparison.Ordinal));
        }

        /// <summary>CSVを復元します。不正な行は他の正常データを守るため行単位で読み飛ばします。</summary>
        private void Load()
        {
            if (!File.Exists(FilePath))
            {
                Save();
                return;
            }

            string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
            for (int i = k_FirstDataLineIndex; i < lines.Length; ++i)
            {
                List<string> columns = ParseCsv(lines[i]);
                long milliseconds;
                int attempts;
                AimRacingOutboxStatus status;
                if (columns.Count < k_MinimumColumnCount ||
                    !long.TryParse(columns[k_TimeMillisecondsColumn], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out milliseconds) ||
                    !Enum.TryParse(columns[k_StatusColumn], out status) ||
                    !int.TryParse(columns[k_AttemptsColumn], NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out attempts))
                {
                    continue;
                }
                m_rows.Add(new Row
                {
                    Packet = new RankingUdpPacket
                    {
                        messageId = columns[k_MessageIdColumn],
                        playerName = columns[k_PlayerNameColumn],
                        timeMilliseconds = milliseconds,
                        recordedAtJst = columns[k_RecordedAtColumn],
                        sourceProject = columns[k_SourceProjectColumn]
                    },
                    Status = status,
                    Attempts = attempts,
                    LastError = columns[k_LastErrorColumn]
                });
            }
        }

        /// <summary>
        /// 一時ファイルへ全件を書いてから置換し、書込み途中のCSVが残りにくいようにします。
        /// </summary>
        private void Save()
        {
            try
            {
                StringBuilder csv = new StringBuilder(k_Header);
                csv.AppendLine();
                for (int i = 0; i < m_rows.Count; ++i)
                {
                    Row row = m_rows[i];
                    csv.Append(Escape(row.Packet.messageId)).Append(',')
                        .Append(Escape(row.Packet.playerName)).Append(',')
                        .Append(row.Packet.timeMilliseconds.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(Escape(row.Packet.recordedAtJst)).Append(',')
                        .Append(Escape(row.Packet.sourceProject)).Append(',')
                        .Append(row.Status).Append(',')
                        .Append(row.Attempts.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(Escape(row.LastError)).AppendLine();
                }

                string temporaryPath = FilePath + ".tmp";
                File.WriteAllText(temporaryPath, csv.ToString(), new UTF8Encoding(true));
                if (File.Exists(FilePath))
                {
                    File.Delete(FilePath);
                }
                File.Move(temporaryPath, FilePath);
            }
            catch (Exception exception)
            {
                AppLog.LogError("ランキング送信コンテナCSVの保存に失敗しました: " + exception.Message);
            }
        }

        /// <summary>カンマ、改行、引用符を含む名前やエラー文字列をCSV形式へ変換します。</summary>
        private static string Escape(string _value)
        {
            string value = _value ?? string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>二重引用符で囲まれたカンマとエスケープ済み引用符を考慮して1行を分解します。</summary>
        private static List<string> ParseCsv(string _line)
        {
            List<string> columns = new List<string>();
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < _line.Length; ++i)
            {
                char character = _line[i];
                if (character == '"')
                {
                    if (quoted && i + 1 < _line.Length && _line[i + 1] == '"')
                    {
                        current.Append('"');
                        ++i;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    columns.Add(current.ToString());
                    current.Length = 0;
                }
                else
                {
                    current.Append(character);
                }
            }
            columns.Add(current.ToString());
            return columns;
        }

        /// <summary>UDP送信データと運用状態をひとまとめにした内部保存単位です。</summary>
        private sealed class Row
        {
            public RankingUdpPacket Packet;
            public AimRacingOutboxStatus Status;
            public int Attempts;
            public string LastError;
        }
    }
}
