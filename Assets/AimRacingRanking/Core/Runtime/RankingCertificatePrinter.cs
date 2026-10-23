/*━━━━━━━━━
@file RankingCertificatePrinter.cs
@brief ランキング結果を画像へ合成してPNG保存・印刷
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/09/17
━━━━━━━━━*/

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

namespace CrossProjectRanking
{
    /// <summary>
    /// UDPランキング保存後、テンプレートCanvasへ結果を書き込みPNG出力・印刷します。
    /// 背景画像や文字位置はUnity Editor上のCanvasで自由にレイアウトできます。
    /// </summary>
    public sealed class RankingCertificatePrinter : MonoBehaviour
    {
        private const int k_MillisecondsPerMinute = 60000; //表示タイムの分を求めるための換算値
        private const int k_MillisecondsPerSecond = 1000;  //表示タイムの秒とミリ秒を求めるための換算値
        private const double k_JapanTimeOffsetHours = 9.0; //記録日時を日本時間へ変換する時差
        private const string k_DefaultPrinterName = "Microsoft Print to PDF"; //既定の仮想プリンター名
        private const string k_PdfPrinterName = "Microsoft Print to PDF"; //PDF確認に使用するWindows標準プリンター名

        [Header("連携")]
        [SerializeField] private RankingUdpReceiver m_receiver;

        [Header("印刷レイアウト")]
        [Tooltip("印刷専用Canvasだけを描画するCameraを指定します。")]
        [SerializeField] private Camera m_printCamera;
        [SerializeField] private TMP_Text m_playerNameText;
        [SerializeField] private TMP_Text m_rankText;
        [SerializeField] private TMP_Text m_timeText;
        [SerializeField] private TMP_Text m_recordedAtText;
        [Tooltip("ONの場合、印刷する文字を各テキストボックスへ収めるようにフォントサイズを自動調整します。")]
        [SerializeField] private bool m_bFitTextToBox = false; //印刷文字をテキストボックスへ収めるか

        [Header("PNG出力")]
        [Min(1)]
        [SerializeField] private int m_outputWidth = 1748;
        [Min(1)]
        [SerializeField] private int m_outputHeight = 2480;
        [Tooltip("相対パスはUnityプロジェクトまたはexeの直下を基準にします。空欄ならApplication.persistentDataPath/RankingPrintsへ保存します。")]
        [SerializeField] private string m_outputDirectoryOverride = "";

        [Header("印刷")]
        [Tooltip("ONの場合だけPNG生成とプリンター出力を行います。OFFの場合はPNGも生成しません。")]
        [FormerlySerializedAs("m_printAutomatically")]
        [SerializeField] private bool m_bPrintAutomatically = true; //ONでPNG生成と印刷を行い、OFFではどちらも行わない
        [Tooltip("Physical Printerは実機印刷、PdfはPrintOutputsへ確認用PDFを保存します。")]
        [SerializeField] private WindowsPrintOutputMode m_printOutputMode =
            WindowsPrintOutputMode.PhysicalPrinter;
        [Tooltip("印刷画面を表示せずに出力するWindowsプリンター名です。")]
        [SerializeField] private string m_printerName = k_DefaultPrinterName;
        [Tooltip("物理プリンタへ要求する用紙サイズです。はがき印刷ではJapanesePostcardを選択します。")]
        [SerializeField] private WindowsPrintPaperSize m_printPaperSize =
            WindowsPrintPaperSize.JapanesePostcard;

        private readonly Queue<PrintJob> m_printQueue = new Queue<PrintJob>();
        private bool m_bIsProcessing; //印刷キューを処理中か

        private RenderTexture m_cachedRenderTexture; //印刷PNG撮影用に使い回すRenderTexture(毎回newしない)
        private Texture2D m_cachedTexture; //印刷PNG撮影用に使い回すTexture2D(毎回newしない)

        //ゴール直後は既にAimRacingResultFlowController側でCSV読込・ソート・順位確定を済ませていることが多く、
        //ここで同じCSVを読み直すと1回のゴールでLoad()が何度も走ってGCの原因になるため、既知の順位は再計算しない
        private readonly struct PrintJob
        {
            public readonly RankingRecord m_record;
            public readonly int m_knownRank; //0以下ならこの時点で未確定(ResolveRankで求める)

            public PrintJob(RankingRecord _record, int _knownrank)
            {
                m_record = _record;
                m_knownRank = _knownrank;
            }
        }

        //印刷スプーラ送信(WindowsNativeImagePrinter.Print)の結果をバックグラウンドスレッドからメインスレッドへ受け渡すための結果
        private readonly struct PrintResult
        {
            public readonly bool m_bSucceeded;
            public readonly string m_imagePath;
            public readonly string m_message;

            public PrintResult(bool _bsucceeded, string _imagepath, string _message)
            {
                m_bSucceeded = _bsucceeded;
                m_imagePath = _imagepath;
                m_message = _message;
            }
        }

        private readonly ConcurrentQueue<PrintResult> m_printResults = new ConcurrentQueue<PrintResult>();

        public string LastOutputPath { get; private set; }
        public event Action<string, RankingRecord> ImageCreated;
        public event Action<string> OutputFailed;
        public event Action<string> PrintRequested;

        public void SetLayoutReferences(
            RankingUdpReceiver _receiver,
            Camera _printcamera,
            TMP_Text _playernametext,
            TMP_Text _ranktext,
            TMP_Text _timetext,
            TMP_Text _recordedattext)
        {
            m_receiver = _receiver;
            m_printCamera = _printcamera;
            m_playerNameText = _playernametext;
            m_rankText = _ranktext;
            m_timeText = _timetext;
            m_recordedAtText = _recordedattext;
        }

        public void SetOutputSettings(
            int _outputwidth,
            int _outputheight,
            string _outputdirectoryoverride,
            bool _printautomatically,
            string _printername)
        {
            m_outputWidth = Mathf.Max(1, _outputwidth);
            m_outputHeight = Mathf.Max(1, _outputheight);
            m_outputDirectoryOverride = _outputdirectoryoverride ?? string.Empty;
            m_bPrintAutomatically = _printautomatically;
            m_printerName = _printername ?? string.Empty;
        }

        private void OnEnable()
        {
            if (m_receiver == null)
            {
#if UNITY_2023_1_OR_NEWER
                m_receiver = FindFirstObjectByType<RankingUdpReceiver>();
#else
                m_receiver = FindObjectOfType<RankingUdpReceiver>();
#endif
            }
            if (m_receiver != null)
            {
                m_receiver.RankingSaved += OnRankingSaved;
            }
        }

        private void OnDisable()
        {
            if (m_receiver != null)
            {
                m_receiver.RankingSaved -= OnRankingSaved;
            }
        }

        private void OnDestroy()
        {
            ReleasePrintTargets();
        }

        private void OnRankingSaved(RankingRecord _record, IPEndPoint _remoteendpoint)
        {
            Enqueue(_record); //UDP受信分は呼び出し側が順位を知らないため、ここでResolveRankする
        }

        /// <summary>
        /// UDP以外の方法で保存したランキングも印刷キューへ追加できます。順位はここで再計算します。
        /// </summary>
        public void Enqueue(RankingRecord _record)
        {
            Enqueue(_record, 0);
        }

        /// <summary>
        /// 呼び出し側で既に順位を確定済みの場合に使います。CSVの再読込・再ソートを省略できるため、
        /// ゴール直後にAimRacingResultFlowControllerと同じ内容を読み直してGCを増やさずに済みます。
        /// </summary>
        /// <param name="_record">印刷するランキング記録</param>
        /// <param name="_knownrank">呼び出し側で確定済みの順位(1始まり)。0以下ならここでResolveRankします</param>
        public void Enqueue(RankingRecord _record, int _knownrank)
        {
            if (!m_bPrintAutomatically) { return; }

            if (_record == null)
            {
                ReportFailure("印刷するランキングデータがありません。");
                return;
            }

            m_printQueue.Enqueue(new PrintJob(_record, _knownrank));
            if (!m_bIsProcessing && isActiveAndEnabled)
            {
                StartCoroutine(ProcessQueue());
            }
        }

        private IEnumerator ProcessQueue()
        {
            m_bIsProcessing = true;
            while (m_printQueue.Count > 0)
            {
                PrintJob job = m_printQueue.Dequeue();
                int rank = job.m_knownRank > 0 ? job.m_knownRank : ResolveRank(job.m_record.m_id);
                ApplyTexts(job.m_record, rank);

                // TextMeshProとCanvasのレイアウト更新を反映してから撮影する。
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();

                string outputPath;
                if (TryCreatePng(job.m_record, out outputPath))
                {
                    LastOutputPath = outputPath;
                    if (ImageCreated != null)
                    {
                        ImageCreated(outputPath, job.m_record);
                    }

                    RequestPrint(outputPath);
                }
            }
            m_bIsProcessing = false;
        }

        private int ResolveRank(string _recordid)
        {
            //m_receiver優先時にRankingStorage.Load()を呼ぶと結果を使わず捨てるだけなので、片方だけ読む
            RankingRecord[] records = m_receiver != null ? m_receiver.LoadSavedRankings() : RankingStorage.Load();
            RankingRecord[] sorted = RankingDateSorter.FastestFirst(records);
            for (int i = 0; i < sorted.Length; ++i)
            {
                if (string.Equals(sorted[i].m_id, _recordid, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private void Update()
        {
            PrintResult result;
            while (m_printResults.TryDequeue(out result))
            {
                if (result.m_bSucceeded)
                {
                    AppLog.Log(result.m_message);
                    if (PrintRequested != null)
                    {
                        PrintRequested(result.m_imagePath);
                    }
                }
                else
                {
                    ReportFailure(result.m_message);
                }
            }
        }

        private void ApplyTexts(RankingRecord _record, int _rank)
        {
            ApplyTextBoxFit(m_playerNameText);
            ApplyTextBoxFit(m_rankText);
            ApplyTextBoxFit(m_timeText);
            ApplyTextBoxFit(m_recordedAtText);

            if (m_playerNameText != null)
            {
                m_playerNameText.text = _record.m_playerName;
            }
            if (m_rankText != null)
            {
                m_rankText.text = "-";
                if (_rank > 0)
                {
                    m_rankText.text = _rank.ToString(CultureInfo.InvariantCulture);
                }
            }
            if (m_timeText != null)
            {
                m_timeText.text = FormatTime(_record.m_timeMilliseconds);
            }
            if (m_recordedAtText != null)
            {
                DateTimeOffset recordedAt;
                m_recordedAtText.text = string.Empty;
                bool parsed = DateTimeOffset.TryParse(
                    _record.m_recordedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out recordedAt);
                if (parsed)
                {
                    m_recordedAtText.text = recordedAt.ToOffset(TimeSpan.FromHours(k_JapanTimeOffsetHours))
                        .ToString("yyyy年MM月dd日 HH:mm", CultureInfo.InvariantCulture);
                }
            }
        }

        /// <summary>
        /// 長い名前などが印刷枠からはみ出さないよう、指定されたTextMeshProの自動サイズ調整を切り替えます。
        /// </summary>
        private void ApplyTextBoxFit(TMP_Text _text)
        {
            if (_text == null) { return; }

            if (m_bFitTextToBox)
            {
                _text.fontSizeMax = Mathf.Max(_text.fontSize, _text.fontSizeMax);
            }
            _text.enableAutoSizing = m_bFitTextToBox;
        }

        private bool TryCreatePng(RankingRecord _record, out string _outputpath)
        {
            _outputpath = null;
            if (m_printCamera == null)
            {
                ReportFailure("Print Cameraが設定されていません。");
                return false;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = m_printCamera.targetTexture;
            bool previousSrgbWrite = GL.sRGBWrite; //手動描画前の色空間書き込み状態

            try
            {
                string directory = ResolveOutputDirectory();
                Directory.CreateDirectory(directory);
                _outputpath = Path.Combine(directory, CreateFileName(_record));

                EnsurePrintTargets(); //RenderTexture/Texture2Dを毎回newせず使い回す(GC対策)

                DisablePrintCameraExposure();
                m_printCamera.targetTexture = m_cachedRenderTexture;
                GL.sRGBWrite = true;
                m_printCamera.Render();
                RenderTexture.active = m_cachedRenderTexture;
                m_cachedTexture.ReadPixels(
                    new Rect(0, 0, m_cachedRenderTexture.width, m_cachedRenderTexture.height),
                    0,
                    0);
                m_cachedTexture.Apply();
                File.WriteAllBytes(_outputpath, m_cachedTexture.EncodeToPNG());

                AppLog.Log("ランキング印刷画像を出力しました: " + _outputpath);
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("PNG出力に失敗しました: " + exception.Message);
                _outputpath = null;
                return false;
            }
            finally
            {
                m_printCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                GL.sRGBWrite = previousSrgbWrite;
            }
        }

        /// <summary>
        /// 印刷PNG撮影用のRenderTexture/Texture2Dを用意します。出力サイズ変更時のみ再生成します。
        /// </summary>
        private void EnsurePrintTargets()
        {
            bool sizeChanged = m_cachedRenderTexture != null &&
                (m_cachedRenderTexture.width != Mathf.Max(1, m_outputWidth) ||
                 m_cachedRenderTexture.height != Mathf.Max(1, m_outputHeight));
            if (sizeChanged) { ReleasePrintTargets(); }

            if (m_cachedRenderTexture == null)
            {
                m_cachedRenderTexture = new RenderTexture(
                    Mathf.Max(1, m_outputWidth),
                    Mathf.Max(1, m_outputHeight),
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB);
            }
            if (m_cachedTexture == null)
            {
                m_cachedTexture = new Texture2D(
                    m_cachedRenderTexture.width,
                    m_cachedRenderTexture.height,
                    TextureFormat.RGB24,
                    false);
            }
        }

        /// <summary>
        /// キャッシュ済みの印刷用RenderTexture/Texture2Dを解放します。
        /// </summary>
        private void ReleasePrintTargets()
        {
            if (m_cachedRenderTexture != null)
            {
                m_cachedRenderTexture.Release();
                Destroy(m_cachedRenderTexture);
                m_cachedRenderTexture = null;
            }
            if (m_cachedTexture != null)
            {
                Destroy(m_cachedTexture);
                m_cachedTexture = null;
            }
        }

        /// <summary>
        /// HDRPの露出補正を印刷専用Cameraに適用しないようにします。
        /// </summary>
        private void DisablePrintCameraExposure()
        {
            HDAdditionalCameraData cameraData = m_printCamera.GetComponent<HDAdditionalCameraData>();
            if (cameraData == null) { return; }

            cameraData.customRenderingSettings = true;
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.Postprocess,
                false);
            cameraData.renderingPathCustomFrameSettings.SetEnabled(
                FrameSettingsField.ExposureControl,
                false);

            FrameSettingsOverrideMask overrideMask =
                cameraData.renderingPathCustomFrameSettingsOverrideMask;
            overrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
            overrideMask.mask[(uint)FrameSettingsField.ExposureControl] = true;
            cameraData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;
        }

        public bool RequestPrint(string _imagepath)
        {
            if (string.IsNullOrWhiteSpace(_imagepath) || !File.Exists(_imagepath))
            {
                ReportFailure("印刷する画像が見つかりません: " + _imagepath);
                return false;
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            string printerName;
            string outputPath;
            switch (m_printOutputMode)
            {
            case WindowsPrintOutputMode.PhysicalPrinter:

                // 実機印刷ではInspectorで指定されたプリンタへ直接出力する。
                printerName = m_printerName;
                if (string.IsNullOrWhiteSpace(printerName))
                {
                    printerName = k_DefaultPrinterName;
                }
                outputPath = null;
                break;

            case WindowsPrintOutputMode.Pdf:

                // PDF確認ではWindows標準PDFプリンタへ保存先を明示して画面表示を防ぐ。
                printerName = k_PdfPrinterName;
                outputPath = Path.ChangeExtension(_imagepath, ".pdf");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
                break;

            default:

                // 未定義値では誤印刷を避けるため処理を中止する。
                ReportFailure("印刷方式が正しく設定されていません。");
                return false;
            }

            try
            {
                //Textureはメインスレッド専用のため、ピクセル抽出だけここで済ませる(軽い)
                WindowsNativeImagePrinter.PreparedPrintImage image = WindowsNativeImagePrinter.PrepareImage(_imagepath);
                WindowsPrintPaperSize paperSize = m_printPaperSize;
                WindowsPrintOutputMode mode = m_printOutputMode;

                //スプーラへの送信(StretchDIBits/EndDoc等)はプリンタ応答待ちで数秒ブロックすることがあるため、
                //メインスレッドを止めないようバックグラウンドスレッドで行う
                Thread printThread = new Thread(() =>
                {
                    try
                    {
                        WindowsNativeImagePrinter.Print(image, printerName, paperSize, outputPath);
                        string message = mode == WindowsPrintOutputMode.Pdf
                            ? "Windows印刷APIでPDFを出力しました: " + outputPath
                            : "Windows印刷APIで物理プリンタへ出力しました: " + _imagepath;
                        m_printResults.Enqueue(new PrintResult(true, _imagepath, message));
                    }
                    catch (Exception exception)
                    {
                        m_printResults.Enqueue(new PrintResult(false, _imagepath, "Windows印刷の開始に失敗しました: " + exception.Message));
                    }
                });
                printThread.IsBackground = true;
                printThread.Name = "Ranking Certificate Print";
                printThread.Start();
                return true;
            }
            catch (Exception exception)
            {
                ReportFailure("Windows印刷の開始に失敗しました: " + exception.Message);
                return false;
            }
#else
            ReportFailure("自動印刷はWindows Standaloneでのみ利用できます。PNGは保存されています。");
            return false;
#endif
        }

        public bool PrintLastOutput()
        {
            return RequestPrint(LastOutputPath);
        }

        private string ResolveOutputDirectory()
        {
            if (string.IsNullOrWhiteSpace(m_outputDirectoryOverride))
            {
                return Path.Combine(Application.persistentDataPath, "RankingPrints");
            }

            if (Path.IsPathRooted(m_outputDirectoryOverride))
            {
                return Path.GetFullPath(m_outputDirectoryOverride);
            }

            string applicationRoot = Directory.GetParent(Application.dataPath).FullName; //Editorではプロジェクト、ビルドではexeの配置先
            return Path.GetFullPath(Path.Combine(applicationRoot, m_outputDirectoryOverride));
        }

        private static string CreateFileName(RankingRecord _record)
        {
            string playerName = SanitizeFileName(_record.m_playerName);
            return DateTime.Now.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) +
                "_" + playerName + "_" + _record.m_id + ".png";
        }

        private static string SanitizeFileName(string _value)
        {
            string result = "NoName";
            if (!string.IsNullOrWhiteSpace(_value))
            {
                result = _value.Trim();
            }
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; ++i)
            {
                result = result.Replace(invalid[i], '_');
            }
            return result;
        }

        private static string FormatTime(long _milliseconds)
        {
            long minutes = _milliseconds / k_MillisecondsPerMinute;
            long seconds = _milliseconds % k_MillisecondsPerMinute / k_MillisecondsPerSecond;
            long remainder = _milliseconds % k_MillisecondsPerSecond;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:00}:{1:00}.{2:000}",
                minutes,
                seconds,
                remainder);
        }

        private void ReportFailure(string _message)
        {
            AppLog.LogError(_message);
            if (OutputFailed != null)
            {
                OutputFailed(_message);
            }
        }
    }
}
