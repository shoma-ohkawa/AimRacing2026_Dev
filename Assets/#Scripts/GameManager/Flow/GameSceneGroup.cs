/*━━━━━━━━━
@file    GameSceneGroup.cs
@brief   1つのGameStateIdに対応する、読み込むべきシーン一覧を表す構造体
@author  24CU0110 大川翔生
@date    作成日   : 2026/09/01
         最終更新日 : 2026/09/01
━━━━━━━━━*/

using System.Collections.Generic;

public readonly struct GameSceneGroup
{
    public readonly string PrimarySceneName; //アクティブシーンに設定する主シーン名
    public readonly IReadOnlyList<string> SceneNames; //このStateで読み込む全シーン名(PrimarySceneName含む)

    /// <summary>
    /// シーングループを構築します。
    /// </summary>
    /// <param name="_primarySceneName">アクティブシーンに設定する主シーン名</param>
    /// <param name="_additionalSceneNames">主シーン以外に読み込む共有シーン名(Manager等)</param>
    public GameSceneGroup(string _primarySceneName, params string[] _additionalSceneNames)
    {
        PrimarySceneName = _primarySceneName;

        List<string> sceneNames = new List<string>(_additionalSceneNames.Length + 1); //構築用の一時リスト
        for (int i = 0; i < _additionalSceneNames.Length; ++i) //追加シーン名を先に積む
        {
            sceneNames.Add(_additionalSceneNames[i]);
        }
        sceneNames.Add(_primarySceneName);

        SceneNames = sceneNames;
    }
}
