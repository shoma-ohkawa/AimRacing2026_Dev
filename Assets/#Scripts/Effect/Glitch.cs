using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.Rendering.PostProcessing
{
    [DisallowMultipleComponent,ExecuteInEditMode]
    [AddComponentMenu("Rendering/Post-process Glitch",10000)]
    [RequireComponent(typeof(Camera))]

    public class Glitch : MonoBehaviour
    {
		#region Inspector Member
		public bool IsActive;
		[Header("波状変位")]
		[SerializeField]
		private float Frequecy;
		[SerializeField]
		private Vector4 DisplAmount;
		[Header("ランダムストライプ")]
		[SerializeField]
		private float RightStripesAmount;
		[SerializeField, RangeAttribute(0, 1)]
		private float RightStripesFill;
		[SerializeField]
		private float LeftStripesAmount;
		[SerializeField, RangeAttribute(0, 1)]
		private float LeftStripesFill;
		[Header("色収差")]
		[SerializeField]
		private float AmountX;
		[SerializeField]
		private float AmountY;
		#endregion

		#region Private Member
		private General _general = new General();
		#endregion

		#region Public Member
		[HideInInspector]
		public Shader _shader;  // publicにしないとexeで動かなくなる。理由は調査中。
		#endregion

		#region Unity Function
		private void OnEnable()
		{
			_shader = Shader.Find("Glitch/GlitchShader");
		}
		#endregion
		//public void Render(PostProcessRenderContext context)
		//{
		//	if (!IsActive) { return; }

		//	var sheet = _general.GetSheet(_shader);
		//	sheet.ClearKeywords();

		//	var cmd = context.command;
		//	// 波状変位
		//	var displAmount = new Vector4(Random.Range(-DisplAmount.x, DisplAmount.x), Random.Range(-DisplAmount.y, DisplAmount.y), DisplAmount.z, DisplAmount.w);
		//	sheet.properties.SetVector("_DisplacementAmount", displAmount);
		//	sheet.properties.SetFloat("_WavyDisplFreq", Random.Range(-Frequecy, Frequecy));
		//	// ランダムストライプ
		//	time += Time.deltaTime * 100;
		//	if (time > RightStripesAmount) time = 0;
		//	sheet.properties.SetFloat("_RightStripesAmount", time /*Random.Range(-RightStripesAmount, RightStripesAmount)*/);
		//	sheet.properties.SetFloat("_RightStripesFill", RightStripesFill);
		//	sheet.properties.SetFloat("_LeftStripesAmount", LeftStripesAmount);
		//	sheet.properties.SetFloat("_LeftStripesFill", LeftStripesFill);

		//	// 色収差
		//	sheet.properties.SetFloat("_ChromAberrAmountX", Random.Range(-AmountX, AmountX));
		//	sheet.properties.SetFloat("_ChromAberrAmountY", Random.Range(-AmountY, AmountY));

		//	cmd.BlitFullscreenTriangle(context.source, context.destination, sheet, 0);
		//}

    }
}
