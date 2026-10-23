using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class AxisPressdOnce
{
    // 入力一時待機用
    bool m_standby = false;
    // シーン遷移許可
    [SerializeField, ShowInInspector]
    bool m_allowScene = false;

    public bool PressedOnce => m_allowScene;

    public void AxisCheck(float _inputValue)
    {
        if (!m_standby)
        {
            // ペダルを離したことを確認
            if (_inputValue <= 0.1f)
            {
                m_standby = true;
            }
        }
        else
        {
            // ペダルを踏んだことを確認
            if (_inputValue >= 0.8f)
            {
                m_standby = false;
                m_allowScene = true;
            }
        }

        AppLog.Log("Standby::[" + m_standby + "] Input::" + _inputValue);
    }

    // 検出状態を未入力に戻す(最終確認キャンセル等、同じ判定をやり直したい場合に使う)
    public void Reset()
    {
        m_standby = false;
        m_allowScene = false;
    }
}
