using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class DebugDevice : SingletonMonoBehaviour<DebugDevice>
{
    [SerializeField]
    float m_refreshInterval = 3f;

    const int m_maxDeviceNum = 30;

    string logText = "";
    GUIStyle guiStyle = new GUIStyle();

    bool m_showLogInGame = false;

    float m_lastRefreshTime;

    #region プロパティ
    public bool ShowLogInGame
    {
        get => m_showLogInGame;
        set => m_showLogInGame = value;
    }
    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // ログのテキストをスタイルに設定
        guiStyle.fontSize = 24;
        guiStyle.normal.textColor = Color.green;

        m_showLogInGame = false;
    }

    private void OnGUI()
    {
        // ゲーム画面中にログを表示（Windowsのビルド時のみ有効かつエディタで実行していない場合のみ有効かつ0キーで表示/非表示を切り替え）
#if UNITY_STANDALONE_WIN
        if (m_showLogInGame)
        {
            GUI.Label(new Rect(Screen.width - 256, 10, 256, Screen.height), logText, guiStyle);
        }
#endif
    }


    // Update is called once per frame
    void Update()
    {
        if(m_showLogInGame && Time.time - m_lastRefreshTime > m_refreshInterval)
        {
            RefreshText();
            m_lastRefreshTime = Time.time;
        }
    }

    /// <summary>
    /// InputSystemのデバイスを列挙しtextに代入
    /// </summary>
    void RefreshText()
    {
        logText = "";

        // InputSystemからデバイスを取得
        var devices = InputSystem.devices;
        // インデックスとデバイスの名前をlogTextに追加
        for (int i = 0; i < devices.Count; ++i)
        {
            logText += (i + ":" + devices[i].name + "\n");
        }

        // 表示するログの行数がMaxLogLinesを超えたら、古いログを削除
        string[] logLines = logText.Split('\n');
        if (logLines.Length > m_maxDeviceNum)
        {
            logText = string.Join("\n", logLines, logLines.Length - m_maxDeviceNum, m_maxDeviceNum);
        }
    }
}
