using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DebugSystem : MonoBehaviour
{
    [SerializeField]
    private GameObject m_car = null;
    
    [SerializeField]
    private GameObject m_shortCutPoint = null;

    private void Start()
    {
        Cursor.visible = false;
    }

    private void Update()
    {
		
	}

    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            m_car.transform.rotation = new Quaternion(0.0f, 0.0f, 0.0f, 0.0f);
            m_car.transform.position = new Vector3(0.0f, 3.0f, 0.0f) + m_car.transform.position;
            m_car.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }

        if (Input.GetKeyDown(KeyCode.End))
        {
            m_car.transform.rotation = m_shortCutPoint.transform.rotation;
            m_car.transform.position = m_shortCutPoint.transform.position;
            m_car.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
        }
	}
}
