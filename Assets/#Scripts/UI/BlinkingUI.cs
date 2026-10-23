using UnityEngine;
using UnityEngine.UI;

public class BlinkingUI : MonoBehaviour
{
    private Image m_Image = null;
    private float m_timer = 0.0f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_Image = GetComponent<Image>();
    }

    // Update is called once per frame
    void Update()
    {

        if (m_Image == null) { return; }

        if(m_timer >= 0.4f)
        {
            m_Image.enabled = !(m_Image.isActiveAndEnabled);
            m_timer = 0.0f;
        }
        m_timer += Time.deltaTime;

        AppLog.Log("afetsdgrhrtyhtry");
    }
}
