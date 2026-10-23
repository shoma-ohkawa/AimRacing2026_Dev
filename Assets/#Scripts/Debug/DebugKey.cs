/*------------------------------------------------------------------
* ファイル名：DebugKey
* 概要：DebugKeyの実装
* 担当者：西林竜輝
* 作成日：08/04
* 
-------------------------------------------------------------------*/
//更新履歴
/*
* 2022/08/04 西林竜輝　F1～F5のデバッグキーの設定
* 2026/10/19 大川翔生　Uキー(MT自動走行を指定の周回数だけ自動で繰り返す)を追加
* 2026/10/19 大川翔生　KeysLocked(自動周回の計測中は、Esc以外のデバッグキーを受け付けない)を追加
*/
//-----------------------------------------------------------------------------------

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum DebugKeyAction
{
    QuitApplication,
    ReturnToTitle,
    StartInGame,
    PauseEditor,
    PauseGame,
    ChangeTransmission,
    ToggleWizmoState,
    ToggleScreenLog,
    ToggleDeviceInfo,
    ShutdownWizmo,
    ReconnectWizmo,
    DisableRankingNetwork,
    EnableRankingNetwork,
    SwitchCamera,
    ToggleInputDebug,
    ResetCar,
    MoveCarToShortcut,
    SkipMovie,
    CompleteRace,
    SkipNameEntry,
    PlayHorn,
    StartAutoDemo,
    //既存の列挙値を変えずMT自動走行を追加する識別子
    StartAutoDemoManual,
    //MT自動走行を指定の周回数(既定10周)だけ自動で繰り返す識別子。もう一度押すと中止する
    StartAutoDemoLoop
}

public class DebugKey : MonoBehaviour
{
    private const float k_PausedTimeScale = 0.0f; //ゲーム停止時の時間倍率
    private const float k_NormalTimeScale = 1.0f; //ゲーム進行時の時間倍率
    private const float k_HornCooldownSeconds = 4.08f; //yankie god horn.wavの再生時間(連打防止用のクールダウン)

    private bool m_bPause; // 一時停止中か
    private float m_hornCooldownEndTime; //クラクションを次に再生できる時刻

    //Esc以外のデバッグキーを受け付けないか。自動周回の計測中(PerfAutoLapRunner)だけtrueになる。計測の途中でキーが押されて、周回が乱れるのを防ぐ
    public static bool KeysLocked { get; set; }

    /// <summary>
    /// ポーズ状態を強制的に解除します。Result→Splash等のセッション区切りで、
    /// m_bPauseとTime.timeScaleの不整合(片方だけ取り残る等)を防ぐために呼びます。
    /// </summary>
    public void ResetPauseState()
    {
        m_bPause = false;
        Time.timeScale = k_NormalTimeScale;
    }

    public static bool GetKeyDown(DebugKeyAction _action)
    {
        return GetKeyDown(GetKeyCode(_action));
    }

    public static bool GetKeyDown(KeyCode _keyCode)
    {
#if ENABLE_INPUT_SYSTEM
        if (GetInputSystemKeyDown(_keyCode)) { return true; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(_keyCode);
#else
        return false;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool GetInputSystemKeyDown(KeyCode _keyCode)
    {
        Keyboard keyboard = Keyboard.current; //現在使用中のキーボード
        if (keyboard == null) { return false; }

        switch (_keyCode)
        {
            case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
            case KeyCode.F1: return keyboard.f1Key.wasPressedThisFrame;
            case KeyCode.F2: return keyboard.f2Key.wasPressedThisFrame;
            case KeyCode.F3: return keyboard.f3Key.wasPressedThisFrame;
            case KeyCode.F4: return keyboard.f4Key.wasPressedThisFrame;
            case KeyCode.F5: return keyboard.f5Key.wasPressedThisFrame;
            case KeyCode.F6: return keyboard.f6Key.wasPressedThisFrame;
            case KeyCode.F8: return keyboard.f8Key.wasPressedThisFrame;
            case KeyCode.F9: return keyboard.f9Key.wasPressedThisFrame;
            case KeyCode.Y: return keyboard.yKey.wasPressedThisFrame;
            case KeyCode.U: return keyboard.uKey.wasPressedThisFrame;
            case KeyCode.F11: return keyboard.f11Key.wasPressedThisFrame;
            case KeyCode.F12: return keyboard.f12Key.wasPressedThisFrame;
            case KeyCode.Insert: return keyboard.insertKey.wasPressedThisFrame;
            case KeyCode.PageUp: return keyboard.pageUpKey.wasPressedThisFrame;
            case KeyCode.PageDown: return keyboard.pageDownKey.wasPressedThisFrame;
            case KeyCode.Home: return keyboard.homeKey.wasPressedThisFrame;
            case KeyCode.End: return keyboard.endKey.wasPressedThisFrame;
            case KeyCode.Delete: return keyboard.deleteKey.wasPressedThisFrame;
            case KeyCode.Alpha0: return keyboard.digit0Key.wasPressedThisFrame;
            case KeyCode.KeypadPeriod: return keyboard.numpadPeriodKey.wasPressedThisFrame;
            case KeyCode.KeypadPlus: return keyboard.numpadPlusKey.wasPressedThisFrame;
            case KeyCode.KeypadMinus: return keyboard.numpadMinusKey.wasPressedThisFrame;
            case KeyCode.KeypadMultiply: return keyboard.numpadMultiplyKey.wasPressedThisFrame;
            default: return false;
        }
    }
#endif

    public static KeyCode GetKeyCode(DebugKeyAction _action)
    {
        switch (_action)
        {
            case DebugKeyAction.QuitApplication: return KeyCode.Escape;
            case DebugKeyAction.ReturnToTitle: return KeyCode.F1;
            case DebugKeyAction.StartInGame: return KeyCode.F2;
            case DebugKeyAction.PauseEditor: return KeyCode.Insert;
            case DebugKeyAction.PauseGame: return KeyCode.F4;
            case DebugKeyAction.ChangeTransmission: return KeyCode.F5;
            case DebugKeyAction.ToggleWizmoState: return KeyCode.PageUp;
            case DebugKeyAction.ToggleScreenLog: return KeyCode.F8;
            case DebugKeyAction.ToggleDeviceInfo: return KeyCode.KeypadPeriod;
            case DebugKeyAction.ShutdownWizmo: return KeyCode.F11;
            case DebugKeyAction.EnableRankingNetwork: return KeyCode.KeypadPlus;
            case DebugKeyAction.ReconnectWizmo: return KeyCode.F12;
            case DebugKeyAction.DisableRankingNetwork: return KeyCode.KeypadMinus;
            case DebugKeyAction.SwitchCamera: return KeyCode.Home;
            case DebugKeyAction.ToggleInputDebug: return KeyCode.Alpha0;
            case DebugKeyAction.ResetCar: return KeyCode.Delete;
            case DebugKeyAction.MoveCarToShortcut: return KeyCode.End;
            case DebugKeyAction.SkipMovie: return KeyCode.PageDown;
            case DebugKeyAction.CompleteRace: return KeyCode.KeypadMultiply;
            case DebugKeyAction.SkipNameEntry: return KeyCode.F3;
            case DebugKeyAction.PlayHorn: return KeyCode.Alpha0;
            case DebugKeyAction.StartAutoDemo: return KeyCode.F9;
            case DebugKeyAction.StartAutoDemoManual: return KeyCode.Y;
            case DebugKeyAction.StartAutoDemoLoop: return KeyCode.U;
            default: return KeyCode.None;
        }
    }

    public static bool IsCompleteRaceKey(KeyCode _keyCode)
    {
        return _keyCode == GetKeyCode(DebugKeyAction.CompleteRace);
    }

    private void Update()
    {
        if (GetKeyDown(DebugKeyAction.QuitApplication)) { Application.Quit(); }

        //自動周回の計測中は、Esc以外のデバッグキーを受け付けない。自動走行の更新(Tick)だけは進める
        if (KeysLocked)
        {
            AutoDemoDriver.Tick();
            return;
        }

        if (GetKeyDown(DebugKeyAction.ReturnToTitle))
        {
            SoundManager.Instance.StopBGM();
            GameManager.Instance.StopG923FFB();
            GameFlowRunner.Instance.EmergencyReset(GameStateId.Load); //LoadでManager/MapEnvironmentの安定待ちとキャッシュリフレッシュを行ってからSplashへ進む
        }
        if (GetKeyDown(DebugKeyAction.StartInGame))
        {
            SoundManager.Instance.StopBGM();
            GameManager.Instance.StopG923FFB();
            GameFlowRunner.Instance.EmergencyReset(GameStateId.Racing);
        }
#if UNITY_EDITOR
        if (GetKeyDown(DebugKeyAction.PauseEditor))
        {
            UnityEditor.EditorApplication.isPaused = true;
        }
#endif
        if (GetKeyDown(DebugKeyAction.PauseGame))
        {
            m_bPause = !m_bPause;
            if (m_bPause)
            {
                SoundManager.Instance.MuteBGM();
                Time.timeScale = k_PausedTimeScale;
            }
            else
            {
                SoundManager.Instance.ResumeBGM();
                Time.timeScale = k_NormalTimeScale;
            }
        }
        if (GetKeyDown(DebugKeyAction.ToggleWizmoState))
        {
            GameManager.Instance.ShowStateWIZMO = !GameManager.Instance.ShowStateWIZMO;
        }
        if (GetKeyDown(DebugKeyAction.ToggleDeviceInfo))
        {
            DebugDevice.Instance.ShowLogInGame = !DebugDevice.Instance.ShowLogInGame;
        }
        if (GetKeyDown(DebugKeyAction.PlayHorn) && IsHornKeyEnabled() && Time.time >= m_hornCooldownEndTime)
        {
            SoundManager.Instance.PlaySE(SoundManager.SE_Type.YankieGodHorn);
            m_hornCooldownEndTime = Time.time + k_HornCooldownSeconds; //再生し終わるまで次の再生を防止する
        }
        if (GetKeyDown(DebugKeyAction.StartAutoDemo)) { AutoDemoDriver.Start(); }
        //文字入力中のYでデモを開始せず既存のF9によるAT操作も維持する
        if (GetKeyDown(DebugKeyAction.StartAutoDemoManual) && IsManualDemoKeyEnabled()) { AutoDemoDriver.Start(true); }
        //Uキー: 周回の自動繰り返しを開始する(Yキーと同じ場面だけ)。繰り返し中は、どの場面でも中止できる
        if (GetKeyDown(DebugKeyAction.StartAutoDemoLoop))
        {
            if (AutoDemoDriver.IsLooping) { AutoDemoDriver.CancelLoop(); }
            else if (IsManualDemoKeyEnabled()) { AutoDemoDriver.StartLoop(true); }
        }
        AutoDemoDriver.Tick(); //自動進行中の処理はBuild上でも動くようUnityEditorに依存しない別クラスへ委譲している
    }

    //名前入力とメニュー選択を妨げず開始画面か走行画面だけでMTデモを受け付ける関数
    private static bool IsManualDemoKeyEnabled()
    {
        if (GameFlowRunner.Instance == null) { return false; }
        GameStateId state = GameFlowRunner.Instance.CurrentId;
        return state == GameStateId.Splash || state == GameStateId.Opening || state == GameStateId.Racing;
    }

    /// <summary>
    /// クラクションのデバッグキーが有効な状況(MenuシーンまたはRacingState中)かを判定する関数
    /// </summary>
    private static bool IsHornKeyEnabled()
    {
        if (GameManager.Instance == null) { return false; }

        GameStateId state = GameManager.Instance.CurrentGameState;
        return state == GameStateId.Menu || state == GameStateId.Racing;
    }
}
