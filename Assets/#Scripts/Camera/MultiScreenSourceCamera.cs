using System.Collections;
using UnityEngine;

//シーンのカメラを複数モニター出力へ登録するクラス
[RequireComponent(typeof(Camera))]
public class MultiScreenSourceCamera : MonoBehaviour
{
    //複数モニターへ出力するカメラ
    Camera m_camera;

    //画面出力の生成を待つ処理
    Coroutine m_registrationRoutine;

    //カメラ有効化時に複数モニター出力への登録を開始する関数
    void OnEnable()
    {
        m_camera = GetComponent<Camera>();
        //==========================
        //常駐する画面出力より先にカメラが有効化されても白画面にならないよう登録完了まで待つ処理
        m_registrationRoutine = StartCoroutine(RegisterWhenOutputIsReady());
        //==========================
    }

    //画面出力が利用可能になった時点で現在のカメラを登録する関数
    IEnumerator RegisterWhenOutputIsReady()
    {
        while (isActiveAndEnabled && MultiScreenOutput.Instance == null) { yield return null; }
        if (!isActiveAndEnabled || MultiScreenOutput.Instance == null) { yield break; }
        MultiScreenOutput.Instance.SetSourceCamera(m_camera);
        m_registrationRoutine = null;
    }

    //無効化されたカメラの登録待機だけを停止する関数
    void OnDisable()
    {
        if (m_registrationRoutine == null) { return; }
        StopCoroutine(m_registrationRoutine);
        m_registrationRoutine = null;
    }
}
