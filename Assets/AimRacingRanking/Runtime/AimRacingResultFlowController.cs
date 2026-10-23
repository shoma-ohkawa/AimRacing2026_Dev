/*━━━━━━━━━
@file AimRacingResultFlowController.cs
@brief ゲーム終了後のリザルト・ランキング・印刷・タイトル復帰をUIへ接続
@author 24CU0312 久場洸太
@date 2026/08/18
最終更新日 2026/09/16
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.Globalization;
using CrossProjectRanking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace AimRacing.Ranking
{
    public sealed class AimRacingResultFlowController : MonoBehaviour
    {
        private const int k_MillisecondsPerMinute = 60000; //表示タイムの分を求めるための換算値
        private const int k_MillisecondsPerSecond = 1000;  //表示タイムの秒とミリ秒を求めるための換算値
        private const string k_ReturnActionPath = "End/BackToStart"; //現行Input ActionsのResult終了操作

        [Header("表示するUI")]
        [Tooltip("リザルト表示中だけ有効にするUIルート。未設定なら表示切替を行いません。")]
        [SerializeField] private GameObject m_resultRoot;
        [SerializeField] private TMP_Text m_playerNameText;
        [SerializeField] private TMP_Text m_resultTimeText;
        [Tooltip("Lap 1から順番に表示するTextMeshProを設定します。")]
        [SerializeField] private List<TMP_Text> m_lapTimeTexts = new List<TMP_Text>();
        [SerializeField] private TMP_Text m_rankText;
        [Tooltip("ランキング全体を複数行で表示するTextMeshPro。")]
        [SerializeField] private TMP_Text m_rankingText;
        [SerializeField] private TMP_Text m_statusText;
        [Min(1)]
        [SerializeField] private int m_maximumRows = 10;

        [Header("画像生成・印刷")]
        [Tooltip("印刷用Canvas/Camera/文字を設定済みのコンポーネント。")]
        [SerializeField] private RankingCertificatePrinter m_certificatePrinter;

        [Header("タイトルへ戻る")]
        [FormerlySerializedAs("m_returnAutomatically")]
        [SerializeField] private bool m_bReturnAutomatically = true; //結果表示後に自動でタイトルへ戻るか
        [Min(0.1f)]
        //ResultState.m_resultDisplayDuration(30) + m_fadeDuration×2(ResultUI→Thank youのFade往復、1×2) + Thank youを見せる秒数(5) = 37
        [SerializeField] private float m_returnDelaySeconds = 37.0f;
        [SerializeField] private float m_bgmFadeOutDuration = 0.1f; //タイトル復帰時にResult BGMをフェードアウトする秒数

        private AimRacingRankingBridge m_bridge;
        private string m_lastHandledMessageId;
        private float m_returnAt;
        private bool m_bResultVisible;   //リザルトUIを表示しているか
        private bool m_bReturnRequested; //タイトル復帰処理を開始済みか
        private InputAction m_returnAction; //G29/T300RSの型番差をInput Actions側で吸収する終了操作

        //Result State自身のFadeIn完了まで証明書印刷(重いカメラ撮影・PNG圧縮・印刷API呼び出し)の開始を遅らせるための状態。
        //FadeIn中に発生するとRacing→ResultのFadeInそのものが固まって見えるため、画面が実際に見えた後へずらす。
        private RankingRecord m_pendingCertificateRecord;
        private int m_pendingCertificateRank; //ShowResult()で確定済みの順位(証明書側の再読込・再ソートを省略するために渡す)
        private bool m_bResultFadeInComplete;

        private void OnEnable()
        {
            ConnectBridge();
            ConnectReturnAction();
            SetResultVisible(false);
            GameFlowRunner.OnStateFadeInComplete += OnStateFadeInComplete;
        }

        private void OnDisable()
        {
            if (m_bridge != null)
            {
                m_bridge.ResultCommitted -= OnResultCommitted;
                m_bridge.StatusChanged -= OnStatusChanged;
            }
            if (m_returnAction != null)
            {
                m_returnAction.performed -= OnReturnActionPerformed;
                m_returnAction.Disable();
                m_returnAction = null;
            }
            GameFlowRunner.OnStateFadeInComplete -= OnStateFadeInComplete;
        }

        //GameFlowRunnerからのFadeIn完了通知を受け、Result自身のものだけ処理する関数
        private void OnStateFadeInComplete(GameStateId _completedId)
        {
            if (_completedId != GameStateId.Result) { return; }
            m_bResultFadeInComplete = true;
            FlushPendingCertificate();
        }

        //保留中の証明書印刷があれば、この時点でRankingCertificatePrinterへ渡す関数
        private void FlushPendingCertificate()
        {
            if (m_pendingCertificateRecord == null || m_certificatePrinter == null) { return; }
            RankingRecord record = m_pendingCertificateRecord;
            int rank = m_pendingCertificateRank;
            m_pendingCertificateRecord = null;
            m_certificatePrinter.Enqueue(record, rank); //既に確定済みの順位を渡し、証明書側でのCSV再読込・再ソートを省略する
        }

        private void Update()
        {
            ConnectBridge();
            ConnectReturnAction();

            bool isResultState = GameManager.Instance != null &&
                GameManager.Instance.CurrentGameState == GameStateId.Result; //Endは廃止されResultへ統合済み
            if (!isResultState) { m_bResultFadeInComplete = false; } //次回のResultへ備えてFadeIn完了フラグをリセットする
            SetResultVisible(isResultState);

            if (isResultState && m_bridge != null && m_bridge.LastResultPacket != null)
            {
                ShowPacketIfNeeded(m_bridge.LastResultPacket);
            }

            if (m_bResultVisible && m_bReturnAutomatically && !m_bReturnRequested &&
                Time.unscaledTime >= m_returnAt)
            {
                ReturnToTitle();
            }
        }

        private void ConnectBridge()
        {
            if (m_bridge != null || AimRacingRankingBridge.Instance == null) return;
            m_bridge = AimRacingRankingBridge.Instance;
            m_bridge.ResultCommitted += OnResultCommitted;
            m_bridge.StatusChanged += OnStatusChanged;
        }

        /// <summary>
        /// ManagerのInput ActionsからResult終了操作を取得します。
        /// デバイス型番ごとのボタン番号はAim2024Input側のBindingへ任せます。
        /// </summary>
        private void ConnectReturnAction()
        {
            if (m_returnAction != null) { return; }

            PlayerInput playerInput = FindFirstObjectByType<PlayerInput>(); //Manager常駐の入力管理
            if (playerInput == null || playerInput.actions == null) { return; }

            m_returnAction = playerInput.actions.FindAction(k_ReturnActionPath, false);
            if (m_returnAction == null) { return; }

            m_returnAction.performed += OnReturnActionPerformed;
            m_returnAction.Enable();
        }

        private void OnReturnActionPerformed(InputAction.CallbackContext _context)
        {
            if (!m_bResultVisible) { return; }
            ReturnToTitle();
        }

        private void OnResultCommitted(RankingRecord _record)
        {
            ShowResult(_record);
        }

        private void ShowPacketIfNeeded(RankingUdpPacket _packet)
        {
            if (_packet == null || string.Equals(m_lastHandledMessageId, _packet.messageId,
                StringComparison.Ordinal)) return;

            ShowResult(new RankingRecord
            {
                m_id = _packet.messageId,
                m_playerName = _packet.playerName,
                m_timeMilliseconds = _packet.timeMilliseconds,
                m_recordedAtUtc = _packet.recordedAtJst
            });
        }

        private void ShowResult(RankingRecord _record)
        {
            if (_record == null || string.Equals(m_lastHandledMessageId, _record.m_id,
                StringComparison.Ordinal)) return;

            m_lastHandledMessageId = _record.m_id;
            m_bReturnRequested = false;
            m_returnAt = Time.unscaledTime + m_returnDelaySeconds;
            SetResultVisible(true);

            RankingRecord[] rankings = LoadRankings();
            int rank = FindRank(rankings, _record.m_id);
            SetText(m_playerNameText, _record.m_playerName);
            SetText(m_resultTimeText, FormatTime(_record.m_timeMilliseconds));
            SetLapTimeTexts();
            string rankText = "-"; //今回記録がCSV内に見つからない場合の順位表示
            if (rank > 0)
            {
                rankText = rank.ToString(CultureInfo.InvariantCulture);
            }
            SetText(m_rankText, rankText);
            SetText(m_rankingText, CreateRankingText(rankings));
            SetText(m_statusText, "ランキングを本体へ保存しました");

            if (m_certificatePrinter != null)
            {
                m_pendingCertificateRecord = _record; //重い撮影・PNG圧縮・印刷処理はFadeIn完了後まで保留する
                m_pendingCertificateRank = rank; //上で確定済みの順位を渡し、証明書側の再読込・再ソートを省略させる(rank=0なら証明書側でResolveRank)
                if (m_bResultFadeInComplete) { FlushPendingCertificate(); } //既にFadeIn済みなら即座に処理する
            }
        }

        private RankingRecord[] LoadRankings()
        {
            if (m_bridge != null)
            {
                RankingStorage.SetSharedDirectory(m_bridge.LocalRankingDirectoryPath);
            }
            return RankingDateSorter.FastestFirst(RankingStorage.Load());
        }

        private void SetLapTimeTexts()
        {
            IReadOnlyList<string> lapTimes = null; //GameManagerから受け取るResult用ラップ値
            if (GameManager.Instance != null)
            {
                lapTimes = GameManager.Instance.LapTimes;
            }

            for (int i = 0; i < m_lapTimeTexts.Count; ++i)
            {
                string value = string.Empty; //対応するラップが存在しない欄は空表示
                if (lapTimes != null && i < lapTimes.Count)
                {
                    value = lapTimes[i];
                }
                SetText(m_lapTimeTexts[i], value);
            }
        }

        private static int FindRank(RankingRecord[] _rankings, string _messageid)
        {
            for (int i = 0; i < _rankings.Length; ++i)
            {
                if (string.Equals(_rankings[i].m_id, _messageid, StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }
            return 0;
        }

        private string CreateRankingText(RankingRecord[] _rankings)
        {
            int count = Mathf.Min(m_maximumRows, _rankings.Length);
            var lines = new List<string>(count);
            for (int i = 0; i < count; ++i)
            {
                RankingRecord row = _rankings[i];
                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.  {1}  {2}",
                    i + 1,
                    row.m_playerName,
                    FormatTime(row.m_timeMilliseconds)));
            }
            if (lines.Count == 0) { return "ランキングデータがありません"; }

            return string.Join("\n", lines);
        }

        public void ReturnToTitle()
        {
            if (m_bReturnRequested) return;
            m_bReturnRequested = true;
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.FadeOutBGM(m_bgmFadeOutDuration); //Result BGMをフェードアウト(従来は止めておらずSplash以降も鳴り続けていた)
            }
            if (GameFlowRunner.Instance != null)
            {
                GameFlowRunner.Instance.AdvanceToNext(); //Result→Splashへの通常のState遷移
            }
        }

        private void OnStatusChanged(string _message)
        {
            SetText(m_statusText, _message);
        }

        private void SetResultVisible(bool _visible)
        {
            m_bResultVisible = _visible;
            if (m_resultRoot != null && m_resultRoot.activeSelf != _visible)
            {
                m_resultRoot.SetActive(_visible);
            }
        }

        private static void SetText(TMP_Text _target, string _value)
        {
            if (_target == null) { return; }

            _target.text = _value;
            if (_value == null)
            {
                _target.text = string.Empty;
            }
        }

        private static string FormatTime(long _milliseconds)
        {
            long minutes = _milliseconds / k_MillisecondsPerMinute;
            long seconds = _milliseconds % k_MillisecondsPerMinute / k_MillisecondsPerSecond;
            long remainder = _milliseconds % k_MillisecondsPerSecond;
            return string.Format(CultureInfo.InvariantCulture,
                "{0:0}:{1:00}.{2:000}", minutes, seconds, remainder);
        }
    }
}
