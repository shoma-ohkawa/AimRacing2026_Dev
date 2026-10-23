using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditor.Callbacks;

public class WIZMOTools : EditorWindow
{
	//Create MenuItem
	/* ignore v4.1
	[MenuItem("GameObject/WIZMO/Default WIZMO System", false, 1)]
	static void AddWIZMOSystem()
	{
		if (GameObject.Find("WIZMOSystem") == null)
		{
			Object obj = Instantiate(Resources.Load("Prefabs/WIZMOSystem"), new Vector3(0.0f, 0.0f, 0.0f), Quaternion.identity);
		}
	}
	*/

    [PostProcessBuild]
	public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
	{
		// Copy of "PublicKey.txt" files
		string sourceFile = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "PublicKey.txt").Replace("\\", "/");
		string targetFile = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(pathToBuiltProject), "PublicKey.txt").Replace("\\", "/");

		if (System.IO.File.Exists(sourceFile))
		{
			//Copy
			if(sourceFile != targetFile) {
				System.IO.File.Copy(sourceFile, targetFile, true);
			}
		}
	}
}