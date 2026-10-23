/*━━━━━━━━━
@file AimRacingAssetUpdateInstaller.cs
@brief Assets上書き後にResultシーンをBuild Settingsへ自動登録
@author 24CU0312 久場洸太
@date 2026/08/20
最終更新日 2026/09/04
@remarks シーン自体は再生成せず、ResultをBuild Settings末尾へ登録する(End.unity削除に伴い、End直前挿入ロジックを簡素化)
━━━━━━━━━*/

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AimRacingAssetUpdateInstaller
{
    private const string k_ResultScenePath = "Assets/!Scenes/Result.unity"; //Resultシーンの配置先

    /// <summary>Unity起動時にResultの自動登録が必要か確認します。</summary>
    static AimRacingAssetUpdateInstaller()
    {
        ApplyIfAvailable();
    }

    /// <summary>ResultをBuild Settings末尾へ登録し、実機用のシーン遷移順を確定します。</summary>
    [MenuItem("AimRacing/実機更新/ResultをBuild Settingsへ登録")]
    public static void Apply()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ResultScenePath) == null)
        {
            Debug.LogError("Resultシーンが見つかりません: " + k_ResultScenePath);
            return;
        }

        EditorBuildSettingsScene[] currentScenes = EditorBuildSettings.scenes; //現在のBuild Settings
        List<EditorBuildSettingsScene> updatedScenes = new List<EditorBuildSettingsScene>(); //更新後のBuild Settings

        for (int i = 0; i < currentScenes.Length; ++i)
        {
            if (currentScenes[i].path == k_ResultScenePath)
            {
                continue;
            }
            updatedScenes.Add(currentScenes[i]);
        }

        updatedScenes.Add(new EditorBuildSettingsScene(k_ResultScenePath, true));
        EditorBuildSettings.scenes = updatedScenes.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("AimRacing実機更新: ResultをBuild Settings末尾へ登録しました。");
    }

    /// <summary>初回Import中でもResultの読込み完了後だけ登録処理を実行します。</summary>
    public static void ApplyIfAvailable()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(k_ResultScenePath) == null) { return; }

        Apply();
    }

}

public sealed class AimRacingAssetUpdatePostprocessor : AssetPostprocessor
{
    private const string k_ResultScenePath = "Assets/!Scenes/Result.unity"; //登録対象のResultシーン

    /// <summary>ResultのImport完了を検出し、初回導入時の読込み順に関係なく登録します。</summary>
    private static void OnPostprocessAllAssets(
        string[] _importedassets,
        string[] _deletedassets,
        string[] _movedassets,
        string[] _movedfromassetpaths)
    {
        for (int i = 0; i < _importedassets.Length; ++i)
        {
            if (_importedassets[i] != k_ResultScenePath) { continue; }

            AimRacingAssetUpdateInstaller.ApplyIfAvailable();
            return;
        }
    }
}
#endif
