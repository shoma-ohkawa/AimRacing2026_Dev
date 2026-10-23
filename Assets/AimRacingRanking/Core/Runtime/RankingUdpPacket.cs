/*━━━━━━━━━
@file RankingUdpPacket.cs
@brief UDPランキング本体とACKの共通通信形式
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/08/06
━━━━━━━━━*/

using System;
using UnityEngine;

namespace CrossProjectRanking
{
    /// <summary>
    /// LAN上で交換するランキングデータです。
    /// JsonUtilityで扱えるよう、プロパティではなくフィールドを使用します。
    /// </summary>
    [Serializable]
    public sealed class RankingUdpPacket
    {
        public const int CurrentProtocolVersion = 1;
        public const string RankingMessageType = "ranking";
        public const string AckMessageType = "ranking_ack";

        public int protocolVersion = CurrentProtocolVersion;
        public string messageType = RankingMessageType;
        public string messageId;
        public string playerName;
        public long timeMilliseconds;
        public string recordedAtJst;
        public string sourceProject;

        public static RankingUdpPacket Create(
            string playerName,
            long timeMilliseconds,
            string sourceProject)
        {
            return new RankingUdpPacket
            {
                messageId = Guid.NewGuid().ToString("N"),
                playerName = playerName == null ? string.Empty : playerName.Trim(),
                timeMilliseconds = timeMilliseconds,
                recordedAtJst = DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(9.0))
                    .ToString("o"),
                sourceProject = sourceProject == null ? string.Empty : sourceProject.Trim()
            };
        }

        public static RankingUdpPacket CreateAck(string messageId)
        {
            return new RankingUdpPacket
            {
                messageType = AckMessageType,
                messageId = messageId
            };
        }

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }

        public static bool TryParse(string json, out RankingUdpPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                packet = JsonUtility.FromJson<RankingUdpPacket>(json);
                return packet != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
