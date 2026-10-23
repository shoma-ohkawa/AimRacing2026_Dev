using AIM;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VehiclePhysics;

public class UI_Meter : MonoBehaviour
{
    [SerializeField]
    private VehicleController _vehicleController = null;
    [SerializeField]
    private TimeKeeper _timeKeeper = null;

    [SerializeField]
    private Image _gaugeImage = null;

    [SerializeField]
    private Image _maskImage = null;

    [SerializeField]
    private TextMeshProUGUI[] _tmp = null;

    [System.Serializable]
    public struct BreakPoints
    {
        public float rpmValue;
        public float fillAmount;
    }
    [SerializeField]
    private BreakPoints[] _breakPoints = null;

    private float _currentRPM = 0;

    [SerializeField]
    private float _flashingCycle = 0.2f;
    private float _timeRate = 0;

    private int m_startState = 0;

    public int StartState
    {
        set => m_startState = value;
	}

    [SerializeField]
    private float m_flashingRPM = 9000f;

    public float FlashingRPM
    {
        get => m_flashingRPM;
        set => m_flashingRPM = value;
    }
    void Start()
    {
        m_startState = 0;
	}

    void Update()
    {
        UpdateGauge();
        UpdateGuide();
        UpdateSectorText();
	}

    void UpdateGauge()
    {
        // Gaugeの最大を補正
        float Min = 0;
        float Max = 0;
        float New = 0;
        float value = 0;

        _currentRPM = _vehicleController.EngineRPM;

        if (_currentRPM >= 0 && _currentRPM < 3000)
        {
            Min = _breakPoints[0].fillAmount;
            Max = _breakPoints[1].fillAmount;
            New = Mathf.InverseLerp(0, 3000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
        if (_currentRPM >= 3000 && _currentRPM < 5000)
        {
            Min = _breakPoints[1].fillAmount;
            Max = _breakPoints[2].fillAmount;
            New = Mathf.InverseLerp(3000, 5000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
        if (_currentRPM >= 5000 && _currentRPM < 6000)
        {
            Min = _breakPoints[2].fillAmount;
            Max = _breakPoints[3].fillAmount;
            New = Mathf.InverseLerp(5000, 6000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
        if (_currentRPM >= 6000 && _currentRPM < 7000)
        {
            Min = _breakPoints[3].fillAmount;
            Max = _breakPoints[4].fillAmount;
            New = Mathf.InverseLerp(6000, 7000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
        if (_currentRPM >= 7000 && _currentRPM < 8000)
        {
            Min = _breakPoints[4].fillAmount;
            Max = _breakPoints[5].fillAmount;
            New = Mathf.InverseLerp(7000, 8000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
        if (_currentRPM >= 8000 && _currentRPM < 9000)
        {
            Min = _breakPoints[5].fillAmount;
            Max = _breakPoints[6].fillAmount;
            New = Mathf.InverseLerp(8000, 9000, _currentRPM);
            value = ((Max - Min) * New) + Min;
        }
		if (_currentRPM >= 9000 && _currentRPM < 10000)
		{
			Min = _breakPoints[6].fillAmount;
			Max = _breakPoints[7].fillAmount;
			New = Mathf.InverseLerp(9000, 10000, _currentRPM);
			value = ((Max - Min) * New) + Min;
		}

		if (m_startState == 0)
        {
			if (_currentRPM >= m_flashingRPM)
			{
				Flashing();
			}
			if (_currentRPM < m_flashingRPM)
			{
				if (_gaugeImage.color.a <= 1)
				{
					Color newColor = new Color(1f, 1f, 1f, 1f);
					_gaugeImage.color = newColor;
				}
			}
		}

        if (m_startState == 1)
        {
			if (_currentRPM >= 9000)
			{
				Flashing();
			}
			if (_currentRPM < 9000)
			{
				if (_gaugeImage.color.a <= 1)
				{
					Color newColor = new Color(1f, 1f, 1f, 1f);
					_gaugeImage.color = newColor;
				}
			}
		}
       
        _maskImage.fillAmount = value;
    }

    void UpdateGuide()
    {
        if(_vehicleController.ActiveGear > 0)
        {
            _tmp[0].text = _vehicleController.ActiveGear.ToString();
        }
        else
        {
            if(_vehicleController.ActiveGear == 0)
            {
                _tmp[0].text = "N";
            }
            else
            {
                _tmp[0].text = "R";
            }
        }
        _tmp[1].text = _vehicleController.KPH.ToString("000");
        _tmp[2].text = _vehicleController.EngineRPM.ToString("0000");
    }

	private void UpdateSectorText()
	{
		int value = _timeKeeper.GetSavedTimeInfoCount() - 1;

		if (value == -1)
		{
            _tmp[3].text = "0:00.000";
		}
		else
		{
            _tmp[3].text = _timeKeeper.RetrieveSavedTimeToMeter(value);
		}
	}

	private void Flashing()
    {
        Color newColor = new Color(1f, 1f, 1f, 1f);

        _timeRate += Time.deltaTime;

        float repeatValue = Mathf.Repeat(_timeRate, _flashingCycle);

        newColor.a = repeatValue >= _flashingCycle * 0.5f ? 1f : 0.5f;

        _gaugeImage.color = newColor;
    }
}
