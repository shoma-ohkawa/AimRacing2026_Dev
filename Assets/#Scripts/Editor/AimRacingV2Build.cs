using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

//明示的に呼び出した場合だけ指定先へWindows検証版を出力するクラス
public static class AimRacingV2Build
{
    //別の出力先を誤って上書きしないための今回の保存先
    const string OutputDirectory = @"C:\Users\prash\Desktop\AimRacingV2";

    //コンパイルと未保存編集を確認してから依頼されたビルドを一度だけ実行する関数
    public static void Run()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Playを停止してからビルドしてください。"); }
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty) { throw new InvalidOperationException("未保存シーンがあるためビルドを中断しました。"); }
            }

            //有効なシーンと現在の保存済み設定をそのまま使用する
            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) { throw new InvalidOperationException("ビルド対象シーンがありません。"); }
            Directory.CreateDirectory(OutputDirectory);
            string executable = Path.Combine(OutputDirectory, "AimRacingV2.exe");
            if (File.Exists(executable)) { throw new InvalidOperationException("同名の実行ファイルがあるため上書きを中断しました。"); }

            //開発用の自動接続を含めない通常のWindows64ビット版を生成する
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            Debug.Log("[AimRacingV2Build] START " + executable);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            string summary = $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\n"
                + $"Warnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\n"
                + $"Bytes: {report.summary.totalSize}\nUnity: {Application.unityVersion}\n"
                + "Scenes:\n" + string.Join("\n", scenes);
            File.WriteAllText(Path.Combine(OutputDirectory, "BuildResult.txt"), summary);
            Debug.Log("[AimRacingV2Build] " + summary);
            if (report.summary.result != BuildResult.Succeeded) { throw new InvalidOperationException("Windowsビルドが失敗しました。"); }
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            throw;
        }
    }
}
