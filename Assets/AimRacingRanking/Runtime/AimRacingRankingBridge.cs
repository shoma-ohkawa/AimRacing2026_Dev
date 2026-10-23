/*━━━━━━━━━
@file AimRacingRankingBridge.cs
@brief AimRacingの結果をローカルCSVと外付けランキングへ連携
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/09/07
━━━━━━━━━*/

using System;
using System.Globalization;
using System.IO;
using CrossProjectRanking;
using UnityEngine;

namespace AimRacing.Ranking
{
    /// <summary>
    /// AimRacingと外付けランキングを接続する常駐窓口です。
    /// ゲームは結果をこのクラスへ渡すだけで、CSV・UDP・再送を意識しません。
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public sealed class AimRacingRankingBridge : MonoBehaviour
    {
        private const string k_ConfigFileName = "aim-ranking-config.json";                 //LAN設定ファイル名
        private const string k_OfflineFlagFileName = "ranking-network-disabled.flag";     //LAN停止状態を保持するファイル名
        private const string k_DefaultPlayerName = "Guest";                               //名前が空欄の場合の表示名
        private const string k_LocalRankingRoot = @"C:\AimRacing";                       //AimRacing本体側の固定保存先
        private const string k_LocalRankingFolderName = "RankingData";                    //本体ランキングCSVのフォルダ名

        private static AimRacingRankingBridge s_instance;
        private RankingUdpSender m_sender;
        private AimRacingRankingOutbox m_outbox;
        private AimRacingRankingConfig m_config;
        private bool m_bWaitingForManualSwitch;          //LAN失敗後に手動切替を待っているか
        private bool m_bResultSubmittedForCurrentRun;    //現在のレース結果を保存済みか

        public static AimRacingRankingBridge Instance { get { return s_instance; } }
        public bool NetworkEnabled { get; private set; }
        public bool WaitingForManualSwitch { get { return m_bWaitingForManualSwitch; } }
        public RankingUdpPacket LastResultPacket { get; private set; }
        public string LocalRankingCsvPath { get { return m_sender.LocalCsvFilePath; } }
        public string LocalRankingDirectoryPath { get { return LocalRankingDirectory; } }
        public string OutboxCsvPath { get { return m_outbox.FilePath; } }

        public event Action<string> StatusChanged;
        public event Action<RankingRecord> ResultCommitted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (s_instance != null)
            {
                return;
            }

            GameObject host = new GameObject("AimRacing Ranking Bridge");
            DontDestroyOnLoad(host);
            s_instance = host.AddComponent<AimRacingRankingBridge>();
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            m_config = AimRacingRankingConfig.LoadOrCreate(ConfigPath);
            NetworkEnabled = m_config.m_bNetworkEnabledByDefault && !File.Exists(OfflineFlagPath);

            m_sender = gameObject.AddComponent<RankingUdpSender>();
            m_sender.ConfigureNetwork(
                m_config.m_receiverAddress,
                m_config.m_receiverPort,
                m_config.m_ackTimeoutSeconds,
                1, // 初回送信 + 再送1回 = 合計2回。2回失敗後は手動切替待ち。
                true);
            m_sender.ConfigureLocalCsv(true, LocalRankingDirectory);
            m_sender.DeliverySucceeded += OnDeliverySucceeded;
            m_sender.DeliveryFailed += OnDeliveryFailed;

            m_outbox = new AimRacingRankingOutbox(
                Path.Combine(Application.persistentDataPath, "AimRacingRanking"));

            if (NetworkEnabled)
            {
                RetryPendingFromPreviousRun();
            }
            if (NetworkEnabled)
            {
                NotifyStatus("ランキングLAN送信: ON");
            }
            else
            {
                NotifyStatus("ランキングLAN送信: OFF（ローカルCSVのみ）");
            }
        }

        private void OnDestroy()
        {
            if (m_sender != null)
            {
                m_sender.DeliverySucceeded -= OnDeliverySucceeded;
                m_sender.DeliveryFailed -= OnDeliveryFailed;
            }
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        private void Update()
        {
            //名前入力中はレース結果が存在しないため、GameManagerへの不要な参照を避ける。
            if (AimRacingNameEntryController.IsInputActive) { return; }

            ObserveAimRacingResult();

            if (m_bWaitingForManualSwitch && DebugKey.GetKeyDown(DebugKeyAction.DisableRankingNetwork))
            {
                DisableNetworkAndContinueOffline();
            }

            if (!NetworkEnabled && DebugKey.GetKeyDown(DebugKeyAction.EnableRankingNetwork))
            {
                EnableNetworkAndRetryPending();
            }
        }

        private void ObserveAimRacingResult()
        {
            // 既存のLine_StartFinishを変更せず、GameManagerが確定した結果だけを監視する。
            // 将来Resultの後へランキングSceneを追加してもBridgeはDontDestroyOnLoadで残る。
            if (global::GameManager.Instance == null)
            {
                return;
            }

            GameStateId state = global::GameManager.Instance.CurrentGameState;
            if (state == GameStateId.Racing)
            {
                m_bResultSubmittedForCurrentRun = false;
                return;
            }

            if (m_bResultSubmittedForCurrentRun || state != GameStateId.Result)
            {
                return;
            }

            string resultTime = global::GameManager.Instance.ResultTime;
            long milliseconds;
            if (!TryParseRaceTime(resultTime, out milliseconds))
            {
                return;
            }

            m_bResultSubmittedForCurrentRun = true;
            SubmitMilliseconds(AimRacingPlayerName.GetName(), milliseconds);
        }

        public static bool SubmitRaceResult(string _playername, string _formattedtime)
        {
            if (s_instance == null)
            {
                Bootstrap();
            }

            long milliseconds;
            if (!TryParseRaceTime(_formattedtime, out milliseconds))
            {
                AppLog.LogError("ランキングへ渡されたタイムを解析できません: " + _formattedtime);
                return false;
            }
            return s_instance.SubmitMilliseconds(_playername, milliseconds);
        }

        public bool SubmitMilliseconds(string _playername, long _milliseconds)
        {
            string resolvedName = k_DefaultPlayerName; //CSVとリザルトへ表示する確定済みプレイヤー名
            if (!string.IsNullOrWhiteSpace(_playername))
            {
                resolvedName = _playername.Trim();
            }

            RankingUdpPacket packet = RankingUdpPacket.Create(
                resolvedName,
                _milliseconds,
                Application.productName);
            LastResultPacket = packet;

            bool saved = m_sender.SavePacketToLocalCsv(packet);
            if (!saved)
            {
                NotifyStatus("ランキングのローカルCSV保存に失敗しました");
            }
            else if (ResultCommitted != null)
            {
                ResultCommitted(new RankingRecord
                {
                    m_id = packet.messageId,
                    m_playerName = packet.playerName,
                    m_timeMilliseconds = packet.timeMilliseconds,
                    m_recordedAtUtc = packet.recordedAtJst
                });
            }

            if (!NetworkEnabled)
            {
                m_outbox.Upsert(packet, AimRacingOutboxStatus.OfflineLocal, 0,
                    "ネットワーク送信OFF。手動同期対象です。");
                NotifyStatus("ランキングをローカル保存しました（LAN送信OFF）");
                return saved;
            }

            m_outbox.Upsert(packet, AimRacingOutboxStatus.Pending, 1, string.Empty);
            bool sent = m_sender.SendPacket(packet);
            if (!sent)
            {
                // SendPacketの失敗イベントで状態と手動切替待ちを更新する。
                return false;
            }
            NotifyStatus("ランキングを送信しました。受信側の保存確認待ちです");
            return saved;
        }

        public void DisableNetworkAndContinueOffline()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(OfflineFlagPath));
            File.WriteAllText(OfflineFlagPath, DateTimeOffset.Now.ToString("o"));
            NetworkEnabled = false;
            m_bWaitingForManualSwitch = false;
            m_outbox.MarkManualRequiredAsOffline();
            NotifyStatus("ランキングLAN送信を停止しました。ゲームは独立動作を継続します");
        }

        public void EnableNetworkAndRetryPending()
        {
            if (File.Exists(OfflineFlagPath))
            {
                File.Delete(OfflineFlagPath);
            }
            NetworkEnabled = true;
            m_bWaitingForManualSwitch = false;
            NotifyStatus("ランキングLAN送信を再開しました");
            RetryPendingFromPreviousRun();
        }

        private void RetryPendingFromPreviousRun()
        {
            RankingUdpPacket packet = m_outbox.GetOldestUnsent();
            if (packet == null)
            {
                return;
            }
            m_outbox.Upsert(packet, AimRacingOutboxStatus.Pending, 1, "起動時再送");
            m_sender.SendPacket(packet);
        }

        private void OnDeliverySucceeded(string _messageid)
        {
            m_outbox.SetStatus(_messageid, AimRacingOutboxStatus.Acknowledged, string.Empty);
            m_bWaitingForManualSwitch = false;
            NotifyStatus("受信側のCSV保存を確認しました: " + _messageid);
            RetryPendingFromPreviousRun();
        }

        private void OnDeliveryFailed(string _messageid, string _reason)
        {
            m_outbox.SetStatus(_messageid, AimRacingOutboxStatus.ManualRequired, _reason);
            m_bWaitingForManualSwitch = true;
            NotifyStatus(
                "LAN送信が2回失敗しました。" + DebugKey.GetKeyCode(DebugKeyAction.DisableRankingNetwork) +
                "キーで送信を停止し、ローカル運用へ切り替えてください");
        }

        private void NotifyStatus(string _message)
        {
            AppLog.Log("[AimRacingRanking] " + _message);
            if (StatusChanged != null)
            {
                StatusChanged(_message);
            }
        }

        private static bool TryParseRaceTime(string _value, out long _milliseconds)
        {
            _milliseconds = 0;
            if (string.IsNullOrWhiteSpace(_value))
            {
                return false;
            }

            string normalized = _value.Trim().Replace('：', ':').Replace('．', '.');
            TimeSpan time;
            if (TimeSpan.TryParseExact(
                normalized,
                new[] { @"mm\:ss\.fff", @"m\:ss\.fff", @"hh\:mm\:ss\.fff" },
                CultureInfo.InvariantCulture,
                out time))
            {
                _milliseconds = (long)Math.Round(time.TotalMilliseconds);
                return _milliseconds > 0;
            }

            double seconds;
            if (double.TryParse(normalized, NumberStyles.Float,
                CultureInfo.InvariantCulture, out seconds) && seconds > 0.0)
            {
                _milliseconds = (long)Math.Round(seconds * 1000.0);
                return true;
            }
            return false;
        }

        private string ConfigPath
        {
            get { return Path.Combine(Application.persistentDataPath, k_ConfigFileName); }
        }

        private string OfflineFlagPath
        {
            get { return Path.Combine(Application.persistentDataPath, k_OfflineFlagFileName); }
        }

        private string LocalRankingDirectory
        {
            get { return Path.Combine(k_LocalRankingRoot, k_LocalRankingFolderName); }
        }
    }
}
