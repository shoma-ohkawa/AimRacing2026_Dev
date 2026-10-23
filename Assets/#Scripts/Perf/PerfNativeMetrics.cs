/*━━━━━━━━━
@file    PerfNativeMetrics.cs
@brief   計測用: 本アプリのCPU使用率・メモリ、GPU(NVML)の使用率・VRAM・温度を取得する
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/16
         最終更新日 : 2026/10/22
━━━━━━━━━*/

using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

//1回の取得結果。取得できなかった値は PerfNativeMetrics.k_Unavailable(-1)になる
public struct PerfNativeSample
{
    public float m_processCpuPercent; //本アプリのCPU使用率(%。全論理コア合計に対する割合)
    public float m_workingSetMB; //本アプリの物理メモリ使用量(MB)
    public float m_privateMB; //本アプリ専用のコミット済みメモリ(MB)
    public float m_gpuPercent; //GPU使用率(%)
    public float m_gpuMemoryUsedMB; //VRAMの使用量(MB。システム全体の値)
    public float m_gpuTemperatureC; //GPUの温度(℃)
}

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

//Windows APIとNVMLで、CPU・メモリ・GPUの状態を取得する。1秒に1回ほどの呼び出しを想定している
//NVIDIA以外のGPUや nvml.dll が無い環境では、GPUの項目だけ取得できない(止まらない)
public static class PerfNativeMetrics
{
    public const float k_Unavailable = -1f; //取得できなかった値

    private const double k_BytesPerMB = 1024.0 * 1024.0; //バイトからMBへの換算
    private const double k_PercentScale = 100.0; //割合から%への換算
    private const double k_TicksPerSecond = 1.0e7; //FILETIMEの1秒あたりの刻み数(100ナノ秒単位)
    private const int k_UIntBits = 32; //FILETIMEの上位・下位を結合する際のビット数
    private const string k_NvmlLibrary = "nvml.dll"; //NVMLのDLL名
    private const int k_NvmlSuccess = 0; //NVML_SUCCESS
    private const int k_NvmlNameLength = 96; //GPU名の最大文字数
    private const uint k_NvmlTemperatureGpu = 0; //NVML_TEMPERATURE_GPU
    private const string k_UnityEditorProcessName = "Unity"; //Unity Editor のプロセス名(Unity.exe)

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint m_low; //下位32ビット
        public uint m_high; //上位32ビット

        //64ビットの値(100ナノ秒単位)にして返す関数
        public ulong ToUInt64()
        {
            return ((ulong)m_high << k_UIntBits) | m_low;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx
    {
        public uint m_size; //この構造体のバイト数
        public uint m_pageFaultCount; //ページフォールトの累計回数
        public UIntPtr m_peakWorkingSetSize; //物理メモリ使用量の最大値
        public UIntPtr m_workingSetSize; //物理メモリ使用量
        public UIntPtr m_quotaPeakPagedPoolUsage; //未使用
        public UIntPtr m_quotaPagedPoolUsage; //未使用
        public UIntPtr m_quotaPeakNonPagedPoolUsage; //未使用
        public UIntPtr m_quotaNonPagedPoolUsage; //未使用
        public UIntPtr m_pagefileUsage; //コミット済みメモリ
        public UIntPtr m_peakPagefileUsage; //コミット済みメモリの最大値
        public UIntPtr m_privateUsage; //このプロセス専用のコミット済みメモリ
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlUtilization
    {
        public uint m_gpu; //GPUの使用率(%)
        public uint m_memory; //VRAMの帯域の使用率(%)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NvmlMemory
    {
        public ulong m_total; //VRAMの総量(バイト)
        public ulong m_free; //VRAMの空き(バイト)
        public ulong m_used; //VRAMの使用量(バイト)
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern bool GetProcessTimes(IntPtr _process, out FileTime _creation, out FileTime _exit, out FileTime _kernel, out FileTime _user);

    [DllImport("psapi.dll")]
    private static extern bool GetProcessMemoryInfo(IntPtr _process, ref ProcessMemoryCountersEx _counters, uint _size);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlInit_v2();

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlShutdown();

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetCount_v2(out uint _count);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetHandleByIndex_v2(uint _index, out IntPtr _device);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetName(IntPtr _device, StringBuilder _name, uint _length);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetUtilizationRates(IntPtr _device, out NvmlUtilization _utilization);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetMemoryInfo(IntPtr _device, out NvmlMemory _memory);

    [DllImport(k_NvmlLibrary, CallingConvention = CallingConvention.Cdecl)]
    private static extern int nvmlDeviceGetTemperature(IntPtr _device, uint _sensor, out uint _temperature);

    private static bool s_bNvmlReady; //NVMLの初期化に成功しているか
    private static IntPtr s_nvmlDevice; //取得対象のGPU
    private static string s_nvmlDeviceName = string.Empty; //取得対象のGPUの名前

    private static ulong s_prevProcessTicks; //前回の本アプリのCPU時間(カーネル+ユーザー)
    private static long s_prevTimestamp; //前回の取得時刻(Stopwatchの刻み)
    private static bool s_bHasPrevious; //前回の値があるか

    //NVMLが使えるか
    public static bool IsGpuAvailable { get { return s_bNvmlReady; } }

    //取得対象のGPUの名前(NVMLが使えなければ空)
    public static string GpuName { get { return s_nvmlDeviceName; } }

    //Unity Editor が動いているかを判定する関数(開いたままだと VRAM を使い、計測が汚れる)。判定できなければ false を返す
    public static bool IsUnityEditorRunning()
    {
        try
        {
            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName(k_UnityEditorProcessName);
            bool bRunning = processes.Length > 0;

            //取得したプロセスの情報を、1つずつ解放する処理
            for (int i = 0; i < processes.Length; ++i) { processes[i].Dispose(); }

            return bRunning;
        }
        catch (Exception)
        {
            return false;
        }
    }

    //NVMLを初期化し、Unityが描画に使っているGPUを選ぶ関数。失敗してもCPU・メモリの取得は続けられる
    public static void Initialize()
    {
        s_bHasPrevious = false;
        s_bNvmlReady = false;

        try
        {
            if (nvmlInit_v2() != k_NvmlSuccess) { return; }
            if (nvmlDeviceGetCount_v2(out uint count) != k_NvmlSuccess || count == 0) { return; }

            s_nvmlDevice = FindNvmlDevice(count);
            s_bNvmlReady = s_nvmlDevice != IntPtr.Zero;
        }
        catch (Exception)
        {
            //NVIDIA以外のGPUや、DLLが無い環境では取得しない
            s_bNvmlReady = false;
        }
    }

    //NVMLを終了する関数
    public static void Shutdown()
    {
        if (!s_bNvmlReady) { return; }

        try { nvmlShutdown(); }
        catch (Exception) { }

        s_bNvmlReady = false;
    }

    //今の状態を取得する関数。CPU使用率は前回の呼び出しとの差から求めるので、最初の1回は取得できない
    public static PerfNativeSample Sample()
    {
        PerfNativeSample sample = new PerfNativeSample();
        sample.m_processCpuPercent = k_Unavailable;
        sample.m_workingSetMB = k_Unavailable;
        sample.m_privateMB = k_Unavailable;
        sample.m_gpuPercent = k_Unavailable;
        sample.m_gpuMemoryUsedMB = k_Unavailable;
        sample.m_gpuTemperatureC = k_Unavailable;

        SampleProcessCpu(ref sample);
        SampleProcessMemory(ref sample);
        SampleGpu(ref sample);

        return sample;
    }

    //本アプリの物理メモリ(MB)だけを取得する関数。Sample()と違い、CPU使用率の前回値を更新しないので、別の用途から呼んでも1秒ごとの記録に影響しない
    public static float GetWorkingSetMB()
    {
        PerfNativeSample sample = new PerfNativeSample();
        sample.m_workingSetMB = k_Unavailable;
        sample.m_privateMB = k_Unavailable;

        SampleProcessMemory(ref sample);

        return sample.m_workingSetMB;
    }

    /// <summary>
    /// 本アプリのCPU使用率を、前回の呼び出しとの差から求めます。
    /// </summary>
    /// <param name="_sample">結果の格納先</param>
    private static void SampleProcessCpu(ref PerfNativeSample _sample)
    {
        try
        {
            if (!GetProcessTimes(GetCurrentProcess(), out FileTime creation, out FileTime exit, out FileTime kernel, out FileTime user)) { return; }

            ulong processTicks = kernel.ToUInt64() + user.ToUInt64();
            long timestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            if (s_bHasPrevious)
            {
                double wallTicks = (timestamp - s_prevTimestamp) * k_TicksPerSecond / System.Diagnostics.Stopwatch.Frequency;
                if (wallTicks > 0.0)
                {
                    _sample.m_processCpuPercent = (float)(k_PercentScale * (processTicks - s_prevProcessTicks) / (wallTicks * SystemInfo.processorCount));
                }
            }

            s_prevProcessTicks = processTicks;
            s_prevTimestamp = timestamp;
            s_bHasPrevious = true;
        }
        catch (Exception)
        {
            //取得できない環境では、CPU使用率だけ空にする
        }
    }

    /// <summary>
    /// 本アプリの物理メモリとコミット済みメモリを取得します。
    /// </summary>
    /// <param name="_sample">結果の格納先</param>
    private static void SampleProcessMemory(ref PerfNativeSample _sample)
    {
        try
        {
            ProcessMemoryCountersEx counters = new ProcessMemoryCountersEx { m_size = (uint)Marshal.SizeOf<ProcessMemoryCountersEx>() };
            if (!GetProcessMemoryInfo(GetCurrentProcess(), ref counters, counters.m_size)) { return; }

            _sample.m_workingSetMB = (float)(counters.m_workingSetSize.ToUInt64() / k_BytesPerMB);
            _sample.m_privateMB = (float)(counters.m_privateUsage.ToUInt64() / k_BytesPerMB);
        }
        catch (Exception)
        {
            //取得できない環境では、空にする
        }
    }

    /// <summary>
    /// NVMLでGPUの使用率・VRAM・温度を取得します。項目ごとに失敗しうる(古いドライバーで関数が無いなど)ので、1つずつ囲みます。
    /// </summary>
    /// <param name="_sample">結果の格納先</param>
    private static void SampleGpu(ref PerfNativeSample _sample)
    {
        if (!s_bNvmlReady) { return; }

        try
        {
            if (nvmlDeviceGetUtilizationRates(s_nvmlDevice, out NvmlUtilization utilization) == k_NvmlSuccess) { _sample.m_gpuPercent = utilization.m_gpu; }
        }
        catch (Exception) { }

        try
        {
            if (nvmlDeviceGetMemoryInfo(s_nvmlDevice, out NvmlMemory memory) == k_NvmlSuccess) { _sample.m_gpuMemoryUsedMB = (float)(memory.m_used / k_BytesPerMB); }
        }
        catch (Exception) { }

        try
        {
            if (nvmlDeviceGetTemperature(s_nvmlDevice, k_NvmlTemperatureGpu, out uint temperature) == k_NvmlSuccess) { _sample.m_gpuTemperatureC = temperature; }
        }
        catch (Exception) { }
    }

    //Unityの描画に使っているGPUの名前と一致するデバイスを探す関数(見つからなければ先頭のデバイス)
    //_count: NVMLが認識しているGPUの数
    private static IntPtr FindNvmlDevice(uint _count)
    {
        IntPtr firstDevice = IntPtr.Zero;
        StringBuilder name = new StringBuilder(k_NvmlNameLength);

        //GPUを1台ずつ調べ、名前が一致するものを探す処理
        for (uint i = 0; i < _count; ++i)
        {
            if (nvmlDeviceGetHandleByIndex_v2(i, out IntPtr device) != k_NvmlSuccess) { continue; }

            name.Length = 0;
            string deviceName = nvmlDeviceGetName(device, name, k_NvmlNameLength) == k_NvmlSuccess ? name.ToString() : string.Empty;

            if (firstDevice == IntPtr.Zero)
            {
                firstDevice = device;
                s_nvmlDeviceName = deviceName;
            }

            if (deviceName.Length > 0 && (SystemInfo.graphicsDeviceName.Contains(deviceName) || deviceName.Contains(SystemInfo.graphicsDeviceName)))
            {
                s_nvmlDeviceName = deviceName;
                return device;
            }
        }

        return firstDevice;
    }
}

#else

//Windows以外では、すべての項目を取得できないものとして返す
public static class PerfNativeMetrics
{
    public const float k_Unavailable = -1f; //取得できなかった値

    //NVMLが使えるか(Windows以外では常に使えない)
    public static bool IsGpuAvailable { get { return false; } }

    //取得対象のGPUの名前(Windows以外では空)
    public static string GpuName { get { return string.Empty; } }

    //Unity Editor の検出は Windows のみ対応なので、常に false を返す関数
    public static bool IsUnityEditorRunning() { return false; }

    //何もしない関数
    public static void Initialize() { }

    //何もしない関数
    public static void Shutdown() { }

    //物理メモリは取得できないので、取得できなかった値を返す関数
    public static float GetWorkingSetMB() { return k_Unavailable; }

    //すべての項目を「取得できなかった」にした値を返す関数
    public static PerfNativeSample Sample()
    {
        PerfNativeSample sample = new PerfNativeSample();
        sample.m_processCpuPercent = k_Unavailable;
        sample.m_workingSetMB = k_Unavailable;
        sample.m_privateMB = k_Unavailable;
        sample.m_gpuPercent = k_Unavailable;
        sample.m_gpuMemoryUsedMB = k_Unavailable;
        sample.m_gpuTemperatureC = k_Unavailable;
        return sample;
    }
}

#endif
