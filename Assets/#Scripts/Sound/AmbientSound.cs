using FMODUnity;
using UnityEngine;

public class AmbientSound : MonoBehaviour
{
	[SerializeField]
	EventReference m_eventRef_Wind;

	[SerializeField, Range(0f, 1f)]
	float m_ingameVolume;

	FMOD.Studio.EventInstance m_ambientEvent;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		m_ambientEvent = RuntimeManager.CreateInstance(m_eventRef_Wind);
		m_ambientEvent.start();
	}

    // Update is called once per frame
    void Update()
    {
		if(GameManager.Instance.CurrentGameState == GameStateId.Racing)
			m_ambientEvent.setParameterByName("Volume", m_ingameVolume);
		else
			m_ambientEvent.setParameterByName("Volume", 1f);
	}

	void OnDestroy()
	{
		m_ambientEvent.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
		m_ambientEvent.release();
	}
}
