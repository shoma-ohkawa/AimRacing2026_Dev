/*━━━━━━━━━
@file RankingUdpReceiver.cs
@brief UDPランキング受信・検証・CSV保存・ACK返信
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/08/06
━━━━━━━━━*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;

namespace CrossProjectRanking
{
    /// <summary>
    /// UDPで受信したランキングを検証し、既存のrace-times.csvへ保存します。
    /// </summary>
    public sealed class RankingUdpReceiver : MonoBehaviour
    {
        private const int k_MaximumPacketBytes = 8192;      //不正な巨大UDPを拒否する上限サイズ
        private const int k_SocketTimeoutMilliseconds = 500; //停止要求を確認する受信待機時間
        private static readonly TimeSpan JapanTimeOffset = TimeSpan.FromHours(9.0);

        [Header("UDP受信")]
        [SerializeField] private int m_listenPort = 50000;
        [FormerlySerializedAs("m_startOnEnable")]
        [SerializeField] private bool m_bStartOnEnable = true; //有効化時に受信を開始するか
        [FormerlySerializedAs("m_sendAcknowledgement")]
        [SerializeField] private bool m_bSendAcknowledgement = true; //CSV保存後にACKを返すか

        [Header("CSV")]
        [Tooltip("空欄なら実行ファイルまたはUnityプロジェクト直下のRankingDataへ保存します。")]
        [SerializeField] private string m_sharedDirectoryOverride = "";

        [Header("検証")]
        [SerializeField] private long m_maximumTimeMilliseconds = 86400000;

        private readonly ConcurrentQueue<ReceivedDatagram> m_received =
            new ConcurrentQueue<ReceivedDatagram>();
        private readonly HashSet<string> m_knownMessageIds =
            new HashSet<string>(StringComparer.Ordinal);

        private UdpClient m_udpClient;
        private Thread m_receiveThread;
        private volatile bool m_bIsRunning; //UDP受信スレッドが動作中か

        public bool IsRunning { get { return m_bIsRunning; } }
        public int ListenPort { get { return m_listenPort; } }
        public string CsvFilePath
        {
            get
            {
                RankingStorage.SetSharedDirectory(m_sharedDirectoryOverride);
                return RankingStorage.SharedFilePath;
            }
        }

        public event Action<RankingRecord, IPEndPoint> RankingSaved;
        public event Action<string> ReceiveError;

        public void Configure(
            int _listenport,
            string _shareddirectoryoverride,
            bool _sendacknowledgement = true)
        {
            if (m_bIsRunning)
            {
                StopReceiver();
            }
            m_listenPort = Mathf.Clamp(_listenport, IPEndPoint.MinPort, IPEndPoint.MaxPort);
            m_sharedDirectoryOverride = _shareddirectoryoverride ?? string.Empty;
            m_bSendAcknowledgement = _sendacknowledgement;
        }

        public int ImportRecords(RankingRecord[] _records)
        {
            if (_records == null || _records.Length == 0)
            {
                return 0;
            }

            RankingStorage.SetSharedDirectory(m_sharedDirectoryOverride);
            RefreshKnownMessageIds();
            int imported = 0;
            for (int i = 0; i < _records.Length; ++i)
            {
                RankingRecord record = _records[i];
                if (record == null || string.IsNullOrWhiteSpace(record.m_id) ||
                    m_knownMessageIds.Contains(record.m_id))
                {
                    continue;
                }
                if (RankingStorage.Append(record))
                {
                    m_knownMessageIds.Add(record.m_id);
                    ++imported;
                }
            }
            return imported;
        }

        /// <summary>
        /// この受信機に設定されたCSVから全ランキングを読み込みます。
        /// 印刷など、保存直後の順位計算にも使用できます。
        /// </summary>
        public RankingRecord[] LoadSavedRankings()
        {
            RankingStorage.SetSharedDirectory(m_sharedDirectoryOverride);
            return RankingStorage.Load();
        }

        private void OnEnable()
        {
            if (m_bStartOnEnable)
            {
                StartReceiver();
            }
        }

        private void OnDisable()
        {
            StopReceiver();
        }

        private void Update()
        {
            ReceivedDatagram datagram;
            while (m_received.TryDequeue(out datagram))
            {
                ProcessDatagram(datagram);
            }
        }

        public bool StartReceiver()
        {
            if (m_bIsRunning)
            {
                return true;
            }

            if (m_listenPort < IPEndPoint.MinPort || m_listenPort > IPEndPoint.MaxPort)
            {
                ReportError("UDP待受ポートが範囲外です: " + m_listenPort);
                return false;
            }

            try
            {
                RankingStorage.SetSharedDirectory(m_sharedDirectoryOverride);
                RefreshKnownMessageIds();

                m_udpClient = new UdpClient(m_listenPort);
                m_udpClient.Client.ReceiveTimeout = k_SocketTimeoutMilliseconds;
                m_bIsRunning = true;
                m_receiveThread = new Thread(ReceiveLoop);
                m_receiveThread.IsBackground = true;
                m_receiveThread.Name = "Ranking UDP Receiver";
                m_receiveThread.Start();
                AppLog.Log("ランキングUDP受信を開始しました。port=" + m_listenPort);
                return true;
            }
            catch (Exception exception)
            {
                CleanupSocket();
                ReportError("UDP受信を開始できませんでした: " + exception.Message);
                return false;
            }
        }

        public void StopReceiver()
        {
            m_bIsRunning = false;
            if (m_udpClient != null)
            {
                m_udpClient.Close();
            }

            if (m_receiveThread != null && m_receiveThread.IsAlive)
            {
                m_receiveThread.Join(1000);
            }

            CleanupSocket();
        }

        private void ReceiveLoop()
        {
            while (m_bIsRunning)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = m_udpClient.Receive(ref remote);
                    if (bytes.Length > 0 && bytes.Length <= k_MaximumPacketBytes)
                    {
                        m_received.Enqueue(new ReceivedDatagram(bytes, remote));
                    }
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.TimedOut && m_bIsRunning)
                    {
                        m_received.Enqueue(new ReceivedDatagram(exception.Message));
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    if (m_bIsRunning)
                    {
                        m_received.Enqueue(new ReceivedDatagram(exception.Message));
                    }
                }
            }
        }

        private void ProcessDatagram(ReceivedDatagram datagram)
        {
            if (!string.IsNullOrEmpty(datagram.Error))
            {
                ReportError("UDP受信エラー: " + datagram.Error);
                return;
            }

            string json = Encoding.UTF8.GetString(datagram.Bytes);
            RankingUdpPacket packet;
            string validationError = "JSONを解析できません";
            if (!RankingUdpPacket.TryParse(json, out packet) ||
                !TryValidateRanking(packet, out validationError))
            {
                ReportError("不正なランキングUDPデータを破棄しました (" +
                    datagram.RemoteEndPoint + "): " + validationError);
                return;
            }

            // UDP再送およびアプリ再起動後の再受信でもCSVを二重登録しない。
            if (m_knownMessageIds.Contains(packet.messageId))
            {
                SendAck(packet.messageId, datagram.RemoteEndPoint);
                return;
            }

            RankingRecord record = new RankingRecord
            {
                m_id = packet.messageId,
                m_playerName = packet.playerName.Trim(),
                m_timeMilliseconds = packet.timeMilliseconds,
                m_recordedAtUtc = NormalizeToJapanTime(packet.recordedAtJst)
            };

            // 同じプロセス内の別コンポーネントが保存先を切り替えていても、
            // この受信機に設定されたCSVへ保存する。
            RankingStorage.SetSharedDirectory(m_sharedDirectoryOverride);
            if (!RankingStorage.Append(record))
            {
                ReportError("受信ランキングをCSVへ保存できませんでした: " + packet.messageId);
                return;
            }

            m_knownMessageIds.Add(packet.messageId);
            SendAck(packet.messageId, datagram.RemoteEndPoint);
            AppLog.Log("UDPランキングをCSVへ保存しました: " +
                record.m_playerName + " / " + record.m_timeMilliseconds + "ms / " +
                datagram.RemoteEndPoint);

            if (RankingSaved != null)
            {
                RankingSaved(record, datagram.RemoteEndPoint);
            }
        }

        private bool TryValidateRanking(RankingUdpPacket packet, out string error)
        {
            error = string.Empty;
            if (packet == null)
            {
                error = "JSONを解析できません";
                return false;
            }
            if (packet.protocolVersion != RankingUdpPacket.CurrentProtocolVersion)
            {
                error = "未対応のprotocolVersionです";
                return false;
            }
            if (!string.Equals(packet.messageType, RankingUdpPacket.RankingMessageType,
                StringComparison.Ordinal))
            {
                error = "messageTypeがrankingではありません";
                return false;
            }
            Guid messageId;
            if (!Guid.TryParseExact(packet.messageId, "N", out messageId))
            {
                error = "messageIdが不正です";
                return false;
            }
            if (string.IsNullOrWhiteSpace(packet.playerName) || packet.playerName.Length > 100)
            {
                error = "playerNameが空、または長すぎます";
                return false;
            }
            if (packet.timeMilliseconds <= 0 ||
                packet.timeMilliseconds > m_maximumTimeMilliseconds)
            {
                error = "timeMillisecondsが範囲外です";
                return false;
            }
            DateTimeOffset recordedAt;
            if (!DateTimeOffset.TryParse(packet.recordedAtJst, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out recordedAt))
            {
                error = "recordedAtJstが不正です";
                return false;
            }
            return true;
        }

        private static string NormalizeToJapanTime(string recordedAt)
        {
            DateTimeOffset value = DateTimeOffset.Parse(
                recordedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind);
            return value.ToOffset(JapanTimeOffset).ToString("o");
        }

        private void RefreshKnownMessageIds()
        {
            m_knownMessageIds.Clear();
            RankingRecord[] records = RankingStorage.Load();
            for (int i = 0; i < records.Length; ++i)
            {
                if (!string.IsNullOrEmpty(records[i].m_id))
                {
                    m_knownMessageIds.Add(records[i].m_id);
                }
            }
        }

        private void SendAck(string messageId, IPEndPoint remote)
        {
            if (!m_bSendAcknowledgement || m_udpClient == null || remote == null)
            {
                return;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(
                    RankingUdpPacket.CreateAck(messageId).ToJson());
                m_udpClient.Send(bytes, bytes.Length, remote);
            }
            catch (Exception exception)
            {
                ReportError("ACK送信エラー: " + exception.Message);
            }
        }

        private void ReportError(string message)
        {
            AppLog.LogWarning(message);
            if (ReceiveError != null)
            {
                ReceiveError(message);
            }
        }

        private void CleanupSocket()
        {
            m_bIsRunning = false;
            if (m_udpClient != null)
            {
                m_udpClient.Close();
                m_udpClient = null;
            }
            m_receiveThread = null;
        }

        private sealed class ReceivedDatagram
        {
            public readonly byte[] Bytes;
            public readonly IPEndPoint RemoteEndPoint;
            public readonly string Error;

            public ReceivedDatagram(byte[] bytes, IPEndPoint remoteEndPoint)
            {
                Bytes = bytes;
                RemoteEndPoint = remoteEndPoint;
            }

            public ReceivedDatagram(string error)
            {
                Error = error;
            }
        }
    }
}
