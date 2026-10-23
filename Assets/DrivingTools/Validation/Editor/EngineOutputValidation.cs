using System;
using FMODUnity;
using UnityEditor;
using UnityEngine;

//ターボを消した状態で既存バンクのエンジン出力とレブ後の復帰を検査するクラス
public static class EngineOutputValidation
{
    //保存済みの物理設定を変更せず既存のTrack AT走行検証を開始する関数
    [MenuItem("Tools/検証/音/Run Track AT Drive")]
    public static void RunTrackDrive() { TrackDriveSession.RunFromWindow(2, false, false, false, false); }
    //音声更新と独立して本番のTrack補助計算の回帰を検査する関数
    [MenuItem("Tools/検証/音/Track Regression")]
    public static void ValidateTrack() { TrackDriftValidation.Run(); }
    //既存バンクへバックタービン専用補正が実際に接続されるか測定する状態
    static bool backTest;
    //アンチラグ補正をバックタービンと別の条件で比較する検査状態
    static bool antiTest;
    static object backGain;
    static System.Reflection.MethodInfo backTick;
    //エンジンとアンチラグの同時再生で補正前後のピークを比較する関数
    [MenuItem("Tools/検証/音/Antilag Gain")]
    public static void RunAntilagGain()
    {
        if (EditorApplication.isPlaying) { return; }
        RunBackGain();
        (backGain as IDisposable)?.Dispose();
        backGain = Activator.CreateInstance(typeof(DriveSound).Assembly.GetType("BackTurbinGain"), new object[] { "str t yar" });
        antiTest = true;
        Begin();
    }

    //単独音とエンジンを含む音で補正前後の出力を比較する関数
    [MenuItem("Tools/検証/音/Back Turbo Gain")]
    public static void RunBackGain()
    {
        if (EditorApplication.isPlaying) { return; }
        Run();
        backTest = true;
        var type = typeof(DriveSound).Assembly.GetType("BackTurbinGain");
        backGain = Activator.CreateInstance(type, true);
        backTick = type.GetMethod("Tick");
        stage = 0;
        Begin();
    }
    //回転数が変わってもタービンの開始音程と上限音程が一致することを検査する関数
    [MenuItem("Tools/検証/音/Turbo Pitch Table")]
    public static void ValidateTurboTable()
    {
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var calculate = typeof(DriveSound).GetMethod("CalculateTurboParameter", flags);
        var bankPitch = typeof(DriveSound).GetMethod("GetBankTurboRPMPitch", flags);
        int cases = 0;
        foreach (float spool in new[] { 0.01f, 0.25f, 0.5f, 0.75f, 1f })
        {
            float expected = float.NaN;
            foreach (float rpm in new[] { 2500f, 3000f, 4500f, 5500f, 7093.3054f, 7500f, 8200f })
            {
                float parameter = (float)calculate.Invoke(null, new object[] { spool, rpm, 2500f });
                float pitch = -11f + 19f * parameter + (float)bankPitch.Invoke(null, new object[] { rpm });
                if (!float.IsNaN(expected) && Mathf.Abs(pitch - expected) > 0.001f) { throw new Exception("タービン音程がRPMで変化しています"); }
                expected = pitch;
                cases++;
            }
        }
        Debug.Log($"[TurboPitchTable] PASS cases={cases} rpm=2500..8200");
    }
    //検査中のイベントを終了時に必ず解放するための参照
    static FMOD.Studio.EventInstance sound;
    //各回転域の出力を別々に測るための条件一覧
    static readonly float[] speeds = { 1200f, 4000f, 8000f, 8200f, 4000f, 4000f };
    //時間経過と出力の最大値を集計する検査状態
    static int stage;
    static double began;
    static float peak;
    //切り替え直後を除いた平均出力を比較するための二乗和と測定数
    static double squareSum;
    static int samples;
    //単独出力と同時再生を変更前後で比較するための検査モード
    static bool balance;

    //エンジンを基準にターボとミッションの埋もれ具合を同じ回転域で測る関数
    [MenuItem("Tools/検証/音/Engine Mix Balance")]
    public static void RunBalance()
    {
        Run();
        if (!sound.isValid()) { return; }
        balance = true;
        stage = 0;
        Begin();
    }

    //走行エンジンだけを鳴らし無音になる回転域を調べる関数
    [MenuItem("Tools/検証/音/Engine Output")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("停止してから音源検査を実行してください。"); return; }
        Stop();
        balance = false;
        EditorUtils.LoadPreviewBanks();
        Check(EditorUtils.System.getEvent("event:/Drive", out var description));
        Check(description.createInstance(out sound));
        foreach (string name in new[] { "MISSIONVOL", "TURBOVOL", "ROADVOL", "GEARUPVOL", "GEARDOWNVOL", "BREAKVOL" }) { Send(name, 0f); }
        Send("ENGINEVOL", 0.895f);
        Send("MUTE", 0f);
        Send("GEAR", 3f);
        Send("SPEED", 100f);
        stage = 0;
        Begin();
        Check(sound.start());
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }

    //同じイベントを使い上限から通常走行へ戻ったときの消音残留も検査する関数
    static void Begin()
    {
        if (backTest)
        {
            Send("RPM", antiTest ? 6000f : 4500f);
            Send("GAS", 0f);
            Send("REVLIMIT", 0f);
            Send("TURBO", 0f);
            Send("TURBOVOL", 0.6f);
            Send("ENGINEVOL", antiTest || stage >= 2 ? 0.95f : 0f);
            Send("MISSIONVOL", stage >= 2 ? 0.67f : 0f);
            Send("BACKTURBIN", 0f);
            began = EditorApplication.timeSinceStartup;
            peak = 0f;
            squareSum = 0;
            samples = 0;
            return;
        }
        if (balance)
        {
            //4000回転と8000回転で旧設定と新設定を同じ条件にそろえる
            bool revised = stage % 8 >= 4;
            int source = stage % 4;
            Send("RPM", stage < 8 ? 4000f : 8000f);
            Send("GAS", 1f);
            Send("REVLIMIT", 0f);
            Send("TURBO", 0.8f);
            Send("ENGINEVOL", source == 0 || source == 3 ? (revised ? 0.95f : 0.895f) : 0f);
            Send("TURBOVOL", source == 1 || source == 3 ? (revised ? 0.7f : 0.8525f) : 0f);
            Send("MISSIONVOL", source == 2 || source == 3 ? (revised ? 0.78f : 0.93f) : 0f);
            began = EditorApplication.timeSinceStartup;
            peak = 0f;
            squareSum = 0;
            samples = 0;
            return;
        }
        Send("RPM", speeds[stage]);
        Send("GAS", stage == 3 || stage == 5 ? 0f : 1f);
        Send("REVLIMIT", stage == 3 ? 1f : 0f);
        began = EditorApplication.timeSinceStartup;
        peak = 0f;
    }

    //ターボを鳴らさない出力のピークを測り送信成功と実出力を区別する関数
    static void Tick()
    {
        EditorUtils.System.update();
        if (backTest)
        {
            Send("BACKTURBIN", !antiTest && EditorApplication.timeSinceStartup - began > 0.2 ? 0.5f : 0f);
            backTick.Invoke(backGain, new object[] { sound, stage % 2 == 1 ? (antiTest ? 3f : 16f) : 0f });
        }
        if (sound.getChannelGroup(out var group) == FMOD.RESULT.OK && group.getDSP(0, out var dsp) == FMOD.RESULT.OK)
        {
            dsp.setMeteringEnabled(true, true);
            if (dsp.getMeteringInfo(IntPtr.Zero, out var meter) == FMOD.RESULT.OK)
            {
                //直前の音やパラメーター追従中の音を次の音源の測定へ混ぜない
                if (EditorApplication.timeSinceStartup - began >= 0.5f)
                {
                    for (int i = 0; i < meter.numchannels; i++)
                    {
                        peak = Mathf.Max(peak, meter.peaklevel[i]);
                        squareSum += meter.rmslevel[i] * meter.rmslevel[i];
                        samples++;
                    }
                }
            }
        }
        if (EditorApplication.timeSinceStartup - began < 2f) { return; }
        if (backTest) { Debug.Log($"[SoundGain] anti={antiTest} stage={stage} corrected={stage % 2 == 1} peak={peak:F6}"); }
        else if (balance)
        {
            //ピークだけでなく持続音の強さも記録してマスキングの判断材料にする
            double rms = samples > 0 ? Math.Sqrt(squareSum / samples) : 0;
            Debug.Log($"[EngineMix] stage={stage} source={stage % 4} revised={stage % 8 >= 4} peak={peak:F6} rms={rms:F6}");
        }
        else { Debug.Log($"[EngineOutput] stage={stage} rpm={speeds[stage]} turbo=0 peak={peak:F6} {(peak > 0.0001f ? "PASS" : "SILENT")}"); }
        stage++;
        if (stage == (backTest ? 4 : balance ? 16 : speeds.Length)) { Stop(); return; }
        Begin();
    }

    //既存パラメーターへの送信失敗を検査エラーとして通知する関数
    static void Send(string name, float value) { Check(sound.setParameterByName(name, value)); }

    //APIが失敗したまま検査成功にしないための関数
    static void Check(FMOD.RESULT result)
    {
        if (result != FMOD.RESULT.OK) { Stop(); throw new InvalidOperationException(result.ToString()); }
    }

    //検査だけの音源を止めゲーム本体や保存済みバンクを変更しない関数
    static void Stop()
    {
        (backGain as IDisposable)?.Dispose();
        backGain = null;
        backTest = false;
        antiTest = false;
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        if (!sound.isValid()) { return; }
        sound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        sound.release();
        sound.clearHandle();
    }
}
