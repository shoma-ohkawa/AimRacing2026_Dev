/**
 * @file    UNKO_Editor.cs
 * @brief　UNKO
 * @author  yuuha terae
 */

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
#endif

#if UNITY_EDITOR
public class UNKO_Editor : EditorWindow {
    [MenuItem("UNKO/UNKO Editor")]
    private static void ShowWindow() {
        var window = GetWindow<UNKO_Editor>("UIElements");
        window.titleContent = new GUIContent("UNKO Editor");
        window.Show();
    }

    private List<string> objOriginalNameBuffer = new List<string>();    // 元の名前を保存しておくバッファ
    const string m_newObjName = "UNKO";    // 変更後の名前

    private void OnGUI() {
        if (GUILayout.Button("Create UNKO")) {
            ChangeAllObjNameToSpecifiedName(m_newObjName);
        }

        if (GUILayout.Button("Flush the UNKO")) {
            ReturnObjNameToOriginalName();
        }
    }

    // 全てのオブジェクトの名前を指定した名前に変更する
    private void ChangeAllObjNameToSpecifiedName(string _newObjName) {
        // ボタンが押された場合、コンソールにログを表示
        Debug.Log("Pressed create UNKO button");

        // Typeで指定した型の全てのオブジェクトを配列で取得し,その要素数分繰り返す.
        foreach (GameObject obj in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(GameObject))) {

            // アセットからパスを取得.シーン上に存在するオブジェクトの場合,シーンファイル（.unity）のパスを取得.
            string path = AssetDatabase.GetAssetOrScenePath(obj);

            // シーン上に存在するオブジェクトかどうか文字列で判定.
            bool isScene = path.Contains(".unity");

            // シーン上に存在するオブジェクトならば処理.
            if (isScene) {
                if (obj.name != _newObjName) {
                    objOriginalNameBuffer.Add(obj.name);
                    obj.name = _newObjName;
                }
            }
        }
    }

    // 全てのオブジェクトの名前を元の名前に戻す
    private void ReturnObjNameToOriginalName() {
        // ボタンが押された場合、コンソールにログを表示
        Debug.Log("Pressed Flush the UNKO button");
        int tmp = 0;
        // Typeで指定した型の全てのオブジェクトを配列で取得し,その要素数分繰り返す.
        foreach (GameObject obj in UnityEngine.Resources.FindObjectsOfTypeAll(typeof(GameObject))) {

            // アセットからパスを取得.シーン上に存在するオブジェクトの場合,シーンファイル（.unity）のパスを取得.
            string path = AssetDatabase.GetAssetOrScenePath(obj);

            // シーン上に存在するオブジェクトかどうか文字列で判定.
            bool isScene = path.Contains(".unity");

            // シーン上に存在するオブジェクトならば処理.
            if (isScene) {
                if (objOriginalNameBuffer.Any()) {
                    if (obj.name != objOriginalNameBuffer[tmp])
                        obj.name = objOriginalNameBuffer[tmp];
                }
                tmp++;
            }
        }
    }
}
#endif
