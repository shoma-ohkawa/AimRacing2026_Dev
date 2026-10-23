/*━━━━━━━━━
@file    GameStateBindingBase.cs
@brief   各シーンに常駐しGameFlowRunnerへ自己登録するStateの基底クラス
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/10/19
━━━━━━━━━*/

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class GameStateBindingBase : MonoBehaviour, IGameState
{
    private static bool s_isBootstrapping; //Manager読込〜Bootstrap()実行中か(同フレームの複数Awakeによる二重ロード防止)

    public abstract GameStateId Id { get; }

    protected virtual void Awake()
    {
        if (IsManagerSceneLoaded())
        {
            GameFlowRunner.Instance.Register(this);
            return;
        }

        if (s_isBootstrapping) //既に他のGameStateBindingBaseがManager読込中なら、自分は読込完了を待って登録だけ行う
        {
            StartCoroutine(RegisterAfterManagerLoads());
            return;
        }

        s_isBootstrapping = true;
        StartCoroutine(BootstrapAfterManagerLoads());
    }

    protected virtual void OnDestroy()
    {
        GameFlowRunner.Instance?.Unregister(this);
    }

    public virtual void Enter() { }

    public virtual void Tick(float _deltaTime) { }

    public virtual void Exit() { }

    private bool IsManagerSceneLoaded()
    {
        Scene managerScene = SceneManager.GetSceneByName(GameFlowGraph.k_ManagerSceneName);
        return managerScene.IsValid() && managerScene.isLoaded;
    }

    //他のGameStateBindingBaseが読込中のManagerロード完了を待ってから登録だけ行う関数(Bootstrap()は呼ばない)
    private IEnumerator RegisterAfterManagerLoads()
    {
        yield return new WaitUntil(IsManagerSceneLoaded);
        GameFlowRunner.Instance.Register(this);
    }

    //初回起動(Managerが未読込)の時だけ通る、1セッション1回きりの待ち合わせ
    private IEnumerator BootstrapAfterManagerLoads()
    {
        float logStart = Time.realtimeSinceStartup; //[LoadTiming] 最初のシーンのAwakeが呼ばれた時刻(=アプリの起動からここまでの秒数)
        yield return SceneManager.LoadSceneAsync(GameFlowGraph.k_ManagerSceneName, LoadSceneMode.Additive);
        float managerLoadSeconds = Time.realtimeSinceStartup - logStart; //Managerシーンの読込にかかった秒数
        AppLog.Log("[LoadTiming] Manager読込: " + managerLoadSeconds.ToString("F2") + "s (このシーンのAwakeまでの起動時間は含まず)");
        s_isBootstrapping = false; //Manager読込完了。以降のAwakeは通常の即時登録経路に戻る
        GameFlowRunner.Instance.Register(this); //Bootstrap対象が兄弟側になる場合に備え、自分自身も先に登録しておく
        GameFlowRunner.Instance.Bootstrap(ResolveBootstrapCandidate(), logStart, managerLoadSeconds); //起動時間と読込秒数は、計測(FlowTiming)に渡すだけ
    }

    /// <summary>
    /// 同一GameObject上の全GameStateBindingBaseから、実際にBootstrap()で入るべきStateを解決します。
    /// 他のどの兄弟からもGameFlowGraph.NextStateの遷移先として指されていない候補(＝そのシーン
    /// グループの入口)を優先します。候補が1つだけ、または該当が無い場合は自分自身を返します。
    /// </summary>
    private GameStateBindingBase ResolveBootstrapCandidate()
    {
        GameStateBindingBase[] siblings = GetComponents<GameStateBindingBase>(); //同一GameObject上の全候補
        for (int i = 0; i < siblings.Length; ++i) //候補を1つずつ検査するループ
        {
            bool hasIncomingFromSibling = false;
            for (int j = 0; j < siblings.Length; ++j) //他の兄弟からの遷移先になっていないか調べる内側ループ
            {
                if (i == j) { continue; }
                if (GameFlowGraph.NextState.TryGetValue(siblings[j].Id, out GameStateId nextId) && nextId == siblings[i].Id)
                {
                    hasIncomingFromSibling = true;
                    break;
                }
            }
            if (!hasIncomingFromSibling) { return siblings[i]; }
        }
        return this; //全候補が互いに遷移し合っている等、判定できない場合のフォールバック
    }
}
