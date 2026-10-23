/*━━━━━━━━━
@file    WindowsMonitorInfo.cs
@brief   Windows APIでモニターの枚数・配置・メイン判定・現在の解像度を取得するユーティリティ
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/27
         最終更新日 : 2026/09/27
━━━━━━━━━*/

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Windowsが認識している1台分のモニター情報。
/// </summary>
public readonly struct WindowsMonitor
{
    public readonly string m_deviceName;    //Windows上のデバイス名(例: \\.\DISPLAY1)
    public readonly bool m_bIsPrimary;      //メインモニターかどうか
    public readonly RectInt m_desktopRect;  //仮想デスクトップ上の矩形(GetMonitorInfo。DPI仮想化の影響を受ける場合がある)
    public readonly RectInt m_pixelRect;    //物理ピクセル単位の現在の解像度と配置(EnumDisplaySettings。左上原点・Y下向き)

    /// <summary>
    /// モニター情報を生成します。
    /// </summary>
    /// <param name="_deviceName">Windows上のデバイス名</param>
    /// <param name="_isPrimary">メインモニターかどうか</param>
    /// <param name="_desktopRect">GetMonitorInfoで取得した仮想デスクトップ上の矩形</param>
    /// <param name="_pixelRect">EnumDisplaySettingsで取得した物理ピクセル単位の矩形</param>
    public WindowsMonitor(
        string _deviceName,
        bool _isPrimary,
        RectInt _desktopRect,
        RectInt _pixelRect)
    {
        m_deviceName = _deviceName;
        m_bIsPrimary = _isPrimary;
        m_desktopRect = _desktopRect;
        m_pixelRect = _pixelRect;
    }

    /// <summary>
    /// ログ出力用に、デバイス名・メイン判定・解像度と配置を文字列にします。
    /// </summary>
    public override string ToString()
    {
        return $"{m_deviceName} primary:{m_bIsPrimary} pixel:{FormatRect(m_pixelRect)} desktopRect:{FormatRect(m_desktopRect)}";
    }

    //矩形を「幅x高さ@(左上X,左上Y)」の文字列にする関数
    static string FormatRect(RectInt _rect)
    {
        return $"{_rect.width}x{_rect.height}@({_rect.x},{_rect.y})";
    }
}

/// <summary>
/// Windows APIでモニター情報を取得するユーティリティ。Windows以外では空の結果を返します。
/// </summary>
public static class WindowsMonitorInfo
{
    /// <summary>
    /// Windowsが認識している全モニターをOSの列挙順で取得します。
    /// Windows以外、またはAPI呼び出しに失敗した場合は空のリストを返します。
    /// </summary>
    /// <returns>モニター情報のリスト</returns>
    public static List<WindowsMonitor> GetMonitors()
    {
        List<WindowsMonitor> monitors = new List<WindowsMonitor>();

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try
        {
            m_enumeratedMonitors.Clear();

            //列挙に失敗した場合に結果を返さない処理
            if (EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, m_enumProc, IntPtr.Zero))
            {
                monitors.AddRange(m_enumeratedMonitors);
            }
            else
            {
                AppLog.LogWarning($"[{nameof(WindowsMonitorInfo)}] EnumDisplayMonitorsが失敗しました。");
            }

            m_enumeratedMonitors.Clear();
        }
        catch (Exception exception)
        {
            AppLog.LogWarning($"[{nameof(WindowsMonitorInfo)}] モニター情報の取得に失敗しました: {exception.Message}");
        }
#endif

        return monitors;
    }

    /// <summary>
    /// Unityのディスプレイ番号の並び(メインモニターが先頭、残りはOSの列挙順)に合わせてモニターを取得します。
    /// Windows以外、またはAPI呼び出しに失敗した場合は空のリストを返します。
    /// </summary>
    /// <returns>Unityのディスプレイ番号順のモニター情報のリスト</returns>
    public static List<WindowsMonitor> GetMonitorsInUnityOrder()
    {
        List<WindowsMonitor> monitors = GetMonitors();

        //メインモニターを先頭へ移し、残りの相対順は保つ処理
        int primaryIndex = monitors.FindIndex(_monitor => _monitor.m_bIsPrimary);
        if (primaryIndex > 0)
        {
            WindowsMonitor primary = monitors[primaryIndex];
            monitors.RemoveAt(primaryIndex);
            monitors.Insert(0, primary);
        }

        return monitors;
    }

    /// <summary>
    /// Windowsが認識しているモニター一覧をログへ出力します。Unityのディスプレイ番号との対応確認に使います。
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogAll()
    {
        List<WindowsMonitor> monitors = GetMonitors();
        AppLog.Log($"[{nameof(WindowsMonitorInfo)}] Windowsが認識しているモニター数: {monitors.Count}");

        //列挙順の番号でモニターごとの情報を出力する処理
        for (int i = 0; i < monitors.Count; ++i)
        {
            AppLog.Log($"[{nameof(WindowsMonitorInfo)}] Monitor[{i}] {monitors[i]}");
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    //user32.dllのAPI名
    const string k_User32 = "user32.dll";

    //MONITORINFOF_PRIMARY: メインモニターを示すフラグ
    const uint k_MonitorInfoFlagPrimary = 0x00000001;

    //ENUM_CURRENT_SETTINGS: 現在の表示モードを指定する値
    const int k_EnumCurrentSettings = -1;

    //DM_POSITION: DEVMODEの位置情報が有効であることを示すフラグ
    const uint k_DmPosition = 0x00000020;

    //DM_PELSWIDTH: DEVMODEの横解像度が有効であることを示すフラグ
    const uint k_DmPelsWidth = 0x00080000;

    //DM_PELSHEIGHT: DEVMODEの縦解像度が有効であることを示すフラグ
    const uint k_DmPelsHeight = 0x00100000;

    //CCHDEVICENAME / CCHFORMNAME: デバイス名・フォーム名の最大文字数
    const int k_NameLength = 32;

    //列挙コールバックを保持してGC回収を避けるためのデリゲート
    static readonly MonitorEnumProc m_enumProc = OnEnumMonitor;

    //列挙コールバックが結果を溜めるリスト(メインスレッド専用)
    static readonly List<WindowsMonitor> m_enumeratedMonitors = new List<WindowsMonitor>();

    //EnumDisplayMonitorsのコールバックの型
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate bool MonitorEnumProc(IntPtr _hMonitor, IntPtr _hdcMonitor, IntPtr _clipRect, IntPtr _data);

    //RECT: 矩形(左上と右下の座標)
    [StructLayout(LayoutKind.Sequential)]
    struct NativeRect
    {
        public int m_left;      //左端
        public int m_top;       //上端
        public int m_right;     //右端
        public int m_bottom;    //下端
    }

    //MONITORINFOEXW: モニターの矩形・メイン判定・デバイス名
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeMonitorInfoEx
    {
        public int m_size;                  //構造体のサイズ(呼び出し前に設定する)
        public NativeRect m_monitorRect;    //モニターの矩形
        public NativeRect m_workRect;       //タスクバーを除いた作業領域の矩形
        public uint m_flags;                //MONITORINFOF_PRIMARYなどのフラグ

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = k_NameLength)]
        public string m_deviceName;         //デバイス名
    }

    //DEVMODEW: 表示モード(表示デバイス用の共用体は位置情報側で定義)
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NativeDeviceMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = k_NameLength)]
        public string m_deviceName;         //デバイス名
        public ushort m_specVersion;        //構造体の仕様バージョン
        public ushort m_driverVersion;      //ドライバのバージョン
        public ushort m_size;               //構造体のサイズ(呼び出し前に設定する)
        public ushort m_driverExtra;        //ドライバ固有データのバイト数
        public uint m_fields;               //有効なメンバーを示すDM_*フラグ
        public int m_positionX;             //仮想デスクトップ上の左上X座標(物理ピクセル)
        public int m_positionY;             //仮想デスクトップ上の左上Y座標(物理ピクセル)
        public uint m_displayOrientation;   //画面の向き
        public uint m_displayFixedOutput;   //解像度が合わない時の拡大方法
        public short m_color;               //印刷用(表示では未使用)
        public short m_duplex;              //印刷用(表示では未使用)
        public short m_yResolution;         //印刷用(表示では未使用)
        public short m_ttOption;            //印刷用(表示では未使用)
        public short m_collate;             //印刷用(表示では未使用)

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = k_NameLength)]
        public string m_formName;           //印刷用(表示では未使用)
        public ushort m_logPixels;          //論理インチあたりのピクセル数
        public uint m_bitsPerPel;           //色のビット数
        public uint m_pelsWidth;            //横解像度(物理ピクセル)
        public uint m_pelsHeight;           //縦解像度(物理ピクセル)
        public uint m_displayFlags;         //表示モードのフラグ
        public uint m_displayFrequency;     //リフレッシュレート
        public uint m_icmMethod;            //ICMの方式
        public uint m_icmIntent;            //ICMの意図
        public uint m_mediaType;            //印刷用(表示では未使用)
        public uint m_ditherType;           //ディザ方式
        public uint m_reserved1;            //予約領域
        public uint m_reserved2;            //予約領域
        public uint m_panningWidth;         //パンニングの幅
        public uint m_panningHeight;        //パンニングの高さ
    }

    /// <summary>
    /// 接続中のモニターを列挙し、モニターごとにコールバックを呼びます。
    /// </summary>
    /// <param name="_hdc">列挙範囲を絞るデバイスコンテキスト(IntPtr.Zeroで全モニター)</param>
    /// <param name="_clipRect">列挙範囲を絞る矩形へのポインタ(IntPtr.Zeroで制限なし)</param>
    /// <param name="_callback">モニターごとに呼ばれるコールバック</param>
    /// <param name="_data">コールバックへそのまま渡される値</param>
    [DllImport(k_User32)]
    static extern bool EnumDisplayMonitors(
        IntPtr _hdc,
        IntPtr _clipRect,
        MonitorEnumProc _callback,
        IntPtr _data);

    //モニターの矩形・メイン判定・デバイス名を取得するAPI
    [DllImport(k_User32, CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    static extern bool GetMonitorInfo(IntPtr _hMonitor, ref NativeMonitorInfoEx _info);

    /// <summary>
    /// 表示デバイスの表示モード(解像度・位置など)を取得します。
    /// </summary>
    /// <param name="_deviceName">対象の表示デバイス名</param>
    /// <param name="_modeNumber">取得するモード番号(k_EnumCurrentSettingsで現在のモード)</param>
    /// <param name="_deviceMode">取得した表示モードの格納先</param>
    [DllImport(k_User32, CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsW")]
    static extern bool EnumDisplaySettings(
        string _deviceName,
        int _modeNumber,
        ref NativeDeviceMode _deviceMode);

    /// <summary>
    /// EnumDisplayMonitorsから呼ばれ、列挙されたモニターの情報をリストへ追加します。
    /// </summary>
    /// <param name="_hMonitor">列挙されたモニターのハンドル</param>
    /// <param name="_hdcMonitor">モニターのデバイスコンテキスト</param>
    /// <param name="_clipRect">モニターと列挙範囲が重なる矩形へのポインタ</param>
    /// <param name="_data">EnumDisplayMonitorsへ渡された値</param>
    /// <returns>列挙を続ける場合はtrue</returns>
    [AOT.MonoPInvokeCallback(typeof(MonitorEnumProc))]
    static bool OnEnumMonitor(
        IntPtr _hMonitor,
        IntPtr _hdcMonitor,
        IntPtr _clipRect,
        IntPtr _data)
    {
        //情報を取得できたモニターだけを結果へ追加する処理
        if (TryCreateMonitor(_hMonitor, out WindowsMonitor monitor)) { m_enumeratedMonitors.Add(monitor); }

        return true;
    }

    //モニターのハンドルからモニター情報を作成する関数
    static bool TryCreateMonitor(IntPtr _hMonitor, out WindowsMonitor _monitor)
    {
        _monitor = default;

        NativeMonitorInfoEx info = new NativeMonitorInfoEx { m_size = Marshal.SizeOf<NativeMonitorInfoEx>() };
        if (!GetMonitorInfo(_hMonitor, ref info)) { return false; }

        RectInt desktopRect = ToRectInt(info.m_monitorRect);
        RectInt pixelRect = GetPixelRect(info.m_deviceName, desktopRect);
        bool isPrimary = (info.m_flags & k_MonitorInfoFlagPrimary) != 0;

        _monitor = new WindowsMonitor(info.m_deviceName, isPrimary, desktopRect, pixelRect);
        return true;
    }

    //現在の表示モードから物理ピクセル単位の解像度と位置を取得し、取得できない項目は代替の矩形で補う関数
    static RectInt GetPixelRect(string _deviceName, RectInt _fallbackRect)
    {
        NativeDeviceMode mode = new NativeDeviceMode { m_size = (ushort)Marshal.SizeOf<NativeDeviceMode>() };
        if (!EnumDisplaySettings(_deviceName, k_EnumCurrentSettings, ref mode)) { return _fallbackRect; }

        bool hasSize = (mode.m_fields & (k_DmPelsWidth | k_DmPelsHeight)) == (k_DmPelsWidth | k_DmPelsHeight);
        bool hasPosition = (mode.m_fields & k_DmPosition) != 0;

        int x = hasPosition ? mode.m_positionX : _fallbackRect.x;
        int y = hasPosition ? mode.m_positionY : _fallbackRect.y;
        int width = hasSize ? (int)mode.m_pelsWidth : _fallbackRect.width;
        int height = hasSize ? (int)mode.m_pelsHeight : _fallbackRect.height;

        return new RectInt(x, y, width, height);
    }

    //RECT構造体をRectIntへ変換する関数
    static RectInt ToRectInt(NativeRect _rect)
    {
        return new RectInt(_rect.m_left, _rect.m_top, _rect.m_right - _rect.m_left, _rect.m_bottom - _rect.m_top);
    }
#endif
}
