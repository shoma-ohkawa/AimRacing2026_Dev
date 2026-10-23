// Copyright (c) 2025 Takeda Carpaccio
// Version 1.1.0
// Released under the MIT license
// https://opensource.org/licenses/mit-license.php

using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;
using Object = UnityEngine.Object;

public class TakedaAllRenamer : EditorWindow
{
    #region Language Data
    private enum Language { Japanese, English }
    private static Language currentLanguage = Language.Japanese;

    private static readonly Dictionary<string, string> LangJP = new Dictionary<string, string>
    {
        {"windowTitle", "All Renamer"},
        {"undoButton", "元に戻す"},
        {"redoButton", "やり直す"},
        {"mainTitle", "All Renamer [Hierarchy & Project対応 一括リネームツール]"},
        {"renameModeLabel", "リネームモード"},
        {"includeChildrenLabel", "子オブジェクトも含む"},
        {"baseNameLabel", "ベース名"},
        {"separatorLabel", "区切り文字"},
        {"startNumberLabel", "開始番号"},
        {"digitCountLabel", "桁数"},
        {"digitCountLabelWithRemove", "桁数"},
        {"searchStringLabel", "検索 (正規表現可)"},
        {"replaceStringLabel", "置換"},
        {"snippetTooltip", "スニペットを挿入"},
        {"affixStringLabel", "追加する文字列"},
        {"affixPositionLabel", "追加する位置"},
        {"insertSeparatorLabel", "区切り文字を挿入"},
        {"caseTypeLabel", "変換形式"},
        {"previewSection", "プレビュー"},
        {"previewPlaceholder", "HierarchyまたはProjectから対象を選択してください。"},
        {"executeButton", "【{0}件】を変更"},
        {"confirmDialogTitle", "確認"},
        {"confirmDialogMessage", "{0}個のオブジェクト/アセットをリネームします。よろしいですか？\nこの操作はツール内のUndoで元に戻せます。"},
        {"yes", "はい"},
        {"no", "いいえ"},
        {"logError", "アセットのリネームに失敗: {0}\nエラー: {1}"},
        {"logComplete", "リネーム操作が完了しました。"},
        {"nameConflictWarning", "Projectの同じフォルダ内に、同じ名前のアセットは作成できません。"},
        {"invalidRegexWarning", "無効な正規表現です。通常の文字列として置換されます。"},
        {"Sequential", "連番 (全て置換)"},
        {"ReplaceNumbering", "番号の整形"},
        {"Replace", "文字列の置換"},
        {"AddAffix", "文字列の追加"},
        {"CaseChange", "大文字・小文字変換"},
        {"Prefix", "前へ"},
        {"Suffix", "後ろへ"},
        {"snippetRemoveUnitySuffix", "Unityサフィックスを削除 (例: Name (1) -> Name)"},
        {"snippetRemovePrefix", "プレフィックスを削除 (例: PRE_Name -> Name)"},
        {"snippetExtractNumber", "番号部分のみ抽出 (例: Name_001 -> 001)"},
        {"snippetSwapLast", "最後の区切り文字で前後を入れ替え (例: A_B -> B_A)"},
        {"SequentialHelp", "選択した全てを、新しいベース名と連番に置き換えます。\n桁数を0にすると、番号は付与されずベース名のみになります。"},
        {"ReplaceNumberingHelp", "名前の末尾にある数字を整形または削除します。\n桁数0で区切り文字も削除されます。"},
        {"ReplaceHelp", "特定の文字列を検索して置換します。正規表現も使用可能です。"},
        {"AddAffixHelp", "名前の前、または後ろに指定した文字列を追加します。"},
        {"CaseChangeHelp", "オブジェクト名を指定した形式（パスカルケースなど）に一括で変換します。"},
        {"UPPERCASE", "すべて大文字 (MY_FILE)"},
        {"lowercase", "すべて小文字 (my_file)"},
        {"PascalCase", "パスカルケース (MyFile)"},
        {"camelCase", "キャメルケース (myFile)"},
        {"snake_case", "スネークケース (my_file)"}
    };

    private static readonly Dictionary<string, string> LangEN = new Dictionary<string, string>
    {
        {"windowTitle", "All Renamer"},
        {"undoButton", "Undo"},
        {"redoButton", "Redo"},
        {"mainTitle", "All Renamer [Hierarchy & Project]"},
        {"renameModeLabel", "Rename Mode"},
        {"includeChildrenLabel", "Include Children"},
        {"baseNameLabel", "Base Name"},
        {"separatorLabel", "Separator"},
        {"startNumberLabel", "Start Number"},
        {"digitCountLabel", "Digits"},
        {"digitCountLabelWithRemove", "Digits"},
        {"searchStringLabel", "Search (Regex)"},
        {"replaceStringLabel", "Replace"},
        {"snippetTooltip", "Insert Snippet"},
        {"affixStringLabel", "Text to Add"},
        {"affixPositionLabel", "Position"},
        {"insertSeparatorLabel", "Insert Separator"},
        {"caseTypeLabel", "Case Type"},
        {"previewSection", "Preview"},
        {"previewPlaceholder", "Please select objects from the Hierarchy or Project."},
        {"executeButton", "Apply Changes to {0} items"},
        {"confirmDialogTitle", "Confirmation"},
        {"confirmDialogMessage", "Are you sure you want to rename {0} objects/assets?\nThis action can be undone with the in-tool Undo button."},
        {"yes", "Yes"},
        {"no", "No"},
        {"logError", "Failed to rename asset: {0}\nError: {1}"},
        {"logComplete", "Rename operation completed."},
        {"nameConflictWarning", "You cannot create assets with the same name in the same folder in the Project."},
        {"invalidRegexWarning", "Invalid regular expression. It will be treated as a plain string."},
        {"Sequential", "Sequential (Replace All)"},
        {"ReplaceNumbering", "Format Numbering"},
        {"Replace", "Replace String"},
        {"AddAffix", "Add Text"},
        {"CaseChange", "Case Change"},
        {"Prefix", "Before"},
        {"Suffix", "After"},
        {"snippetRemoveUnitySuffix", "Remove Unity Suffix (e.g., Name (1) -> Name)"},
        {"snippetRemovePrefix", "Remove Prefix (e.g., PRE_Name -> Name)"},
        {"snippetExtractNumber", "Extract Number (e.g., Name_001 -> 001)"},
        {"snippetSwapLast", "Swap around last separator (e.g., A_B -> B_A)"},
        {"SequentialHelp", "Replaces all selected names with a new base name and sequential number.\nSet Digits to 0 to use only the base name without numbers."},
        {"ReplaceNumberingHelp", "Formats or removes the last number in a name. If Digits is 0, the separator is also removed."},
        {"ReplaceHelp", "Finds and replaces a specific string. Regular expressions can be used."},
        {"AddAffixHelp", "Adds the specified string to the beginning or end of the name."},
        {"CaseChangeHelp", "Converts the object's name to a specified format (e.g., PascalCase, camelCase)."},
        {"UPPERCASE", "UPPERCASE (MY_FILE)"},
        {"lowercase", "lowercase (my_file)"},
        {"PascalCase", "PascalCase (MyFile)"},
        {"camelCase", "camelCase (myFile)"},
        {"snake_case", "snake_case (my_file)"}
    };

    private static string T(string key)
    {
        var langDict = currentLanguage == Language.Japanese ? LangJP : LangEN;
        return langDict.ContainsKey(key) ? langDict[key] : key;
    }
    #endregion

    #region Enums and Fields
    private enum RenameMode { Sequential, ReplaceNumbering, Replace, AddAffix, CaseChange }
    private RenameMode renameMode = RenameMode.Sequential;

    private enum CaseType { PascalCase, camelCase, snake_case, UPPERCASE, lowercase }
    private CaseType caseType = CaseType.PascalCase;

    private enum AffixPosition { Prefix, Suffix }
    private AffixPosition affixPosition = AffixPosition.Prefix;

    private string baseName = "Item";
    private string separator = "_";
    private int startNumber = 1;
    private int digitCount = 3;
    private string searchString = "";
    private string replaceString = "";
    private bool includeChildren = false;
    
    private readonly List<(string key, string search, string replace)> regexSnippets = new List<(string, string, string)>
    {
        ("snippetRemoveUnitySuffix", @" \(\d+\)$", ""),
        ("snippetRemovePrefix", @"^[^_]*_", ""),
        ("snippetExtractNumber", @"^.*?(\d+)$", "$1"),
        ("snippetSwapLast", @"^(.*)_([^_]*)$", "$2_$1")
    };

    private string affixString = "";
    private bool insertAffixSeparator = true;
    private bool insertSequentialSeparator = true;

    private Vector2 mainScrollPosition;
    private Object[] processedObjects = new Object[0];
    private readonly Stack<List<KeyValuePair<Object, string>>> undoStack = new Stack<List<KeyValuePair<Object, string>>>();
    private readonly Stack<List<KeyValuePair<Object, string>>> redoStack = new Stack<List<KeyValuePair<Object, string>>>();
    
    private GUIStyle wordWrapLabelStyle;
    private GUIStyle wordWrapTitleStyle;
    private GUIStyle sectionHeaderStyle;
    #endregion

    [MenuItem("TakedaTools/All Renamer")]
    public static void ShowWindow()
    {
        GetWindow<TakedaAllRenamer>(T("windowTitle"));
    }
    
    private void InitStyles()
    {
        if (wordWrapLabelStyle == null)
        {
            wordWrapLabelStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = true };
            wordWrapTitleStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true, fontSize = 13 };
            sectionHeaderStyle = new GUIStyle(EditorStyles.boldLabel) { wordWrap = true };
        }
    }

    private TEnum TranslatedEnumPopup<TEnum>(string label, TEnum selectedEnum, bool toolbarStyle = false) where TEnum : System.Enum
    {
        var enumNames = System.Enum.GetNames(typeof(TEnum));
        var translatedNames = enumNames.Select(n => T(n)).ToArray();
        int selectedIndex = Array.IndexOf(enumNames, selectedEnum.ToString());
        
        int newIndex;
        if (toolbarStyle)
        {
            newIndex = GUILayout.Toolbar(selectedIndex, translatedNames);
        }
        else
        {
            newIndex = EditorGUILayout.Popup(label, selectedIndex, translatedNames);
        }
        
        return (TEnum)Enum.Parse(typeof(TEnum), enumNames[newIndex]);
    }

    private void OnGUI()
    {
        InitStyles();
        
        mainScrollPosition = EditorGUILayout.BeginScrollView(mainScrollPosition);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        currentLanguage = (Language)EditorGUILayout.EnumPopup(currentLanguage, GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck()) { Repaint(); }
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField(T("mainTitle"), wordWrapTitleStyle);
        EditorGUILayout.Space(2);

        EditorGUI.BeginChangeCheck();
        renameMode = TranslatedEnumPopup(T("renameModeLabel"), renameMode);
        
        includeChildren = EditorGUILayout.ToggleLeft(T("includeChildrenLabel"), includeChildren);

        if(EditorGUI.EndChangeCheck())
        {
            UpdateProcessedObjects();
        }
        
        EditorGUILayout.Space(2);
        
        switch (renameMode)
        {
            case RenameMode.Sequential: DrawSequentialUI(); break;
            case RenameMode.ReplaceNumbering: DrawReplaceNumberingUI(); break;
            case RenameMode.Replace: DrawReplaceUI(); break;
            case RenameMode.AddAffix: DrawAddAffixUI(); break;
            case RenameMode.CaseChange: DrawCaseChangeUI(); break;
        }

        EditorGUILayout.Space();

        var operations = new List<(Object obj, string newName)>();
        var conflictingObjectIDs = new HashSet<EntityId>(); //Unity 6.6でGetInstanceID()が廃止されたためEntityIdに置き換え(2026/10/15)
        
        if (processedObjects.Length > 0)
        {
            var potentialTargets = new Dictionary<string, List<Object>>();
            
            for(int i = 0; i < processedObjects.Length; i++)
            {
                var obj = processedObjects[i];
                if (obj == null) continue;
                
                var originalName = GetOriginalName(obj);
                var newName = GenerateNewName(originalName, i);
                
                if (originalName != newName)
                {
                    operations.Add((obj, newName));
                }
                
                if (AssetDatabase.Contains(obj))
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    string directory = Path.GetDirectoryName(path);
                    string extension = Path.GetExtension(obj.name);
                    string targetFullPath = $"{directory}/{newName}{extension}".Replace('\\', '/');

                    if (!potentialTargets.ContainsKey(targetFullPath))
                    {
                        potentialTargets[targetFullPath] = new List<Object>();
                    }
                    potentialTargets[targetFullPath].Add(obj);
                }
            }
            
            foreach (var pair in potentialTargets)
            {
                if (pair.Value.Count > 1)
                {
                    foreach (var obj in pair.Value)
                    {
                        conflictingObjectIDs.Add(obj.GetEntityId());
                    }
                }
            }
        }
        
        GUILayout.Label(T("previewSection"), sectionHeaderStyle);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        if (processedObjects.Length > 0)
        {
            int displayCount = Mathf.Min(processedObjects.Length, 100); 
            for (int i = 0; i < displayCount; i++)
            {
                var obj = processedObjects[i];
                if (obj == null) continue;
                string originalName = GetOriginalName(obj);
                string newName = GenerateNewName(originalName, i);
                string labelText = (originalName != newName) ? $"{originalName}  →  {newName}" : originalName;

                if (conflictingObjectIDs.Contains(obj.GetEntityId()))
                {
                    string colorHex = EditorGUIUtility.isProSkin ? "FF8080" : "CC0000";
                    labelText = $"<color=#{colorHex}>[!] {labelText}</color>";
                }
                else if (originalName != newName)
                {
                    string colorHex = EditorGUIUtility.isProSkin ? "A0D0A0" : "003000";
                    labelText = $"<color=#{colorHex}>{labelText}</color>";
                }
                GUILayout.Label(labelText, wordWrapLabelStyle);
            }
            if(processedObjects.Length > displayCount)
            {
                EditorGUILayout.LabelField($"... and {processedObjects.Length - displayCount} more items.");
            }
        }
        else
        {
            EditorGUILayout.LabelField(T("previewPlaceholder"));
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space();
        
        bool hasConflicts = conflictingObjectIDs.Any();
        if (hasConflicts)
        {
            EditorGUILayout.HelpBox(T("nameConflictWarning"), MessageType.Error);
        }

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = undoStack.Count > 0;
        if (GUILayout.Button(T("undoButton"))) { UndoRename(); }
        GUI.enabled = redoStack.Count > 0;
        if (GUILayout.Button(T("redoButton"))) { RedoRename(); }
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        GUI.enabled = operations.Count > 0 && !hasConflicts;
        if (GUILayout.Button(string.Format(T("executeButton"), operations.Count), GUILayout.Height(30)))
        {
            ExecuteRename(operations);
        }
        GUI.enabled = true;

        EditorGUILayout.EndScrollView();
    }

    #region UI Drawing Methods
    private void DrawSequentialUI()
    {
        EditorGUILayout.HelpBox(T("SequentialHelp"), MessageType.Info);
        EditorGUILayout.Space(2);
        baseName = EditorGUILayout.TextField(T("baseNameLabel"), baseName);
        digitCount = EditorGUILayout.IntSlider(T("digitCountLabelWithRemove"), digitCount, 0, 8);

        EditorGUI.BeginDisabledGroup(digitCount == 0);
        startNumber = EditorGUILayout.IntField(T("startNumberLabel"), startNumber);
        if (startNumber < 1) startNumber = 1;
        separator = EditorGUILayout.TextField(T("separatorLabel"), separator);
        insertSequentialSeparator = EditorGUILayout.Toggle(T("insertSeparatorLabel"), insertSequentialSeparator);
        EditorGUI.EndDisabledGroup();
    }

    private void DrawReplaceNumberingUI()
    {
        EditorGUILayout.HelpBox(T("ReplaceNumberingHelp"), MessageType.Info);
        EditorGUILayout.Space(2);
        digitCount = EditorGUILayout.IntSlider(T("digitCountLabelWithRemove"), digitCount, 0, 8);
        separator = EditorGUILayout.TextField(T("separatorLabel"), separator);
    }

    private void DrawReplaceUI()
    {
        EditorGUILayout.HelpBox(T("ReplaceHelp"), MessageType.Info);
        bool isRegexValid = true;
        if (!string.IsNullOrEmpty(searchString))
        {
            try { new Regex(searchString); }
            catch (ArgumentException) { isRegexValid = false; }
        }
        if (!isRegexValid)
        {
            EditorGUILayout.HelpBox(T("invalidRegexWarning"), MessageType.Warning);
        }
        EditorGUILayout.Space(2);
        EditorGUILayout.BeginHorizontal();
        searchString = EditorGUILayout.TextField(T("searchStringLabel"), searchString);
        var dropdownContent = new GUIContent("...", T("snippetTooltip"));
        if (EditorGUILayout.DropdownButton(dropdownContent, FocusType.Passive, EditorStyles.miniButton, GUILayout.Width(30), GUILayout.Height(EditorGUIUtility.singleLineHeight)))
        {
            var menu = new GenericMenu();
            foreach (var snippet in regexSnippets)
            {
                menu.AddItem(new GUIContent(T(snippet.key)), false, () => {
                    Undo.RecordObject(this, "Apply Snippet");
                    searchString = snippet.search;
                    replaceString = snippet.replace;
                    GUI.FocusControl(null);
                    Repaint();
                });
            }
            menu.ShowAsContext();
        }
        EditorGUILayout.EndHorizontal();
        replaceString = EditorGUILayout.TextField(T("replaceStringLabel"), replaceString);
    }

    private void DrawAddAffixUI()
    {
        EditorGUILayout.HelpBox(T("AddAffixHelp"), MessageType.Info);
        EditorGUILayout.Space(2);
        
        affixString = EditorGUILayout.TextField(T("affixStringLabel"), affixString);
        separator = EditorGUILayout.TextField(T("separatorLabel"), separator);
        insertAffixSeparator = EditorGUILayout.Toggle(T("insertSeparatorLabel"), insertAffixSeparator);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(T("affixPositionLabel"), GUILayout.Width(EditorGUIUtility.labelWidth - 4));
        affixPosition = TranslatedEnumPopup("", affixPosition, true);
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawCaseChangeUI()
    {
        EditorGUILayout.HelpBox(T("CaseChangeHelp"), MessageType.Info);
        EditorGUILayout.Space(2);
        caseType = TranslatedEnumPopup(T("caseTypeLabel"), caseType);
    }
    #endregion
    
    #region Core Logic
    private string GetOriginalName(Object obj)
    {
        string name = obj.name;
        bool isAsset = AssetDatabase.Contains(obj);
        if (isAsset)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (!AssetDatabase.IsValidFolder(path))
            {
                return Path.GetFileNameWithoutExtension(name);
            }
        }
        return name;
    }

    private string GenerateNewName(string originalName, int index)
    {
        switch (renameMode)
        {
            case RenameMode.Sequential:
                return ProcessSequential(index);
            case RenameMode.ReplaceNumbering:
                return ProcessReplaceNumbering(originalName);
            case RenameMode.Replace:
                return ProcessReplace(originalName);
            case RenameMode.AddAffix:
                return ProcessAddAffix(originalName);
            case RenameMode.CaseChange:
                return ProcessCaseChange(originalName);
            default:
                return originalName;
        }
    }

    private string ProcessSequential(int index)
    {
        if (digitCount == 0)
        {
            return baseName;
        }
        
        string numberString = (startNumber + index).ToString("D" + digitCount);
        if (insertSequentialSeparator && !string.IsNullOrEmpty(baseName))
            return baseName + separator + numberString;
        else
            return baseName + numberString;
    }

    private string ProcessReplaceNumbering(string originalName)
    {
        var match = Regex.Match(originalName, @"^(.*?)((_|-|\s)?\d+| \(\d+\))$");
        
        if (match.Success)
        {
            string basePart = match.Groups[1].Value;
            
            if (digitCount == 0)
            {
                return basePart;
            }
            
            string numberPart = match.Groups[2].Value;
            var numMatch = Regex.Match(numberPart, @"\d+");
            
            if (numMatch.Success && int.TryParse(numMatch.Value, out int num))
            {
                string newNumStr = num.ToString("D" + digitCount);
                return basePart + separator + newNumStr;
            }
        }
        
        return originalName;
    }
    
    private string ProcessAddAffix(string originalName)
    {
        if (string.IsNullOrEmpty(affixString))
        {
            return originalName;
        }

        if (affixPosition == AffixPosition.Prefix)
        {
            return insertAffixSeparator ? affixString + separator + originalName : affixString + originalName;
        }
        else 
        {
            return insertAffixSeparator ? originalName + separator + affixString : originalName + affixString;
        }
    }

    private (string core, string suffix) SplitUnitySuffix(string name)
    {
        var match = Regex.Match(name, @" \(\d+\)$");
        if (match.Success)
        {
            return (name.Substring(0, match.Index), match.Value);
        }
        return (name, "");
    }

    private string ProcessReplace(string originalName)
    {
        if (string.IsNullOrEmpty(searchString)) return originalName;
        
        string newName;
        try
        {
            newName = Regex.Replace(originalName, searchString, replaceString);
        }
        catch (ArgumentException)
        {
            newName = originalName.Replace(searchString, replaceString);
        }
        
        if (string.IsNullOrEmpty(newName) && !string.IsNullOrEmpty(originalName))
        {
            return originalName;
        }

        return newName;
    }
    
    private string ProcessCaseChange(string originalName)
    {
        var (core, unitySuffix) = SplitUnitySuffix(originalName);
        return ToCase(core, caseType) + unitySuffix;
    }
    
    private string ToCase(string input, CaseType type)
    {
        var words = Regex.Matches(input, @"([A-Z]?[a-z]+)|([A-Z]+(?![a-z]))|(\d+)")
                         .OfType<Match>()
                         .Select(m => m.Value);

        switch (type)
        {
            case CaseType.UPPERCASE: return input.ToUpper();
            case CaseType.lowercase: return input.ToLower();
            case CaseType.PascalCase: 
                return string.Concat(words.Select(w => {
                    if(string.IsNullOrEmpty(w)) return "";
                    if(char.IsDigit(w[0])) return w;
                    return char.ToUpper(w[0]) + w.Substring(1).ToLower();
                }));
            case CaseType.camelCase:
                var wordList = words.ToList();
                if (!wordList.Any()) return "";
                var firstWord = wordList.First();
                if(string.IsNullOrEmpty(firstWord)) return "";
                string firstPart = char.IsDigit(firstWord[0]) ? firstWord : firstWord.ToLower();
                
                return firstPart + string.Concat(wordList.Skip(1).Select(w => {
                    if(string.IsNullOrEmpty(w)) return "";
                    if(char.IsDigit(w[0])) return w;
                    return char.ToUpper(w[0]) + w.Substring(1).ToLower();
                }));
            case CaseType.snake_case: 
                return string.Join("_", words.Select(w => w.ToLower()));
            default: return input;
        }
    }

    private void OnSelectionChange()
    {
        UpdateProcessedObjects();
        Repaint();
    }

    private void OnFocus()
    {
        UpdateProcessedObjects();
        Repaint();
    }

    private void UpdateProcessedObjects()
    {
        var selection = Selection.objects;

        if (!includeChildren)
        {
            if (selection.Length <= 1)
            {
                processedObjects = selection;
                return;
            }
            
            var alphabeticallySorted = selection.OrderBy(o => o.name, StringComparer.Ordinal).ToArray();
            bool isUnityDefaultSort = selection.SequenceEqual(alphabeticallySorted);
            processedObjects = isUnityDefaultSort ? selection.OrderBy(obj => CreateSortableKey(obj.name)).ToArray() : selection;
        }
        else
        {
            var allObjects = new List<Object>();
            foreach (var obj in selection)
            {
                if (obj is GameObject go)
                {
                    var transforms = go.GetComponentsInChildren<Transform>(true); 
                    allObjects.AddRange(transforms.Select(t => t.gameObject as Object));
                }
                else
                {
                    allObjects.Add(obj);
                }
            }
            
            var roots = selection.Where(o => o is GameObject).Select(o => (o as GameObject).transform.root).Distinct();
            var sortedList = allObjects.Distinct().OrderBy(o => {
                if (o is GameObject go)
                {
                    return string.Join("/", go.transform.GetComponentsInParent<Transform>().Reverse().Select(t => t.GetSiblingIndex().ToString("D4")));
                }
                return o.name;
            }).ToList();
            
            processedObjects = sortedList.ToArray();
        }
    }
    
    private string CreateSortableKey(string name)
    {
        return Regex.Replace(name, @"\d+", match => match.Value.PadLeft(10, '0'));
    }

    private void ExecuteRename(List<(Object obj, string newName)> operations)
    {
        if (!EditorUtility.DisplayDialog(T("confirmDialogTitle"),
            string.Format(T("confirmDialogMessage"), operations.Count),
            T("yes"), T("no")))
        {
            return;
        }
        var originalNames = new List<KeyValuePair<Object, string>>();
        foreach (var op in operations)
        {
            if (op.obj != null)
                originalNames.Add(new KeyValuePair<Object, string>(op.obj, op.obj.name));
        }
        undoStack.Push(originalNames);
        redoStack.Clear();
        PerformRename(operations.Select(op => new KeyValuePair<Object, string>(op.obj, op.newName)).ToList());
    }

    private void UndoRename()
    {
        if (undoStack.Count == 0) return;
        var lastState = undoStack.Pop();
        var currentState = new List<KeyValuePair<Object, string>>();
        foreach (var pair in lastState)
        {
            if (pair.Key != null)
                currentState.Add(new KeyValuePair<Object, string>(pair.Key, pair.Key.name));
        }
        redoStack.Push(currentState);
        PerformRename(lastState);
    }
    
    private void RedoRename()
    {
        if (redoStack.Count == 0) return;
        var nextState = redoStack.Pop();
        var currentState = new List<KeyValuePair<Object, string>>();
        foreach (var pair in nextState)
        {
            if (pair.Key != null)
                currentState.Add(new KeyValuePair<Object, string>(pair.Key, pair.Key.name));
        }
        undoStack.Push(currentState);
        PerformRename(nextState);
    }
    
    private void PerformRename(List<KeyValuePair<Object, string>> renamePairs)
    {
        var projectAssets = renamePairs.Select(p => p.Key).Where(obj => obj != null && AssetDatabase.Contains(obj)).ToList();
        if (projectAssets.Any())
        {
            AssetDatabase.StartAssetEditing();
        }
        try
        {
            foreach (var pair in renamePairs)
            {
                var obj = pair.Key;
                if (obj == null) continue;
                string originalFullName = obj.name;
                string newNameCore = pair.Value;
                if (AssetDatabase.Contains(obj))
                {
                    string path = AssetDatabase.GetAssetPath(obj);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        if (originalFullName == newNameCore) continue;
                        AssetDatabase.RenameAsset(path, newNameCore);
                    }
                    else
                    {
                        string extension = Path.GetExtension(originalFullName);
                        string newFullName = newNameCore + extension;
                        if (originalFullName == newFullName) continue;
                        
                        string error = AssetDatabase.RenameAsset(path, newNameCore);
                        if (!string.IsNullOrEmpty(error))
                        {
                            Debug.LogErrorFormat(T("logError"), path, error);
                        }
                    }
                }
                else
                {
                    if (originalFullName == newNameCore) continue;
                    Undo.RecordObject(obj, "Batch Rename (via tool)");
                    obj.name = newNameCore;
                }
            }
        }
        finally
        {
            if (projectAssets.Any())
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        Debug.Log(T("logComplete"));
        UpdateProcessedObjects();
        Repaint();
    }
    #endregion
}