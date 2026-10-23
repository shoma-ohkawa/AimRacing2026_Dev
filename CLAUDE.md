# プロジェクト概要

## DEV

- Unity 6000.6.5f1
- Visual Studio 2026

# コーディングルール

## 変数名

| 種類 | 接頭辞 |
|---|---|
| メンバ | `m_` |
| ポインタ | `m_p` |
| bool | `m_b` |
| 定数 | `k_` |

- マジックナンバーは使わない。
- C# の `m_p` は「別の場所で管理されているクラスのインスタンスへの参照」に付ける。
  - `m_p` を付ける: `GameObject` / `Transform` / `Rigidbody` などのコンポーネント、`ScriptableObject`、自作クラスのインスタンス参照、アセット参照（`Sprite` / `AudioClip` / `Material` / `Texture` / Prefab など）
  - `m_` のみ: `string`、配列、`List<T>`、`Dictionary<,>`、値型（`int` / `float` / `Vector3` など）

## 関数の引数

- 引数は、先頭にアンダーバー + 小文字。 ex) `void Get(int _num)`
- 引数の説明を書く。3個以上は改行して見やすく並べる。

## スクリプト

自分の書いたスクリプト(`.cs`)の一番上に、下記のヘッダを付ける。

```
/*━━━━━━━━━
@file ファイル名
@brief 概要を簡単に
@author 24CU0110 大川翔生
@date 作成日
最終更新日 
━━━━━━━━━*/
```

## { } とコメント

- if 文・for 文・関数の後の `{ }` は改行する。
- 二重ループの添字は `i, j, k, l, m, n` の順。
- 変数のコメントは宣言の横に書く。 ex) `int num;	//コメント`

```csharp
//コメント
if (true)
	{
		処理
	}

//コメント
for (int i = 0; i < n; ++i)
	{
		処理
	}

//コメント
int Get(int _num)
	{
		処理
	}
```

## return

if 文・for 文・関数で `return` のみを返す場合は、改行せずすぐ横に書く。

```csharp
if (true) { return; }
for (int i = 0; i < n; ++i) { return; }
int Set() { return ; }
```

# プラグイン

Unity 公式プラグインを使う。

```
/plugin marketplace add Unity-Technologies/unity-agent-plugin
/plugin install unity@unity-agent-plugin
```

# docs

設計と計測の資料。

| ファイル | 内容 |
|---|---|
| `GameFlow.md` | State 遷移エンジン |
| `GameStates.md` | 各 State の実装 |
| `TransitionEffects.md` | フェード、Loading 画面、タイトルロゴ、セッションのリフレッシュ |
| `MultiScreenOutput.md` | 3 画面出力 |
| `CommonFoundation.md` | `SingletonMonoBehaviour` と `AppLog` |
| `DebugKey.md` | デバッグキー一覧 |
| `Perf.md` | 計測機能(記録・周回・放置・集計・グラフ)と PDCA |
| `PerfFindings.md` | 計測結果(3PC の実測)と課題の一覧 |
| `CurrentStateAnalysis.md` | 改良・最適化の候補の現状分析(利点・欠点・問題点。ID は他の docs が参照) |

- 読む順: 進行は `GameFlow.md` → `GameStates.md` → `TransitionEffects.md`。計測は `Perf.md` → `PerfFindings.md`。
- ヘッダに `作成: 日付 / 作成者: 24CU0110 大川翔生` と対象パスを書く。
- 機能ごとの docs は、末尾に PDCA(目標・課題・検証・結果)を持つ(ポートフォリオ用)。
- 日付つきの実測(`PerfFindings.md` の記録など)は書き換えず、追記で変化を示す。それ以外は、状況が変わったら更新する。
- 節の見出しを、他の docs とコードが参照している。節は末尾に足し、既存の見出しは変えない。変えたら参照元も直す。参照元は、`GameFlowRunner.cs`・`SessionCacheRefresher.cs` のコメント(`Perf.md` の「transitions.csv」の節)。

# 計測記録

- 計測は、Development Build を OFF にしたリリースビルドで行う。計測の前に Unity Editor を閉じる(開いたままだと VRAM が約 3.2 GB 増える)。起動時に Editor が動いていても中断はせず、`meta.json` の `editor_running` と開始時の VRAM(`start_vram_mb`)に記録する。
- 周回と放置は、別のデータとして扱う。

| 種類 | 起動 | ゲームの出力先 | 回収先 | 集計 |
|---|---|---|---|---|
| 周回 | `Tools\PerfViewer\run_lap_variants.bat [周回数] [条件] [xN] [until]`(既定: 10 周 × 3 ラウンド × 4 条件 = 12 本、約 8 時間) | `...\perf\` | `PerfRecords\<PC>\perf\` | `lap_report.py --root <perf>` |
| 放置 | `Tools\PerfViewer\run_idle.bat [分] [条件] [xN] [until]`(既定: 120 分 × 4 条件 = 約 8 時間) | `...\idle\` | `PerfRecords\<PC>\idle\` | `lap_report.py --idle --root <idle>` |

- 条件(周回・放置とも)は `base`・`nodlss`・`maponly`・`unloadoff`。指定がなければ4つとも回す。`nodlss` は原因の確認用の対照。`xN` は同じ条件を回すラウンド数(条件を順に回す)。映像を止める条件(`novideo`・`both`)は、メモリ増加の主因でないと分かったため、2026/10/22 に bat から外した(ゲーム側の `-perfnovideo` は残る)。
- `run_lap_variants.bat` は、引数なしで起動すると、周回数・条件・ラウンド数・`until`・ビルドの場所をコンソールで聞く(Enter で既定値)。開始前に、同じ実行にする引数つきのコマンドを表示する。引数ありの起動では、聞かない。
- `until`: VRAM が総量の 99% 以上、または FPS が基準の 8 割未満(周回は 1 周目、放置は最初の 10 分)になった時点で終える。周数・分は上限になる。終えた理由は `meta.json` の `stop_reason`。
- 起動のフォルダ `<ラベル>_<起動日時>\` の中身: `meta.json`、`perf_<日付>.csv`、`transitions.csv`、`Player.log`(bat で回した起動だけ)。
- CSV の `state`(`GameStateId` の番号)・`lap`(周回の番号。放置は 0)・`trans`(遷移などの最中は 1)で、State 別・周回別に集計する。
- グラフは `Tools/PerfViewer/start_viewer.bat`(`server.py --root <perf または idle>`)で見る。
- 2026/10/22 より前の記録には、`state`・`lap`・`trans` 列がない。`lap_report.py` は、その記録の State 別・周回別の値を `transitions.csv` から推定する。
- 記録は、プロジェクト直下の `PerfRecords\<PC>\` に置く(Git 管理。`Player.log` は LFS)。別の PC の記録は混ぜて比べない。集計は PC ごとに行う。

| PC | フォルダ |
|---|---|
| 開発用(Ryzen 5 5600X / RTX 3060 Ti) | `5600X_3060Ti` |
| 開発用(i5-13600K / RTX 4070 Ti) | `13600K_4070Ti` |
| 展示用(Core Ultra 9 285K / RTX 5060 Ti) | `Ultra9_5060Ti` |

```
python Tools/PerfViewer/lap_report.py --root "PerfRecords/<PC>/perf"
```

# 作業・返答のルール

どの PC でも同じ振る舞いにする。PC ごとの設定(メモリ、ユーザー設定)には頼らず、ここに書く。

## 返答

- 日本語で、です・ます調。コード・ファイルパス・コマンドは原文のまま。
- 結論を先に書く。前置き・経過・結びの要約は書かない。結果、変更したファイル、確認できていない点だけを書く。
- 長さは依頼に合わせる。質問には答えだけを返す。提案は、依頼に関係するものを 1〜2 件まで。
- 見出し・表・箇条書きは、構造がある内容にだけ使う。一覧・比較は表にする。
- ファイルはプロジェクトからの相対パスのリンクで書く。コマンドはコードブロックに入れる(1 ブロックに 1 コマンド)。
- CLAUDE.md と docs の記述も、同じ書き方にする。

## 判断

- 確認の種類を分けて書く: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- 記録がない数値や結果は、推測で埋めず「未記録」「未確認」と書く。「次の候補」は、実施済みとは書かない。
- 削除・移動・上書きは、先に対象の中身を見る。取り返しがつかない操作は、対象を示して確認を取ってから行う。移動は、前後で SHA-256 の一致を確認する。
- 変更したら、変えた内容と、確認していないことを報告する。

## ワークツリー

- ワークツリーは `main` 以外を使用しない。

# Claude の標準設定

PC ごとに違う設定を、次にそろえる。

| 項目 | 標準 | 設定する場所 |
|---|---|---|
| 返答・判断のルール | このファイル | プロジェクト(全 PC に届く) |
| 出力スタイル | `Concise` | アプリの Settings → Claude Code → Output style |
| モデル | Sonnet 5.5(`claude-sonnet-5-5`) | モデル選択 |
| 権限モード | `default` | 権限の設定 |
| プラグイン | `unity@unity-agent-plugin` | 上の「プラグイン」 |

- 全 PC に確実に届くのは、このファイルと `docs/` だけ。出力スタイル・モデル・権限モードは、PC ごとに手で合わせる。
- メモリはプロジェクトのパスごとに保存される。パスが違う PC では、別のメモリになる。メモリには頼らない。
- プロジェクトの `.claude/` は Project Zip に入らない。
