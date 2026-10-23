/*━━━━━━━━━
@file    SingletonMonoBehaviour.cs
@brief   MonoBehaviour用の汎用シングルトン基底クラス
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/01
━━━━━━━━━*/

using UnityEngine;

public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T s_instance; //唯一のインスタンス
    private static bool s_bHasLoggedMissingInstance; //未アタッチ警告を出力済みか(型ごとに1回のみ出す)

    public static T Instance
    {
        get
        {
            if (s_instance == null && !s_bHasLoggedMissingInstance) //未アタッチかつ未警告の場合のみログを出す
            {
                s_bHasLoggedMissingInstance = true;
                AppLog.LogError(typeof(T) + "をアタッチしているGameObjectはありません");
            }
            return s_instance;
        }
    }

    protected virtual void Awake()
    {
        //このゲームオブジェクトにアタッチされているか調べ、重複していれば破棄する
        CheckInstance();
    }

    protected bool CheckInstance()
    {
        if (s_instance == null)
        {
            s_instance = this as T;
            s_bHasLoggedMissingInstance = false; //再アタッチされたので次回未アタッチになった際に再度警告できるようにする
            return true;
        }
        else if (s_instance == this)
        {
            return true;
        }

        Destroy(this); //重複コンポーネントのみ破棄し、同居する他コンポーネントは残す
        return false;
    }
}
