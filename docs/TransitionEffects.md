# 遷移演出(フェード・Loading 画面)

作成: 2026/10/10(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/GameManager/Flow/`(`TransitionFadeController`、`SessionCacheRefresher`)、`Assets/#Scripts/UI/Loading/`(`LoadingScreenController`、`TitleLogoOverlay`)

State の切り替えの最中に、画面を覆って次のシーンを読み込み、終わったら戻すための演出と後始末。呼ばれる側で、いつ呼ぶかは遷移エンジン(リフレッシュだけは `LoadState`)が決める(`GameFlow.md`)。秒数・色・待ち時間は、すべてシーンの Inspector で設定する。

## 構成

| ファイル | 種別 | 役割 | 作成 / 更新 |
|---|---|---|---|
| `TransitionFadeController.cs` | シングルトン | State 別の秒数・色で、全モニターを単色で覆う・戻す | 09/01 / 09/10 |
| `LoadingScreenController.cs` | MonoBehaviour | 遷移の間だけ追加ロードされる Loading 画面。離脱元 State 別に FadeIn・保持・FadeOut の秒数を持つ | 09/13 / 09/17 |
| `TitleLogoOverlay.cs` | シングルトン | タイトルロゴの表示(FadeIn / FadeOut / 即時非表示) | 09/13 / 09/14 |
| `SessionCacheRefresher.cs` | static class | 前のプレイの状態のリセットと、GC・アセットの回収。各ステップを `FlowTiming` へ通知する | 09/16 / 10/19 |

## 画面を覆う 3 つの層

| 層 | クラス | シーン | 描画順 | 表示するモニター | 役割 |
|---|---|---|---|---|---|
| 遷移フェード | `TransitionFadeController` | `Manager` | 1000 | 全モニター | 遷移の最初に覆い、最後に戻す |
| Loading 画面 | `LoadingScreenController` | `Loading`(遷移の間だけ) | 1100 | 全モニター | ロードの間、黒背景で覆い続ける |
| タイトルロゴ | `TitleLogoOverlay` | `Manager` | 1100 | Display 0 のみ | Opening を中心に、ロゴを出し続ける |

- 全モニター用の層は、モニターの数だけ Canvas を作る(`Display.displays.Length`、最低 1。Editor は Display 0 の 1 枚)。
- Loading 画面を挟む遷移では、手前の Loading 画面の黒が消えたあとも、奥の遷移フェードの黒が残る。画面が見えるのは、最後の遷移フェードの FadeIn。色はすべて黒。

### 遷移ごとの設定(シーンの値)

FadeOut は離脱元、FadeIn は遷移先の State の設定を使う。Loading 画面の秒数は離脱元の設定を使う。

| 遷移 | FadeOut | Loading 画面 | FadeIn |
|---|---|---|---|
| Load → Splash | 未登録(覆わない) | なし | 0.2 秒 |
| Splash → Opening | 1 秒 | FadeIn 0.3 + 待機 0.3、保持は BGM の曲ごと(下記)、FadeOut 1 秒 | 0.2 秒 |
| Opening → Menu | 2.5 秒 | FadeIn 0.3、保持 2.312 秒、FadeOut 1 秒 | 0.2 秒 |
| Menu → Intro | 3 秒 | FadeIn 0.3、保持 0、FadeOut 1 秒 | 0.2 秒 |
| Intro → Racing | 0 秒 | なし | 0 秒 |
| Racing → Result | 1 秒 | FadeIn 0.3、保持 0、FadeOut 1 秒 | 0.2 秒 |
| Result → Load | 1 秒 | FadeIn 0.3、保持 0、FadeOut 1 秒 | 未登録(即座に戻す) |

## TransitionFadeController

`Manager` に 1 つ。起動直後は黒(alpha 1)から始まり、最初の FadeIn は遷移エンジンが呼ぶ。

| メンバー | 内容 |
|---|---|
| `FadeOut(float? _duration, Color? _color = null)` | 秒数が `null`(未登録)なら、覆わず即座に透明へ戻して終える。それ以外は全モニターを同時に alpha 1 へ。色の省略時は黒 |
| `FadeIn(float? _duration)` | 秒数が `null` なら即座に透明にする(黒のまま固定されるのを防ぐ)。それ以外は alpha 0 へ |
| `ResolveFadeOutDuration` / `ResolveFadeInDuration` / `ResolveFadeOutColor` | State 別の秒数・色。未登録は `null`(色は黒) |
| `m_stateFadeSettings` | State ごとの `m_fadeOutDuration`・`m_fadeInDuration`・`m_fadeOutColor`(Inspector) |

- 全モニターの画像を同時に動かし、すべての完了を待つ。
- DOTween を `SetUpdate(true)` にしてあり、`Time.timeScale` が 0 でも動く。
- フェード中は画像が入力を遮る(`raycastTarget`)。
- 白でのフェードは、Opening の曲間の Splash 挿入が、秒数と色を直接指定して使う。

登録(`Manager.unity`、色はすべて黒): Splash 1 / 0.2、Opening 2.5 / 0.2、Menu 3 / 0.2、Intro 0 / 0.2、Racing 1 / 0、Result 1 / 0.2(FadeOut / FadeIn の秒数)。Load は未登録。

## LoadingScreenController

`Loading` シーンに 1 つ。遷移エンジンが遷移の間だけ追加ロードし、終わると解放する。`Awake` で、モニターごとに黒背景の Canvas を作る(最初は alpha 0)。

| メンバー | 内容 |
|---|---|
| `Configure(_fromState, _toState)` | 離脱元と遷移先を受け取る。以降の秒数と動作が決まる |
| `PlayFadeIn()` | FadeIn して完了まで待つ。遷移エンジンはこれが終わってから次のシーンを読み始める。Opening → Menu では Entry の BGM を先行再生。Splash → Opening では、FadeIn のあと `m_titleLogoFadeInDelay` 待つ |
| `TriggerOpeningTitleReveal()` | Splash → Opening のときだけ。Opening の BGM を再生し、その時刻を記録し、ロゴを FadeIn する。Splash のアンロード直後に呼ばれる |
| `WaitForKeep()` | FadeIn のあとの保持時間だけ待つ(実時間) |
| `PlayFadeOut()` | FadeOut する。Opening → Menu では、ロゴの FadeOut も同時に始める |

- 秒数は、離脱元の State ごとに `m_fadeSettings` から引く。未登録は `null`(FadeIn・FadeOut は即座に切り替え、保持は待たない)。
- **Splash → Opening だけ、保持の秒数を再生中の BGM で上書きする。** 再生中のクリップ名で `m_splashToOpeningBgmKeepOverrides` を探し、一致すればその秒数、しなければ Splash の保持秒数。さらに、BGM を再生してからの経過時間を引いた残りだけ待つ(`保持 − 経過`、下限 0)。BGM の再生開始から Opening が見え始めるまでの合計を、曲ごとにそろえるため。

| 離脱元 | FadeIn | 保持 | FadeOut |
|---|---|---|---|
| Splash | 0.3 | 1.5 | 1 |
| Opening | 0.3 | 2.312 | 1 |
| Menu / Racing / Result | 0.3 | 0 | 1 |

| 設定 | 値 |
|---|---|
| `m_titleLogoFadeInDelay` | 0.3 秒 |
| Splash → Opening の保持(クリップ名 `OP1`) | 4.55 秒 |
| 同(`OP2 0913 AM04_44修正済み差し替え希望`) | 1.8 秒 |
| 同(`OP3`) | 3 秒 |

流れ:

```
Splash → Opening
遷移フェード FadeOut(1)→ Loading FadeIn(0.3)+ 待機(0.3)
→ Splash の Exit、Opening のロード、Splash のアンロード
→ TriggerOpeningTitleReveal(Opening の BGM + ロゴ FadeIn 0.5)
→ Opening の Enter(BGM は再生中なので鳴らし直さない)→ 2 フレーム待つ
→ 保持(曲ごとの秒数 − 経過)→ Loading FadeOut(1)→ Loading をアンロード
→ 遷移フェード FadeIn(0.2)

Opening → Menu
遷移フェード FadeOut(2.5)→ Loading FadeIn(0.3)(この間に Entry の BGM を先行再生)
→ Opening の Exit、Menu のロード、Opening のアンロード、Menu の Enter → 2 フレーム待つ
→ 保持(2.312)→ Loading FadeOut(1)+ ロゴ FadeOut(1)→ Loading をアンロード
→ 遷移フェード FadeIn(0.2)
```

## TitleLogoOverlay

`Manager` に 1 つ。ロゴ画像(`m_logoImage`)は専用の Canvas(描画順 1100、Display 0)にある。起動時に alpha 0 にする(シーンは Editor で見やすいよう不透明のまま保存)。

| メンバー | 内容 |
|---|---|
| `FadeIn()` | `m_fadeInDuration`(0.5 秒)で表示 |
| `FadeOut()` | `m_fadeOutDuration`(1 秒)で隠す |
| `ForceHide()` | 進行中のトゥイーンを止め、即座に非表示 |

表示する区間は、Splash → Opening で Splash のアンロードが終わった直後(Opening の BGM と同時)から、Opening → Menu の Loading 画面の FadeOut と同時まで。`Opening` と `Menu` 以外の State に入るときは、遷移エンジンが `ForceHide()` を呼ぶ(緊急リセットでもロゴを残さないため)。

## SessionCacheRefresher

1 セッション分のプレイで溜まる状態とガベージを片付ける。呼び出し元は `LoadState` だけで、Load の FadeIn が終わって画面が見えてから実行する。元は Loading 画面側の Result → Splash の特殊な場合だったものを、Load の State に一本化した。

`Refresh()`(コルーチン)の手順:

1. プレイヤー名のクリア(`AimRacingPlayerName.Clear()`)
2. 結果とラップタイムのクリア(`GameManager`)
3. サウンドのフェード状態のリセット(`SoundManager.ResetFadeState()`。再生中の BGM の番号と長さも初期値へ)
4. ポーズ状態のリセット(`DebugKey.ResetPauseState()`)
5. `GC.Collect()`(未参照のアセットを正しく判定させるため先に)
6. `Resources.UnloadUnusedAssets()` を待つ
7. `GC.Collect()` → `WaitForPendingFinalizers()` → `GC.Collect()`(ファイナライザ待ちは 1 回で回収されないことがある)

再初期化しないもの: WIZMO の接続とキャリブレーション、Logitech SDK の初期化状態、ランキングの送信キュー、BGM の抽選サイクル、TCP サーバーの接続状態、証明書の `RenderTexture` のキャッシュ、デバッグ用のトグル、`GameFlowRunner` の内部状態。

## 他のメンバーのモジュール

| モジュール | 使い方 |
|---|---|
| DOTween(外部) | フェードのアニメーション |
| `SoundManager`(小林大輝が作成、後に更新あり) | BGM の先行再生、再生中のクリップ名の取得、フェード状態のリセット |
| `GameManager`、`DebugKey`(要確認) | 結果・ラップタイムのクリア、ポーズ状態のリセット |
| `AimRacingPlayerName`(久場洸太) | プレイヤー名のクリア |

## 未対応・要確認

| 項目 | 内容 |
|---|---|
| 描画順の重なり | ロゴの Canvas と Loading 画面の Canvas が同じ描画順(1100)。ロゴは Loading 画面の黒の上に見える必要があるが、同値の前後関係は設定の値から決まらない。実機で確認する |
| 保持秒数の文字列一致 | BGM 別の保持秒数は、クリップ名の文字列一致で決まる。`OP2` は作業メモ入りの長い名前で、クリップを差し替えて名前が変わると、**エラーも警告も出ずに** Splash の保持(1.5 秒)に戻る |
| `m_titleLogoFadeInDelay` | 名前と説明は「ロゴの FadeIn を始めるまでの待機」だが、現在はロゴの FadeIn を `TriggerOpeningTitleReveal` が始める。この値は、Loading 画面の FadeIn のあと、シーン切り替えの前に一呼吸置く待機として使われている。名前と説明が実態と違う |
| `ForceHide()` | `DOKill()` で止めるが完了コールバックは呼ばれない。待っているコルーチンは `LoadingScreenController` で動いており、Loading シーンのアンロードで止まる。現在の流れでは実害なし |
| Load → Splash | `Load` の FadeOut が未登録なので、覆わずに切り替わる。意図か未確認 |

## PDCA

担当: 9 名のチーム制作のうち、遷移の演出 4 本。サウンドやランキングなどの連携先は別のメンバー。期間: 2026/09/01〜09/18。

| | 内容 |
|---|---|
| 目標 | ロードの重さや切り替えの乱れを来場者に見せず、前のプレイの状態を引きずらない。① 秒数・色・待ち時間をコードを変えず State 別に調整 ② ロードの負荷でフェードが乱れない ③ Opening の BGM と映像が見え始めるタイミングを曲ごとにそろえる ④ 前の状態を引き継がず、ガベージを溜めない ⑤ すべてのモニターを覆う。達成の基準の数値は未記載 |
| 課題 | ロードとフェードが重なるとフェードが乱れる / State が増えるたびにコードが増える、未登録で黒く固定される / OP の曲ごとに長さが違う / ロゴを Opening の間だけ出し、緊急リセットでは隠す機会がない / 前のプレイの状態とガベージが残る / モニター数が変わる / `timeScale` が 0 でも演出を動かす |
| 検証・結果 | 秒数・色・保持時間を Inspector だけで調整できる(フェード 6 State、Loading 画面 5 離脱元、BGM 別の保持 3 曲)。リフレッシュを Load の 1 か所に集約した(2026/09/16)。BGM の再生開始から Opening が見え始めるまでの時間を、曲ごとにそろえる仕組みを作った。ほとんどは実装とコードの確認のみ |
| 未記録 | 遷移 1 回あたりの所要時間(2026/10/19 から `transitions.csv` に記録できる。突き合わせは未実施) / 曲ごとの見え始めの時刻の実測 / リフレッシュ前後のメモリと GC(`transitions.csv` の `Refresh` に記録できる。読み取りは未実施) / 複数モニターの実機確認 / 展示(TGS2026)の結果 |
| 次の候補 | ロゴと Loading 画面の重なり順を実機で確認し、必要なら描画順を分ける / 保持秒数の上書きを文字列一致以外にする検討 / `m_titleLogoFadeInDelay` の名前と説明を直す / リフレッシュ前後のメモリと所要時間の確認 |
