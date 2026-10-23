/*━━━━━━━━━
@file    GameStateId.cs
@brief   新シーン遷移エンジンにおける唯一のState識別子
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/10/19
━━━━━━━━━*/

public enum GameStateId
{
    Splash,
    Opening,
    Menu,
    Intro = 4, //値3(旧NameEntry)を欠番にして4へ固定する。Inspectorに保存済みの整数値(Manager.unity・Loading.unityのm_state等)がずれないよう、並び替えや欠番を詰める変更をしてはいけない
    Racing,
    Result,
    Load //既存値の並び替えによるInspector上の整数値ズレ(Loading.unity等のm_leavingState等)を避けるため末尾に追加
}
