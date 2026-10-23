/*━━━━━━━━━
@file    GameFlowGraph.cs
@brief   State→シーングループ、State→次State を1箇所に宣言する唯一の定義箇所
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/10/19
━━━━━━━━━*/

using System.Collections.Generic;

public static class GameFlowGraph
{
    public const string k_ManagerSceneName = "Manager"; //常駐シーン名(GameStateBindingBaseのcold boot判定でも使用)
    private const string k_LoadSceneName = "Load"; //起動時とF1リセット時に必ず経由する中継シーン名(自前のカメラを持ち、Manager/MapEnvironmentの安定を待つ)
    private const string k_SplashSceneName = "Splash"; //起動シーン名
    private const string k_OpeningSceneName = "Opening"; //オープニングシーン名
    private const string k_MenuSceneName = "Menu"; //メニューシーン名
    private const string k_MapEnvironmentSceneName = "MapEnvironment"; //Map静的環境の永続シーン名(観客・並木・ガードレール)
    private const string k_MapSceneName = "Map"; //コースシーン名
    private const string k_ResultSceneName = "Result"; //結果表示シーン名(旧Endを吸収)

    //State→シーングループの対応表
    public static readonly IReadOnlyDictionary<GameStateId, GameSceneGroup> SceneGroups = new Dictionary<GameStateId, GameSceneGroup>
    {
        //MapEnvironmentを含めることで、起動時のBootstrapRoutine/F1のEmergencyResetがどちらも
        //既存のシーングループ読込ループだけでManager+MapEnvironmentの読込と安定待ちを済ませられる
        { GameStateId.Load,    new GameSceneGroup(k_LoadSceneName,    k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Splash,  new GameSceneGroup(k_SplashSceneName,  k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Opening, new GameSceneGroup(k_OpeningSceneName, k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Menu,    new GameSceneGroup(k_MenuSceneName,    k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Intro,   new GameSceneGroup(k_MapSceneName,     k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Racing,  new GameSceneGroup(k_MapSceneName,     k_ManagerSceneName, k_MapEnvironmentSceneName) },
        { GameStateId.Result,  new GameSceneGroup(k_ResultSceneName,  k_ManagerSceneName, k_MapEnvironmentSceneName) },
    };

    //宣言的な遷移順序。各Stateは自分の次がどこかを知らなくてよい
    public static readonly IReadOnlyDictionary<GameStateId, GameStateId> NextState = new Dictionary<GameStateId, GameStateId>
    {
        { GameStateId.Load,    GameStateId.Splash },
        { GameStateId.Splash,  GameStateId.Opening },
        { GameStateId.Opening, GameStateId.Menu },
        { GameStateId.Menu, GameStateId.Intro },
        { GameStateId.Intro,   GameStateId.Racing },
        { GameStateId.Racing,  GameStateId.Result },
        { GameStateId.Result,  GameStateId.Load }, //セッションキャッシュのリフレッシュをLoadStateへ一本化するため、Splashへ直接ではなくLoadを経由する
    };
}
