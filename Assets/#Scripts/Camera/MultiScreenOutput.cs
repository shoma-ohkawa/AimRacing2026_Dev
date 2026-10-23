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

    //出力に使用するモニター数の下限(シングルモニターでも起動できる枚数)
    const int k_MinDisplayCount = 1;

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

    //Unityのディスプレイ番号ごとの、左右の物理配置に対応する映像番号(左端が0)
    int[] m_sliceIndexPerDisplay;

    //全モニター分の映像を横並びで保持するテクスチャ
    RenderTexture m_renderTexture;

    //実際に接続されている出力対象モニター数
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

        ActivateDisplays();
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

    //接続中のモニターを検出・有効化し、出力枚数・解像度・左右の配置を自動設定する関数
    void ActivateDisplays()
    {
        AppLog.Log($"[{nameof(MultiScreenOutput)}] OSが検出しているディスプレイ数: {Display.displays.Length}");

        //Windows APIで取得したモニター情報をUnityのディスプレイ番号順で受け取る処理
        List<WindowsMonitor> monitors = WindowsMonitorInfo.GetMonitorsInUnityOrder();

        m_activeDisplayCount = DecideActiveDisplayCount(monitors);
        ApplyMainDisplayResolution(monitors);

        //メイン以外のモニターを有効化する処理
        for (int i = 1; i < m_activeDisplayCount; ++i)
        {
            Display.displays[i].Activate();
            LogDisplayState(i);
        }

        SetupSliceIndices(monitors);
    }

    //Windows APIとUnityが認識しているモニター数から、出力枚数を決める関数(最低1枚)
    int DecideActiveDisplayCount(List<WindowsMonitor> _monitors)
    {
        int unityCount = Display.displays.Length;

        //Windows APIで取得できない場合はUnityが認識している台数を使用する処理
        if (_monitors.Count == 0)
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] Windows APIでモニター情報を取得できませんでした。" +
                "Unityのディスプレイ番号順に左から割り当てます。");
            return Mathf.Max(k_MinDisplayCount, unityCount);
        }

        //Windows APIとUnityで台数が食い違う場合に、確認を促した上で少ない方へ合わせる処理
        if (_monitors.Count != unityCount)
        {
            AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] モニター数がWindows API({_monitors.Count})とUnity({unityCount})で異なります。" +
                "少ない方の枚数で出力します。");
        }

        int activeCount = Mathf.Max(k_MinDisplayCount, Mathf.Min(_monitors.Count, unityCount));

        //1枚での起動は通常動作として記録する処理
        if (activeCount == k_MinDisplayCount)
        {
            AppLog.Log($"[{nameof(MultiScreenOutput)}] シングルモニターで起動します。" +
                "複数画面で出力する場合はWindowsの表示設定が「拡張」になっているか確認してください。");
        }

        return activeCount;
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
        m_sliceIndexPerDisplay = new int[m_activeDisplayCount];

        for (int i = 0; i < m_activeDisplayCount; ++i)
        {
            m_sliceIndexPerDisplay[i] = GetSliceIndex(_monitors, i);

            //Windows APIで取得できていない場合は対応関係を確認できないため記録のみ行う処理
            if (_monitors.Count == 0) { continue; }

            AppLog.Log($"[{nameof(MultiScreenOutput)}] Display[{i}] = {_monitors[i].m_deviceName} " +
                $"→ 左から{m_sliceIndexPerDisplay[i] + 1}番目 ({_monitors[i].m_pixelRect.width}x{_monitors[i].m_pixelRect.height}@{_monitors[i].m_pixelRect.x})");

            //Unityの認識解像度と食い違う場合は、番号の対応が合っていない可能性を警告する処理
            Display display = Display.displays[i];
            if (display.systemWidth != _monitors[i].m_pixelRect.width || display.systemHeight != _monitors[i].m_pixelRect.height)
            {
                AppLog.LogWarning($"[{nameof(MultiScreenOutput)}] Display[{i}]の解像度がWindows APIと一致しません。" +
                    $"Unity: {display.systemWidth}x{display.systemHeight} / Windows: {_monitors[i].m_pixelRect.width}x{_monitors[i].m_pixelRect.height}");
            }
        }
    }

    //モニターの左右の位置関係から、左端を0とする映像番号を取得する関数
    int GetSliceIndex(List<WindowsMonitor> _monitors, int _displayIndex)
    {
        //Windows APIで配置を取得できない場合はUnityのディスプレイ番号をそのまま左からの順序として使用する処理
        if (_monitors.Count == 0) { return _displayIndex; }

        int sliceIndex = 0;
        int ownX = _monitors[_displayIndex].m_pixelRect.x;

        //自分より左にあるモニターの枚数を数える処理(同じ位置の場合は番号の小さい方を左とみなす)
        for (int i = 0; i < m_activeDisplayCount; ++i)
        {
            int otherX = _monitors[i].m_pixelRect.x;
            if (otherX < ownX || (otherX == ownX && i < _displayIndex)) { ++sliceIndex; }
        }

        return sliceIndex;
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
        //モニター有効化直後の不安定な描画を避けるための待機処理
        for (int i = 0; i < k_ActivationWaitFrameCount; ++i)
        {
            yield return null;
        }

        for (int i = 0; i < m_activeDisplayCount; ++i)
        {
            LogDisplayState(i);
            CreateDisplayCanvas(i);
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
