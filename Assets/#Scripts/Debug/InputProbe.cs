/*------------------------------------------------------------------
* ファイル名：InputProbe
* 概要：ビルドでキーボード入力が届かない原因の切り分け用プローブ(診断後に削除する)
*
* 使い方：ビルドして起動し、キーを押してPlayer.logの[InputProbe]行を確認する
*   旧Inputだけ出る  → 新Input System側の問題
*   新Inputだけ出る  → 旧Input側の問題
*   どちらも出ない   → OS/フォーカス/デバイス側の問題(heartbeatで状態を確認)
*   両方出る         → 入力は届いているのでゲーム側の処理の問題
-------------------------------------------------------------------*/
#if !UNITY_EDITOR
using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

public class InputProbe : MonoBehaviour
{
    private const float k_HeartbeatSeconds = 5.0f; //状態を定期出力する間隔
    private const string k_Tag = "[InputProbe] ";

    private static bool s_created; //二重生成の防止
    private float m_nextHeartbeat; //次のheartbeat出力時刻
    private int m_legacyKeyCount; //旧Inputで検出したキー押下の累計
    private int m_newKeyCount; //新Inputで検出したキー押下の累計
#if ENABLE_LEGACY_INPUT_MANAGER
    private static readonly KeyCode[] s_keyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));
#endif
#if ENABLE_INPUT_SYSTEM
    private Keyboard m_subscribedKeyboard; //文字入力イベントを購読中のキーボード
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Create()
    {
        if (s_created) { return; }
        s_created = true;
        GameObject host = new GameObject("InputProbe");
        DontDestroyOnLoad(host);
        host.AddComponent<InputProbe>();
    }

    private void Start()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(k_Tag).Append("start unity=").Append(Application.unityVersion);
        sb.Append(" focused=").Append(Application.isFocused);
#if ENABLE_LEGACY_INPUT_MANAGER
        sb.Append(" ENABLE_LEGACY_INPUT_MANAGER=on");
#else
        sb.Append(" ENABLE_LEGACY_INPUT_MANAGER=off");
#endif
#if ENABLE_INPUT_SYSTEM
        sb.Append(" ENABLE_INPUT_SYSTEM=on");
        sb.Append(" updateMode=").Append(InputSystem.settings.updateMode);
        sb.Append(" backgroundBehavior=").Append(InputSystem.settings.backgroundBehavior);
        int keyboardCount = 0; //認識されているキーボードの数
        foreach (InputDevice device in InputSystem.devices)
        {
            if (device is Keyboard) { ++keyboardCount; }
        }
        sb.Append(" keyboards=").Append(keyboardCount);
        Debug.Log(sb.ToString());
        foreach (InputDevice device in InputSystem.devices)
        {
            Debug.Log(k_Tag + "device name=" + device.displayName + " layout=" + device.layout + " enabled=" + device.enabled + " id=" + device.deviceId);
        }
#else
        sb.Append(" ENABLE_INPUT_SYSTEM=off");
        Debug.Log(sb.ToString());
#endif
        m_nextHeartbeat = Time.unscaledTime + k_HeartbeatSeconds;
    }

    private void Update()
    {
        ProbeLegacy();
        ProbeNew();
        if (Time.unscaledTime >= m_nextHeartbeat)
        {
            m_nextHeartbeat = Time.unscaledTime + k_HeartbeatSeconds;
            LogHeartbeat();
        }
    }

    //旧Inputでキー押下とinputStringを検出する関数
    private void ProbeLegacy()
    {
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            string typed = Input.inputString;
            if (!Input.anyKeyDown && typed.Length == 0) { return; }

            StringBuilder keys = new StringBuilder();
            for (int i = 0; i < s_keyCodes.Length; ++i)
            {
                if (s_keyCodes[i] != KeyCode.None && Input.GetKeyDown(s_keyCodes[i])) { keys.Append(s_keyCodes[i]).Append(' '); }
            }
            ++m_legacyKeyCount;
            Debug.Log(k_Tag + "legacy frame=" + Time.frameCount + " keys=[" + keys + "] inputString='" + typed + "'");
        }
        catch (Exception e)
        {
            Debug.LogWarning(k_Tag + "legacy read failed: " + e.GetType().Name + " " + e.Message);
        }
#endif
    }

    //新Input Systemでキー押下を検出し、文字入力イベントも購読する関数
    private void ProbeNew()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current; //現在使用中のキーボード
        if (keyboard != m_subscribedKeyboard)
        {
            if (m_subscribedKeyboard != null) { m_subscribedKeyboard.onTextInput -= OnTextInput; }
            m_subscribedKeyboard = keyboard;
            if (keyboard != null) { keyboard.onTextInput += OnTextInput; }
            Debug.Log(k_Tag + "Keyboard.current changed: " + (keyboard != null ? keyboard.displayName + " enabled=" + keyboard.enabled : "null"));
        }
        if (keyboard == null || !keyboard.anyKey.wasPressedThisFrame) { return; }

        StringBuilder keys = new StringBuilder();
        foreach (KeyControl key in keyboard.allKeys)
        {
            if (key.wasPressedThisFrame) { keys.Append(key.keyCode).Append(' '); }
        }
        ++m_newKeyCount;
        Debug.Log(k_Tag + "new    frame=" + Time.frameCount + " keys=[" + keys + "]");
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private void OnTextInput(char _character)
    {
        Debug.Log(k_Tag + "new    onTextInput='" + _character + "' (" + (int)_character + ")");
    }
#endif

    //プローブが生きていること、デバイスとUIの状態を定期出力する関数
    private void LogHeartbeat()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(k_Tag).Append("heartbeat t=").Append(Time.unscaledTime.ToString("F1"));
        sb.Append(" timeScale=").Append(Time.timeScale);
        sb.Append(" focused=").Append(Application.isFocused);
        sb.Append(" legacyKeys=").Append(m_legacyKeyCount);
        sb.Append(" newKeys=").Append(m_newKeyCount);
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        sb.Append(" keyboardCurrent=").Append(keyboard != null);
        if (keyboard != null) { sb.Append(" kbLastUpdate=").Append(keyboard.lastUpdateTime.ToString("F1")); }
#endif
        EventSystem eventSystem = EventSystem.current;
        sb.Append(" eventSystem=").Append(eventSystem != null ? eventSystem.currentInputModule != null ? eventSystem.currentInputModule.GetType().Name : "noModule" : "null");
        Debug.Log(sb.ToString());
    }

    private void OnDestroy()
    {
#if ENABLE_INPUT_SYSTEM
        if (m_subscribedKeyboard != null) { m_subscribedKeyboard.onTextInput -= OnTextInput; }
#endif
    }
}
#endif
