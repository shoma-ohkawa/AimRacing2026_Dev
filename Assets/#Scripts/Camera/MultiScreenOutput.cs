/*━━━━━━━━━
@file    MultiScreenOutput.cs
@brief   接続されているモニターの数だけ横に並べた RenderTexture に映像を描き、モニターごとに該当部分を切り出して表示する
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/27
         最終更新日 : 2026/11/10
━━━━━━━━━*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

//一台のカメラ映像を接続中の複数モニター(N画面)へ分割表示するクラス
[DisallowMultipleComponent]
public class MultiScreenOutput : SingletonMonoBehaviour<MultiScreenOutput>
{
    //分割表示用テクスチャの深度ビット数
    const int k_RenderTextureDepthBits = 24;

    //ディスプレイ有効化後に描画を待つフレーム数
    const int k_ActivationWaitFrameCount = 5;

    //ディスプレイを有効化してから、そのウィンドウが現れるまで待つ最大フレーム数
    const int k_WindowWaitMaxFrameCount = 120;

    //ウィンドウが現れてから、モニターへの配置が落ち着くまで待つフレーム数
    const int k_WindowSettleFrameCount = 3;

    //出力に使用するモニター数の下限(シングルモニターでも起動できる枚数)
    const int k_MinDisplayCount = 1;

    //メインモニターのUnityのディスプレイ番号(ゲームウィンドウの表示先で、描画対象から外せない)
    const int k_MainDisplayIndex = 0;

    //モニターの解像度を検出できない場合に使用する横解像度
    const int k_FallbackWidth = 1920;

    //モニターの解像度を検出できない場合に使用する縦解像度
    const int k_FallbackHeight = 1080;

    //複数モニターへ表示する映像を撮影するカメラ
    Camera m_sourceCamera;

    //一台のモニターへ表示する横解像度(メインモニターの物理解像度)
    int m_widthPerDisplay = k_FallbackWidth;

    //一台のモニターへ表示する縦解像度(メインモニターの物理解像度)
    int m_heightPerDisplay = k_FallbackHeight;

    //映像を描画するモニター(横画面)のUnityのディスプレイ番号(昇順。縦画面のモニターは含まない)
    int[] m_renderDisplayIndices = { k_MainDisplayIndex };

    //m_renderDisplayIndicesの各ディスプレイに対応する、Windows APIのモニター番号(同じ並び。Windows APIで取得できない場合は空)
    //最初は番号順の対応で、ディスプレイを有効化したあとに、実際の配置を確認した対応へ置き換える
    int[] m_renderMonitorIndices = new int[0];

    //Windows APIで取得したモニター情報(メインモニターが先頭)
    List<WindowsMonitor> m_windowsMonitors = new List<WindowsMonitor>();

    //Unityのディスプレイ番号ごとの、左右の物理配置に対応する映像番号(左端が0。描画対象でないモニターは-1)
    int[] m_sliceIndexPerDisplay;

    //全モニター分の映像を横並びで保持するテクスチャ
    RenderTexture m_renderTexture;

    //映像を描画するモニター数(縦画面のモニターを除いた出力対象の枚数)
    int m_activeDisplayCount;

    //接続中のモニターをゲーム開始前に有効化する関数
    protected override void Awake()
    {
        base.Awake();

        //重複した常駐オブジェクトによる再初期化を防ぐ処理
        if (Instance != this) { return; }

        //Windows APIとUnityのそれぞれが認識しているモニター一覧の記録(ディスプレイ番号との対応確認用)
        WindowsMonitorInfo.LogAll();
        LogUnityDisplayLayout();

        //==========================
        //Unity Editorでは通常のGame Viewを維持し、実機用のモニター有効化を行わない処理
        if (Application.isEditor) { m_activeDisplayCount = 1; return; }
        //==========================

        DetectDisplays();
    }

    //映像テクスチャと各モニターの表示領域を準備する関数
    void Start()
    {
        SetupRenderTexture();

        //先に登録されたカメラへ出力先を設定する処理
        if (m_sourceCamera != null) { SetupSourceCamera(); }

        StartCoroutine(SetupDisplayCanvasesRoutine());
    }

    //複数モニターへ表示するカメラを切り替える関数
    public void SetSourceCamera(Camera _camera)
    {
        m_sourceCamera = _camera;

        //テクスチャ生成後ならカメラの出力先を直ちに更新する処理
        if (m_renderTexture != null) { SetupSourceCamera(); }
    }

    //カメラ参照と描画用テクスチャを解放する関数
    void OnDestroy()
    {
        //破棄済みテクスチャをカメラが参照し続けないための処理
        if (m_sourceCamera != null) { m_sourceCamera.targetTexture = null; }

        if (m_renderTexture != null) { m_renderTexture.Release(); }
    }

    //接続中のモニターを検出し、出力枚数・解像度を自動設定する関数
    //ディスプレイの有効化と、左右の配置の決定は、Start後のコルーチン(ActivateDisplaysRoutine)で行う
    void DetectDisplays()
    {
        AppLog.Log($"[{nameof(MultiScreenOutput)}] OSが検出しているディスプレイ数: {Display.displays.Length}");

        //Windows APIで取得したモニター情報を、メインモニターが先頭の並びで受け取る処理
        m_windowsMonitors = WindowsMonitorInfo.GetMonitorsInUnityOrder();

        DecideRenderTargets(m_windowsMonitors, GetUnityPortraitFlags());
        ApplyMainDisplayResolution(m_windowsMonitors);
    }

    //描画モニターを1枚ずつ有効化し、Unityのディスプレイが実際に載ったモニターを確かめる関数
    //Unityのディスプレイ番号とWindows APIのモニターの並びは、PCによって一致しない(2026/11/10 のテストで、縦画面があると
    //番号が入れ替わり、横画面だけでも左右が逆になった)ので、番号の対応は当てにできない。そのため有効化のたびに、
    //現れたウィンドウがどのモニターにあるかをWindows APIで調べる。確認できない場合は番号順の対応のままにする
    IEnumerator ActivateDisplaysRoutine()
    {
        //実際の配置を確認できる条件(Windows APIで取得でき、描画モニターごとの対応が決まっている)
        bool canProbe = m_windowsMonitors.Count > 0 && m_renderMonitorIndices.Length == m_renderDisplayIndices.Length;
        bool isProbeSucceeded = canProbe;

        //確認した対応(確認できていないものは-1)
        int[] probedMonitorIndices = new int[m_renderDisplayIndices.Length];
        for (int i = 0; i < probedMonitorIndices.Length; ++i)
        {
            probedMonitorIndices[i] = -1;
        }

        //有効化する前からあるウィンドウ(メインのゲームウィンドウ)
        List<System.IntPtr> mainWindows = WindowsMonitorInfo.GetOwnUnityWindows();

        for (int n = 0; n < m_renderDisplayIndices.Length; ++n)
        {
            int displayIndex = m_renderDisplayIndices[n];

            //メインはすでにウィンドウがある。1つだけに絞れる場合は、そのウィンドウが載っているモニターを使う処理
            if (displayIndex == k_MainDisplayIndex)
            {
                if (canProbe && mainWindows.Count == 1) { probedMonitorIndices[n] = FindMonitorIndexOfWindow(mainWindows[0]); }
                continue;
            }

            //メイン以外の描画モニターを有効化する処理(縦画面のモニターは有効化しない)
            List<System.IntPtr> windowsBefore = WindowsMonitorInfo.GetOwnUnityWindows();
            Display.displays[displayIndex].Activate();
            LogDisplayState(displayIndex);

            if (!canProbe) { continue; }

            //有効化で現れたウィンドウを待つ処理
            System.IntPtr window = System.IntPtr.Zero;
            for (int frame = 0; frame < k_WindowWaitMaxFrameCount && window == System.IntPtr.Zero; ++frame)
            {
                yield return null;
                window = FindNewWindow(windowsBefore);
            }

            if (window == System.IntPtr.Zero) { isProbeSucceeded = false; continue; }

            //モニターへの配置が落ち着いてから、載っているモニターを調べる処理
            for (int frame = 0; frame < k_WindowSettleFrameCount; ++frame)
            {
                yield return null;
            }

            probedMonitorIndices[n] = FindMonitorIndexOfWindow(window);
            if (probedMonitorIndices[n] < 0) { isProbeSucceeded = false; }
        }

        if (canProbe) { ApplyProbedMonitorIndices(probedMonitorIndices, isProbeSucceeded); }
    }

    //確認した対応を、描画モニターの対応として採用する関数(確認できなかった場合は番号順の対応のままにする)
    void ApplyProbedMonitorIndices(int[] _probedMonitorIndices, bool _isProbeSucceeded)
    {
        //メインのウィンドウを特定できなかった場合は、番号順の対応(メインモニター)のままにする処理
        for (int n = 0; n < _probedMonitorIndices.Length; ++n)
        {
            if (_probedMonitorIndices[n] < 0 && m_renderDisplayIndices[n] == k_MainDisplayIndex)
            {
                _probedMonitorIndices[n] = m_renderMonitorIndices[n];
            }
        }

        //同じモニターが重複していないかを確かめる処理
        bool hasDuplicate = false;
        for (int i = 0; i < _probedMonitorIndices.Length; ++i)
        {
            for (int j = i + 1; j < _probedMonitorIndices.Length; ++j)
            {
                if (_probedMonitorIndices[i] == _probedMonitorIndices[j]) { hasDuplicate = true; }
            }
        }

        if (!_isProbeSucceeded || hasDuplicate || System.Array.IndexOf(_probedMonitorIndices, -1) >= 0)
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] ディスプレイが実際に載ったモニターを確認できませんでした。" +
                "番号順の対応のままにします(左右が逆に出る場合があります)。");
            return;
        }

        //番号順の対応と食い違っていた場合は記録する処理
        for (int n = 0; n < _probedMonitorIndices.Length; ++n)
        {
            if (_probedMonitorIndices[n] == m_renderMonitorIndices[n]) { continue; }

            AppLog.Log($"[{nameof(MultiScreenOutput)}] Display[{m_renderDisplayIndices[n]}] は、番号順の対応(Windows APIの{m_renderMonitorIndices[n]}番目)ではなく、" +
                $"{_probedMonitorIndices[n]}番目({m_windowsMonitors[_probedMonitorIndices[n]].m_deviceName})に載っていました。");
        }

        m_renderMonitorIndices = _probedMonitorIndices;
    }

    //現在のUnityのウィンドウのうち、_knownWindowsにないものを返す関数(なければIntPtr.Zero)
    System.IntPtr FindNewWindow(List<System.IntPtr> _knownWindows)
    {
        foreach (System.IntPtr window in WindowsMonitorInfo.GetOwnUnityWindows())
        {
            if (!_knownWindows.Contains(window)) { return window; }
        }

        return System.IntPtr.Zero;
    }

    //ウィンドウが載っているモニターの、Windows APIでの番号を返す関数(見つからない場合は-1)
    int FindMonitorIndexOfWindow(System.IntPtr _window)
    {
        if (!WindowsMonitorInfo.TryGetMonitorOfWindow(_window, out WindowsMonitor monitor)) { return -1; }

        return m_windowsMonitors.FindIndex(_candidate => _candidate.m_deviceName == monitor.m_deviceName);
    }

    //Unityが認識しているディスプレイごとに、縦画面かどうかを返す関数(Unityのディスプレイ番号順)
    //Unityが報告する解像度は、縦向きに回転したモニターでは縦長(例: 1080x1920)になる(2026/11/10 のテストのログで確認)
    bool[] GetUnityPortraitFlags()
    {
        bool[] flags = new bool[Display.displays.Length];

        for (int i = 0; i < flags.Length; ++i)
        {
            Display display = Display.displays[i];

            //解像度を取得できない場合は、縦画面とみなさない
            flags[i] = display.systemWidth > 0 && display.systemHeight > display.systemWidth;
        }

        return flags;
    }

    //Windows APIとUnityが認識しているモニターから、映像を描画する横画面のモニターを決める関数(最低1枚)
    //Windows APIの列挙順とUnityのディスプレイ番号は、縦画面があると一致しないことがある(2026/11/10 のテストでは、
    //縦画面のディスプレイ番号が起動ごとに入れ替わった)。そのため番号で対応させず、それぞれの側で縦画面を除いた
    //横画面だけを集め、先頭から順に対応させる。縦画面のモニターは描画モニターに換算しない
    //(メインモニターだけは、ゲームウィンドウの表示先なので除外できない)
    void DecideRenderTargets(List<WindowsMonitor> _monitors, bool[] _unityIsPortrait)
    {
        bool[] monitorIsPortrait = new bool[_monitors.Count];
        for (int i = 0; i < monitorIsPortrait.Length; ++i)
        {
            monitorIsPortrait[i] = _monitors[i].IsPortrait;
        }

        MultiScreenTargetSelector.Select(
            _unityIsPortrait,
            monitorIsPortrait,
            k_MainDisplayIndex,
            out m_renderDisplayIndices,
            out m_renderMonitorIndices);
        m_activeDisplayCount = m_renderDisplayIndices.Length;

        LogRenderTargets(_monitors, _unityIsPortrait, monitorIsPortrait);
    }

    //描画モニターの決定結果と、除外したモニター・食い違いを記録する関数
    void LogRenderTargets(List<WindowsMonitor> _monitors, bool[] _unityIsPortrait, bool[] _monitorIsPortrait)
    {
        //Windows APIで取得できない場合は、Unityが縦画面と報告したものだけを除く処理
        if (_monitors.Count == 0)
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] Windows APIでモニター情報を取得できませんでした。" +
                "Unityのディスプレイ番号順に左から割り当てます(縦画面はUnityの報告する解像度で判断します)。");
        }

        //メインモニターが縦画面の場合は除外できないため、警告して描画モニターに含める処理
        if (_unityIsPortrait[k_MainDisplayIndex] || (_monitors.Count > 0 && _monitorIsPortrait[k_MainDisplayIndex]))
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] メインモニターが縦画面です。" +
                "ゲームウィンドウの表示先のため、除外せず描画モニターに含めます。");
        }

        //縦画面のモニターを描画モニターに換算しない処理(有効化も映像の割り当てもしない)
        for (int i = 0; i < _unityIsPortrait.Length; ++i)
        {
            if (!_unityIsPortrait[i] || i == k_MainDisplayIndex) { continue; }

            Display display = Display.displays[i];
            AppLog.Log($"[{nameof(MultiScreenOutput)}] Unityの Display[{i}] は縦画面({display.systemWidth}x{display.systemHeight})のため、描画モニターに含めません。");
        }

        for (int i = 0; i < _monitors.Count; ++i)
        {
            if (!_monitorIsPortrait[i] || i == k_MainDisplayIndex) { continue; }

            AppLog.Log($"[{nameof(MultiScreenOutput)}] Windows APIの {_monitors[i].m_deviceName} は縦画面" +
                $"({_monitors[i].m_pixelRect.width}x{_monitors[i].m_pixelRect.height})のため、描画モニターに含めません。");
        }

        //横画面の数がWindows APIとUnityで食い違う場合に、確認を促した上で少ない方へ合わせた旨を記録する処理
        int unityLandscapeCount = CountLandscape(_unityIsPortrait);
        int monitorLandscapeCount = CountLandscape(_monitorIsPortrait);
        if (_monitors.Count > 0 && unityLandscapeCount != monitorLandscapeCount)
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] 横画面のモニター数がWindows API({monitorLandscapeCount})とUnity({unityLandscapeCount})で異なります。" +
                "少ない方の枚数で出力します。");
        }

        //1枚での起動は通常動作として記録する処理(Windows APIで取得できない場合は、Unityの台数で判断する)
        int connectedCount = _monitors.Count == 0 ? _unityIsPortrait.Length : Mathf.Min(_monitors.Count, _unityIsPortrait.Length);
        if (connectedCount <= k_MinDisplayCount)
        {
            AppLog.Log($"[{nameof(MultiScreenOutput)}] シングルモニターで起動します。" +
                "複数画面で出力する場合はWindowsの表示設定が「拡張」になっているか確認してください。");
        }
        else if (m_renderDisplayIndices.Length == k_MinDisplayCount)
        {
            AppLog.Log($"[{nameof(MultiScreenOutput)}] 縦画面を除くと描画モニターは1枚です。シングルモニターで起動します。");
        }
    }

    //縦画面を除いた枚数を数える関数(メインは縦画面でも数える)
    int CountLandscape(bool[] _isPortrait)
    {
        int count = 0;

        for (int i = 0; i < _isPortrait.Length; ++i)
        {
            if (!_isPortrait[i] || i == k_MainDisplayIndex) { ++count; }
        }

        return count;
    }

    //メインモニターの物理解像度を取得し、ゲームウィンドウを全画面ウィンドウで合わせる関数
    void ApplyMainDisplayResolution(List<WindowsMonitor> _monitors)
    {
        Display mainDisplay = Display.main;

        //Windows APIで取得した物理解像度を優先し、取得できない場合はUnityの認識解像度を使用する処理
        if (_monitors.Count > 0 && _monitors[0].m_pixelRect.width > 0 && _monitors[0].m_pixelRect.height > 0)
        {
            m_widthPerDisplay = _monitors[0].m_pixelRect.width;
            m_heightPerDisplay = _monitors[0].m_pixelRect.height;
        }
        else
        {
            DetectNativeResolution(mainDisplay);
        }

        //検出した解像度で全画面ウィンドウを表示する処理
        Screen.SetResolution(m_widthPerDisplay, m_heightPerDisplay, FullScreenMode.FullScreenWindow);

        AppLog.Log($"[{nameof(MultiScreenOutput)}] メインディスプレイの解像度: {m_widthPerDisplay}x{m_heightPerDisplay} " +
            $"(Unityの認識: {mainDisplay.systemWidth}x{mainDisplay.systemHeight})");
    }

    //指定したモニターの解像度を取得する関数
    void DetectNativeResolution(Display _display)
    {
        //解像度を取得できない場合は既定の解像度を維持する処理
        if (_display.systemWidth <= 0 || _display.systemHeight <= 0) { return; }

        m_widthPerDisplay = _display.systemWidth;
        m_heightPerDisplay = _display.systemHeight;
    }

    //Unityのディスプレイ番号ごとに、左右の物理配置に対応する映像番号を決める関数
    void SetupSliceIndices(List<WindowsMonitor> _monitors)
    {
        //描画対象でないモニター(縦画面)には映像番号を割り当てない(-1のまま)処理
        m_sliceIndexPerDisplay = new int[Display.displays.Length];
        for (int i = 0; i < m_sliceIndexPerDisplay.Length; ++i)
        {
            m_sliceIndexPerDisplay[i] = -1;
        }

        int[] sliceIndices = GetSliceIndices(_monitors);

        for (int n = 0; n < m_renderDisplayIndices.Length; ++n)
        {
            int displayIndex = m_renderDisplayIndices[n];
            m_sliceIndexPerDisplay[displayIndex] = sliceIndices[n];

            //Windows APIで取得できていない場合は対応関係を確認できないため記録のみ行う処理
            if (m_renderMonitorIndices.Length == 0) { continue; }

            WindowsMonitor monitor = _monitors[m_renderMonitorIndices[n]];
            AppLog.Log($"[{nameof(MultiScreenOutput)}] Display[{displayIndex}] = {monitor.m_deviceName} " +
                $"→ 左から{sliceIndices[n] + 1}番目 ({monitor.m_pixelRect.width}x{monitor.m_pixelRect.height}@{monitor.m_pixelRect.x})");

            //Unityの認識解像度と食い違う場合は、対応が合っていない可能性を警告する処理
            Display display = Display.displays[displayIndex];
            if (display.systemWidth != monitor.m_pixelRect.width || display.systemHeight != monitor.m_pixelRect.height)
            {
                AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] Display[{displayIndex}]の解像度がWindows APIと一致しません。" +
                    $"Unity: {display.systemWidth}x{display.systemHeight} / Windows: {monitor.m_pixelRect.width}x{monitor.m_pixelRect.height}");
            }
        }
    }

    //描画モニターごとに、左右の物理配置に対応する映像番号(左端が0)を求める関数(m_renderDisplayIndicesと同じ並び)
    int[] GetSliceIndices(List<WindowsMonitor> _monitors)
    {
        int[] monitorX = new int[m_renderDisplayIndices.Length];

        //Windows APIで配置を取得できない場合はUnityのディスプレイ番号をそのまま左からの順序として使用する処理(全部同じ位置とみなす)
        if (m_renderMonitorIndices.Length == 0) { return MultiScreenTargetSelector.GetSliceIndices(monitorX); }

        for (int n = 0; n < monitorX.Length; ++n)
        {
            monitorX[n] = _monitors[m_renderMonitorIndices[n]].m_pixelRect.x;
        }

        return MultiScreenTargetSelector.GetSliceIndices(monitorX);
    }

    //指定したモニターの接続状態と解像度を記録する関数
    void LogDisplayState(int _displayIndex)
    {
        Display display = Display.displays[_displayIndex];
        string systemSize = $"{display.systemWidth}x{display.systemHeight}";
        string renderSize = $"{display.renderingWidth}x{display.renderingHeight}";
        AppLog.Log($"[{nameof(MultiScreenOutput)}] Display[{_displayIndex}] active:{display.active} systemSize:{systemSize} renderSize:{renderSize}");
    }

    //UnityのScreen APIが認識しているモニターの名前・解像度・配置を記録する関数
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    void LogUnityDisplayLayout()
    {
        List<DisplayInfo> displayLayout = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displayLayout);
        AppLog.Log($"[{nameof(MultiScreenOutput)}] Screen.GetDisplayLayout: {displayLayout.Count}台 / Display.displays: {Display.displays.Length}台");

        //Unityが認識しているモニターごとの名前・解像度・作業領域を出力する処理
        for (int i = 0; i < displayLayout.Count; ++i)
        {
            DisplayInfo info = displayLayout[i];
            AppLog.Log($"[{nameof(MultiScreenOutput)}] DisplayInfo[{i}] name:{info.name} size:{info.width}x{info.height} workArea:{info.workArea}");
        }
    }

    //全モニターの映像を保持する共有テクスチャを作成する関数
    void SetupRenderTexture()
    {
        int totalWidth = m_widthPerDisplay * m_activeDisplayCount;

        m_renderTexture = new RenderTexture(totalWidth, m_heightPerDisplay, k_RenderTextureDepthBits);
        m_renderTexture.name = "MultiScreen_RT";
        m_renderTexture.Create();
    }

    //実行環境に合わせてカメラの出力先を切り替える関数
    void SetupSourceCamera()
    {
        if (m_sourceCamera == null)
        {
            AppLog.LogError($"[{nameof(MultiScreenOutput)}] ソースカメラが割り当てられていません。", this);
            return;
        }

        //==========================
        //Unity Editorでは実カメラをGame Viewへ直接描画し、No cameras renderingと白画面を防ぐ処理
        if (Application.isEditor)
        {
            m_sourceCamera.targetTexture = null;
            m_sourceCamera.targetDisplay = 0;
            return;
        }
        //==========================
        m_sourceCamera.targetTexture = m_renderTexture;
        m_sourceCamera.aspect = (float)m_renderTexture.width / m_renderTexture.height;
    }

    //モニター有効化後に分割表示用Canvasを順番に作成する関数
    IEnumerator SetupDisplayCanvasesRoutine()
    {
        //==========================
        //Unity Editorでは分割表示Canvasを作らず通常のGame View描画を維持する処理
        if (Application.isEditor) { yield break; }
        //==========================
        //描画モニターを1枚ずつ有効化し、実際の配置から左右の映像番号を決める処理
        yield return ActivateDisplaysRoutine();
        SetupSliceIndices(m_windowsMonitors);

        //モニター有効化直後の不安定な描画を避けるための待機処理
        for (int i = 0; i < k_ActivationWaitFrameCount; ++i)
        {
            yield return null;
        }

        //描画モニターにだけ表示用Canvasを作成する処理(縦画面のモニターには作らない)
        foreach (int displayIndex in m_renderDisplayIndices)
        {
            LogDisplayState(displayIndex);
            CreateDisplayCanvas(displayIndex);
        }
    }

    //指定したモニターへ対応する映像範囲を表示するCanvasを作成する関数
    void CreateDisplayCanvas(int _displayIndex)
    {
        GameObject canvasObject = new GameObject($"DisplayCanvas_{_displayIndex}");
        canvasObject.transform.SetParent(transform);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.targetDisplay = _displayIndex;

        GameObject imageObject = new GameObject("SlicedImage");
        imageObject.transform.SetParent(canvasObject.transform, false);

        RawImage rawImage = imageObject.AddComponent<RawImage>();
        rawImage.texture = m_renderTexture;

        //モニターの物理配置に対応する横方向の映像範囲を切り出す処理
        int sliceIndex = m_sliceIndexPerDisplay[_displayIndex];
        float sliceWidth = 1f / m_activeDisplayCount;
        rawImage.uvRect = new Rect(sliceWidth * sliceIndex, 0f, sliceWidth, 1f);

        RectTransform rect = rawImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
