/*━━━━━━━━━
@file RankingUdpSender.cs
@brief UDP送信・ACK照合・タイムアウト再送を管理
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/08/06
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrossProjectRanking
{
    /// <summary>
    /// ランキング1件をLAN上の受信機へUDP送信します。
    /// </summary>
    public sealed class RankingUdpSender : MonoBehaviour
    {
        private const double k_MillisecondsPerSecond = 1000.0; //秒からミリ秒へ変換するための換算値

        [SerializeField] private string m_destinationAddress = "169.254.224.90";
        [SerializeField] private int m_destinationPort = 50000;
        [SerializeField] private string m_sourceProject = "";
        [FormerlySerializedAs("m_enableBroadcast")]
        [SerializeField] private bool m_bEnableBroadcast = true; //UDPブロードキャストを許可するか

        [Header("実機ローカルCSV")]
        [Tooltip("通信結果に関係なく、送信元の実機にもランキングをCSV保存します。")]
        [FormerlySerializedAs("m_saveToLocalCsv")]
        [SerializeField] private bool m_bSaveToLocalCsv = true; //通信状態に関係なく本体CSVへ保存するか
        [Tooltip("空欄なら実行ファイルまたはUnityプロジェクト直下のRankingDataへ保存します。")]
        [SerializeField] private string m_localCsvDirectoryOverride = "";

        [Header("再送")]
        [Tooltip("ACKが返らない場合に同じmessageIdで再送します。")]
        [FormerlySerializedAs("m_requireAcknowledgement")]
        [SerializeField] private bool m_bRequireAcknowledgement = true; //受信側の保存ACKを必須にするか
        [Min(0.1f)]
        [SerializeField] private float m_ackTimeoutSeconds = 1.0f;
        [Min(0)]
        [Tooltip("最初の送信を除いた再送回数です。")]
        [SerializeField] private int m_maximumRetries = 3;

        private readonly Dictionary<string, PendingTransmission> m_pending =
            new Dictionary<string, PendingTransmission>(StringComparer.Ordinal);
        private readonly List<string> m_workMessageIds = new List<string>();
        private UdpClient m_udpClient;

        public string LastSentMessageId { get; private set; }
        public int PendingCount { get { return m_pending.Count; } }
        public string LocalCsvFilePath
        {
            get
            {
                RankingStorage.SetSharedDirectory(ResolveLocalCsvDirectory());
                return RankingStorage.SharedFilePath;
            }
        }

        /// <summary>受信側からACKが届いた時にmessageIdを通知します。</summary>
        public event Action<string> DeliverySucceeded;

        /// <summary>再送上限に達した時にmessageIdと理由を通知します。</summary>
        public event Action<string, string> DeliveryFailed;

        /// <summary>実機側のローカルCSVへ保存した時に記録を通知します。</summary>
        public event Action<RankingRecord> LocalCsvSaved;

        /// <summary>実機側のローカルCSV保存に失敗した時に理由を通知します。</summary>
        public event Action<string> LocalCsvSaveFailed;

        /// <summary>AimRacingなどの実機設定ファイルからLAN送信条件を適用します。</summary>
        public void ConfigureNetwork(
            string _destinationaddress,
            int _destinationport,
            float _acktimeoutseconds,
            int _maximumretries,
            bool _requireacknowledgement = true)
        {
            if (!string.IsNullOrWhiteSpace(_destinationaddress))
            {
                m_destinationAddress = _destinationaddress.Trim();
            }
            m_destinationPort = Mathf.Clamp(
                _destinationport,
                IPEndPoint.MinPort,
                IPEndPoint.MaxPort);
            m_ackTimeoutSeconds = Mathf.Max(0.1f, _acktimeoutseconds);
            m_maximumRetries = Mathf.Max(0, _maximumretries);
            m_bRequireAcknowledgement = _requireacknowledgement;
        }

        public void ConfigureLocalCsv(bool _enabled, string _directoryoverride = "")
        {
            m_bSaveToLocalCsv = _enabled;
            m_localCsvDirectoryOverride = _directoryoverride ?? string.Empty;
        }

        private void Update()
        {
            ReceiveAcknowledgements();
            RetryTimedOutPackets();
        }

        private void OnDisable()
        {
            if (m_pending.Count > 0)
            {
                FailAllPending("送信コンポーネントが停止しました。");
            }

            CloseClient();
        }

        public bool SendRanking(string _playername, float _timeseconds)
        {
            if (_timeseconds <= 0.0f || float.IsNaN(_timeseconds) || float.IsInfinity(_timeseconds))
            {
                AppLog.LogWarning("送信するランキングタイムが不正です。");
                return false;
            }

            long milliseconds = (long)Math.Round(_timeseconds * k_MillisecondsPerSecond);
            return SendRankingMilliseconds(_playername, milliseconds);
        }

        public bool SendRankingMilliseconds(string _playername, long _timemilliseconds)
        {
            if (string.IsNullOrWhiteSpace(_playername) || _timemilliseconds <= 0)
            {
                AppLog.LogWarning("プレイヤー名またはランキングタイムが不正です。");
                return false;
            }

            string sourceProject = m_sourceProject; //受信側で送信元を識別するプロジェクト名
            if (string.IsNullOrWhiteSpace(sourceProject))
            {
                sourceProject = Application.productName;
            }

            RankingUdpPacket packet = RankingUdpPacket.Create(
                _playername,
                _timemilliseconds,
                sourceProject);

            // ネットワーク障害時にも実機上へ記録を残すため、通信より先に保存する。
            bool localCsvSaved = !m_bSaveToLocalCsv || SavePacketToLocalCsv(packet);

            bool networkSendStarted = SendPacket(packet);

            LastSentMessageId = packet.messageId;
            // 片方が失敗しても、もう片方の処理は実行する。
            return localCsvSaved && networkSendStarted;
        }

        public bool SavePacketToLocalCsv(RankingUdpPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(packet.messageId) ||
                string.IsNullOrWhiteSpace(packet.playerName) ||
                packet.timeMilliseconds <= 0 ||
                string.IsNullOrWhiteSpace(packet.recordedAtJst))
            {
                ReportLocalCsvFailure("保存するランキングデータが不正です。");
                return false;
            }

            RankingRecord[] existingRecords;
            try
            {
                RankingStorage.SetSharedDirectory(ResolveLocalCsvDirectory());
                existingRecords = RankingStorage.Load();
            }
            catch (Exception exception)
            {
                ReportLocalCsvFailure("保存先を準備できませんでした: " + exception.Message);
                return false;
            }

            // 同じパケットをアプリ側から再度渡された場合も二重保存しない。
            for (int i = 0; i < existingRecords.Length; ++i)
            {
                if (string.Equals(existingRecords[i].m_id, packet.messageId,
                    StringComparison.Ordinal))
                {
                    return true;
                }
            }

            RankingRecord record = new RankingRecord
            {
                m_id = packet.messageId,
                m_playerName = packet.playerName.Trim(),
                m_timeMilliseconds = packet.timeMilliseconds,
                m_recordedAtUtc = packet.recordedAtJst
            };

            if (!RankingStorage.Append(record))
            {
                ReportLocalCsvFailure("CSVへの追記に失敗しました: " + packet.messageId);
                return false;
            }

            AppLog.Log("実機側CSVへランキングを保存しました: " +
                RankingStorage.SharedFilePath + " / " + packet.messageId);
            if (LocalCsvSaved != null)
            {
                LocalCsvSaved(record);
            }
            return true;
        }

        private string ResolveLocalCsvDirectory()
        {
            if (!string.IsNullOrWhiteSpace(m_localCsvDirectoryOverride))
            {
                return m_localCsvDirectoryOverride;
            }

            // 実機のインストール先が読み取り専用でも保存できる、
            // Unityが保証するアプリ専用の永続データ領域を使用する。
            return Path.Combine(Application.persistentDataPath, "RankingData");
        }

        public bool SendPacket(RankingUdpPacket packet)
        {
            if (packet == null ||
                string.IsNullOrWhiteSpace(packet.messageId) ||
                m_destinationPort < IPEndPoint.MinPort ||
                m_destinationPort > IPEndPoint.MaxPort)
            {
                return false;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(packet.ToJson());
                EnsureClient();
                m_udpClient.Send(bytes, bytes.Length, m_destinationAddress, m_destinationPort);

                if (m_bRequireAcknowledgement)
                {
                    // 呼出側が同じパケットを明示的に再送した場合も、
                    // 待機情報を上書きしてACK待ちを継続する。
                    m_pending[packet.messageId] = new PendingTransmission(
                        bytes,
                        Time.realtimeSinceStartup + m_ackTimeoutSeconds);
                }

                AppLog.Log("ランキングUDPを送信しました: " + packet.messageId +
                    " -> " + m_destinationAddress + ":" + m_destinationPort);
                return true;            }
            catch (Exception exception)
            {
                ReportDeliveryFailure(
                    packet.messageId,
                    "初回送信で例外が発生しました: " + exception.Message);
                CloseClient();
                return false;

            }
        }

        public bool IsWaitingForAck(string messageId)
        {
            return !string.IsNullOrEmpty(messageId) && m_pending.ContainsKey(messageId);
        }

        private void EnsureClient()
        {
            if (m_udpClient != null)
            {
                return;
            }

            // 送信に使用した同じソケットを開いたままにし、
            // 受信側が送信元ポートへ返すACKを受け取る。
            m_udpClient = new UdpClient(0);
            m_udpClient.EnableBroadcast = m_bEnableBroadcast;
            m_udpClient.Client.Blocking = false;
        }

        private void ReceiveAcknowledgements()
        {
            if (m_udpClient == null)
            {
                return;
            }

            try
            {
                while (m_udpClient.Available > 0)
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = m_udpClient.Receive(ref remote);
                    RankingUdpPacket ack;
                    if (!RankingUdpPacket.TryParse(Encoding.UTF8.GetString(bytes), out ack) ||
                        ack == null ||
                        ack.protocolVersion != RankingUdpPacket.CurrentProtocolVersion ||
                        !string.Equals(ack.messageType, RankingUdpPacket.AckMessageType,
                            StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(ack.messageId))
                    {
                        continue;
                    }

                    PendingTransmission transmission;
                    if (!m_pending.TryGetValue(ack.messageId, out transmission))
                    {
                        continue;
                    }

                    m_pending.Remove(ack.messageId);
                    AppLog.Log("ランキングUDPの保存ACKを受信しました: " + ack.messageId +
                        " / " + remote);
                    if (DeliverySucceeded != null)
                    {
                        DeliverySucceeded(ack.messageId);
                    }
                }
            }
            catch (SocketException exception)
            {
                // ノンブロッキングソケットにデータが無い時のWouldBlockは正常。
                if (exception.SocketErrorCode != SocketError.WouldBlock)
                {
                    AppLog.LogWarning("ランキングACKの受信に失敗しました: " + exception.Message);
                }
            }
            catch (ObjectDisposedException)
            {
                CloseClient();
            }
        }

        private void RetryTimedOutPackets()
        {
            if (m_udpClient == null || m_pending.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            m_workMessageIds.Clear();
            foreach (KeyValuePair<string, PendingTransmission> pair in m_pending)
            {
                if (now >= pair.Value.NextAttemptAt)
                {
                    m_workMessageIds.Add(pair.Key);
                }
            }

            for (int i = 0; i < m_workMessageIds.Count; ++i)
            {
                string messageId = m_workMessageIds[i];
                PendingTransmission transmission;
                if (!m_pending.TryGetValue(messageId, out transmission))
                {
                    continue;
                }

                if (transmission.RetryCount >= m_maximumRetries)
                {
                    m_pending.Remove(messageId);
                    ReportDeliveryFailure(
                        messageId,
                        "ACKが返らず、再送上限に達しました。送信回数=" +
                        (transmission.RetryCount + 1));
                    continue;
                }

                try
                {
                    m_udpClient.Send(
                        transmission.Bytes,
                        transmission.Bytes.Length,
                        m_destinationAddress,
                        m_destinationPort);
                    transmission.RetryCount++;
                    transmission.NextAttemptAt = now + m_ackTimeoutSeconds;
                    AppLog.LogWarning("ランキングUDPを再送しました: " + messageId +
                        " / retry=" + transmission.RetryCount + "/" + m_maximumRetries);
                }
                catch (Exception exception)
                {
                    // 一時的な送信例外も1回の再送試行として数え、次の周期で再試行する。
                    transmission.RetryCount++;
                    transmission.NextAttemptAt = now + m_ackTimeoutSeconds;
                    AppLog.LogWarning("ランキングUDPの再送に失敗しました: " +
                        exception.Message);
                }
            }
        }

        private void FailAllPending(string reason)
        {
            m_workMessageIds.Clear();
            foreach (string messageId in m_pending.Keys)
            {
                m_workMessageIds.Add(messageId);
            }
            m_pending.Clear();

            for (int i = 0; i < m_workMessageIds.Count; ++i)
            {
                ReportDeliveryFailure(m_workMessageIds[i], reason);
            }
        }

        private void ReportDeliveryFailure(string messageId, string reason)
        {
            AppLog.LogError("ランキングUDPの配送に失敗しました: " +
                messageId + " / " + reason);
            if (DeliveryFailed != null)
            {
                DeliveryFailed(messageId, reason);
            }
        }

        private void ReportLocalCsvFailure(string reason)
        {
            AppLog.LogError("実機側ランキングCSVの保存に失敗しました: " + reason);
            if (LocalCsvSaveFailed != null)
            {
                LocalCsvSaveFailed(reason);
            }
        }

        private void CloseClient()
        {
            if (m_udpClient != null)
            {
                m_udpClient.Close();
                m_udpClient = null;
            }
        }

        private sealed class PendingTransmission
        {
            public readonly byte[] Bytes;
            public int RetryCount;
            public float NextAttemptAt;

            public PendingTransmission(byte[] bytes, float nextAttemptAt)
            {
                Bytes = bytes;
                NextAttemptAt = nextAttemptAt;
            }
        }
    }
}
