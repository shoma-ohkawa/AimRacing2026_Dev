using System.Collections.Generic;
using UnityEngine;

//Editorとビルドで同じ運転処理を明示的に開始するクラス
public static partial class TrackDriveSession
{
    //一時停止中に自動入力が更新されないよう判定する値
    static bool IsDriverPaused => Time.timeScale <= 0f;

    //ビルドで状態遷移通知を重複購読しないための状態
    static bool runtimeSubscribed;

    //開始キーから選択した変速方式で通常のメニュー遷移を始める関数
    public static void BeginRuntime(bool manual)
    {
        if (!Application.isPlaying || GameFlowRunner.Instance == null || GameManager.Instance == null) { return; }
        ArmRuntime(manual);
        if (SoundManager.Instance != null) { SoundManager.Instance.StopBGM(); }
        GameManager.Instance.StopG923FFB();
        GameFlowRunner.Instance.EmergencyReset(GameStateId.Load);
        Debug.Log("[TrackDrive] shared driver requested: " + (manual ? "MT" : "AT"));
    }

    //リセットを行わず、今の流れのままOpening・Menuの自動入力とRacingでの自動走行を予約する関数(周回の自動繰り返しが、Splashへ戻った所で次の周を始めるために使う)
    public static void ArmRuntime(bool manual)
    {
        if (!Application.isPlaying || GameFlowRunner.Instance == null || GameManager.Instance == null) { return; }
        Stop();
#if !UNITY_EDITOR
        if (!runtimeSubscribed)
        {
            GameFlowRunner.OnStateFadeInComplete += OnGameStateFadeInComplete;
            runtimeSubscribed = true;
        }
#endif
        profile = manual ? 3 : 2;
        earlyTurn = false;
        driftChallenge = true;
        previousTrackTuning = false;
        leverTest = false;
        analogTest = true;
        TrackDrivePreferences.SetBool(HairpinTestKey, true);
        armed = true;
        TrackDrivePreferences.SetBool("Sep11RecheckArmed", true);
        autoReturnFromResult = true; //この走行のResultでは、自動でEnterを押して戻る(走行が終わるとarmedは下りるため、別の状態で覚える)
        resultReturnStep = -1;
    }

    //Resultからの自動復帰の予約を取り消す関数(周回の繰り返しを中止したときに使う)
    public static void CancelResultReturn()
    {
        autoReturnFromResult = false;
        resultReturnStep = -1;
    }

    //Editorの更新登録と重複せずビルドだけで共通処理を進める関数
    public static void RuntimeTick()
    {
#if !UNITY_EDITOR
        UpdateMenuInputs();
        UpdateRecheck();
        Tick();
#endif
    }
}

//Editorの再読み込み保持とビルド内の一時設定を同じ窓口で扱うクラス
static class TrackDrivePreferences
{
    //ビルド中だけ選択した検証条件を保持し保存済み設定を変更しない辞書
    static readonly Dictionary<string, bool> flags = new Dictionary<string, bool>();

    //現在の実行環境から検証条件を読む関数
    public static bool GetBool(string key, bool fallback)
    {
#if UNITY_EDITOR
        return UnityEditor.SessionState.GetBool(key, fallback);
#else
        return flags.TryGetValue(key, out bool value) ? value : fallback;
#endif
    }

    //現在の実行環境へ検証条件だけを設定する関数
    public static void SetBool(string key, bool value)
    {
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool(key, value);
#else
        flags[key] = value;
#endif
    }

#if UNITY_EDITOR
    //再生開始時にEditorで選んだ走行条件を読み戻す関数
    public static int GetInt(string key, int fallback) { return UnityEditor.SessionState.GetInt(key, fallback); }

    //再生開始前の走行条件をEditorの再読み込みへ引き継ぐ関数
    public static void SetInt(string key, int value) { UnityEditor.SessionState.SetInt(key, value); }
#endif
}
