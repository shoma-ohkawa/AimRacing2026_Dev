using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.PostProcessing
{
    public class General
    {
		/// <summary>
		/// スプライトをメッシュに変換する
		/// </summary>
		/// <param name="sprite">用意したSprite</param>
		/// <returns>Spriteを変換したMesh</returns>
		public Mesh SpriteToMesh(Sprite sprite)
		{
			var mesh = new Mesh();
			mesh.SetVertices(Array.ConvertAll(sprite.vertices, c => (Vector3)c).ToList());
			mesh.SetUVs(0, sprite.uv.ToList());
			mesh.SetTriangles(Array.ConvertAll(sprite.triangles, c => (int)c), 0);

			return mesh;
		}

		/// <summary>
		///　PropertySheetの作成
		/// </summary>
		/// <param name="shader">用意したShader</param>
		/// <returns>PropertySheet</returns>
		//public PropertySheet GetSheet(Shader shader)
		//{
		//	var shaderName = shader.name;
		//	var material = new Material(shader)
		//	{
		//		name = string.Format("PostProcess - {0}", shaderName.Substring(shaderName.LastIndexOf('/') + 1)),
		//		hideFlags = HideFlags.DontSave
		//	};
		//	var sheet = new PropertySheet(material);
		//	return sheet;
		//}
	}
}
