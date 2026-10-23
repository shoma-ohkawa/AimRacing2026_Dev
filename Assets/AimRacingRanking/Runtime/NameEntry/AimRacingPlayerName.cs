/*━━━━━━━━━
@file AimRacingPlayerName.cs
@brief 名前入力画面で確定したプレイヤー名を保持
@author 24CU0312 久場洸太
@date 2026/09/01
最終更新日 2026/09/07
━━━━━━━━━*/

using System;

namespace AimRacing.Ranking
{
    /// <summary>
    /// シーンをまたいでプレイヤー名を参照するための小さな共有領域です。
    /// MonoBehaviourに保持しないため、NameEntryをアンロードした後もResultまで値が残ります。
    /// </summary>
    public static class AimRacingPlayerName
    {
        private static string m_playerName = string.Empty; //新しいNameEntryシーンで確定した名前

        /// <summary>前後の空白を除去し、Result以降で使用する名前を確定します。</summary>
        public static void SetName(string _playername)
        {
            m_playerName = _playername == null ? string.Empty : _playername.Trim();
        }

        /// <summary>
        /// 確定済みの名前を消去します。次のプレイヤーへ前回の名前を引き継がないよう、
        /// Result→Splash等セッションの節目で呼び出します。
        /// </summary>
        public static void Clear()
        {
            m_playerName = string.Empty;
        }

        /// <summary>
        /// 新画面で確定した名前を優先し、未設定時だけ旧Entryの名前を返します。
        /// これにより、旧シーンを残した状態でもランキング処理との互換性を維持できます。
        /// </summary>
        public static string GetName()
        {
            if (!string.IsNullOrWhiteSpace(m_playerName))
            {
                return m_playerName;
            }

            return global::Entry.GetPlayerName();
        }
    }
}
