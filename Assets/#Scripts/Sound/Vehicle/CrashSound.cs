using UnityEngine;
using FMODUnity;

public class CrashSound : MonoBehaviour
{
	[SerializeField]
	EventReference m_eventName;
	Rigidbody m_rigidbody;

	[SerializeField]
	VehicleController m_vehicle;
	Vector3 m_crashedPosition;

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		
	}

    // Update is called once per frame
    void Update()
    {
    }

	private void OnCollisionEnter(Collision collision)
	{
		if (!collision.gameObject.CompareTag("Wall"))
			return;

		if(m_vehicle.KPH > 5)
		{
			Vector3 hit = collision.contacts[0].point;
			AudioHelper.PlayOneShotWithParameters(m_eventName, hit, ("Speed", m_vehicle.KPH),("Volume",1f));
		}
	}

	
}
