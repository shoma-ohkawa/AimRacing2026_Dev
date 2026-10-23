/*━━━━━━━━━
@file    ProjectZipWhitelist.cs
@brief   ホワイトリスト方式でUnityプロジェクトをZIP化する自己完結型のEditorツール(他のスクリプトやアセットに依存せず、どのUnityプロジェクトにも入れられる)
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/11
         最終更新日 : 2026/10/22
━━━━━━━━━*/

//==========================
//ビルドエラーを起こさないための設計(このファイルを他のプロジェクトへコピーするときの前提)
//1. ファイル全体を #if UNITY_EDITOR で囲む(Editorフォルダ以外へ置いても、プレイヤービルドに影響しない)
//2. 専用の名前空間に閉じ込め、型はすべて internal にする(他のプロジェクトの型名と衝突しない / XMLドキュメント警告も出ない)
//3. using はすべて名前空間の内側に書く(プロジェクト側にグローバルな Debug / Path / File などがあっても、ここで宣言した方が優先される)
//4. 依存するのは UnityEngine / UnityEditor の基本APIと .NET の基本クラスだけ。FMOD・TextMeshPro・他のスクリプトは一切参照しない
//5. ZIPの書き出しは自前で実装する。ZipArchive / ZipFile / CreateEntryFromFile は、API互換レベルによって参照できないことがあるため使わない。圧縮は DeflateStream だけを使用する
//6. C# の新しい構文(文字列補間・?.・out var・パターンマッチ・nameof など)を使わない(古いUnityのコンパイラでも通る)
//7. 警告も出さない(-warnaserror を指定しているプロジェクトでもエラーにならない)
//==========================

#if UNITY_EDITOR
namespace ProjectZipWhitelistTool
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using StopwatchTimer = global::System.Diagnostics.Stopwatch;
    using UnityApplication = global::UnityEngine.Application;
    using UnityAssetDatabase = global::UnityEditor.AssetDatabase;
    using UnityDebug = global::UnityEngine.Debug;
    using UnityEditorApplication = global::UnityEditor.EditorApplication;
    using UnityEditorPrefs = global::UnityEditor.EditorPrefs;
    using UnityEditorUtility = global::UnityEditor.EditorUtility;
    using UnityMenuItem = global::UnityEditor.MenuItem;

    //プロジェクトルート直下のホワイトリストに載せた項目だけをZIPへ書き出す、プロジェクト非依存のEditorツール
    internal static class WhitelistZipExporter
    {
        //==========================
        //設定(ZIPに含める項目を変えるときは、この2つだけを編集する)
        //==========================

        //プロジェクトルート直下で、ZIPに含めるフォルダの名前(名前の完全一致。大文字小文字は区別しない)
        //  Assets / FMOD_2024 / Packages / ProjectSettings : ゲーム本体とその設定
        //  docs : 設計・使い方の文書(計測の Perf.md を含む)
        //  Tools : Assets の外に置いた補助ツール(計測のグラフ表示・集計・無人実行の PerfViewer など)
        //含めないもの: Library / Temp / Logs / UserSettings / *.csproj / *.slnx は、Unityが作り直せる、またはこの PC だけの情報
        private static readonly string[] k_WhitelistDirectoryNames =
        {
            "Assets",
            "docs",
            "FMOD_2024",
            "Packages",
            "ProjectSettings",
            "Tools",
        };

        //プロジェクトルート直下で、ZIPに含めるファイルの名前
        //拡張子を除いた名前、または拡張子を含む完全な名前のどちらかに一致すれば含める(大文字小文字は区別しない)
        //  PublicKey : PublicKey.txt や PublicKey.pem が対象
        //  CLAUDE : プロジェクトの指示(CLAUDE.md)
        //  .vsconfig : Visual Studio に必要な構成(拡張子を除くと名前が空になるため、完全な名前で指定する)
        private static readonly string[] k_WhitelistFileNames =
        {
            "PublicKey",
            "CLAUDE",
            ".vsconfig",
        };

        //すでに圧縮されているため、再圧縮せずにそのまま格納する拡張子
        private static readonly string[] k_StoredExtensions =
        {
            ".png", ".jpg", ".jpeg", ".gif", ".zip", ".7z", ".rar", ".gz", ".mp3", ".ogg", ".mp4", ".mov", ".webm",
        };

        //==========================
        //メニュー・ダイアログ・保存先
        //==========================
        private const string k_MenuPath = "Tools/Project Zip (Whitelist)";
        private const string k_DialogTitle = "プロジェクトZIP(ホワイトリスト)";
        private const string k_ProgressTitle = "プロジェクトZIPを作成中";
        private const string k_DialogOk = "OK";
        private const string k_DialogCreate = "作成";
        private const string k_DialogCancel = "キャンセル";
        private const string k_ZipExtension = ".zip";
        private const string k_ZipExtensionWithoutDot = "zip";
        private const string k_TempExtension = ".tmp";
        private const string k_FileNameSeparator = "_";
        private const string k_TimestampFormat = "yyyyMMdd_HHmmss";
        private const string k_PrefsLastDirectory = "ProjectZipWhitelistTool.LastDirectory";
        private const string k_EntrySeparator = "/";
        private const int k_MaxListedNames = 15; //確認ダイアログに並べる項目名の最大数
        private const int k_MaxDirectoryDepth = 64; //フォルダをたどる深さの上限(リンクの循環を検出するため)
        private const long k_ProgressIntervalMs = 100; //進捗バーを更新する最短の間隔(ミリ秒)
        private const double k_BytesPerMegabyte = 1048576.0;
        private const double k_MillisecondsPerSecond = 1000.0;

        //==========================
        //ZIP形式の定数(PKWARE APPNOTE 準拠)
        //==========================
        private const uint k_SignatureLocalHeader = 0x04034B50u;
        private const uint k_SignatureCentralHeader = 0x02014B50u;
        private const uint k_SignatureEndOfCentral = 0x06054B50u;
        private const uint k_SignatureZip64End = 0x06064B50u;
        private const uint k_SignatureZip64Locator = 0x07064B50u;
        private const uint k_VersionDefault = 20u; //Deflateと格納に必要なバージョン
        private const uint k_VersionZip64 = 45u; //ZIP64に必要なバージョン
        private const uint k_FlagUtf8 = 0x0800u; //ファイル名がUTF-8であることを示すフラグ(日本語名の保持に必要)
        private const uint k_MethodStored = 0u;
        private const uint k_MethodDeflate = 8u;
        private const uint k_ExternalAttrFile = 0x20u;
        private const uint k_ExternalAttrDirectory = 0x10u;
        private const uint k_Zip64ExtraId = 0x0001u;
        private const uint k_ExtraHeaderSize = 4u; //拡張フィールドの先頭(ID 2バイト + サイズ 2バイト)
        private const uint k_Zip64SizesDataSize = 16u; //圧縮前と圧縮後のサイズ(各8バイト)
        private const uint k_Zip64OffsetDataSize = 8u; //ローカルヘッダの位置(8バイト)
        private const uint k_Marker32 = 0xFFFFFFFFu; //「ZIP64拡張フィールドを見よ」を意味する32bitの値
        private const uint k_Marker16 = 0xFFFFu; //同じく16bitの値
        private const uint k_TotalDiskCount = 1u;
        private const ulong k_Zip64EndBodySize = 44ul; //ZIP64終端レコードの、サイズ欄より後ろの長さ
        private const ulong k_Zip64EntryThreshold = 0xF0000000ul; //これ以上のファイルは、ローカルヘッダもZIP64形式で書く(4GB未満の余裕を持たせる)
        private const ulong k_Zip64EntryCountThreshold = 0xFFFFul; //項目数がこれ以上なら、終端レコードをZIP64形式で書く
        private const ulong k_Zip64OffsetThreshold = 0xFFFFFFFFul; //位置や中央ディレクトリのサイズがこれ以上なら、ZIP64形式で書く
        private const int k_LocalHeaderFixedSize = 30;
        private const int k_LocalCrcOffset = 14; //ローカルヘッダの先頭からCRC欄までの位置
        private const uint k_Zip64LocalExtraDataSize = 16u;
        private const uint k_Zip64LocalExtraTotalSize = 20u;
        private const int k_MaxNameBytes = 65535;

        //==========================
        //CRC32・DOS日時・バッファ
        //==========================
        private const uint k_CrcPolynomial = 0xEDB88320u;
        private const uint k_CrcInitial = 0xFFFFFFFFu;
        private const uint k_CrcFinalXor = 0xFFFFFFFFu;
        private const int k_CrcTableSize = 256;
        private const uint k_ByteMaskUInt = 0xFFu;
        private const ulong k_ByteMaskULong = 0xFFul;
        private const int k_BitsPerByte = 8;
        private const int k_Bytes16 = 2;
        private const int k_Bytes32 = 4;
        private const int k_Bytes64 = 8;
        private const int k_ScratchSize = 8;
        private const int k_BufferSize = 81920;
        private const int k_DosMinYear = 1980;
        private const int k_DosMaxYear = 2107;
        private const int k_DosLastMonth = 12;
        private const int k_DosLastDay = 31;
        private const int k_DosLastHour = 23;
        private const int k_DosLastMinute = 59;
        private const int k_DosLastSecond = 58; //DOS時刻は2秒単位
        private const int k_DosYearShift = 9;
        private const int k_DosMonthShift = 5;
        private const int k_DosHourShift = 11;
        private const int k_DosMinuteShift = 5;
        private const uint k_DosSecondDivisor = 2u;

        private static readonly UTF8Encoding k_Utf8NoBom = new UTF8Encoding(false);
        private static readonly uint[] k_CrcTable = CreateCrcTable();

        //==========================
        //メニュー(UI)
        //==========================

        //メニューから呼ばれ、確認 → 保存先の選択 → ZIP作成 → 結果表示までを行う関数
        [UnityMenuItem(k_MenuPath)]
        private static void ExportFromMenu()
        {
            //再生中やコンパイル中は、ファイルが書き換わる可能性があるため実行しない
            if (UnityEditorApplication.isPlayingOrWillChangePlaymode || UnityEditorApplication.isCompiling)
            {
                UnityEditorUtility.DisplayDialog(k_DialogTitle, "再生中またはコンパイル中は実行できません。終了してからもう一度実行してください。", k_DialogOk);
                return;
            }

            string projectRoot = FindProjectRoot();
            if (projectRoot == null)
            {
                UnityEditorUtility.DisplayDialog(k_DialogTitle, "プロジェクトのフォルダを特定できませんでした。", k_DialogOk);
                return;
            }

            string projectName = new DirectoryInfo(projectRoot).Name;
            List<ZipSource> included = new List<ZipSource>();
            List<string> excluded = new List<string>();
            ClassifyRootEntries(projectRoot, included, excluded);
            if (included.Count == 0)
            {
                UnityEditorUtility.DisplayDialog(k_DialogTitle, "ホワイトリストに該当する項目がプロジェクトに見つかりませんでした。", k_DialogOk);
                return;
            }

            bool confirmed = UnityEditorUtility.DisplayDialog(
                k_DialogTitle,
                BuildConfirmMessage(projectName, included, excluded),
                k_DialogCreate,
                k_DialogCancel);
            if (!confirmed) { return; }

            string zipPath = AskOutputPath(projectRoot, projectName);
            if (string.IsNullOrEmpty(zipPath)) { return; }

            ProgressReporter reporter = new ProgressReporter();
            try
            {
                //未保存のアセットをディスクへ書き出してから、ファイルを読み取る
                UnityAssetDatabase.SaveAssets();
                ExportResult result = CreateZip(projectRoot, zipPath, reporter);
                UnityEditorUtility.ClearProgressBar();
                UnityDebug.Log("[ProjectZipWhitelist] 完了: " + zipPath);
                UnityEditorUtility.DisplayDialog(k_DialogTitle, BuildResultMessage(zipPath, result), k_DialogOk);
                UnityEditorUtility.RevealInFinder(zipPath);
            }
            catch (ZipCanceledException)
            {
                UnityEditorUtility.ClearProgressBar();
                UnityEditorUtility.DisplayDialog(k_DialogTitle, "キャンセルしました。ZIPは作成されていません。", k_DialogOk);
            }
            catch (Exception exception)
            {
                UnityEditorUtility.ClearProgressBar();
                UnityDebug.LogError("[ProjectZipWhitelist] 失敗: " + exception);
                UnityEditorUtility.DisplayDialog(k_DialogTitle, "ZIPの作成に失敗しました。ZIPは作成されていません。\n\n" + exception.Message, k_DialogOk);
            }
            finally
            {
                //どの経路で終わっても進捗バーが残らないようにする
                UnityEditorUtility.ClearProgressBar();
            }
        }

        //Application.dataPath(…/Assets)の一つ上、つまりプロジェクトルートを返す関数。特定できなければnull
        private static string FindProjectRoot()
        {
            DirectoryInfo assets = new DirectoryInfo(UnityApplication.dataPath);
            DirectoryInfo parent = assets.Parent;
            if (parent == null) { return null; }
            return parent.FullName;
        }

        /// <summary>
        /// ZIPの保存先をユーザーに選ばせる。キャンセルされたら空文字を返す。
        /// </summary>
        /// <param name="_projectRoot">プロジェクトルート</param>
        /// <param name="_projectName">プロジェクト名(ファイル名の先頭に使う)</param>
        private static string AskOutputPath(string _projectRoot, string _projectName)
        {
            string defaultDirectory = UnityEditorPrefs.GetString(k_PrefsLastDirectory, string.Empty);
            if (string.IsNullOrEmpty(defaultDirectory) || !Directory.Exists(defaultDirectory))
            {
                //プロジェクトの一つ上のフォルダ(プロジェクトの外)を初期位置にする
                DirectoryInfo parent = new DirectoryInfo(_projectRoot).Parent;
                defaultDirectory = (parent != null) ? parent.FullName : _projectRoot;
            }

            string defaultName = _projectName + k_FileNameSeparator + DateTime.Now.ToString(k_TimestampFormat, CultureInfo.InvariantCulture) + k_ZipExtension;
            string selected = UnityEditorUtility.SaveFilePanel(k_DialogTitle, defaultDirectory, defaultName, k_ZipExtensionWithoutDot);
            if (string.IsNullOrEmpty(selected)) { return string.Empty; }

            if (!selected.EndsWith(k_ZipExtension, StringComparison.OrdinalIgnoreCase)) { selected += k_ZipExtension; }

            string selectedDirectory = Path.GetDirectoryName(selected);
            if (!string.IsNullOrEmpty(selectedDirectory)) { UnityEditorPrefs.SetString(k_PrefsLastDirectory, selectedDirectory); }
            return selected;
        }

        /// <summary>
        /// 実行前に表示する確認メッセージを作る。
        /// </summary>
        /// <param name="_projectName">プロジェクト名</param>
        /// <param name="_included">ZIPに含める項目</param>
        /// <param name="_excluded">ZIPに含めない項目の名前</param>
        private static string BuildConfirmMessage(string _projectName, List<ZipSource> _included, List<string> _excluded)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("プロジェクト: ").Append(_projectName).Append("\n\n");

            builder.Append("【ZIPに含める項目】\n");
            for (int i = 0; i < _included.Count; ++i)
            {
                builder.Append("  ").Append(_included[i].m_name).Append(_included[i].m_bIsDirectory ? "/" : string.Empty).Append("\n");
            }

            //ホワイトリストに載っているのに、このプロジェクトには無いフォルダ
            List<string> missing = new List<string>();
            for (int i = 0; i < k_WhitelistDirectoryNames.Length; ++i)
            {
                if (!ContainsName(_included, k_WhitelistDirectoryNames[i])) { missing.Add(k_WhitelistDirectoryNames[i]); }
            }
            if (missing.Count > 0)
            {
                builder.Append("\n【このプロジェクトには無いため、含まれない項目】\n  ").Append(string.Join(", ", missing.ToArray())).Append("\n");
            }

            builder.Append("\n【含めない項目(ホワイトリスト外)】\n  ");
            builder.Append(JoinLimited(_excluded, k_MaxListedNames)).Append("\n");

            builder.Append("\n※ 元のプロジェクトのファイルは、変更も削除もしません。");
            builder.Append("\n※ 未保存のシーンは反映されません。先に保存してください。");
            builder.Append("\n\n次に保存先を選びます。続行しますか?");
            return builder.ToString();
        }

        //作成が終わった時に表示する結果メッセージを作る関数
        private static string BuildResultMessage(string _zipPath, ExportResult _result)
        {
            double zipMegabytes = _result.m_zipBytes / k_BytesPerMegabyte;
            double sourceMegabytes = _result.m_sourceBytes / k_BytesPerMegabyte;
            double seconds = _result.m_elapsedMilliseconds / k_MillisecondsPerSecond;

            StringBuilder builder = new StringBuilder();
            builder.Append("ZIPを作成しました。\n\n");
            builder.Append(_zipPath).Append("\n\n");
            builder.Append("ファイル数: ").Append(_result.m_fileCount.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append("空フォルダ: ").Append(_result.m_emptyDirectoryCount.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append("元のサイズ: ").Append(sourceMegabytes.ToString("F1", CultureInfo.InvariantCulture)).Append(" MB\n");
            builder.Append("ZIPのサイズ: ").Append(zipMegabytes.ToString("F1", CultureInfo.InvariantCulture)).Append(" MB\n");
            builder.Append("所要時間: ").Append(seconds.ToString("F1", CultureInfo.InvariantCulture)).Append(" 秒");
            return builder.ToString();
        }

        //名前を先頭からk_MaxListedNames個まで「, 」で連結し、超えた分は件数にして省略する関数
        private static string JoinLimited(List<string> _names, int _limit)
        {
            if (_names.Count == 0) { return "(なし)"; }

            List<string> shown = new List<string>();
            for (int i = 0; i < _names.Count && i < _limit; ++i) { shown.Add(_names[i]); }

            string joined = string.Join(", ", shown.ToArray());
            if (_names.Count > _limit) { joined += " ほか " + (_names.Count - _limit).ToString(CultureInfo.InvariantCulture) + " 件"; }
            return joined;
        }

        //指定した名前の項目が、含める項目の中にあるかを返す関数
        private static bool ContainsName(List<ZipSource> _sources, string _name)
        {
            for (int i = 0; i < _sources.Count; ++i)
            {
                if (string.Equals(_sources[i].m_name, _name, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        //==========================
        //ZIP作成(UIに依存しない。テストからも呼べる)
        //==========================

        /// <summary>
        /// ホワイトリストの項目だけを、1つのZIPへ書き出す。途中で失敗しても、保存先に壊れたZIPを残さない。
        /// </summary>
        /// <param name="_projectRoot">プロジェクトルート(Assetsの一つ上)</param>
        /// <param name="_zipPath">出力するZIPのパス(プロジェクトのホワイトリスト項目の中は指定できない)</param>
        /// <param name="_sink">進捗の通知先。通知が不要ならnull</param>
        internal static ExportResult CreateZip(string _projectRoot, string _zipPath, IProgressSink _sink)
        {
            StopwatchTimer timer = StopwatchTimer.StartNew();

            List<ZipSource> included = new List<ZipSource>();
            List<string> excluded = new List<string>();
            ClassifyRootEntries(_projectRoot, included, excluded);
            if (included.Count == 0) { throw new InvalidOperationException("ホワイトリストに該当する項目がありません。"); }

            ValidateOutputPath(_zipPath, included);

            //ZIPに入れる全ファイルを、決まった順序で集める
            string projectName = new DirectoryInfo(_projectRoot).Name;
            List<ZipSource> sources = new List<ZipSource>();
            for (int i = 0; i < included.Count; ++i)
            {
                string entryName = projectName + k_EntrySeparator + included[i].m_name;
                if (included[i].m_bIsDirectory) { CollectDirectory(included[i].m_fullPath, entryName, 0, sources); }
                else { sources.Add(ZipSource.CreateFile(included[i].m_name, entryName, included[i].m_fullPath)); }
            }

            ExportResult result = new ExportResult();
            for (int i = 0; i < sources.Count; ++i)
            {
                if (sources[i].m_bIsDirectory) { ++result.m_emptyDirectoryCount; }
                else { ++result.m_fileCount; result.m_sourceBytes += sources[i].m_length; }
            }
            if (_sink != null) { _sink.Begin(result.m_sourceBytes); }

            //先に一時ファイルへ書き、最後まで成功したときだけ本来の名前にする
            string tempPath = _zipPath + k_TempExtension;
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, k_BufferSize))
                {
                    ZipBuilder builder = new ZipBuilder(stream);
                    for (int i = 0; i < sources.Count; ++i)
                    {
                        if (sources[i].m_bIsDirectory) { builder.AddDirectory(sources[i].m_entryName, sources[i].m_fullPath, _sink); }
                        else { builder.AddFile(sources[i].m_entryName, sources[i].m_fullPath, _sink); }
                    }
                    builder.Finish();
                }

                //利用者が上書きを確認したファイルだけを置き換える
                if (File.Exists(_zipPath)) { File.Delete(_zipPath); }
                File.Move(tempPath, _zipPath);
            }
            catch
            {
                TryDeleteTemp(tempPath);
                throw;
            }

            result.m_zipBytes = new FileInfo(_zipPath).Length;
            result.m_elapsedMilliseconds = timer.ElapsedMilliseconds;
            return result;
        }

        //このツールが作った一時ファイル(拡張子が.tmp)だけを、エラーを無視して削除する関数
        private static void TryDeleteTemp(string _tempPath)
        {
            if (!_tempPath.EndsWith(k_TempExtension, StringComparison.OrdinalIgnoreCase)) { return; }
            try
            {
                if (File.Exists(_tempPath)) { File.Delete(_tempPath); }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        /// <summary>
        /// プロジェクトルート直下の項目を、ZIPに含めるものと含めないものに分ける。
        /// </summary>
        /// <param name="_projectRoot">プロジェクトルート</param>
        /// <param name="_included">含める項目の格納先(名前順)</param>
        /// <param name="_excluded">含めない項目の名前の格納先(名前順)</param>
        private static void ClassifyRootEntries(string _projectRoot, List<ZipSource> _included, List<string> _excluded)
        {
            string[] entries = Directory.GetFileSystemEntries(_projectRoot);
            Array.Sort(entries, StringComparer.Ordinal);

            for (int i = 0; i < entries.Length; ++i)
            {
                string name = Path.GetFileName(entries[i]);
                bool bIsDirectory = Directory.Exists(entries[i]);

                if (IsWhitelisted(name, bIsDirectory))
                {
                    _included.Add(bIsDirectory ? ZipSource.CreateDirectoryRoot(name, entries[i]) : ZipSource.CreateFileRoot(name, entries[i]));
                }
                else
                {
                    _excluded.Add(bIsDirectory ? name + "/" : name);
                }
            }
        }

        //ルート直下の項目名が、ホワイトリストに載っているかを返す関数
        private static bool IsWhitelisted(string _name, bool _bIsDirectory)
        {
            if (_bIsDirectory)
            {
                for (int i = 0; i < k_WhitelistDirectoryNames.Length; ++i)
                {
                    if (string.Equals(k_WhitelistDirectoryNames[i], _name, StringComparison.OrdinalIgnoreCase)) { return true; }
                }
                return false;
            }

            string baseName = Path.GetFileNameWithoutExtension(_name);
            for (int i = 0; i < k_WhitelistFileNames.Length; ++i)
            {
                if (string.Equals(k_WhitelistFileNames[i], baseName, StringComparison.OrdinalIgnoreCase)) { return true; }
                if (string.Equals(k_WhitelistFileNames[i], _name, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// 出力先が、ZIPに含める項目の中にないことを確認する(ZIPが自分自身を取り込むのを防ぐ)。問題があれば例外を投げる。
        /// </summary>
        /// <param name="_zipPath">出力するZIPのパス</param>
        /// <param name="_included">ZIPに含める項目</param>
        private static void ValidateOutputPath(string _zipPath, List<ZipSource> _included)
        {
            string zipFullPath = Path.GetFullPath(_zipPath);
            for (int i = 0; i < _included.Count; ++i)
            {
                string rootFullPath = Path.GetFullPath(_included[i].m_fullPath);
                bool bInside;
                if (_included[i].m_bIsDirectory)
                {
                    string prefix = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    bInside = zipFullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    bInside = string.Equals(zipFullPath, rootFullPath, StringComparison.OrdinalIgnoreCase);
                }

                if (bInside)
                {
                    throw new InvalidOperationException("出力先がZIPに含める項目(" + _included[i].m_name + ")の中にあります。プロジェクトの外を指定してください。");
                }
            }
        }

        /// <summary>
        /// フォルダの中身を再帰的に集める。中身が空のフォルダは、フォルダ自体を1件として加える。
        /// </summary>
        /// <param name="_directory">集める対象のフォルダ</param>
        /// <param name="_entryName">ZIP内でのこのフォルダの名前(区切りは/)</param>
        /// <param name="_depth">現在の深さ(0から)</param>
        /// <param name="_output">集めた項目の格納先</param>
        private static void CollectDirectory(string _directory, string _entryName, int _depth, List<ZipSource> _output)
        {
            if (_depth > k_MaxDirectoryDepth)
            {
                throw new InvalidOperationException("フォルダの階層が深すぎます(リンクの循環の可能性があります): " + _directory);
            }

            string[] files = Directory.GetFiles(_directory);
            string[] directories = Directory.GetDirectories(_directory);
            Array.Sort(files, StringComparer.Ordinal);
            Array.Sort(directories, StringComparer.Ordinal);

            if (files.Length == 0 && directories.Length == 0)
            {
                _output.Add(ZipSource.CreateEmptyDirectory(_entryName, _directory));
                return;
            }

            for (int i = 0; i < files.Length; ++i)
            {
                string fileName = Path.GetFileName(files[i]);
                _output.Add(ZipSource.CreateFile(fileName, _entryName + k_EntrySeparator + fileName, files[i]));
            }

            for (int i = 0; i < directories.Length; ++i)
            {
                string directoryName = Path.GetFileName(directories[i]);
                CollectDirectory(directories[i], _entryName + k_EntrySeparator + directoryName, _depth + 1, _output);
            }
        }

        //==========================
        //ZIPの書き出し部分(自前実装)
        //==========================

        //ZIPに入れる1件(ファイル・空フォルダ・ルート直下の項目)を表すデータ
        internal sealed class ZipSource
        {
            public string m_name = string.Empty; //ルート直下の項目の名前(ルート項目でない場合はファイル名)
            public string m_entryName = string.Empty; //ZIP内での名前(区切りは/)
            public string m_fullPath = string.Empty; //ディスク上のパス
            public bool m_bIsDirectory; //フォルダならtrue
            public ulong m_length; //ファイルのサイズ(フォルダは0)

            //ルート直下のフォルダ項目を作る関数
            public static ZipSource CreateDirectoryRoot(string _name, string _fullPath)
            {
                ZipSource source = new ZipSource();
                source.m_name = _name;
                source.m_fullPath = _fullPath;
                source.m_bIsDirectory = true;
                return source;
            }

            //ルート直下のファイル項目を作る関数
            public static ZipSource CreateFileRoot(string _name, string _fullPath)
            {
                ZipSource source = new ZipSource();
                source.m_name = _name;
                source.m_fullPath = _fullPath;
                return source;
            }

            //空フォルダ1件を作る関数
            public static ZipSource CreateEmptyDirectory(string _entryName, string _fullPath)
            {
                ZipSource source = new ZipSource();
                source.m_entryName = _entryName;
                source.m_fullPath = _fullPath;
                source.m_bIsDirectory = true;
                return source;
            }

            //ファイル1件を作る関数。サイズはこの時点のものを使い、進捗の分母にする
            public static ZipSource CreateFile(string _name, string _entryName, string _fullPath)
            {
                ZipSource source = new ZipSource();
                source.m_name = _name;
                source.m_entryName = _entryName;
                source.m_fullPath = _fullPath;
                source.m_length = (ulong)new FileInfo(_fullPath).Length;
                return source;
            }
        }

        //ZIP作成の結果
        internal sealed class ExportResult
        {
            public int m_fileCount; //書き出したファイルの数
            public int m_emptyDirectoryCount; //書き出した空フォルダの数
            public ulong m_sourceBytes; //元のファイルの合計サイズ
            public long m_zipBytes; //できあがったZIPのサイズ
            public long m_elapsedMilliseconds; //かかった時間
        }

        //進捗の通知先
        internal interface IProgressSink
        {
            //全ファイルの合計サイズが決まった時に呼ばれる
            void Begin(ulong _totalBytes);

            //処理が進むたびに呼ばれる。trueを返すとキャンセルになる
            bool Report(string _entryName, ulong _bytes);
        }

        //利用者が進捗バーのキャンセルを押した時に投げる例外
        internal sealed class ZipCanceledException : Exception
        {
            public ZipCanceledException() : base("キャンセルされました。") { }
        }

        //進捗バーを更新し、キャンセルを検知する
        private sealed class ProgressReporter : IProgressSink
        {
            private ulong m_totalBytes; //全ファイルの合計サイズ
            private ulong m_processedBytes; //処理し終えたサイズ
            private long m_lastUpdateMs = -k_ProgressIntervalMs; //最後に進捗バーを更新した時刻
            private readonly StopwatchTimer m_timer = StopwatchTimer.StartNew(); //経過時間

            //合計サイズを受け取り、進捗の分母にする関数
            public void Begin(ulong _totalBytes)
            {
                m_totalBytes = _totalBytes;
            }

            //進んだ分を加算し、一定間隔ごとに進捗バーを更新する関数。キャンセルされたらtrue
            public bool Report(string _entryName, ulong _bytes)
            {
                m_processedBytes += _bytes;

                long now = m_timer.ElapsedMilliseconds;
                if (now - m_lastUpdateMs < k_ProgressIntervalMs) { return false; }
                m_lastUpdateMs = now;

                float ratio = (m_totalBytes == 0ul) ? 0f : (float)((double)m_processedBytes / (double)m_totalBytes);
                ratio = Math.Min(1f, ratio);
                return UnityEditorUtility.DisplayCancelableProgressBar(k_ProgressTitle, _entryName, ratio);
            }
        }

        //数値をリトルエンディアンで書き込む(実行環境のバイト順に依存しない)
        private sealed class LittleEndianWriter
        {
            private readonly Stream m_pStream; //書き込み先
            private readonly byte[] m_scratch = new byte[k_ScratchSize]; //数値をバイトに分解するための作業領域

            public LittleEndianWriter(Stream _stream)
            {
                m_pStream = _stream;
            }

            //16bitの値を書く関数
            public void Write16(uint _value) { WriteValue(_value, k_Bytes16); }

            //32bitの値を書く関数
            public void Write32(uint _value) { WriteValue(_value, k_Bytes32); }

            //64bitの値を書く関数
            public void Write64(ulong _value) { WriteValue(_value, k_Bytes64); }

            //バイト列をそのまま書く関数
            public void WriteBytes(byte[] _bytes) { m_pStream.Write(_bytes, 0, _bytes.Length); }

            //下位バイトから順に、指定したバイト数だけ書く関数
            private void WriteValue(ulong _value, int _byteCount)
            {
                ulong value = _value;
                for (int i = 0; i < _byteCount; ++i)
                {
                    m_scratch[i] = (byte)(value & k_ByteMaskULong);
                    value >>= k_BitsPerByte;
                }
                m_pStream.Write(m_scratch, 0, _byteCount);
            }
        }

        //中央ディレクトリに書く、1件分の情報
        private sealed class CentralRecord
        {
            public byte[] m_nameBytes = new byte[0]; //UTF-8のファイル名
            public uint m_method; //格納(0)か圧縮(8)か
            public uint m_dosTime; //更新時刻(DOS形式)
            public uint m_dosDate; //更新日(DOS形式)
            public uint m_crc; //CRC32
            public ulong m_compressedSize; //圧縮後のサイズ
            public ulong m_uncompressedSize; //圧縮前のサイズ
            public ulong m_headerOffset; //ローカルヘッダの位置
            public uint m_externalAttributes; //ファイル属性
            public bool m_bZip64Local; //4GB近い大きなファイルで、ローカルヘッダをZIP64形式にしたか
        }

        //ZIPファイルを先頭から順に書き出す
        internal sealed class ZipBuilder
        {
            private readonly FileStream m_pStream; //書き込み先
            private readonly LittleEndianWriter m_pWriter; //数値の書き込み
            private readonly List<CentralRecord> m_records = new List<CentralRecord>(); //書き終えた項目(最後に中央ディレクトリへ書く)
            private readonly byte[] m_copyBuffer = new byte[k_BufferSize]; //読み込み用のバッファ

            public ZipBuilder(FileStream _stream)
            {
                m_pStream = _stream;
                m_pWriter = new LittleEndianWriter(_stream);
            }

            /// <summary>
            /// 空フォルダを1件加える。
            /// </summary>
            /// <param name="_entryName">ZIP内での名前</param>
            /// <param name="_sourcePath">元のフォルダ(更新日時の取得に使う)</param>
            /// <param name="_sink">進捗の通知先。不要ならnull</param>
            public void AddDirectory(string _entryName, string _sourcePath, IProgressSink _sink)
            {
                NotifyProgress(_sink, _entryName, 0ul);

                CentralRecord record = new CentralRecord();
                string name = _entryName.EndsWith(k_EntrySeparator, StringComparison.Ordinal) ? _entryName : _entryName + k_EntrySeparator;
                record.m_nameBytes = EncodeName(name);
                record.m_method = k_MethodStored;
                record.m_externalAttributes = k_ExternalAttrDirectory;
                ToDosDateTime(Directory.GetLastWriteTime(_sourcePath), out record.m_dosTime, out record.m_dosDate);

                record.m_headerOffset = (ulong)m_pStream.Position;
                WriteLocalHeader(record);
                m_records.Add(record);
            }

            /// <summary>
            /// ファイルを1件加える。読み込みながらCRCを計算し、圧縮して書き込む。
            /// </summary>
            /// <param name="_entryName">ZIP内での名前</param>
            /// <param name="_sourcePath">元のファイル</param>
            /// <param name="_sink">進捗の通知先。不要ならnull</param>
            public void AddFile(string _entryName, string _sourcePath, IProgressSink _sink)
            {
                NotifyProgress(_sink, _entryName, 0ul);

                FileInfo info = new FileInfo(_sourcePath);
                ulong declaredLength = (ulong)info.Length;

                CentralRecord record = new CentralRecord();
                record.m_nameBytes = EncodeName(_entryName);
                record.m_method = ShouldStore(_sourcePath, declaredLength) ? k_MethodStored : k_MethodDeflate;
                record.m_externalAttributes = k_ExternalAttrFile;
                record.m_bZip64Local = declaredLength >= k_Zip64EntryThreshold;
                ToDosDateTime(info.LastWriteTime, out record.m_dosTime, out record.m_dosDate);

                //サイズとCRCは書き終えるまで分からないので、仮の値でヘッダを書き、後で書き戻す
                record.m_headerOffset = (ulong)m_pStream.Position;
                WriteLocalHeader(record);
                long dataStart = m_pStream.Position;

                uint crc = k_CrcInitial;
                ulong totalRead;
                using (FileStream source = new FileStream(_sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, k_BufferSize, FileOptions.SequentialScan))
                {
                    if (record.m_method == k_MethodDeflate)
                    {
                        //leaveOpenをtrueにして、圧縮ストリームを閉じてもZIP本体は閉じない
                        using (DeflateStream deflate = new DeflateStream(m_pStream, CompressionMode.Compress, true))
                        {
                            totalRead = CopyAndHash(source, deflate, ref crc, _entryName, _sink);
                        }
                    }
                    else
                    {
                        totalRead = CopyAndHash(source, m_pStream, ref crc, _entryName, _sink);
                    }
                }

                record.m_crc = crc ^ k_CrcFinalXor;
                record.m_uncompressedSize = totalRead;
                record.m_compressedSize = (ulong)(m_pStream.Position - dataStart);

                //読んでいる間にファイルが4GB以上へ伸びた場合は、ヘッダの形式が合わないため中止する
                if (!record.m_bZip64Local && (record.m_uncompressedSize >= k_Marker32 || record.m_compressedSize >= k_Marker32))
                {
                    throw new IOException("ZIPへの書き出し中にファイルが大きくなりました: " + _sourcePath);
                }

                PatchLocalHeader(record);
                m_records.Add(record);
            }

            //全項目を書き終えたあと、中央ディレクトリと終端レコードを書く関数
            public void Finish()
            {
                long centralStart = m_pStream.Position;
                for (int i = 0; i < m_records.Count; ++i) { WriteCentralRecord(m_records[i]); }
                long centralEnd = m_pStream.Position;

                ulong entryCount = (ulong)m_records.Count;
                ulong centralSize = (ulong)(centralEnd - centralStart);
                ulong centralOffset = (ulong)centralStart;
                bool bZip64 = entryCount >= k_Zip64EntryCountThreshold || centralSize >= k_Zip64OffsetThreshold || centralOffset >= k_Zip64OffsetThreshold;

                if (bZip64)
                {
                    //項目数や位置が32bitに収まらないときだけ、ZIP64の終端レコードとその所在を書く
                    m_pWriter.Write32(k_SignatureZip64End);
                    m_pWriter.Write64(k_Zip64EndBodySize);
                    m_pWriter.Write16(k_VersionZip64);
                    m_pWriter.Write16(k_VersionZip64);
                    m_pWriter.Write32(0u);
                    m_pWriter.Write32(0u);
                    m_pWriter.Write64(entryCount);
                    m_pWriter.Write64(entryCount);
                    m_pWriter.Write64(centralSize);
                    m_pWriter.Write64(centralOffset);

                    m_pWriter.Write32(k_SignatureZip64Locator);
                    m_pWriter.Write32(0u);
                    m_pWriter.Write64((ulong)centralEnd);
                    m_pWriter.Write32(k_TotalDiskCount);
                }

                m_pWriter.Write32(k_SignatureEndOfCentral);
                m_pWriter.Write16(0u);
                m_pWriter.Write16(0u);
                m_pWriter.Write16(bZip64 ? k_Marker16 : (uint)entryCount);
                m_pWriter.Write16(bZip64 ? k_Marker16 : (uint)entryCount);
                m_pWriter.Write32(bZip64 ? k_Marker32 : (uint)centralSize);
                m_pWriter.Write32(bZip64 ? k_Marker32 : (uint)centralOffset);
                m_pWriter.Write16(0u);
            }

            //進捗を通知し、キャンセルされていたら例外を投げる関数
            private static void NotifyProgress(IProgressSink _sink, string _entryName, ulong _bytes)
            {
                if (_sink == null) { return; }
                if (_sink.Report(_entryName, _bytes)) { throw new ZipCanceledException(); }
            }

            /// <summary>
            /// 入力を読みながらCRC32を計算し、出力へ書く。読み込んだ合計バイト数を返す。
            /// </summary>
            /// <param name="_source">読み込み元</param>
            /// <param name="_destination">書き込み先(圧縮ストリームか、ZIP本体)</param>
            /// <param name="_crc">計算途中のCRC32(更新される)</param>
            /// <param name="_entryName">進捗表示に使うZIP内の名前</param>
            /// <param name="_sink">進捗の通知先。不要ならnull</param>
            private ulong CopyAndHash(Stream _source, Stream _destination, ref uint _crc, string _entryName, IProgressSink _sink)
            {
                ulong total = 0ul;
                uint crc = _crc;
                while (true)
                {
                    int read = _source.Read(m_copyBuffer, 0, m_copyBuffer.Length);
                    if (read <= 0) { break; }

                    crc = UpdateCrc(crc, m_copyBuffer, read);
                    _destination.Write(m_copyBuffer, 0, read);
                    total += (ulong)read;
                    NotifyProgress(_sink, _entryName, (ulong)read);
                }
                _crc = crc;
                return total;
            }

            //ローカルファイルヘッダを書く関数。CRCとサイズは仮の値で、書き終えた後にPatchLocalHeaderで書き戻す
            private void WriteLocalHeader(CentralRecord _record)
            {
                m_pWriter.Write32(k_SignatureLocalHeader);
                m_pWriter.Write16(_record.m_bZip64Local ? k_VersionZip64 : k_VersionDefault);
                m_pWriter.Write16(k_FlagUtf8);
                m_pWriter.Write16(_record.m_method);
                m_pWriter.Write16(_record.m_dosTime);
                m_pWriter.Write16(_record.m_dosDate);
                m_pWriter.Write32(0u);
                m_pWriter.Write32(_record.m_bZip64Local ? k_Marker32 : 0u);
                m_pWriter.Write32(_record.m_bZip64Local ? k_Marker32 : 0u);
                m_pWriter.Write16((uint)_record.m_nameBytes.Length);
                m_pWriter.Write16(_record.m_bZip64Local ? k_Zip64LocalExtraTotalSize : 0u);
                m_pWriter.WriteBytes(_record.m_nameBytes);

                if (_record.m_bZip64Local)
                {
                    m_pWriter.Write16(k_Zip64ExtraId);
                    m_pWriter.Write16(k_Zip64LocalExtraDataSize);
                    m_pWriter.Write64(0ul);
                    m_pWriter.Write64(0ul);
                }
            }

            //書き終えたあとの、実際のCRCとサイズをローカルヘッダへ書き戻す関数
            private void PatchLocalHeader(CentralRecord _record)
            {
                long endPosition = m_pStream.Position;

                m_pStream.Position = (long)_record.m_headerOffset + k_LocalCrcOffset;
                m_pWriter.Write32(_record.m_crc);

                if (_record.m_bZip64Local)
                {
                    //32bit欄は0xFFFFFFFFのまま、拡張フィールド内の64bit欄へ実際のサイズを書く
                    m_pStream.Position = (long)_record.m_headerOffset + k_LocalHeaderFixedSize + _record.m_nameBytes.Length + (int)k_ExtraHeaderSize;
                    m_pWriter.Write64(_record.m_uncompressedSize);
                    m_pWriter.Write64(_record.m_compressedSize);
                }
                else
                {
                    m_pWriter.Write32((uint)_record.m_compressedSize);
                    m_pWriter.Write32((uint)_record.m_uncompressedSize);
                }

                m_pStream.Position = endPosition;
            }

            //中央ディレクトリの1件分を書く関数
            private void WriteCentralRecord(CentralRecord _record)
            {
                bool bSizesZip64 = _record.m_bZip64Local;
                bool bOffsetZip64 = _record.m_headerOffset >= k_Zip64OffsetThreshold;
                uint extraDataSize = (bSizesZip64 ? k_Zip64SizesDataSize : 0u) + (bOffsetZip64 ? k_Zip64OffsetDataSize : 0u);
                bool bHasExtra = extraDataSize > 0u;
                uint version = (bSizesZip64 || bOffsetZip64) ? k_VersionZip64 : k_VersionDefault;

                m_pWriter.Write32(k_SignatureCentralHeader);
                m_pWriter.Write16(version);
                m_pWriter.Write16(version);
                m_pWriter.Write16(k_FlagUtf8);
                m_pWriter.Write16(_record.m_method);
                m_pWriter.Write16(_record.m_dosTime);
                m_pWriter.Write16(_record.m_dosDate);
                m_pWriter.Write32(_record.m_crc);
                m_pWriter.Write32(bSizesZip64 ? k_Marker32 : (uint)_record.m_compressedSize);
                m_pWriter.Write32(bSizesZip64 ? k_Marker32 : (uint)_record.m_uncompressedSize);
                m_pWriter.Write16((uint)_record.m_nameBytes.Length);
                m_pWriter.Write16(bHasExtra ? extraDataSize + k_ExtraHeaderSize : 0u);
                m_pWriter.Write16(0u);
                m_pWriter.Write16(0u);
                m_pWriter.Write16(0u);
                m_pWriter.Write32(_record.m_externalAttributes);
                m_pWriter.Write32(bOffsetZip64 ? k_Marker32 : (uint)_record.m_headerOffset);
                m_pWriter.WriteBytes(_record.m_nameBytes);

                if (bHasExtra)
                {
                    m_pWriter.Write16(k_Zip64ExtraId);
                    m_pWriter.Write16(extraDataSize);
                    if (bSizesZip64)
                    {
                        m_pWriter.Write64(_record.m_uncompressedSize);
                        m_pWriter.Write64(_record.m_compressedSize);
                    }
                    if (bOffsetZip64) { m_pWriter.Write64(_record.m_headerOffset); }
                }
            }
        }

        //==========================
        //ZIP書き出しの補助
        //==========================

        //ZIP内の名前をUTF-8に変換する関数。長すぎる場合は例外
        private static byte[] EncodeName(string _name)
        {
            byte[] bytes = k_Utf8NoBom.GetBytes(_name);
            if (bytes.Length > k_MaxNameBytes)
            {
                throw new InvalidOperationException("ファイルのパスが長すぎてZIPに記録できません: " + _name);
            }
            return bytes;
        }

        //圧縮せずに格納するかを返す関数(空ファイル、またはすでに圧縮された形式の拡張子)
        private static bool ShouldStore(string _path, ulong _length)
        {
            if (_length == 0ul) { return true; }

            string extension = Path.GetExtension(_path);
            for (int i = 0; i < k_StoredExtensions.Length; ++i)
            {
                if (string.Equals(k_StoredExtensions[i], extension, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }

        /// <summary>
        /// 日時をZIPのDOS形式(2秒単位、1980〜2107年)へ変換する。範囲外の日時は端の値にする。
        /// </summary>
        /// <param name="_time">変換する日時</param>
        /// <param name="_dosTime">DOS形式の時刻</param>
        /// <param name="_dosDate">DOS形式の日付</param>
        private static void ToDosDateTime(DateTime _time, out uint _dosTime, out uint _dosDate)
        {
            DateTime time = _time;
            if (time.Year < k_DosMinYear)
            {
                time = new DateTime(k_DosMinYear, 1, 1, 0, 0, 0);
            }
            else if (time.Year > k_DosMaxYear)
            {
                time = new DateTime(k_DosMaxYear, k_DosLastMonth, k_DosLastDay, k_DosLastHour, k_DosLastMinute, k_DosLastSecond);
            }

            _dosTime = ((uint)time.Hour << k_DosHourShift) | ((uint)time.Minute << k_DosMinuteShift) | ((uint)time.Second / k_DosSecondDivisor);
            _dosDate = ((uint)(time.Year - k_DosMinYear) << k_DosYearShift) | ((uint)time.Month << k_DosMonthShift) | (uint)time.Day;
        }

        //CRC32の計算表を作る関数
        private static uint[] CreateCrcTable()
        {
            uint[] table = new uint[k_CrcTableSize];
            for (int i = 0; i < k_CrcTableSize; ++i)
            {
                uint value = (uint)i;
                for (int bit = 0; bit < k_BitsPerByte; ++bit)
                {
                    value = ((value & 1u) != 0u) ? ((value >> 1) ^ k_CrcPolynomial) : (value >> 1);
                }
                table[i] = value;
            }
            return table;
        }

        //CRC32を、渡されたバイト列の分だけ進める関数
        private static uint UpdateCrc(uint _crc, byte[] _buffer, int _count)
        {
            uint crc = _crc;
            for (int i = 0; i < _count; ++i)
            {
                crc = k_CrcTable[(crc ^ _buffer[i]) & k_ByteMaskUInt] ^ (crc >> k_BitsPerByte);
            }
            return crc;
        }
    }
}
#endif
