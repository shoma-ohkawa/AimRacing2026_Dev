/*━━━━━━━━━
@file    RacingEnterEffectPattern.cs
@brief   Racingに入った時の演出パターンの識別子
@author  24CU0110 大川翔生
@date    作成日   : 2026/10/15
         最終更新日 : 2026/10/15
━━━━━━━━━*/

//Racingに入った時に再生する演出の種類(すべてポストプロセス)。RacingStateのInspectorで選ぶ
//Inspectorには整数で保存されるため、並び替えや途中への挿入はしない(追加は末尾に行う)
public enum RacingEnterEffectPattern
{
    None,           //演出なし
    VignetteOpen,   //周辺の暗がりが開く
    ChromaticFlash, //色収差・ブルーム・露出の閃光が収まる
    LaunchCombo,    //ビネットと色収差をまとめて使う
    BlurFocus,      //画面全体がぼけた状態から、徐々にピントが合って鮮明になる
}
