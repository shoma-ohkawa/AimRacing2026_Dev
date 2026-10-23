# 改良・最適化の対象の現状分析

作成: 2026/10/15(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象: 改良・最適化の候補一覧のすべて(ID は S / R / V / H / L / M / T / F / U / X / UP)。10/12 に「対象外」「保留」だった項目(T-5 UI 画像、F-1 メッシュ、X-1 未参照アセット)も含む。
前提: Unity 6000.6.5f1(HDRP 17.6.0)に更新した後の現状。2026/10/15 の調査に、2026/10/20 までの変化(解消、計測の記録)を反映した(発見 11 のみ、2026/10/22 の計測も反映)。

- **利点**: 今の良い点。改良しても損なわないようにしたい点。
- **欠点・問題点**: 構造上の弱さ・制約と、実際に起きている、または起きうる不具合・無駄・リスク。
- **確認**: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`未測定`、`未確認`。記録がない数値は、推測で埋めず「未測定」「未確認」と書く。

## 1. 先に見てほしい発見

優先度は付けていない。

| # | 発見 | 対象 | 確認 |
|---|---|---|---|
| 1 | **電柱の Prefab 1 つの中に、ケーブルが 1,125 本**(`UtilityPoleCable` と `LineRenderer` が各 1,125)。`LateUpdate` で毎フレーム、全ケーブルの `positionCount` を設定し、12 点を `SetPosition` し直している(約 13,500 回 / フレーム)。電柱は動かないので、1 回計算すれば足りる | R-2 | 実測・コード |
| 2 | `sf-pro-rounded-regular SDF.asset` は、**63 文字しかないのに 8192×8192 のアトラス**(128 MB)。`NotoSansJP` も 8192×8192(7,130 文字、静的)。2 本でアトラスだけで約 259 MB | U-1 | 実測 |
| 3 | FBX 153 本のうち **122 本が Read/Write 有効**(CPU 側にメッシュのコピーが残る)。メッシュ圧縮は全部オフ、ブレンドシェイプの取り込みは全部オン | F-1 | 実測 |
| 4 | `Opening` の 70 灯は、**すべて Mixed の Hard Shadow**(スポット 69、範囲 3.44〜10 m)。Prefab にも 23 灯(`chu~car~jo~` に 22) | L-1 | 実測 |
| 5 | Rich FX の HDRP への登録が **2 系統で食い違う**。新形式の一覧に使用中の ChromaLines がなく、未使用の Slice・Underwater が入っている。Rich FX のシェーダー 40 本は `Resources/` にあり、**使わなくても常にビルドに入る** | V-10、M-3 | コード |
| 6 | 緊急リセット(F1 など)は `Exit()` を呼ばず、`MenuState` の `TcpServer.OnNameReceived` の購読が残る。次の名前受信で破棄済みのハンドラが呼ばれ、例外になる恐れ(未検証) | S-2、S-3 | 文書 |
| 7 | 各条件 1 枚のスクリーンショット(瞬間値)の FPS 比較では、施策ごとの効果が切り分けられていない。計測の仕組みは用意した(`Perf.md`)が、出力方式・最適化・DLSS・解像度それぞれの効果の切り分けは、まだ | S-6、S-8 | コード・文書 |
| 8 | コース路面の法線マップ 20 枚(304 MB のフォルダ)が**圧縮なし・ミップなし**。計算上は 2048²×4 byte ≈ 16.8 MB / 枚 × 20 ≈ 335 MB(実測ではない) | T-2 | 実測・計算 |
| 9 | `BlinkingUI` が `Update` で毎フレーム `AppLog.Log("afetsdgrhrtyhtry")` を呼ぶ(消し忘れとみられる。リリースでは除去される)。`goal_destroyAtEndScene` は、現行ビルドにない "End" シーンを待って、毎フレーム `SceneManager.GetActiveScene().name`(文字列の生成)を呼ぶ | R-3、R-2 | コード |
| 10 | `GarageGlitchTitle` は `m_Volume.sharedProfile`(**アセットそのもの**)の Glitch の値を実行時に書き換える。Editor では `garage!move.asset` が変わってしまう。どのシーンにも配置されていない | R-1、V-6 | コード |
| 11 | **周回でメモリと VRAM が増え、VRAM が上限に達すると FPS が崩れる。主因は DLSS のカメラの破棄**(3 台の PC の計測で確認)。`unloadoff`(破棄の前に DLSS を止める)は、展示用 PC で測定した(2026/10/22)。増加は +872 MB/周で、基準(+813〜+976)から減らなかった。他の 2 台は未測定 | S-6、UP-6 | 実測(`PerfFindings.md`) |

**解消済み**

| 内容 | 解消 |
|---|---|
| リリースビルドでは `AppLog.LogError` も除去され、エラーが一切出力されなかった(旧 発見 7) | 2026/10/20 に `LogWarning`・`LogError` をリリースでも出力するよう変更(`CommonFoundation.md`) |
| 毎フレームの素の `Debug.Log` 5 か所(`ChairController`、`ReturnRoad`)で、`Player.log` が 10 周で 73.5 MB | 2026/10/20 に削除(`PerfFindings.md` の B-1)。担当者への確認は未 |
| 変更前後を同じ条件で測る手段が無かった(S-8) | FPS・CPU・GPU・メモリ・GC と遷移・ロード時間の記録を実装し、3 台で実測(`Perf.md`) |

## 2. 自作スクリプト・自作機能(S)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| S-1 共通基盤 | 常駐マネージャー 8 クラスが 1 つの基底で一意性を共通化(未配置のエラーは 1 回だけ。重複は `Destroy(this)` で同居コンポーネントを巻き込まない)。`AppLog.Log` はリリースで呼び出しごと除去。自作の本体 23 本は素の `Debug.Log` が 0 か所 | `HasInstance` がない(存在確認でも `Instance` 参照でエラーが 1 回出る)。`LogException` / `LogFormat` がない。初期化の抜け方(`base.Awake()` と `Instance != this`)は各サブクラスの自己責任。`GameManager` は重複時の確認がなく、`Manager` が複数置かれると初期化が二重に走る恐れ。`Assets/#Scripts` に他メンバーの素の `Debug.Log` が 37 か所。ドメインリロードを無効にすると静的変数のリセットが必要(現在は無効にしていないので問題なし) | 文書・コード |
| S-2 遷移エンジン | 遷移の定義が `GameFlowGraph` の 1 箇所。各 State は `AdvanceToNext()` だけ。差分のロード / アンロード、どのシーンからでも起動できる Bootstrap、`GameStateId` の固定ルール(`Intro = 4`) | 緊急リセットは Loading 画面を使わず、**`Exit()` も呼ばない**ので購読が残る(発見 6)。`Intro` と `Racing` を同じ GameObject に置く前提(入口判定)。Loading 画面を挟む State がコード内の定数。遷移中の `AdvanceToNext()` は、ログも出ずに黙って無視される。コメントと実装の食い違いが 2 か所(もう 1 か所は 2026/10/19 に直した) | 文書・コード |
| S-3 各 State | すべて `GameStateBindingBase` を継承。画面が見える前の入力を無視するガード(Opening は FadeIn 完了、Menu は UI 表示完了)。ペダルは「一度離してから踏む」判定。BGM は再生中かを確認してから鳴らす。ゴール時の FFB 停止は `try/catch` | 緊急リセットの購読の残り(発見 6)。ペダル軸の正規化式が 3 か所に重複。`RacingState` の状態が整数の定数。`OpeningState` に未使用の定数(`k_SecondSongStartTime`)とコメントの食い違い。`Ride` と `RideOff` が現在は同じ値。曲間の Splash と Load への折り返しの先後が BGM の長さで変わる(要確認)。入力の検証ツールが、現行の `MenuState` にない `m_selectionReady` を参照(未実行)。静止ペダルの対策(`ObserveReleasedPedal`)が実行時に使われていない。F4 のポーズ中、Result の Thank you への切り替えは止まり、復帰のタイマーは進む | 文書・コード |
| S-4 遷移演出 | 秒数・色・保持時間を State 別に Inspector だけで調整できる。未登録の State は覆わず素通しで、画面が黒く固定されない。DOTween を `SetUpdate(true)` にしてポーズ中も動く。リフレッシュを Load に一本化。GC を 2 回に分けて回収 | BGM 別の保持秒数がクリップ名の**文字列一致**(OP2 のクリップ名は作業メモ入りの長い名前)で、クリップを差し替えて名前が変わると、**エラーも警告も出ずに**保持秒数が Splash の値に戻る。ロゴの Canvas と Loading 画面の Canvas が同じ描画順(1100)で前後が不定(実機で未確認)。`m_titleLogoFadeInDelay` の名前と用途が違う。`ForceHide()` は進行中のトゥイーンの完了コールバックを呼ばない(実害なし)。Load → Splash は覆わずに切り替わる(意図か未確認)。リフレッシュの所要時間は `transitions.csv` に記録される(`Perf.md`)。リフレッシュ前後のメモリの変化は未確認 | 文書・コード |
| S-5 残り時間表示 | Menu が見えてから(`OnStateFadeInComplete`)減り始める(`Enter()` から減らすと約 3.5 秒を来場者が見る前に使う)。実時間で進み、ポーズの影響を受けない。確定で止まる。74 行と小さい | 毎フレーム `ToString("00")` で文字列を作る(表示が変わるのは 1 秒に 1 回)。開始値 99 がコードとシーンの 2 か所。0 になっても何も起きない(仕様か未実装か未確認)。この値を読む外部のコードがない | 文書・コード |
| S-6 マルチモニター出力 | 枚数・解像度・左右の並びを自動検出(手動設定 4 つを廃止)。1 枚から N 枚まで起動し、検出に失敗しても警告で継続。映像を 1 回だけ描く RenderTexture 方式で、Surround に依存しない。展示用 PC の参考値: 63.6 FPS / GPU 100%(5/28 版)→ 88.6 FPS / GPU 78%(現行) | FPS の差は、出力方式・最適化・DLSS・解像度の合計で、施策ごとの効果は切り分けていない(発見 7)。解像度の違うモニターは引き伸ばされる。`SetSourceCamera` が古いカメラの出力先を外さない。展示用 PC で左・中央・右の並びを目視した記録がない。1 枚・4 枚以上・DPI 設定の環境が未検証。カメラを増やす方式で左右が映らなかった原因が特定できていない。位置の規則(`Display[0]` がメイン、以降は列挙順)は 3 画面 1 構成でしか確認していない。起動順のエラーが 1 回出る。ヘッダなし、`m_p` なし、アクセス修飾子なしで、規約と差がある | 文書 |
| S-7 突入演出 | シーンのファイルを変えずに、実行時に作って壊す(終了・`Exit()`・破棄のどれでも後片付けされる)。パターンを Inspector で選べる(`None` で無効)。実時間で動き、ポーズで止まらない。Unity 6.6 でもコンパイルが通る | パラメーターがコードの定数で、Inspector で調整できない。動かす仕組みが 2 系統(ビネットなどは Volume の重み、ぼかしは Custom Pass の半径)。単独の docs がない。**`BlurFocus` は 3 画面の合成映像(7680×1440)全体に、全画面ガウスぼかし(半径 32)を 2.5 秒間かける**。負荷は未測定。`BeforePostProcess` に入れているため、DLSS の履歴との相互作用(ちらつき)が未確認。各パターンの強さの調整は未了 | コード・文書 |
| S-8 計測 | 実装済みで、3 台の PC で実測した(`Perf.md`、`PerfFindings.md`) | 実機の確認が残っている(`Perf.md` の確認状況) | コード |
| S-9 開発ツール | ファイル全体が `#if UNITY_EDITOR`、専用の名前空間、型はすべて `internal`、依存は基本 API だけの自己完結で、他のプロジェクトへコピーできる。ホワイトリスト方式で必要なものだけを ZIP にできる | 1,027 行が 1 ファイル。標準の圧縮 API を使わず、ZIP を自前で組み立てている(理由は未記録)。素の `Debug.Log` が 2 か所(設計上)。ドキュメントがない。ホワイトリストの一覧と、復元の検証の記録がない。バックアップ運用との関係を整理する必要がある | コード・実測 |

## 3. 描画関連スクリプト(R) 他メンバー作成・20 本

`配置のみ` は、行数と配置・構造の抜き出しだけで、中身は読んでいない。

| ID | スクリプト | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|---|
| R-1 | `SwitchingCamera`(127、`Map`) | Home キーで Cinemachine のカメラを優先度で切り替える。車体が傾いても画面の横傾きだけを水平に戻す | 定数が PascalCase(規約は `k_`)。コメントに「NewAimRacing と同じ」とあり、由来が外部。旧カメラ追従(`VPCameraController`)との競合を避けるため、実行時に無効化する処理を持つ。Cinemachine 3.1.2 → 6.6.0 の更新後の動作は、Editor での確認の対象 | コード |
| R-1 | `CustomizeCameraRotation`(57、`Map`) | Cinemachine の拡張で、ピッチとヨーを固定または加算する | Euler 角を経由するので、ピッチが大きいときに不安定になりうる。`[SaveDuringPlay]` と Cinemachine 3.x 前提のコメント(2.x なら `using` を変更)。コメントに英語が混在。6.6.0 での挙動は未確認 | コード |
| R-1 | `EdgeSpectrumBars`(378、`Opening`)/ `RingSpectrumBars`(459、`Opening` に 3 つ) | 配列を事前に確保、`MarkDynamic`、Inspector で調整できる。`ExecuteAlways` で Editor でも確認できる | **毎フレーム `mesh.Clear()` して、頂点・色・三角形をすべて設定し直す**(三角形は変わらない)。`Bounds` を毎フレーム作る。FFT を各コンポーネントが別々に取得(`Edge` 2 つ + `Ring` 3 つ。`Ring` の既定は 4096)。`SG_ECGLine` のマテリアルが前提(Depth Test の設定が 2 本で違う)。`Opening` の描画・CPU 負荷への寄与は未測定 | コード |
| R-1 | `Glitch`(78、コードから参照) | ほぼなし(旧 Post Processing Stack v2 用) | 描画処理がコメントアウトされている。`Shader.Find` を使う(ビルドでストリップされる恐れ)。HDRP 17.6 では使えない旧方式 | コード |
| R-1 | `GarageGlitchTitle`(75、**未参照**) | Rich FX の Glitch を一定間隔で発火する | **`sharedProfile` を実行時に書き換える**(発見 10)。フレーム数(600〜603 フレームごと)に依存し、FPS が変わると間隔が変わる。どのシーンにも置かれていない | コード |
| R-2 | `UtilityPoleCable`(34、電柱の Prefab) | 電柱間にケーブルをたるませて描く。たるみ量と点の数を調整できる | **Prefab 1 つに 1,125 本**(発見 1)。`ExecuteAlways` の `LateUpdate` で毎フレーム全点を再計算(電柱は動かない)。`LineRenderer` が 1,125 個あることの負荷は未測定 | 実測・コード |
| R-2 | `CarVisible1`(33、**未参照**) | ― | フレーム数(1800 / 5400)で表示・非表示を切り替える(FPS 依存)。毎フレーム `SetActive` を呼ぶ。どのシーンにも置かれていない | コード |
| R-2 | `goal_destroyAtEndScene`(36、`Map`) | 空の `Start`。クラス名が `goal_destoryAtEndScene` と綴りが違う。**"End" シーンは現行のビルドにないので、実質動かない**のに、毎フレーム `SceneManager.GetActiveScene().name` を呼ぶ(発見 9) | コード |
| R-3 | `BlinkingUI`(30、`Map`) | Image の点滅だけの小さな処理 | **`Update` で毎フレーム `AppLog.Log("afetsdgrhrtyhtry")`**(発見 9)。Editor と開発ビルドで毎フレームログが出る | コード |
| R-3 | `DisplaySectorAnimation`(160、`Map` に 3 つ) | 点滅 → 移動・拡大・透明度の変化で表示する。Editor 上で終了姿勢を保存できる | `ContextMenu` の関数で `GetComponentInChildren` を何度も呼ぶ(実行時の処理ではない)。詳細は未精査 | 配置のみ |
| R-3 | `SpriteAnimationNew`(95、`Menu`) | `SpriteRenderer` と `Image` の両方に対応する | `Start` で同じ `GetComponent` を 2 回呼ぶ。`isRoop` と綴りが違う。詳細は未精査 | コード(一部) |
| R-3 | `UI_StarCountDown`(167、`Map`) | 開始の判定を `VehicleController.CountdownReady` と共通化。3・2・1・GO の SE と連動 | `UpdateCountDown()` が毎回、色とスケールを初期値へ書き戻してから `Lerp` する(`_deltaTimer` に依存し、フレームレートが変わると見え方が変わる)。`public` と `[SerializeField]` が混在。コメントアウトした旧判定が残る | コード |
| R-3 | `SplashFade`(107、`Splash`) | 画像を順に FadeIn → 保持 → FadeOut。`SplashState` が `IsEndAnimation` で終了を待つ | `Input.GetKeyDown(PageDown)` を直接読む(`DebugKey` を経由しない)。ヘッダなし。詳細は未精査 | コード(一部) |
| R-3 | `Start_UI`(39、`Opening`) | `CanvasRenderer` の滑らかな点滅(コルーチン) | 毎フレーム `GetColor` / `SetColor` を呼ぶ。実害は小さい(1 つだけ) | コード |
| R-3 | `Entry`(236、コードから参照のみ) | G29 の入力で 20×20 の文字を選ぶ旧方式の入力画面 | `GameObject.Find("G29")` で取得。現行の名前入力(TCP)とは別系統で、現行のフローでは使われていない旧コード | コード(一部) |
| R-4 | `LED_Controller`(80、`Map` に 9 つ) | Image の LED を点灯・消灯・点滅で表示 | 詳細は未精査 | 配置のみ |
| R-4 | `LED_IndicatorController`(164、`Map`) | エンジン回転数に合わせて LED を順に点灯。閾値を配列で調整できる | `FindFirstObjectByType<VehicleController>` で補完(`Start` のみ)。詳細は未精査 | コード(一部) |
| R-4 | `Load_Indicator`(85、`Map`) | 四輪の接地荷重をメーターのタイヤ色に反映する | `Update` で毎フレーム `RefreshDisplay()`。`FindObjectsByType` で補完(`OnEnable` のみ)。詳細は未精査 | コード(一部) |
| R-4 | `UI_Meter`(207、コードから参照のみ) | ゲージ画像とテキストによるメーター表示 | 現行のメーター(`MeterUIManager` が参照する `MeterController`)とは別のクラスで、どのシーン・Prefab にも配置されていない。中身は未精査 | 配置のみ |

メーターの Canvas は、`MeterUIManager.m_targetDisplay` の指定で**1 つのディスプレイ**に出る。3 画面の映像(RenderTexture)とは出力経路が違うので、ポストプロセス(ぼかしなど)の対象にならない。

## 4. Volume Profile(V)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| V-1 `Map2025_tmp terae` | 屋外コースの見た目(雲・空・フォグ・SSAO・Bloom・DoF など 19 項目)を 1 本で定義。影の最大距離 500、コンタクトシャドウ無効などで、5/28 版からの GPU 負荷の削減に使われた | HDRISky と PhysicallyBasedSky の**両方**を持ち、どちらを使うか(VisualEnvironment)が未確認。ContactShadows は項目が有効なのに `enable` を 0 に上書き。V-2 とほぼ同一(二重管理)。負荷の大きい項目(VolumetricClouds 33、PBSky 27、SSAO 21、Fog 18、DoF 17 の上書き)のコストが項目別に未測定 | 文書 |
| V-2 `Map2025` | HDRP アセットの既定として全カメラの一番下の層になり、`Load`・`Splash`・`Result` の見た目を決める | 屋外コース(Map)向けの内容が全シーンの土台になっている。`Opening`・`Menu` は影の最大距離が 1000 のまま(Map は 500)。Volume が上書きしていない項目は Map 向けの値が効く(意図かは未確認) | 文書 |
| V-3 `DefaultSettingsVolumeProfile` | Rich FX の全 40 種類を既定で持つので、どの Volume からも上書きできる | 164.9 KB・サブオブジェクト 167 個のうち **83 個がどのリストにも載っていない**。使用は 3 種類だけ。Unity 上で安全に削除できるか未確認。`Map`・`Opening` の Static Lighting Sky もこのプロファイルを指す(削除の影響範囲)。実行時の負荷への寄与は未測定 | 文書 |
| V-4 `Opening 2025` | `Opening` の見た目(Fog・Exposure・DoF・Tonemapping・SSAO など)を定義 | V-5 とほぼ同一(差は 14 行)。2024 年版と 2025 年版の世代が混在 | 文書 |
| V-5 `Opening2024` | `Menu` の見た目を定義(Priority 100) | V-4 とほぼ同一。DoF が無効。`Menu` が 2024 版、`Opening` が 2025 版を使う理由が未記録で、どちらに合わせるかが未決 | 文書 |
| V-6 `garage!move` | 1.7 KB と小さい | Glitch は有効、ChromaLines は無効という中途半端な状態。操作するスクリプトが未配置で、Glitch がどの値でかかっているか未確認 | 文書・コード |
| V-7 `MenuScene` | DoF だけの最小構成 | Volume ではなく、カメラの Cinemachine Volume Settings から参照される特殊な使い方。確認した範囲では問題なし | 文書 |
| V-8 `DefaultLookDevProfile` | HDRP の LookDev(Editor)用で、ゲームの実行には影響しない | 通常は使わないのに 7.4 KB 保持。確認した範囲では問題なし | 文書 |
| V-9 突入演出の実行時 Volume | シーンの Volume とは別に作って壊すので、既存の Volume を汚さない | アセットではないので、Editor で見ながら調整できない。優先度 1000 の固定で、他の Volume と競合しうる(現在は 0 と 100 だけ)。S-7 と同時に判断が必要 | コード |
| V-10 Rich FX の登録 | 使用中の Glitch・HueSaturationInvert は新形式の一覧に載っている | 旧形式(`Assembly-CSharp`)と新形式(`Assembly-CSharp-firstpass`)の一覧が別々にあり、中身が違う。**新形式に使用中の ChromaLines がなく、未使用の Slice・Underwater が入っている。** どちらが有効かを未確認。Rich FX のシェーダー 40 本が `Resources/` にあり、常にビルドに含まれる | コード |

Rich FX は 40 種類のうち、使用は 3 種類(Glitch、ChromaLines、HueSaturationInvert)。残りの 37 種類は V-3 に既定値として入っているだけ。

## 5. 描画系アセット

### HDRP の設定(H)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| H-1 HDRP アセット | 画質に必要な機能(SSAO・Volumetrics・Volumetric Clouds・Decals・Custom Pass)が有効。GPU Resident Drawer と Dynamic Resolution が有効。影を 5/28 版から下げて(2048 → 1024、フィルタ High → Medium)GPU に余裕を作った。HDRP 17.6 でもファイルは更新されていない | SSGI の対応フラグが有効なのに、どの Volume にも SSGI の項目がない。Lit シェーダーモードが Both(Deferred Only から変更。目的が未確認)。設定ごとの FPS への効果が未測定。SSGI の対応フラグそのもののメモリ・ビルドへの影響は未測定 | 文書 |
| H-2 グローバル設定 | HDRP 17.6 の構成に自動で移行され、インポートは成功した | Rich FX の登録が 2 系統(V-10)。設定の一覧が作り直され、新しい項目が 1 つ増えた(内容は未確認) | 実測 |
| H-3 Graphics / Quality / HDRPProject | レンダーパイプラインの割り当てが一貫している | 内容を精査していない(10/12 は割り当ての確認のみ) | 未確認 |

### ライティング・空・フォグ(L)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| L-1 `Opening` のライト | 70 灯がすべて Mixed なので、ベイクと組み合わせた見た目を作れる | 70 灯すべてが Hard Shadow(影の描画を要求する)。影のアトラス(ポイント・エリア 4096、リクエスト最大 128)への負荷が考えられるが、未測定。同じシーンの Prefab にさらに 23 灯 | 実測 |
| L-2 `Map` / `MapEnvironment` | `Map` は方向光 1 灯(Realtime)で軽い。`MapEnvironment` はライトなし | `MapEnvironment` は MeshRenderer 159・Prefab インスタンス 79 を持つ(静的環境)。影の最大距離は Volume 側(V-1)で決まり、`Map` と他のシーンで食い違う(V-2) | 実測・文書 |
| L-3 Lighting Settings | `Okutama.lighting`、`Opening2024.lighting` の 2 つに整理されている | 名前が 2024 年版のまま。2025/2026 年の `Opening` の構成に合っているかは未確認 | 文書 |
| L-4 反射プローブ・Occlusion Culling | Occlusion Culling のデータがある | 反射プローブの EXR 2 枚は非圧縮・ミップなし。`Opening.unity` 自体にプローブがなく、`chu~car~jo~.prefab` だけが参照。Occlusion Culling のデータが現在の `Map` の構成と合っているかは未確認 | 実測・文書 |
| L-5 空の画像(計 56.6 MB) | 取り込みの上限は 2048 に揃っている(8192×4096 の PNG は縮小される) | ソースが大きい。形式がバラバラ(PNG・HDR・EXR)で、圧縮・ミップの設定もそれぞれ違う。どの画像をどのシーンが使っているかは、10/12 の集計(3 点とも使用中)のまま | 実測 |
| L-6 Volumetric Fog の素材 | ― | `Texture3D_Dust.png` は名前に "3D" とあるが、実際は PNG(4000×3000、12.2 MB)。`Texture3D_test_noise3d.tga` は名前が "test" で、`Assets` 直下にある | 実測 |

### マテリアル・シェーダー(M)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| M-1 マテリアル 296 | 使用中 247 個のうち、HDRP/Lit が 200 で、シェーダーの種類が少ない | 使用中のうち、アルファテスト 71・透過 16・両面 47(オーバードローの要因になりうる)。未参照 49 個(`people` 39 など)が残る。項目別の描画コストは未測定 | 文書 |
| M-2 ShaderGraph 3 本 | 17.6 でインポートし、コンパイルエラーはなかった。`SG_ECGLine`(5 マテリアル)、`TembleGrass`(4)、`leaf`(2) | 作成者が未確認。17.6 での見た目は Editor での確認の対象 | 実測 |
| M-3 シェーダー 51 | ― | 内訳は Rich FX 40、TextMesh Pro 8、UI 1、UniGLTF 2。Rich FX の 40 本が `Resources/` にあり、使わなくても常にビルドに入る(使用は 3 種類)。ビルドの容量とバリアントへの影響は未測定 | 実測 |

### テクスチャ(T) 全 794 枚

全体(Import 設定): 上限サイズ 2048 が 792 枚。Read/Write は全枚数で無効。圧縮は、標準 655、圧縮なし 73、High Quality 5、旧形式で未指定 61。ミップなし 199(うちスプライト 144)。Mipmap Streaming が有効なのは 22 枚だけ。

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| T-1 OP の建物(TGA 55 枚 / 379 MB) | 2048×2048 に揃っている。Read/Write は無効 | 1 枚 12〜16 MB の非圧縮ソース。`.meta` が旧形式(`serializedVersion: 2`)のものが 38 枚あり、`textureFormat: -1`(自動)。6.6 で実際にどの圧縮形式になっているかを Editor で確認する必要がある。OP 専用のメモリとロード時間は未測定 | 実測 |
| T-2 コース路面(PNG 55 枚 / 304 MB) | 上限 2048 に揃っている | 法線マップが 1 枚約 11 MB。**20 枚が圧縮なし・ミップなし**(発見 8)。ミップなしは遠方のちらつきの原因になりうる | 実測・計算 |
| T-3 Import 設定の全体 | Read/Write が全部無効。上限サイズが揃っている | 非圧縮(スプライト以外)58 枚 / 217 MB のソース。Mipmap Streaming が 22 枚だけ。プラットフォーム別の上書きと、品質設定側の Mipmap Streaming の有効 / 無効は未確認。旧形式 61 枚の実フォーマットは未確認 | 実測 |
| T-4 大きな単体のソース | ― | `DayInTheClouds4k.hdr` 22.5 MB、`Epic_BlueSunset` 18.6 MB、`Texture3D_Dust` 12.2 MB など(L-5、L-6)。取り込みで 2048 に縮小されるが、ソースは大きいまま(ZIP・バックアップの容量) | 実測 |
| T-5 UI 画像 | スプライトが整理されている(144 枚) | `Menu/Images` の連番 PNG 80 枚(24.9 MB)。`UI/逆走 1.png` は 7750×4390 で、上限 2048 に縮小される。UI の画質と圧縮の設定の関係は未確認 | 実測 |

### メッシュ・フォント・未参照(F、U、X)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| F-1 メッシュ | FBX 153 本で 51.5 MB と小さい。タンジェントは全部 MikkTSpace、頂点の溶接も全部オン | **Read/Write 有効が 122 / 153**(実行時にメッシュが CPU 側にも残り、メモリが倍近くになりうる。どのスクリプトがメッシュを読むかで確認が必要)。メッシュ圧縮は全部オフ。ブレンドシェイプの取り込みが全部オン。アニメーションの取り込みが 143 本(種類が None のものが 116)。ライト・カメラの取り込みが 84 本。コース FBX は日付違いの 4 本、縁石は 2 本が全部参照されている(旧版が残っている可能性)。車両モデルのメッシュは `.asset` で 50 本 | 実測 |
| U-1 フォントの SDF アトラス | `NotoSansJP` は日本語 7,130 文字を事前に生成した静的アトラスで、実行時の生成負荷がない | **`sf-pro-rounded-regular` は 63 文字しかないのに 8192×8192 のアトラス(パディング 126)で 128 MB。** `NotoSansJP` も 8192×8192(131 MB)。`LiberationSans` は 1024×1024(2.2 MB)。アトラスだけで約 259 MB(Alpha8。8192² は 1 枚 64 MB)。63 文字なら、はるかに小さいアトラスで足りる可能性が高い(未検証)。使われている文字数・表示サイズとの突き合わせが必要 | 実測 |
| X-1 未参照アセット(182 ファイル / 約 306 MB) | 動的に読み込まれる可能性があるものが混ざるため、まだ消えていない(安全側) | `###Map2024` 99 ファイル / 169.3 MB、`###Map2022/wall` 10 / 84.7 MB、`Tree/SUGI3003` 7 / 27.6 MB ほか。ビルドとバックアップの容量を増やす。コードからの動的な読み込みは辿れていない(`Assets/Resources` は空)。10/12 の集計のまま、再集計はしていない | 文書 |

## 6. Unity 6000.6.5f1 への更新に由来する項目(UP)

| ID | 利点 | 欠点・問題点 | 確認 |
|---|---|---|---|
| UP-1 外部プラグインの暫定修正 7 本 | 最小の修正でコンパイルと全アセットのインポートが通った。変更した 7 本の内容は、更新作業の中で記録した(作業記録のファイルは、現在 docs にない) | FMOD の Editor 用 5 本は、型名を位置指定で直した。FMOD のプラグインを直接書き換えているので、公式の更新版を入れると上書きされる。Editor 用のツール(イベントブラウザなど)の動作は未確認 | 実測 |
| UP-2 Editor ツール 2 本 | `MouseNavigation` は、6.6 のライブラリで引数の型(`EntityId`)を確認してから直した | `MouseNavigation` は Unity の内部メソッドをリフレクションで呼ぶので、今後の更新でまた壊れうる | 実測 |
| UP-3 古い形式の `.meta` | インポートは成功している | 警告が 1,019 行(PluginImporter v1 が主、TextureImporter v2〜9 もある)。再保存しないと警告が毎回出る。再保存すると多くの `.meta` が書き換わる | 実測 |
| UP-4 廃止予定の API | Editor 専用の 4 件だけで、ゲーム本体にはない | `FindFirstObjectByType`(`FindAnyObjectByType` を勧められている)。将来のバージョンでエラーになりうる | 実測 |
| UP-5 パッケージのメジャー更新 | 本体のスクリプトはコンパイルが通った | Cinemachine・Timeline のバージョンの付け方が変わった(3.1.2 → 6.6.0、1.8.11 → 6.6.0)。Burst 2.0.0、Collections 6.6.0。R-1 の Cinemachine 拡張と、Timeline を使う `Opening` の挙動は、Editor での確認の対象 | 実測 |
| UP-6 6.6 の新機能 | ― | 未調査。HDRP 17.6 のリリースノートを読んでいない(DLSS のメモリ増加の既知の問題の調査も、ここに当たる: `PerfFindings.md` の A-4) | 未確認 |

## 7. 横断的な傾向

1. **計測の基盤は、用意した**(`Perf.md`)。施策ごとの FPS 効果の切り分け(S-6、H-1)は、これから。
2. **設定の二重管理。** V-1 と V-2、V-4 と V-5、Rich FX の登録の 2 系統(V-10)、コースの日付違いの FBX(F-1)。
3. **旧版・未使用の残存。** `Legacy` のコード(`WheelController2024` は `Map` に配置)、未参照のスクリプト 12 本、未参照のアセット 約 306 MB、Rich FX の 37 / 40 が未使用。
4. **毎フレームの無駄は、自作よりも他メンバーの描画関連に偏る。** 自動検出では、221 本中 14 本。大きいのは R-2(電柱ケーブル)で、自作の `Update` 系は小さい。
5. **サイズの大きさは、少数のアセットに集中する。** U-1(259 MB)、T-1(379 MB)、T-2(304 MB)、X-1(306 MB)。
6. **実機・Editor での確認が足りない項目が多い。** 展示用 PC での並びの目視、`BlurFocus` の負荷、Rich FX の有効な登録、HDRP 17.6 での見た目。

## 8. 確認できていないこと

- 使用中 / 未参照のアセットの内訳は 10/12 のまま。再集計していない。
- 負荷(CPU / GPU)の実測は、項目別にはない(全体の FPS・GPU 使用率・メモリは、3 台の PC で記録した。`PerfFindings.md`)。「負荷が高い」「無駄」は、構造と数からの推定で、効果の大きさは測っていない。
- R のうち `配置のみ` のスクリプトは、中身を読んでいない。
- `CurveSign.cs`(`UI/`、70 個が 1 つの Prefab に入っている)は、10/12 に他メンバーのものとして除外され、今回の対象にも入っていない。`UtilityPoleCable` と同じ傾向(同じスクリプトの大量配置)があるかは未確認。
