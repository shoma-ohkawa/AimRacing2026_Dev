#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

//再生開始時に残った一時停止を解除して白画面を防ぐクラス
[InitializeOnLoad]
static class AimRacingPlayModeGuard
{
    //Unityエディターの再生状態変更を監視する関数
    static AimRacingPlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += MaintainKeyboardDebugTimeScale;
    }

    //再生開始時だけエディターの残留一時停止を解除する関数
    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        //==========================
        //前回の一時停止状態が残ったまま再生してゲーム画面が白くなることを防ぐ処理
        if (state == PlayModeStateChange.EnteredPlayMode && EditorApplication.isPaused) { EditorApplication.isPaused = false; }
        //==========================
    }

    //G923未接続でもキーボード検証中の車両物理を継続する関数
    static void MaintainKeyboardDebugTimeScale()
    {
        if (!EditorApplication.isPlaying || Time.timeScale > 0f) { return; }
        bool isMapPlaying = Object.FindFirstObjectByType<IntroState>() != null ||
            Object.FindFirstObjectByType<RacingState>() != null;
        if (!isMapPlaying) { return; }

        //==========================
        //G923未接続時の時間停止でイントロ後の画面が固まらないようEditorだけ等速へ戻す処理
        Time.timeScale = 1f;
        //==========================
        Debug.Log("キーボード検証を続けるためEditorの時間停止を解除しました。");
    }
}
#endif
