using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class goal_destoryAtEndScene : MonoBehaviour
{
	Coroutine co = null;

	void Start()
	{

	}

	void Update()
	{

		if (SceneManager.GetActiveScene().name == "End")
		{
			if (co == null)
			{
				co = StartCoroutine(Delay_destroy(0.5f));
			}

		}

	}

	IEnumerator Delay_destroy(float wiattime)
	{
		yield return new WaitForSeconds(wiattime);

		Destroy(gameObject);
	}

}

