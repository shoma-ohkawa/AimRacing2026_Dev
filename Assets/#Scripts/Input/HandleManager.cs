/// <summary>
/// LogicoolSDK���g�����A�n���h���R���g���[���[�ł̓��͂��Ǘ�����
/// </summary>
using System.Text;
using System;
using UnityEngine;
using VehiclePhysics;
using UnityEngine.InputSystem;

public class HandleManager : SingletonMonoBehaviour<HandleManager>
{
    [SerializeField]
    VehicleController m_vehicleController;
    [SerializeField]
    WheelController2024 m_front;
    [SerializeField]
    Rigidbody m_vehicleRigitbody;

	[Space]
	[SerializeField, ShowInInspector]
	float m_steerInput;

    [Header("ControllerProperties")]
    [SerializeField]
    int m_wheelRange = 360;//ハンドル取得範囲
    [SerializeField]
	int m_overallGain = 100;
	[SerializeField]
	int m_springGain = 100;
	[SerializeField]
	int m_damperGain = 100;
	[SerializeField]
	bool m_defaultSpringEnabled = true;
	[SerializeField]
	int m_defaultSpringGain = 100;
	
    [Header("Spring")]
    [SerializeField]
    float m_spFadeRange;

    int m_logiIndex = 0;
    LogitechGSDK.LogiControllerPropertiesData m_properties;
    LogitechGSDK.LogiControllerPropertiesData m_defaultProperties;

    string m_errorText = "";
    public string ErrorMessage => m_errorText;

    private void Start()
    {
		//AppLog.Log("SteeringInit:" + LogitechGSDK.LogiSteeringInitialize(false));

       

		LogitechGSDK.LogiGetCurrentControllerProperties(0, ref m_defaultProperties);
    }


    private void FixedUpdate()
    {
		if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(m_logiIndex))
		{
            if (LogitechGSDK.LogiIsDeviceConnected(m_logiIndex, LogitechGSDK.LOGI_DEVICE_TYPE_WHEEL))
            {
                UpdateForceFeedBack();
            }
            else
            {
                ChangeIndex();
            }

        }
        else if(!LogitechGSDK.LogiIsConnected(m_logiIndex))
        {
            //AppLog.LogError("LogiConnected(" + m_logiIndex + ")がfalseです。");
        }
    }

    private void OnDestroy()
    {

	}


	void ApplyPrperties()
	{
		m_properties = m_defaultProperties;

        m_properties.wheelRange = m_wheelRange;
        m_properties.overallGain = m_springGain;
        m_properties.springGain = m_overallGain;
        m_properties.damperGain = m_damperGain;
        m_properties.defaultSpringEnabled = m_defaultSpringEnabled;
        m_properties.defaultSpringGain = m_defaultSpringGain;

        LogitechGSDK.LogiSetPreferredControllerProperties(m_properties);
    }
	
    // コントローラーインデックス切り替え
    public void ChangeIndex()
    {
        m_logiIndex++;

        if (m_logiIndex > InputSystem.devices.Count)
            m_logiIndex = 0;
    }

	void UpdateForceFeedBack()
    {
        LogitechGSDK.LogiPlaySpringForce(m_logiIndex, 0, 100, 100);
	}
}
