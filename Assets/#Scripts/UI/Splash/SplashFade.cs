using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplashFade : MonoBehaviour
{
	[SerializeField]
	private Image[] m_image = null;

	[System.Serializable]
	public struct FadeTime
	{
		public float In;
		public float Full;
		public float Out;
	}
	[SerializeField] private FadeTime m_fadeTime;

	private bool m_isSkip = false;

	private bool m_isEndAnimation = false;

	public bool IsEndAnimation => m_isEndAnimation;

	private void Awake()
	{
		for (int i = 0; i < m_image.Length; i++)
		{
			m_image[i].color = new Color(1f, 1f, 1f, 0f);
			m_image[i].enabled = false;
		}
	}

	private void Start()
	{
		StartCoroutine(StartAnimation());
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.PageDown))
		{
			m_isSkip = true;
		}
	}

	//登録されている枚数分、フェードイン→保持→フェードアウトを順番に再生する関数
	private IEnumerator StartAnimation()
	{
		for (int i = 0; i < m_image.Length; i++)
		{
			yield return FadeIn(i);

			yield return new WaitForSeconds(m_fadeTime.Full);

			yield return FadeOut(i);

			m_isSkip = false;	//スキップ入力を画像ごとにリセット(以後の画像まで早送りが続かないように)
		}

		m_isEndAnimation = true;
		yield return null;
	}

	private IEnumerator FadeIn(int count)
	{
		m_image[count].enabled = true;
		float elapsedTime = 0.0f;
		Color startColor = m_image[count].color;
		Color endColor = new Color(startColor.r, startColor.g, startColor.b, 1f);

		while (elapsedTime < m_fadeTime.In)
		{
			if (m_isSkip)
			{
				elapsedTime = m_fadeTime.In;
			}
			elapsedTime += Time.deltaTime;
			float t = Mathf.Clamp01(elapsedTime / m_fadeTime.In);
			m_image[count].color = Color.Lerp(startColor, endColor, t);
			yield return null;
		}

		m_image[count].color = endColor;
	}

	private IEnumerator FadeOut(int count)
	{
		float elapsedTime = 0.0f;
		Color startColor = m_image[count].color;
		Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

		while (elapsedTime < m_fadeTime.Out)
		{
			if (m_isSkip)
			{
				elapsedTime = m_fadeTime.Out;
			}
			elapsedTime += Time.deltaTime;
			float t = Mathf.Clamp01(elapsedTime / m_fadeTime.Out);
			m_image[count].color = Color.Lerp(startColor, endColor, t);
			yield return null;
		}

		m_image[count].color = endColor;
	}
}
