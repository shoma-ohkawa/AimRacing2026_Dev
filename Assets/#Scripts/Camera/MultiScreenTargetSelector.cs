/*━━━━━━━━━
@file    MultiScreenTargetSelector.cs
@brief   映像を描画する横画面のモニターを決め、左右の並びから映像番号を求める(Unityに依存しない判定)
@author  24CU0110 大川翔生
@date    作成日   : 2026/11/10
         最終更新日 : 2026/11/10
━━━━━━━━━*/

using System.Collections.Generic;

/// <summary>
/// 映像を描画するモニターを決める判定をまとめたクラス。
/// Unityのディスプレイ番号とWindows APIのモニター番号は、縦画面があると一致しないことがある
/// (2026/11/10 のテストで、縦画面のディスプレイ番号が起動ごとに入れ替わった)。
/// そのため番号では対応させず、それぞれの側で縦画面を除いた横画面だけを集め、先頭から順に対応させる。
/// </summary>
public static class MultiScreenTargetSelector
{
    /// <summary>
    /// 描画する横画面のUnityのディスプレイ番号と、それに対応するWindows APIのモニター番号を決めます。
    /// メインモニターはゲームウィンドウの表示先なので、縦画面でも除外しません。
    /// 横画面の数がUnityとWindows APIで異なる場合は、少ない方に合わせます。
    /// </summary>
    /// <param name="_unityIsPortrait">Unityのディスプレイ番号ごとの、縦画面かどうか</param>
    /// <param name="_monitorIsPortrait">Windows APIのモニター番号(メインが先頭)ごとの、縦画面かどうか。取得できない場合は空</param>
    /// <param name="_mainIndex">メインモニターの番号(UnityとWindows APIで共通)</param>
    /// <param name="_displayIndices">描画するUnityのディスプレイ番号(昇順)</param>
    /// <param name="_monitorIndices">_displayIndicesと同じ並びの、対応するWindows APIのモニター番号。取得できない場合は空</param>
    public static void Select(
        bool[] _unityIsPortrait,
        bool[] _monitorIsPortrait,
        int _mainIndex,
        out int[] _displayIndices,
        out int[] _monitorIndices)
    {
        List<int> displayTargets = CollectLandscape(_unityIsPortrait, _mainIndex);

        //Windows APIで取得できない場合は、Unityの横画面のディスプレイをすべて使う処理
        if (_monitorIsPortrait.Length == 0)
        {
            _displayIndices = displayTargets.ToArray();
            _monitorIndices = new int[0];
            return;
        }

        List<int> monitorTargets = CollectLandscape(_monitorIsPortrait, _mainIndex);

        //横画面の数が合わない場合は、少ない方に合わせる処理
        int count = System.Math.Min(displayTargets.Count, monitorTargets.Count);

        _displayIndices = displayTargets.GetRange(0, count).ToArray();
        _monitorIndices = monitorTargets.GetRange(0, count).ToArray();
    }

    /// <summary>
    /// 描画するモニターごとに、左端を0とする映像番号(左右の並び)を求めます。
    /// 同じ位置の場合は、並びの先の方を左とみなします。
    /// </summary>
    /// <param name="_monitorX">描画するモニターごとの、左端のX座標</param>
    /// <returns>描画するモニターごとの映像番号</returns>
    public static int[] GetSliceIndices(int[] _monitorX)
    {
        int[] sliceIndices = new int[_monitorX.Length];

        for (int i = 0; i < _monitorX.Length; ++i)
        {
            int sliceIndex = 0;

            //自分より左にあるモニターの枚数を数える処理
            for (int j = 0; j < _monitorX.Length; ++j)
            {
                if (_monitorX[j] < _monitorX[i] || (_monitorX[j] == _monitorX[i] && j < i)) { ++sliceIndex; }
            }

            sliceIndices[i] = sliceIndex;
        }

        return sliceIndices;
    }

    //縦画面を除いた番号を集める関数(メインは必ず含める。最低1つ返す)
    static List<int> CollectLandscape(bool[] _isPortrait, int _mainIndex)
    {
        List<int> targets = new List<int>();

        for (int i = 0; i < _isPortrait.Length; ++i)
        {
            if (!_isPortrait[i] || i == _mainIndex) { targets.Add(i); }
        }

        //番号を1つも渡されなかった場合も、メインだけは描画する処理
        if (targets.Count == 0) { targets.Add(_mainIndex); }

        return targets;
    }
}
