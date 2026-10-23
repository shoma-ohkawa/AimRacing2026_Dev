/**
 * @file SteeringIndicatorr.cs
 * @brief SteeringIndicatorの制御
 * @author yuuha terae
 */

using UnityEngine;
using UnityEngine.UI;

public class SteeringIndicator : MonoBehaviour
{
    // 車体
    [SerializeField] VehicleController m_vehicleController;

    // Indicatorの画像
    private Image m_indicatorImage;

    // Steeringの入力値格納用
    private float m_steeringAngle;

    // indicatorの可動範囲
    private float m_indicatorRange;

    void Start()
    {
        m_indicatorImage = GetComponent<Image>();
    }

    void Update()
    {
        // Steeringの入力値を取得
        m_steeringAngle = m_vehicleController.Steering;
        m_indicatorImage.rectTransform.anchoredPosition = new Vector2(m_steeringAngle * 100.0f, 0.0f);
    }
}
