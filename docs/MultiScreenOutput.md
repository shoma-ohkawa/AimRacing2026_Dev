# マルチモニター出力

作成: 2026/10/10(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/Camera/`

1 台のカメラの映像を、接続されているモニターの数だけ横に並べた 1 枚の RenderTexture に描き、モニターごとに該当部分を切り出して表示する。枚数・解像度・左右の並びは、起動時にすべて自動で検出して割り当てる(Inspector の手動設定はない)。モニターが 1 枚でも起動する。

## 構成

| ファイル | 種別 | 役割 | 作成 / 更新 |
|---|---|---|---|
| `MultiScreenOutput.cs` | シングルトン | モニターの検出と有効化、RenderTexture の作成、モニターごとの表示 Canvas の作成 | ヘッダなし |
| `MultiScreenSourceCamera.cs` | MonoBehaviour | シーンのカメラを、出力元として `MultiScreenOutput` に登録する | ヘッダなし |
| `WindowsMonitorInfo.cs` | static class | Windows API で、モニターの枚数・配置・メイン判定・解像度を取得する | 09/27 / 09/27 |

## 仕組み

```
  カメラ(1 台)── 描画 ──▶ RenderTexture(幅 = 1 台分 × 枚数、高さ = 1 台分)
                                      │  モニターごとに、横方向の範囲を切り出す(uvRect)
                 ┌────────────────────┼────────────────────┐
             Display[1]           Display[0]           Display[2]
             (左から 1 番目)       (メイン、2 番目)       (左から 3 番目)
```

- 上の割り当ては、3 画面の実機での出力例。
- 映像は 1 回だけ描き、モニターごとに切り出す。モニターごとにカメラを置く方式ではない。
- 切り出した範囲は、そのモニターの画面いっぱいに引き伸ばす(解像度の違うモニターは伸びる)。
- 表示は `ScreenSpaceOverlay` の Canvas を、モニターごとに 1 枚(`targetDisplay` にディスプレイ番号)。

## 起動の流れ

| 順 | 処理 | 内容 |
|---|---|---|
| 1 | `Awake()` | 重複なら何もしない。Windows API と Unity のモニター一覧をログに出す(Editor と Development Build のみ) |
| 2 | Editor | 出力枚数を 1 にして終える(通常の Game View を維持) |
| 3 | `ActivateDisplays()` | モニターを検出して枚数を決める → メインの解像度を決め、全画面ウィンドウで設定 → メイン以外を有効化 → 映像の割り当てを決める |
| 4 | `Start()` | RenderTexture を作る。登録済みのカメラがあれば出力先を設定。表示 Canvas の作成を始める |
| 5 | Canvas の作成 | モニターの有効化直後は描画が不安定なので、5 フレーム待ってから、モニターごとに作る |
| 6 | `OnDestroy()` | カメラの出力先を外し、RenderTexture を解放する |

- `SetSourceCamera(_camera)` で出力元のカメラを切り替える。RenderTexture が作成済みなら、直ちに出力先を更新する。
- 実機では、カメラの `targetTexture` を RenderTexture にし、アスペクト比を RenderTexture の幅 ÷ 高さに合わせる。出力元が未登録なら、エラーログを出して何もしない。
- Editor では分割しない。`targetTexture` を外して Display 0 に直接描き、表示 Canvas も作らない(Editor で RenderTexture に描くと「No cameras rendering」の表示や白い画面になるため)。分割した出力の確認は、ビルドで行う。

## 自動検出の規則

| 項目 | 規則 |
|---|---|
| 出力枚数 | `min(Windows API のモニター数, Display.displays.Length)`。下限 1。食い違うと警告を出して少ない方に合わせる。Windows API で取れなければ Unity の台数 |
| 1 枚の解像度 | メインモニターの物理解像度(Windows API)。取れなければ Unity の `systemWidth` / `systemHeight`。それも無理なら 1920×1080 |
| RenderTexture | 幅 = 1 枚の幅 × 枚数、高さ = 1 枚の高さ、深度 24 ビット |
| 左右の並び | 各モニターの `m_pixelRect.x`(左端の座標)が小さい順に 0, 1, 2 …。同じ座標なら Unity のディスプレイ番号が小さい方が左 |
| Unity と Windows の対応 | `Display[0]` はメインモニター。`Display[1]` 以降は Windows API の列挙順(メインを除く) |
| 取得できない場合 | モニター一覧が空なら、Unity のディスプレイ番号順をそのまま左からの並びにして警告 |
| 警告 | Unity と Windows API で解像度が異なる場合 |
| 1 枚の接続 | 異常ではなく通常の動作として、通常のログ(警告にしない) |

起動ログの例(3 画面の実機):

```
[WindowsMonitorInfo] Windowsが認識しているモニター数: 3
[WindowsMonitorInfo] Monitor[0] \\.\DISPLAY1 primary:False pixel:1920x1080@(-1920,0)
[WindowsMonitorInfo] Monitor[1] \\.\DISPLAY2 primary:True  pixel:1920x1080@(0,0)
[WindowsMonitorInfo] Monitor[2] \\.\DISPLAY3 primary:False pixel:1920x1080@(1920,0)
[MultiScreenOutput] メインディスプレイの解像度: 1920x1080 (Unityの認識: 1920x1080)
[MultiScreenOutput] Display[0] = \\.\DISPLAY2 → 左から2番目 (1920x1080@0)
[MultiScreenOutput] Display[1] = \\.\DISPLAY1 → 左から1番目 (1920x1080@-1920)
[MultiScreenOutput] Display[2] = \\.\DISPLAY3 → 左から3番目 (1920x1080@1920)
```

## WindowsMonitorInfo

`user32.dll` を呼び出してモニター情報を取る。Windows 以外では空の結果を返す。

| メンバー | 内容 |
|---|---|
| `WindowsMonitor`(構造体) | デバイス名、メインか、`m_desktopRect`(仮想デスクトップ上の矩形)、`m_pixelRect`(物理ピクセルの現在の解像度と位置) |
| `GetMonitors()` | OS の列挙順で取得。失敗しても例外を投げず、空のリスト |
| `GetMonitorsInUnityOrder()` | メインを先頭に移し、残りは列挙順。Unity の `Display.displays` に対応させる |
| `LogAll()` | 結果をログに出す。Editor と Development Build 以外では呼び出しごと除去 |

| 取りたい情報 | API |
|---|---|
| 枚数 | `EnumDisplayMonitors` |
| 配置・メイン判定・デバイス名 | `GetMonitorInfoW` |
| 物理解像度と位置 | `EnumDisplaySettingsW`(`ENUM_CURRENT_SETTINGS`) |

- `m_desktopRect` は DPI の仮想化の影響を受けることがある。実際の解像度は `m_pixelRect` を使い、`EnumDisplaySettingsW` が使えなければ `m_desktopRect` で代用する。
- IL2CPP でも動くよう、列挙のコールバックに `[AOT.MonoPInvokeCallback]` を付ける。デリゲートは `static readonly` で保持し、結果のリストは使い回す(メインスレッド専用)。構造体のサイズは呼び出し前に設定する。

## MultiScreenSourceCamera

シーンの Main Camera に付ける。`OnEnable()` で `MultiScreenOutput` の生成を毎フレーム待ち、生成されたら `SetSourceCamera` で登録する。待っている間に無効化されたら、待機だけを止める。常駐の画面出力より先にカメラが有効になっても、白い画面にならないようにするため。

## 配置と設定

| 項目 | 内容 |
|---|---|
| `MultiScreenOutput` | `Manager` シーンに 1 つ |
| `MultiScreenSourceCamera` | `Load`・`Splash`・`Opening`・`Menu`・`Map`・`Result` の Main Camera。`Loading` には付いていない |
| カメラの Viewport Rect | 上の 6 シーンとも x=0, y=0, 幅=1, 高さ=1。**幅は 1.0 にする**(狭めると、その割合の範囲にしか映らない) |
| カメラの出力先 | シーンには設定せず、実行時に `MultiScreenOutput` が設定 |
| ProjectSettings | `runInBackground` ON、`fullscreenMode` FullScreenWindow、既定の画面サイズ 2560×1440 |

他のモジュール: `SingletonMonoBehaviour`・`AppLog`(`CommonFoundation.md`)。`TransitionFadeController`・`LoadingScreenController` は `Display.displays.Length` を基にモニターごとの Canvas を作る。この仕組みの有効化は `min(Windows API の台数, Unity の台数)` で決まるので、台数が食い違うと、有効化されないディスプレイ向けの Canvas ができる(表示されないだけ)。

## 未対応・要確認

| 項目 | 内容 |
|---|---|
| 起動順のエラー | `MultiScreenOutput.Instance` は、インスタンスがない状態で最初に参照されるとエラーを 1 回出す。`Manager` の読み込みより先にカメラが有効になるシーンで出る(Editor と Development Build のみ)。動作に影響なし。対策案は、`Instance` を使わず存在を確認すること |
| 古いカメラの出力先 | `SetSourceCamera` は、前のカメラの `targetTexture` を外さない。遷移で新旧のシーンが同時にある間は、2 台が同じ RenderTexture に描く。画面は覆われているので見えないが、実害の有無は未確認 |
| 解像度の違うモニター | 1 枚の解像度はメインに合わせ、他は引き伸ばす。解像度やアスペクト比の違うモニターを混ぜると、映像が伸び縮みする |
| 対応規則の確認範囲 | 「`Display[0]` がメイン、`Display[1]` 以降は列挙順」は、3 画面の実機で 2 回起動して確認した。展示用 PC では、枚数と解像度の自動検出は確認したが、左・中央・右の並びで映ったかの記録がない。ずれた場合の代替案は、`DisplayInfo.name` と Windows のデバイス名の突き合わせ |
| 規約との差 | `MultiScreenOutput.cs` と `MultiScreenSourceCamera.cs` にファイルヘッダがない。定数・フィールドにアクセス修飾子がない。参照型(`Camera`、`RenderTexture`)のフィールドに `m_p` がない |
| 未検証 | モニターが 4 枚以上の場合 / 拡大縮小(DPI)の設定がある環境 / シングルモニターでの実機起動 |

旧方式のスクリプト `MultiDispllays.cs`(ウィンドウ位置を固定値で指定)は、未使用のため 2026/10/10 にプロジェクト外へ退避した。

## PDCA

担当: 9 名のチーム制作のうち、画面出力の仕組み(3 本)を全面的に担当。性能の計測と HDRP の設定の見直しも担当。期間: 2026/09/27〜09/29(自動検出の実装と実機検証)、性能の比較は 2026/10/03。

用語: **5/28 版** = 2026/05/28 時点のバージョン。NVIDIA Surround(7680×1080 を 1 つのディスプレイとして出力)で、`MultiScreenOutput` はなかった。**展示用 PC** = Intel Ultra 9 / RTX 5060 Ti / メモリ 64 GB。

| | 内容 |
|---|---|
| 目標 | 会場の構成に左右されず、設定なしで画面出力が立ち上がる。3 画面の出力解像度を上げても、Volumetric Fog を保ったまま滑らかに動かす。① 1 枚でも N 枚でも起動 ② 枚数・解像度・並びを自動で検出 ③ 検出に失敗しても止まらない ④ 映像は 1 回だけ描く。達成の基準の数値は未記載 |
| 課題 | 4 つの手動設定が必要だった / 2 枚以上が前提で 1 枚では起動できなかった / Windows のモニター名の番号は物理の並びともメインとも無関係で、起動のたびに入れ替わる環境だった / モニターごとにカメラを置く方式は、2026/09/29 の試験で左右のモニターが映らなかった(ログに例外なし。原因は特定できていない) / 5/28 版は 7680×1080 で 63.6 FPS・GPU 100% が限界。今作は 7680×1440 で、総ピクセル数が約 1.33 倍(8,294,400 → 11,059,200) |
| 検証・結果 | 手動設定 4 つを廃止し、1 枚から N 枚まで設定なしで起動する。3 画面の実機(1920×1080、2026/09/29)で、自動検出どおりに割り当てられ、映像が左・中央・右に正しく映った(解像度の食い違いの警告 0 件。2 回の起動で、デバイス名の番号が入れ替わる環境でも規則が成り立った)。展示用 PC(2560×1440 の 3 画面、2026/10/03)で、枚数と解像度の自動検出を確認 |
| 未記録 | 展示用 PC での左・中央・右の並びの目視 / シングルモニターでの実機起動 / 施策ごとの FPS への効果 / 展示(TGS2026)の結果 |
| 次の候補 | 並びがずれた場合にモニター名で突き合わせる方式 / パネル本来の解像度の取得(`QueryDisplayConfig` など) / `MultiScreenSourceCamera` の起動順のエラーの解消 / カメラを増やす方式で左右が映らない原因の特定 / 施策ごとの効果を切り分ける計測 / 未実施の削減候補(Async Compute、GPU オクルージョンカリング、動的解像度の単独検討、オーバードロー可視化、影のカスケード数とシャドウアトラスの見直し) |

### FPS の比較(2026/10/03)

条件: 展示用 PC、リリースビルド、垂直同期なし、撮影位置は 3 条件とも同じ。各条件 1 枚のスクリーンショット(0.5 秒平均の瞬間値)。

| 項目 | 1: 5/28 版(Surround) | 2: カメラ 3 台方式(参考値) | 3: 現行(RenderTexture) |
|---|---|---|---|
| FPS | 63.6 | 77.9 | **88.6** |
| フレーム時間 | 15.7 ms | 12.8 ms | 11.3 ms |
| Resolution | 7680x1080 | 2560x1440 | 2560x1440(render 7680x1440) |
| Displays | 1 (active 1) | 3 (**active 1**) | 3 (active 3) |
| Cameras | 2 | 3 | 1 |
| CPU(全体 / アプリ) | 23% / 20% | 23% / 20% | 19% / 15% |
| GPU | 100% | 99% | **78%** |
| Memory | 20%(13.2 / 63.4 GB) | 22%(14.2 / 63.4 GB) | 19%(12.4 / 63.4 GB) |
| Surround | True | False | False |

- 条件 1 → 3: +25.0 FPS(約 1.39 倍)、フレーム時間 4.4 ms 短縮(約 28% 減)。条件 2 → 3: +10.7 FPS(約 1.14 倍)。
- 条件 1 と 2 は GPU が 99〜100% で限界とみられる。条件 3 は 78% で余裕があるが、次に何が限界かは切り分けていない。
- **条件 2 は、実際に 3 画面へ出力できたカメラ 3 台方式の計測ではない。** カメラが正常に描画されず、1 枚だけに出力した(`Displays 3 (active 1)`)。3 画面分のレンダリング負荷をかけた参考値で、「カメラ 3 台方式が動作した」「約 14% 速い」とは言えない。
- 各条件 1 枚のスクリーンショット(瞬間値)で、誤差を含む。条件 2 → 3 の約 12% の差は傾向として読む。
- 条件 1 は DLSS が無効、条件 3 は走行シーンのメインカメラで DLSS(Balanced)が有効。条件 1 は別のプロジェクト・別の解像度・別の方式なので、**1 → 3 の差は、出力方式・最適化・DLSS・解像度をすべて含んだ全体の結果で、施策ごとの効果は測っていない。**
- NVIDIA コントロールパネルの垂直同期の設定は記録していない。以前に申告された別の測定値(38〜42 → 78〜85 FPS)は、条件の記録がないので使わない。

5/28 版からの変更(FPS の差に含まれる):

| 変更 | 場所 | 元 → 現在 |
|---|---|---|
| 方向光の影の最大解像度 | HDRP アセット | 2048 → 1024 |
| 方向光の影のフィルタ品質 | HDRP アセット | High → Medium |
| 影の最大描画距離 | Map 用 Volume | 1000 → 500 |
| コンタクトシャドウ | Map 用 Volume | 有効 → 無効 |
| 走行シーンのメインカメラの DLSS | `Map.unity` | 無効 → 有効(Balanced) |
| `Map` シーン内のカメラ台数 | `Map.unity` | 9 → 1(未使用の 8 台を削除。出力方式とは無関係) |
| フレームタイミング統計 | ProjectSettings | 0 → 1(計測用) |
| `runInBackground` / 全画面モード / 既定の画面サイズ | ProjectSettings | 0 → 1 / Windowed → FullScreenWindow / 7680×1080 → 2560×1440(出力方式に伴う) |

- 変更していないもの: Volumetric Fog(有効)、影のカスケード数(4)、影のアトラス(4096)、HDRP アセット側の DLSS・動的解像度、Async Compute(無効)、SSAO(有効)。Volumetric Fog は、無効化すると画面効果が減ったので、画質を優先して有効に戻した。
- 目的が未確認の変更: HDRP アセットの Lit シェーダーのモード(Deferred Only → Both)。
