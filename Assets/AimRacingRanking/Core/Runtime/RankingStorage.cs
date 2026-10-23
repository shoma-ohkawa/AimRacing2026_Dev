/*━━━━━━━━━
@file RankingStorage.cs
@brief ランキングCSVの保存と読み込み
@author 24cu0312 久場洸太
@date 2026/07/02
最終更新日 2026/07/02
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace CrossProjectRanking
{
    /// <summary>
    /// ランキングCSVを実際に読み書きするクラスです。
    /// CSVの保存場所、CSV形式への変換、CSV形式からの復元を担当します。
    /// </summary>
    public static class RankingStorage
    {
        private const string FolderName = "RankingData";       //CSVを入れるフォルダ名。保存先未指定時はプロジェクト直下にこの名前で作成する
        private const string FileName = "race-times.csv";      //ランキングCSVのファイル名。入力側と表示側はこの同じファイルを読み書きする
        private const string ProjectRootPath = "..";           //Application.dataPathはAssetsを指すため、プロジェクト直下へ戻る相対パス
        private const string CsvSeparator = ",";               //CSV列の区切り文字。ToCsvLineで各列を結合する時に使う
        private const string CsvQuote = "\"";                  //CSV文字列を囲むダブルクォート。名前にカンマがあっても列が壊れないようにする
        private const string CsvEscapedQuote = "\"\"";         //文字列内のダブルクォートをCSV形式で表すためのエスケープ文字
        private const string Header = "ID,PlayerName,TimeMilliseconds,TimeSeconds,RecordedAtJst"; //新規CSVに書く列名。記録日時は日本標準時（UTC+9）
        private const int HeaderLineCount = 1;                 //CSV先頭のヘッダ行数。Load時に記録データの開始行を決める
        private const int IdFormatColumnCount = 5;             //ID付き新形式CSVを1件の記録として読み込むために必要な列数
        private const int NewFormatMinimumColumnCount = 4;     //新形式CSVを1件の記録として読み込むために必要な列数
        private const int OldFormatMinimumColumnCount = 7;     //旧形式CSVを1件の記録として読み込むために必要な列数
        private const int IdFormatIdColumn = 0;                //ID付き新形式CSVのID列。同じ内容の記録を別データとして識別するために使う
        private const int IdFormatPlayerNameColumn = 1;        //ID付き新形式CSVのプレイヤー名列。ランキング表示名として使う
        private const int IdFormatTimeMillisecondsColumn = 2;  //ID付き新形式CSVのミリ秒タイム列。最速順ソートに使う
        private const int IdFormatRecordedAtUtcColumn = 4;     //ID付き新形式CSVの記録日時列。その日のランキング判定に使う
        private const int NewPlayerNameColumn = 0;             //IDなし新形式CSVのプレイヤー名列。互換読み込み用
        private const int NewTimeMillisecondsColumn = 1;       //IDなし新形式CSVのミリ秒タイム列。互換読み込み用
        private const int NewRecordedAtUtcColumn = 3;          //IDなし新形式CSVの記録日時列。互換読み込み用
        private const int OldIdColumn = 0;                     //旧形式CSVのID列。互換読み込み用
        private const int OldPlayerNameColumn = 1;             //旧形式CSVのプレイヤー名列。互換読み込み用
        private const int OldTimeMillisecondsColumn = 3;       //旧形式CSVのミリ秒タイム列。互換読み込み用
        private const int OldRecordedAtUtcColumn = 5;          //旧形式CSVの記録日時列。互換読み込み用

        private static string m_sharedDirectoryOverride;       //保存先・読込元を固定したい時に使う共有フォルダパス。nullなら自動パスを使う

        public static string SharedFilePath
        {
            get
            {
                //共有フォルダが指定されている場合は、入力側・表示側で同じCSVを参照する。
                if (!string.IsNullOrWhiteSpace(m_sharedDirectoryOverride))
                {
                    return Path.Combine(m_sharedDirectoryOverride, FileName);
                }

                //共有フォルダが未指定の場合は、現在のUnityプロジェクト直下にRankingDataを作る。
                return Path.Combine(ProjectOrBuildRoot, FolderName, FileName);
            }
        }

        private static string ProjectOrBuildRoot
        {
            get
            {
                //Unity Editorではプロジェクト直下、ビルド版では実行ファイル付近を基準フォルダとして扱う。
                return Path.GetFullPath(Path.Combine(Application.dataPath, ProjectRootPath));
            }
        }

        public static void SetSharedDirectory(string _directorypath)
        {
            //空欄の場合は上書き設定を解除する。
            //絶対パスの場合はそのまま使い、相対パスの場合はプロジェクト直下からの相対パスとして扱う。
            //誤って race-times.csv まで含めた場合でも、親フォルダを保存先として扱う。
            m_sharedDirectoryOverride = string.IsNullOrWhiteSpace(_directorypath)
                ? null
                : ResolveDirectoryPath(_directorypath.Trim());
        }

        private static string ResolveDirectoryPath(string _directorypath)
        {
            string path = _directorypath;                       //Inspectorや関数から渡された保存先文字列

            if (string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase))
            {
                //Shared Directory Overrideは本来フォルダを指定する項目。
                //ただし実行環境準備中に ../RankingData/race-times.csv のようにファイル名まで入れても動くよう、
                //ファイル名を取り除いて親フォルダだけを使う。
                path = Path.GetDirectoryName(path);
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return ProjectOrBuildRoot;
            }

            //C:\Data のような絶対パスは、その場所をそのまま共有フォルダにする。
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            //RankingData のような相対パスは、プロジェクト直下を基準に絶対パスへ変換する。
            return Path.GetFullPath(Path.Combine(ProjectOrBuildRoot, path));
        }

        public static RankingRecord[] Load()
        {
            //CSVがまだ作られていない場合、ランキングは空として扱う。
            if (!File.Exists(SharedFilePath)) { return Array.Empty<RankingRecord>(); }

            try
            {
                List<RankingRecord> records = new List<RankingRecord>();       //読み込んだ記録
                string[] lines = File.ReadAllLines(SharedFilePath, Encoding.UTF8);  //CSV全行

                //0行目はヘッダなので、1行目から記録として読み込む。
                for (int i = HeaderLineCount; i < lines.Length; ++i)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        continue;
                    }

                    List<string> columns = ParseCsvLine(lines[i]);             //CSV列
                    RankingRecord record;                                      //CSVから復元した記録

                    //新形式または旧形式として読み込めない行は壊れた行として飛ばす。
                    if (!TryCreateRecord(columns, out record))
                    {
                        AppLog.LogWarning($"CSVの{i + HeaderLineCount}行目を読み飛ばしました。");
                        continue;
                    }

                    records.Add(record);
                }

                return records.ToArray();
            }
            catch (Exception _exception)
            {
                //ファイルが他アプリでロックされている場合などはここに入る。
                //ゲームを止めないため、エラーを出して空配列を返す。
                AppLog.LogError($"ランキングデータの読み込みに失敗しました: {_exception.Message}");
                return Array.Empty<RankingRecord>();
            }
        }

        public static bool Append(RankingRecord _record)
        {
            try
            {
                string directory = Path.GetDirectoryName(SharedFilePath);       //CSV保存フォルダ

                //RankingDataフォルダが存在しない場合は初回保存時に作成する。
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                bool m_bIsNewFile = !File.Exists(SharedFilePath) || new FileInfo(SharedFilePath).Length == 0;  //新規CSVか

                //CSVは追記モードで開く。
                //既存記録を消さず、1レースごとに1行ずつ追加する。
                using (StreamWriter writer = new StreamWriter(SharedFilePath, true, new UTF8Encoding(true)))
                {
                    if (m_bIsNewFile)
                    {
                        writer.WriteLine(Header);
                    }

                    writer.WriteLine(ToCsvLine(_record));
                }

                return true;
            }
            catch (Exception _exception)
            {
                //保存失敗時もゲームを落とさず、falseを返して呼び出し元に失敗を知らせる。
                AppLog.LogError($"ランキングデータの保存に失敗しました: {_exception.Message}");
                return false;
            }
        }

        private static string ToCsvLine(RankingRecord _record)
        {
            //RankingRecordの値をCSV1行分の文字列へ変換する。
            //文字列列はカンマやダブルクォートを含んでも壊れないようにEscapeする。
            return string.Join(CsvSeparator, new[]
            {
                Escape(_record.m_id),
                Escape(_record.m_playerName),
                _record.m_timeMilliseconds.ToString(CultureInfo.InvariantCulture),
                _record.TimeSeconds.ToString("0.000", CultureInfo.InvariantCulture),
                Escape(_record.m_recordedAtUtc)
            });
        }

        private static bool TryCreateRecord(List<string> _columns, out RankingRecord _record)
        {
            //CSVの列数から新形式か旧形式かを判断し、RankingRecordへ変換する。
            //旧形式は過去に出力していたID・CourseName・SourceProject付きCSVを読むために残している。
            //IDは重複排除ではなく、同じ内容の記録を別データとして見分けるために保持する。
            _record = null;

            if (_columns.Count >= OldFormatMinimumColumnCount)
            {
                return TryCreateOldFormatRecord(_columns, out _record);
            }

            if (_columns.Count >= IdFormatColumnCount)
            {
                return TryCreateIdFormatRecord(_columns, out _record);
            }

            if (_columns.Count >= NewFormatMinimumColumnCount)
            {
                return TryCreateNewFormatRecord(_columns, out _record);
            }

            return false;
        }

        private static bool TryCreateIdFormatRecord(List<string> _columns, out RankingRecord _record)
        {
            long milliseconds;                                 //ID付き新形式CSVから読み取った走行タイム
            _record = null;

            if (!long.TryParse(_columns[IdFormatTimeMillisecondsColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }

            _record = new RankingRecord
            {
                m_id = _columns[IdFormatIdColumn],
                m_playerName = _columns[IdFormatPlayerNameColumn],
                m_timeMilliseconds = milliseconds,
                m_recordedAtUtc = _columns[IdFormatRecordedAtUtcColumn]
            };

            return true;
        }

        private static bool TryCreateNewFormatRecord(List<string> _columns, out RankingRecord _record)
        {
            long milliseconds;                                 //新形式CSVから読み取った走行タイム
            _record = null;

            if (!long.TryParse(_columns[NewTimeMillisecondsColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }

            _record = new RankingRecord
            {
                m_id = Guid.NewGuid().ToString("N"),
                m_playerName = _columns[NewPlayerNameColumn],
                m_timeMilliseconds = milliseconds,
                m_recordedAtUtc = _columns[NewRecordedAtUtcColumn]
            };

            return true;
        }

        private static bool TryCreateOldFormatRecord(List<string> _columns, out RankingRecord _record)
        {
            long milliseconds;                                 //旧形式CSVから読み取った走行タイム
            _record = null;

            if (!long.TryParse(_columns[OldTimeMillisecondsColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds))
            {
                return false;
            }

            _record = new RankingRecord
            {
                m_id = _columns[OldIdColumn],
                m_playerName = _columns[OldPlayerNameColumn],
                m_timeMilliseconds = milliseconds,
                m_recordedAtUtc = _columns[OldRecordedAtUtcColumn]
            };

            return true;
        }

        private static string Escape(string _value)
        {
            //CSVでは文字列をダブルクォートで囲む。
            //文字列内のダブルクォートは2つ重ねる必要がある。
            string value = _value ?? string.Empty;     //CSV用文字列
            return CsvQuote + value.Replace(CsvQuote, CsvEscapedQuote) + CsvQuote;
        }

        private static List<string> ParseCsvLine(string _line)
        {
            //CSV1行を列ごとの文字列に分解する。
            //単純なSplit(',')では、名前にカンマが入った時に壊れるため手動で解析する。
            List<string> values = new List<string>();  //CSV列一覧
            StringBuilder value = new StringBuilder(); //現在解析中の列
            bool m_bQuoted = false;                    //ダブルクォート内か

            for (int i = 0; i < _line.Length; ++i)
            {
                char current = _line[i];               //現在の文字

                if (current == '"')
                {
                    //ダブルクォート内で "" が出た場合は、文字としての " を意味する。
                    if (m_bQuoted && i + HeaderLineCount < _line.Length && _line[i + HeaderLineCount] == '"')
                    {
                        value.Append('"');
                        ++i;
                    }
                    else
                    {
                        m_bQuoted = !m_bQuoted;
                    }
                }
                else if (current == ',' && !m_bQuoted)
                {
                    //ダブルクォート外のカンマだけを列区切りとして扱う。
                    values.Add(value.ToString());
                    value.Length = 0;
                }
                else
                {
                    value.Append(current);
                }
            }

            values.Add(value.ToString());
            return values;
        }
    }
}
