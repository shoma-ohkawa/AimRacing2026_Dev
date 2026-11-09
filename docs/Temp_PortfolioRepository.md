# 一時メモ: ポートフォリオ用リポジトリ

作成: 2026/11/08 / 作成者: 24CU0110 大川翔生
対象パス: `E:\AimRacing\AimRacing2026_Portfolio`(ポートフォリオ用リポジトリ。ローカル)

一時的なメモ。ポートフォリオの公開が終わったら削除する。`CLAUDE.md` の docs の表には載せていない。

- 確認: `実測`(コマンドの出力で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。
- 日付は 2026/11/07〜08 に確認した内容。

## 結論

1. 参考にしたリポジトリと同じ形(`README.md` + `Source/` + Releases のビルド)の下書きを、`E:\AimRacing\AimRacing2026_Portfolio` に git 化した。コミットは 1 つ。
2. **GitHub には、まだ何も公開していない。** remote は未登録。push も Release も未実施。
3. 公開するビルドは、Unity 6000.6.5f1 で**作り直す**ことに決めた。ビルドは**まだ始めていない**。この PC には、ゲームの exe がない。

## 目的

自分が書いたソースとビルドを、git で管理する。参考のリポジトリのように、ソースは GitHub のリポジトリに、ビルドは Releases の zip にする。

## 参考にしたリポジトリ

`nyankomaru/AimRacing2026_Portfolio`(他のアカウントのリポジトリ)。ページの表示から確認した(`実測`。取得は要約を通したので、細部は未確認)。

| 項目 | 内容 |
|---|---|
| ルート直下 | `README.md` と `Source/` だけ |
| 説明文 | 「G923ハンドルコントローラーとWIZMOモーションチェアを使用した体験型レーシングゲーム」 |
| コミット数 | 10 |
| README | 制作情報(8 名、担当はプログラマー)、担当内容、実行データの入手方法、操作方法、実装の工夫、注意事項 |
| ビルドの置き場 | git ではなく Releases。ビルド手順は README にない |
| Release | タグ `v1.0.0`、タイトル「AimRacing2026 Portfolio Build」、Latest。添付 3 件 |
| リリースノート | zip を展開して `AimRacing2026.exe` を実行する。通常プレイはキーボードとゲームパッド。G923 と WIZMO は展示用の追加機能。「開発用ファイルと認証情報を除いた」と明記 |
| ライセンス | ページ上に記載なし |
| 未確認 | 添付 3 件のファイル名とサイズ(リリースノートに `AimRacing2026_Build.zip` の名前はある)、言語の内訳 |

## ポートフォリオ用リポジトリの現状

| 項目 | 内容 |
|---|---|
| 場所 | `E:\AimRacing\AimRacing2026_Portfolio`(`AimRacing2026_Dev` とは別のリポジトリ) |
| ブランチ | `main` |
| コミット | `585d098` の 1 つ(「ポートフォリオの初回コミット」) |
| 作成者 | `大川翔生`。メールは現在使っているものと同じ(この文書にはアドレスを書かない) |
| 追跡ファイル | 47 個: `Source/` の `.cs` 34 本、`Tools/PerfViewer/` 10 本、`README.md`、`.gitignore`、`.gitattributes` |
| 容量 | 641 KB(Git LFS は不要) |
| remote | 未登録 |
| 公開先(予定) | `https://github.com/shoma-ohkawa/AimRacing2026_Portfolio`(README のリンク。GitHub 側にあるかは未確認) |

`Source/` の内訳: `Camera` 3、`Editor` 2、`Effect/RacingEnter` 3、`GameManager/Flow` 9、`GameManager/GameStates` 7、`Perf` 5、`UI` 3、`Utility` 2。

- `.gitignore`: ビルド(`Build/`、`*.zip`、`*.exe`)、計測の記録(`PerfRecords/`、`perf_*.csv`、`transitions.csv`、`*.log`)、Python・IDE・OS のファイル、`.claude/settings.local.json`。
- `.gitattributes`: 既定は `text=auto`。`.bat`・`.cmd`・`.ps1` は CRLF、`.sh` は LF に固定。
- コミットの作成者は、初回は PC 名つきの名前だった。公開前に `大川翔生` へ付け替えた(`74c8dc6` → `585d098`)。古い `74c8dc6` は、この PC の reflog にだけ残る(push しても送られない)。

## 開発側(`AimRacing2026_Dev`)との差(`実測`)

| 項目 | 結果 |
|---|---|
| `Tools/PerfViewer` | 10 本とも一致 |
| `Source/` | 2 本が違う: `MultiScreenOutput.cs`、`MultiScreenSourceCamera.cs`。違いはファイルヘッダだけで、ポートフォリオ側にだけある(開発側はヘッダなし) |
| `AutoDemoDriver.cs` | 著者は自分(`24CU0110`)だが、ポートフォリオに入っていない。計測が `CurrentLap` を参照している。入れるかは未決 |

## 公開前の確認

- 秘密情報の検索: `password`・`secret`・`api key`・`token`・IP・端末のシリアル・`C:\Users\`・`shoma`・`AppData` で、ポートフォリオのフォルダ内を検索した。該当は、`127.0.0.1`(ローカル専用の指定)、`%USERPROFILE%\AppData\LocalLow`(記録の既定の場所)、バッチの変数名 `TOKEN` だけ。範囲はポートフォリオのフォルダ内だけ。
- 計測ツールには、会社名(`日本電子専門学校ゲーム制作研究科`)が定数として入っている。公開してよいかは未確認。
- `README.md` は下書き。`【要確認】` と `【準備中】` が残っている。公開前にすべて解消する。

## ビルド

| 項目 | 内容 |
|---|---|
| 方針 | Unity 6000.6.5f1 で作り直す(TGS 時点の 6000.3.11f1 のビルドは使わない) |
| 今のビルド | この PC にない。`AimRacing2026*.exe` を `C:\Production`、`D:\`、`E:\ポートフォリオ`、`E:\AimRacing`(深さ 4 まで)で探したが、見つからなかった |
| 文書の記録 | 別の PC の `D:\AimRacing2026_Dev\Build\AimRacing2026_Unity6.3.exe`(文書) |
| 状況 | **ビルドは未開始** |

ビルド前の確認(`実測`、2026/11/07):

| 項目 | 結果 |
|---|---|
| Unity 6000.6.5f1 | `C:\Program Files\Unity\Hub\Editor\6000.6.5f1` にある |
| Windows のビルドサポート | ある(Mono と IL2CPP の両方) |
| `Library`・`Temp`・`Build`・`Logs`・`UserSettings` | 開発側にいずれもない。初回は全アセットの取り込みから始まる(所要時間は未記録) |
| Git LFS の実体 | 実体になっている(`GAME BGM3.wav` が 44.3 MB) |
| Build Settings | 9 シーンすべて有効: Load、Splash、Opening、Menu、Manager、Map、MapEnvironment、Loading、Result |
| `productName` / `bundleVersion` | `AimRacing2026_Unity6.3` / `0.24.0` |
| 既定の画面 | 2560×1440、`fullscreenMode` 1(FullScreenWindow)、`runInBackground` 1 |
| Unity のプロセス | `unity` と Unity Hub が動いていた。開発側のプロジェクトを開いているかは未確認。ビルドの前に Editor を閉じる |
| スクリプトのバックエンド | 未確認(設定ファイルの値を読んでいない) |

## 残っていること

| 順 | 内容 | 備考 |
|---|---|---|
| 1 | ビルドを作る | Editor を閉じてから。Development Build は OFF |
| 2 | zip に入れるものを決める | `PublicKey.txt`(WIZMO の公開鍵。ビルド時に出力へコピーされる: `文書`)、`nvngx_dlss.dll`(再配布してよいか)を確認する |
| 3 | exe 名と zip 名を決める | **決定済み(2026/11/08)**: exe は `AimRacing2026.exe`、zip は `AimRacing2026_Build.zip`。exe 名はビルドの保存名で決まる。`productName`(`AimRacing2026_Unity6.3`)は変えない(計測ツールの定数と、`LocalLow` の出力先がこの値に依存する)。ビルド時に保存名を合わせる |
| 4 | GitHub で空のリポジトリを作る | 公開範囲を決める。README・ライセンスは付けない。`gh` がこの PC にないので、ブラウザで作る |
| 5 | remote を登録して push する | ポートフォリオ用リポジトリ側で行う |
| 6 | Releases で `v1.0.0` を作り、zip を添付する | |
| 7 | `README.md` の `【要確認】`・`【準備中】` を解消する | 2026/11/08 に、ライセンス・UI・突入演出・ファイル名の `【要確認】` は解消。プレイ動画は 2026/11/08 に Google ドライブのリンクを入れた。残りは、下書きの注記、ビルドデータ(ビルドと Release の後) |
| 8 | `AutoDemoDriver.cs` を `Source/` に入れるか決める | |

## 未確認

- GitHub に `shoma-ohkawa/AimRacing2026_Portfolio` があるか。`AimRacing2026_Dev` の公開範囲(`gh` がなく確認できない)。
- 参考のリポジトリの添付 3 件の中身。
- 作り直したビルドが動くこと(ビルド自体が未開始)。
- ポートフォリオ用リポジトリのクローンと、`.gitattributes` による改行の動作。
