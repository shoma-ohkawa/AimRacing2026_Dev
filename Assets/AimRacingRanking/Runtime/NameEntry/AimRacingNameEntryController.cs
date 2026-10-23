/*━━━━━━━━━
@file AimRacingNameEntryController.cs
@brief 物理キーボードによるアルファベット・ひらがな名前入力を管理
@author 24CU0312 久場洸太
@date 2026/09/01
最終更新日 2026/09/07
━━━━━━━━━*/

using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
namespace AimRacing.Ranking
{
    /// <summary>
    /// 名前入力を半角英字または全角ひらがなへ制限します。
    /// 確定値は既存のAimRacingPlayerNameへ渡すため、Resultとランキング側の変更は不要です。
    /// </summary>
    public sealed class AimRacingNameEntryController : MonoBehaviour
    {
        public static bool IsInputActive { get; private set; } //RankingBridgeが入力中のGameManager参照を避けるための状態

        private const int k_DefaultMaximumCharacters = 8; //Resultと印刷の名前欄へ収める初期上限
        private const string k_HalfWidthModeLabel = "半角：アルファベット";
        private const string k_FullWidthModeLabel = "全角：ひらがな";
        private const string k_EmptyNameGuide = "ここに名前が表示されます";
        private const string k_NgWordDirectoryName = "NameEntry";
        private const string k_NgWordFileName = "ng_words.csv";
        private const string k_NgWordMessage = "使用できない言葉が含まれています";

        [Header("表示")]
        [SerializeField] private TMP_Text m_inputText; //確定済み文字と変換待ちローマ字の表示先
        [SerializeField] private TMP_Text m_modeText; //現在の入力モードの表示先
        [SerializeField] private TMP_Text m_compositionText; //変換成立前のローマ字の表示先
        [SerializeField] private GameObject m_confirmationPanel; //名前確定前の確認画面
        [SerializeField] private TMP_Text m_confirmationNameText; //確認対象の名前表示
        [SerializeField] private TMP_Text m_validationMessageText; //NGワード検出時の案内表示

        [Header("入力設定")]
        [SerializeField, Min(1)] private int m_maximumCharacters = k_DefaultMaximumCharacters;
        [SerializeField] private bool m_bStartWithHiragana = true; //開始時を全角ひらがなにする

        private string m_inputName = string.Empty; //Resultとランキングへ渡す確定済み部分
        private string m_pendingRoman = string.Empty; //ひらがな変換待ちのローマ字
        private bool m_bHiraganaMode; //trueは全角ひらがな、falseは半角英字
        private bool m_bStarted; //GameStateからの重複初期化を防ぐ
        private bool m_bFinished; //Enter連打による二重遷移を防ぐ
        private bool m_bConfirming; //「これでいいですか？」を表示している状態
        private int m_lastProcessedFrame = -1; //同じフレーム内の重複更新を防ぐ
        private bool m_bLoggedFirstUpdate; //入力更新開始ログを一度だけ出すためのフラグ
        private readonly List<string> m_ngWords = new List<string>(); //外部テキストから読み込んだ照合用一覧

        private void OnEnable()
        {
            IsInputActive = true;
        }

        private void OnDisable()
        {
            IsInputActive = false;
        }

        /// <summary>
        /// GameManagerを参照せず、このNameEntryシーンだけで名前入力を開始します。
        /// </summary>
        private void Awake()
        {
            AppLog.Log("[NameEntryInputDebug] 0: Controller.Awakeへ到達しました。");
            Begin();
        }

        /// <summary>
        /// 入力中にGameManagerへアクセスしないよう、Controller自身から更新します。
        /// </summary>
        private void Update()
        {
            if (!m_bLoggedFirstUpdate)
            {
                m_bLoggedFirstUpdate = true;
                AppLog.Log("[NameEntryInputDebug] 1: Controller.Updateを開始しました。");
            }
            StateUpdate();
        }

        // IMEに依存せず、ゲーム内だけで変換を完結させるための対応表です。
        private static readonly Dictionary<string, string> m_romanToHiragana =
            new Dictionary<string, string>
            {
                { "a", "あ" }, { "i", "い" }, { "u", "う" }, { "e", "え" }, { "o", "お" },
                { "ka", "か" }, { "ki", "き" }, { "ku", "く" }, { "ke", "け" }, { "ko", "こ" },
                { "ga", "が" }, { "gi", "ぎ" }, { "gu", "ぐ" }, { "ge", "げ" }, { "go", "ご" },
                { "sa", "さ" }, { "si", "し" }, { "shi", "し" }, { "su", "す" }, { "se", "せ" }, { "so", "そ" },
                { "za", "ざ" }, { "zi", "じ" }, { "ji", "じ" }, { "zu", "ず" }, { "ze", "ぜ" }, { "zo", "ぞ" },
                { "ta", "た" }, { "ti", "ち" }, { "chi", "ち" }, { "tu", "つ" }, { "tsu", "つ" }, { "te", "て" }, { "to", "と" },
                { "da", "だ" }, { "di", "ぢ" }, { "du", "づ" }, { "de", "で" }, { "do", "ど" },
                { "na", "な" }, { "ni", "に" }, { "nu", "ぬ" }, { "ne", "ね" }, { "no", "の" },
                { "ha", "は" }, { "hi", "ひ" }, { "hu", "ふ" }, { "fu", "ふ" }, { "he", "へ" }, { "ho", "ほ" },
                { "ba", "ば" }, { "bi", "び" }, { "bu", "ぶ" }, { "be", "べ" }, { "bo", "ぼ" },
                { "pa", "ぱ" }, { "pi", "ぴ" }, { "pu", "ぷ" }, { "pe", "ぺ" }, { "po", "ぽ" },
                { "ma", "ま" }, { "mi", "み" }, { "mu", "む" }, { "me", "め" }, { "mo", "も" },
                { "ya", "や" }, { "yu", "ゆ" }, { "yo", "よ" },
                { "ra", "ら" }, { "ri", "り" }, { "ru", "る" }, { "re", "れ" }, { "ro", "ろ" },
                { "wa", "わ" }, { "wo", "を" }, { "nn", "ん" },
                { "kya", "きゃ" }, { "kyu", "きゅ" }, { "kyo", "きょ" },
                { "gya", "ぎゃ" }, { "gyu", "ぎゅ" }, { "gyo", "ぎょ" },
                { "sha", "しゃ" }, { "shu", "しゅ" }, { "sho", "しょ" },
                { "sya", "しゃ" }, { "syu", "しゅ" }, { "syo", "しょ" },
                { "ja", "じゃ" }, { "ju", "じゅ" }, { "jo", "じょ" },
                { "zya", "じゃ" }, { "zyu", "じゅ" }, { "zyo", "じょ" },
                { "cha", "ちゃ" }, { "chu", "ちゅ" }, { "cho", "ちょ" },
                { "tya", "ちゃ" }, { "tyu", "ちゅ" }, { "tyo", "ちょ" },
                { "nya", "にゃ" }, { "nyu", "にゅ" }, { "nyo", "にょ" },
                { "hya", "ひゃ" }, { "hyu", "ひゅ" }, { "hyo", "ひょ" },
                { "bya", "びゃ" }, { "byu", "びゅ" }, { "byo", "びょ" },
                { "pya", "ぴゃ" }, { "pyu", "ぴゅ" }, { "pyo", "ぴょ" },
                { "mya", "みゃ" }, { "myu", "みゅ" }, { "myo", "みょ" },
                { "rya", "りゃ" }, { "ryu", "りゅ" }, { "ryo", "りょ" },
                { "fa", "ふぁ" }, { "fi", "ふぃ" }, { "fe", "ふぇ" }, { "fo", "ふぉ" },
                { "va", "ゔぁ" }, { "vi", "ゔぃ" }, { "vu", "ゔ" }, { "ve", "ゔぇ" }, { "vo", "ゔぉ" },
                { "xa", "ぁ" }, { "xi", "ぃ" }, { "xu", "ぅ" }, { "xe", "ぇ" }, { "xo", "ぉ" },
                { "la", "ぁ" }, { "li", "ぃ" }, { "lu", "ぅ" }, { "le", "ぇ" }, { "lo", "ぉ" },
                { "xya", "ゃ" }, { "xyu", "ゅ" }, { "xyo", "ょ" },
                { "lya", "ゃ" }, { "lyu", "ゅ" }, { "lyo", "ょ" },
                { "xtu", "っ" }, { "xtsu", "っ" }, { "ltu", "っ" }, { "ltsu", "っ" }
            };

        /// <summary>表示と内部値を初期化し、Mapの事前読込みを開始します。</summary>
        public void Begin()
        {
            if (m_bStarted) { return; }

            m_bStarted = true;
            m_bFinished = false;
            m_bConfirming = false;
            m_inputName = string.Empty;
            m_pendingRoman = string.Empty;
            m_bHiraganaMode = m_bStartWithHiragana;
            m_lastProcessedFrame = -1;
            // OS側IMEの変換候補を出さず、ゲーム内のローマ字変換だけを使用します。
            Input.imeCompositionMode = IMECompositionMode.Off;
            LoadNgWords();
            if (m_confirmationPanel != null) { m_confirmationPanel.SetActive(false); }
            ClearValidationMessage();
            UpdateDisplay();
        }

        /// <summary>NameEntry表示中の入力を毎フレーム処理します。</summary>
        public void StateUpdate()
        {
            if (!m_bStarted || m_bFinished) { return; }
            if (m_lastProcessedFrame == Time.frameCount) { return; }

            m_lastProcessedFrame = Time.frameCount;

            // 確認中は入力文字を変更せず、Enterで確定、BackspaceまたはEscapeで入力へ戻します。
            if (m_bConfirming)
            {
                if (ReadConfirm())
                {
                    ConfirmEntry();
                }
                else if (ReadDelete() || ReadCancel())
                {
                    CancelConfirmation();
                }
                return;
            }

            if (ReadModeSwitch())
            {
                ToggleInputMode();
                return;
            }
            if (ReadDelete())
            {
                DeleteCharacter();
                return;
            }
            if (ReadConfirm())
            {
                ShowConfirmation();
                return;
            }

            // Spaceを含む英字以外は無視し、OSの変換候補を入力値へ混ぜません。
            string frameInput = ReadAlphabetInput();
            if (frameInput.Length > 0)
            {
                ClearValidationMessage();
                AppLog.Log("[NameEntryInputDebug] 2: キーボード文字を検出しました: " + frameInput);
            }
            for (int i = 0; i < frameInput.Length; ++i)
            {
                char inputCharacter = frameInput[i];
                if (!IsAlphabet(inputCharacter)) { continue; }

                if (m_bHiraganaMode)
                {
                    AddRomanCharacter(char.ToLowerInvariant(inputCharacter));
                }
                else
                {
                    AppendConfirmed(inputCharacter.ToString());
                }
            }
            UpdateDisplay();
        }

        private bool ReadModeSwitch()
        {
            // Both設定での二重取得を避け、文字入力と同じLegacy経路へ統一します。
            return Input.GetKeyDown(KeyCode.BackQuote);
        }

        private static bool ReadConfirm()
        {
            return Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        private static bool ReadDelete()
        {
            return Input.GetKeyDown(KeyCode.Backspace);
        }

        private static bool ReadCancel()
        {
            return Input.GetKeyDown(KeyCode.Escape);
        }

        private static string ReadAlphabetInput()
        {
            string result = string.Empty;
            string legacyInput = Input.inputString;
            for (int i = 0; i < legacyInput.Length; ++i)
            {
                if (IsAlphabet(legacyInput[i]))
                {
                    result += legacyInput[i];
                }
            }
            // Both設定では新旧入力の更新時期がずれて同じキーを二度拾うため、文字列はLegacy側だけで取得します。
            return result;
        }

        private static bool IsAlphabet(char _character)
        {
            return (_character >= 'A' && _character <= 'Z') ||
                (_character >= 'a' && _character <= 'z');
        }

        private void ToggleInputMode()
        {
            // ひらがな変換途中で英字へ切り替える場合、入力済みローマ字を捨てずに残します。
            if (m_bHiraganaMode && m_pendingRoman.Length > 0)
            {
                AppendConfirmed(m_pendingRoman);
                m_pendingRoman = string.Empty;
            }
            m_bHiraganaMode = !m_bHiraganaMode;
            UpdateDisplay();
        }

        private void AddRomanCharacter(char _character)
        {
            if (m_inputName.Length >= m_maximumCharacters) { return; }

            m_pendingRoman += _character;
            ConvertPendingRoman();
        }

        private void ConvertPendingRoman()
        {
            while (m_pendingRoman.Length > 0)
            {
                // 同じ子音の連続は、次の音を残しながら小さい「っ」へ変換します。
                if (m_pendingRoman.Length >= 2 &&
                    m_pendingRoman[0] == m_pendingRoman[1] &&
                    IsConsonant(m_pendingRoman[0]) && m_pendingRoman[0] != 'n')
                {
                    AppendConfirmed("っ");
                    m_pendingRoman = m_pendingRoman.Substring(1);
                    continue;
                }

                string converted;
                bool hasExactMatch = m_romanToHiragana.TryGetValue(m_pendingRoman, out converted);
                if (hasExactMatch && !HasLongerRomanMatch(m_pendingRoman))
                {
                    AppendConfirmed(converted);
                    m_pendingRoman = string.Empty;
                    continue;
                }
                if (HasRomanPrefix(m_pendingRoman)) { return; }

                // nの直後が変換可能な母音等でなければ、先頭のnだけ「ん」として確定します。
                if (m_pendingRoman[0] == 'n' && m_pendingRoman.Length >= 2)
                {
                    AppendConfirmed("ん");
                    m_pendingRoman = m_pendingRoman.Substring(1);
                    continue;
                }
                m_pendingRoman = m_pendingRoman.Substring(1);
            }
        }

        private static bool HasRomanPrefix(string _roman)
        {
            foreach (string key in m_romanToHiragana.Keys)
            {
                if (key.StartsWith(_roman)) { return true; }
            }
            return false;
        }

        private static bool HasLongerRomanMatch(string _roman)
        {
            foreach (string key in m_romanToHiragana.Keys)
            {
                if (key.Length > _roman.Length && key.StartsWith(_roman)) { return true; }
            }
            return false;
        }

        private static bool IsConsonant(char _character)
        {
            return IsAlphabet(_character) &&
                _character != 'a' && _character != 'i' && _character != 'u' &&
                _character != 'e' && _character != 'o';
        }

        private void AppendConfirmed(string _value)
        {
            if (string.IsNullOrEmpty(_value) || m_inputName.Length >= m_maximumCharacters) { return; }

            int remaining = m_maximumCharacters - m_inputName.Length;
            m_inputName += _value.Length <= remaining ? _value : _value.Substring(0, remaining);
        }

        private void DeleteCharacter()
        {
            ClearValidationMessage();
            // 変換途中のローマ字を先に戻し、確定済みの名前を不用意に消さないようにします。
            if (m_pendingRoman.Length > 0)
            {
                m_pendingRoman = m_pendingRoman.Substring(0, m_pendingRoman.Length - 1);
            }
            else if (m_inputName.Length > 0)
            {
                m_inputName = m_inputName.Substring(0, m_inputName.Length - 1);
            }
            UpdateDisplay();
        }

        private void FlushPendingRoman()
        {
            if (m_pendingRoman == "n")
            {
                AppendConfirmed("ん");
            }
            m_pendingRoman = string.Empty;
        }

        private void ShowConfirmation()
        {
            FlushPendingRoman();
            if (string.IsNullOrWhiteSpace(m_inputName))
            {
                UpdateDisplay();
                return;
            }
            if (ContainsNgWord(m_inputName))
            {
                if (m_validationMessageText != null) { m_validationMessageText.text = k_NgWordMessage; }
                UpdateDisplay();
                return;
            }

            ClearValidationMessage();
            m_bConfirming = true;
            if (m_confirmationNameText != null) { m_confirmationNameText.text = m_inputName; }
            if (m_confirmationPanel != null) { m_confirmationPanel.SetActive(true); }
            UpdateDisplay();
        }

        private void CancelConfirmation()
        {
            m_bConfirming = false;
            if (m_confirmationPanel != null) { m_confirmationPanel.SetActive(false); }
            UpdateDisplay();
        }

        /// <summary>
        /// StreamingAssetsのCSVを読み込みます。
        /// 一行を一語として扱い、空行は無視します。
        /// </summary>
        private void LoadNgWords()
        {
            m_ngWords.Clear();
            string filePath = Path.Combine(
                Application.streamingAssetsPath,
                k_NgWordDirectoryName,
                k_NgWordFileName);
            if (!File.Exists(filePath))
            {
                Debug.LogWarning("NameEntry: NGワードリストが見つかりません: " + filePath);
                return;
            }

            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);
            for (int i = 0; i < lines.Length; ++i)
            {
                string word = NormalizeForValidation(lines[i].Trim('"'));
                if (string.IsNullOrEmpty(word)) { continue; }
                if (!m_ngWords.Contains(word)) { m_ngWords.Add(word); }
            }
        }

        /// <summary>名前の一部に登録語が含まれているかを判定します。</summary>
        private bool ContainsNgWord(string _name)
        {
            string normalizedName = NormalizeForValidation(_name);
            for (int i = 0; i < m_ngWords.Count; ++i)
            {
                if (normalizedName.IndexOf(m_ngWords[i], System.StringComparison.Ordinal) >= 0) { return true; }
            }
            return false;
        }

        /// <summary>全角半角や英字の大小を揃え、表記違いによる判定漏れを防ぎます。</summary>
        private static string NormalizeForValidation(string _value)
        {
            if (string.IsNullOrWhiteSpace(_value)) { return string.Empty; }
            return _value.Trim().Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        }

        private void ClearValidationMessage()
        {
            if (m_validationMessageText != null) { m_validationMessageText.text = string.Empty; }
        }

        private void ConfirmEntry()
        {
            if (!m_bConfirming || string.IsNullOrWhiteSpace(m_inputName)) { return; }

            m_bFinished = true;
            // 既存の受渡し先を維持するため、Result・ランキング・印刷側は変更しません。
            AimRacingPlayerName.SetName(m_inputName);
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySE(SoundManager.SE_Type.EntryPushSE);
            }
            if (GameFlowRunner.Instance == null)
            {
                AppLog.LogError("NameEntry: GameFlowRunnerが見つからないため次の画面へ進めません。");
                m_bFinished = false;
                return;
            }
            GameFlowRunner.Instance.AdvanceToNext();
        }

        private void UpdateDisplay()
        {
            if (m_inputText != null)
            {
                string displayName = m_inputName + m_pendingRoman;
                m_inputText.text = string.IsNullOrEmpty(displayName) ? k_EmptyNameGuide : displayName;
            }
            if (m_compositionText != null)
            {
                m_compositionText.text = m_bHiraganaMode && m_pendingRoman.Length > 0 ?
                    "入力中：" + m_pendingRoman : string.Empty;
            }
            if (m_modeText != null)
            {
                m_modeText.text = m_bHiraganaMode ? k_FullWidthModeLabel : k_HalfWidthModeLabel;
            }
        }
    }
}
