# AimRacing2026 Portfolio

【要確認】作品の一行説明(参考: ジャンルと、使う機材が分かる一文)

ポートフォリオ用に、自分が書いたソースと実行データを公開するリポジトリです。ソースは `Source/`、実行データは [Releases](https://github.com/shoma-ohkawa/AimRacing2026_Portfolio/releases) の zip です。

## 制作情報

| 項目 | 内容 |
|---|---|
| 制作 | 【要確認】人数 |
| 担当 | 【要確認】 |
| 期間 | 【要確認】 |
| ジャンル | 【要確認】 |
| 開発環境 | Unity 6000.6.5f1 / C# / Visual Studio 2026 |
| 作成者 | 24CU0110 大川翔生 |

## 担当内容

【要確認】担当した機能を、箇条書きで書く。

## 使用技術

【要確認】

## 実行データ

【準備中】[Releases](https://github.com/shoma-ohkawa/AimRacing2026_Portfolio/releases) から `AimRacing2026_Build.zip` をダウンロードし、解凍して `AimRacing2026.exe` を起動します。

## 実行環境

【要確認】通常プレイに必要な環境と、展示用の追加機材。

## 操作方法

【要確認】

## Source について

自分が書いたスクリプトだけを入れています。Unity のプロジェクト全体(シーン、アセット、プレハブ)は入っていないため、このリポジトリだけではビルドできません。

| フォルダ | 内容 |
|---|---|
| `Source/GameManager/Flow` | State 遷移エンジン(遷移グラフ、進行、フェード、セッションのリフレッシュ) |
| `Source/GameManager/GameStates` | 各 State の実装 |
| `Source/Camera` | 3 画面出力(複数モニターへの分割表示) |
| `Source/Effect/RacingEnter` | レース突入演出 |
| `Source/UI` | Loading 画面、タイトルロゴ、残り時間 |
| `Source/Perf` | 計測の記録(性能・遷移時間)と自動周回 |
| `Source/Utility` | `SingletonMonoBehaviour`、`AppLog` |
| `Source/Editor` | エディタ拡張 |
| `Tools/PerfViewer` | 計測結果の集計とグラフ表示(Python・HTML) |

## 実装上の工夫

【要確認】

## 注意事項

【要確認】

## リンク

- プレイ動画: 【要確認】
- 実行データ: [Releases](https://github.com/shoma-ohkawa/AimRacing2026_Portfolio/releases)
