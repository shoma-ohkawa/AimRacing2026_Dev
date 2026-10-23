/*━━━━━━━━━
@file AimRacingRankingConfig.cs
@brief AimRacing入力側のLAN通信設定をJSONで管理
@author 24cu0312 久場洸太
@date 2026/08/06
最終更新日 2026/08/06
━━━━━━━━━*/

using System;
using System.IO;
using UnityEngine;
using UnityEngine.Serialization;

namespace AimRacing.Ranking
{
    /// <summary>
    /// 実機ごとに変更する受信先、応答待ち時間、手動切替キーを保持します。
    /// JsonUtilityで直接読み書きできるよう、設定項目はpublicフィールドにしています。
    /// </summary>
    [Serializable]
    public sealed class AimRacingRankingConfig
    {
        // 出力側PCのIPv4アドレス。実機では127.0.0.1から出力PCの固定IPへ変更します。
        [FormerlySerializedAs("receiverAddress")]
        public string m_receiverAddress = "169.254.224.90";
        // UDPの送信先ポート。出力側の待受ポートと同じ値にします。
        [FormerlySerializedAs("receiverPort")]
        public int m_receiverPort = 50000;
        // ACKを待つ秒数。経過すると失敗として再送判定へ進みます。
        [FormerlySerializedAs("ackTimeoutSeconds")]
        public float m_ackTimeoutSeconds = 1.0f;
        // 初回起動時にLAN送信を有効にするかを指定します。
        [FormerlySerializedAs("networkEnabledByDefault")]
        public bool m_bNetworkEnabledByDefault = true;
        /// <summary>
        /// JSON設定を読み込みます。未作成・破損・アクセス失敗の場合でもゲームを止めず、
        /// 初期値を返すことでランキング装置を外した状態でもAimRacingを継続できます。
        /// </summary>
        public static AimRacingRankingConfig LoadOrCreate(string _path)
        {

            AppLog.Log("[Ranking]Ranking接続テスト");

            try
            {
                if (File.Exists(_path))
                {
                    AimRacingRankingConfig loaded =
                        JsonUtility.FromJson<AimRacingRankingConfig>(File.ReadAllText(_path));
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }

                AimRacingRankingConfig created = new AimRacingRankingConfig();
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(_path, JsonUtility.ToJson(created, true));
                return created;
            }
            catch (Exception exception)
            {
                AppLog.LogWarning("ランキング設定の読込/作成に失敗しました: " + exception.Message);
                return new AimRacingRankingConfig();
            }
        }
    }
}
