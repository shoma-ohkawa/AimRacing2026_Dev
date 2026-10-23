using System;
using FMODUnity;
using UnityEditor;
using UnityEngine;

//既存バンクの再生条件と音声出力をシーンを書き換えず検査するクラス
public static class SoundBankValidation
{
    //検査中の音源を次の検査へ持ち越さないための参照
    static FMOD.Studio.EventInstance sound;
    //各条件を一定時間鳴らして出力を集計するための状態
    static int sample;
    static double began;
    static float peak;
    static readonly string[] Cases = { "Turbo", "Mission", "BackTurbin1", "BackTurbin05", "Rev", "Afterfire", "Antilag", "Brake", "Tire" };

    //バンクのパラメーター一覧を調べて音源別の検査を始める関数
    [MenuItem("Tools/検証/Validate Sound Bank")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { return; }
        Stop();
        EditorUtils.LoadPreviewBanks();
        Require(EditorUtils.System.getEvent("event:/Drive", out var description));
        Require(description.getParameterDescriptionCount(out int count));
        for (int i = 0; i < count; i++)
        {
            Require(description.getParameterDescriptionByIndex(i, out var parameter));
            Debug.Log($"[Sep13Sound] Parameter {(string)parameter.name} {parameter.minimum}..{parameter.maximum}");
        }
        sample = 0;
        Begin();
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Stop;
    }

    //他トラックを消音し検査対象の既存音源だけを鳴らす関数
    static void Begin()
    {
        string label = Cases[sample];
        Require(EditorUtils.System.getEvent(label == "Tire" ? "event:/Tire" : "event:/Drive", out var description));
        Require(description.createInstance(out sound));
        if (label == "Tire")
        {
            Send("SLIP_VOL", 1f);
            Send("SLIP_PITCH", 0.7f);
        }
        else
        {
            foreach (string name in new[] { "ENGINEVOL", "MISSIONVOL", "TURBOVOL", "ROADVOL", "GEARUPVOL", "GEARDOWNVOL", "BREAKVOL" })
            {
                Send(name, 0f);
            }
            Send("RPM", 6000f);
            Send("SPEED", 100f);
            Send("GEAR", 3f);
            Send("GAS", 1f);
            if (label == "Turbo") { Send("TURBOVOL", 1f); Send("TURBO", 1f); }
            if (label == "Mission") { Send("MISSIONVOL", 1f); }
            if (label.StartsWith("BackTurbin")) { Send("TURBOVOL", 1f); Send("BACKTURBIN", label == "BackTurbin1" ? 1f : 0.5f); }
            if (label == "Rev") { Send("ENGINEVOL", 1f); Send("GAS", 0f); Send("REVLIMIT", 1f); }
            if (label == "Afterfire") { Send("GEARDOWNVOL", 1f); Send("SHIFTDOWN", 1f); }
            if (label == "Antilag") { Send("ENGINEVOL", 1f); Send("GAS", 0f); }
            if (label == "Brake") { Send("BREAKVOL", 1f); Send("BRAKE", 1f); }
        }
        Require(sound.start());
        began = EditorApplication.timeSinceStartup;
        peak = 0f;
    }

    //再生中の波形出力を測り無音と送信成功を区別する関数
    static void Tick()
    {
        EditorUtils.System.update();
        if (sound.getChannelGroup(out var group) == FMOD.RESULT.OK && group.getDSP(0, out var dsp) == FMOD.RESULT.OK)
        {
            dsp.setMeteringEnabled(true, true);
            if (dsp.getMeteringInfo(IntPtr.Zero, out var meter) == FMOD.RESULT.OK)
            {
                for (int channel = 0; channel < meter.numchannels; channel++) { peak = Mathf.Max(peak, meter.peaklevel[channel]); }
            }
        }
        if (EditorApplication.timeSinceStartup - began < 1.2) { return; }
        Debug.Log($"[Sep13Sound] {Cases[sample]} outputPeak={peak:F6}");
        sound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        sound.release();
        sound.clearHandle();
        sample++;
        if (sample >= Cases.Length) { Stop(); return; }
        Begin();
    }

    //設定名がバンクに存在しない場合に検査ログへ原因を残す関数
    static void Send(string name, float value)
    {
        FMOD.RESULT result = sound.setParameterByName(name, value);
        if (result != FMOD.RESULT.OK) { Debug.LogError($"[Sep13Sound] {name}={value}: {result}"); }
    }

    //検査に必要なAPIが失敗した場合は無音のまま合格にしない関数
    static void Require(FMOD.RESULT result)
    {
        if (result == FMOD.RESULT.OK) { return; }
        Stop();
        throw new InvalidOperationException($"[Sep13Sound] {result}");
    }

    //検査終了や再コンパイル時に検査音源と更新登録を解放する関数
    static void Stop()
    {
        EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Stop;
        if (!sound.isValid()) { return; }
        sound.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        sound.release();
        sound.clearHandle();
    }
}
