/*━━━━━━━━━
@file    IGameState.cs
@brief   State実装が満たすべき最小限の契約
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/01
━━━━━━━━━*/

public interface IGameState
{
    GameStateId Id { get; }

    /// <summary>シーングループ有効化直後に1回呼ばれます。</summary>
    void Enter();

    /// <summary>毎フレーム呼ばれます。不要な場合は空実装で構いません。</summary>
    /// <param name="_deltaTime">前フレームからの経過秒数</param>
    void Tick(float _deltaTime);

    /// <summary>次State切替直前に1回呼ばれます。</summary>
    void Exit();
}
