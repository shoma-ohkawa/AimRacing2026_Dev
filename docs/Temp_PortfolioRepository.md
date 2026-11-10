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

## どの PC でも同じ手順(2026/11/10 追記)

PC ごとの手作業(ファイルのコピー、`.gitignore` の書き直し)をなくすため、`Tools/Portfolio/` に書き出しのスクリプトを置いた。`AimRacing2026_Dev` の HEAD から書き出すので、どの PC でも同じ結果になる。

| ファイル | 役割 |
|---|---|
| `manifest.txt` | 書き出すファイルの一覧(`.cs` 34 本と `Tools/PerfViewer/`)。入れる・外すはここで決める |
| `sync_portfolio.bat` / `.ps1` | 書き出し。書き出し先に `.git` がなければ `git init`、GitHub に `main` があれば履歴を取り込む。`-Commit` でコミットまで |
| `template.gitignore` / `.gitattributes` / `.README.md` | 書き出し先に無いときだけ作る(上書きしない。README は手で書き足していく) |

手順:

1. Dev の変更をコミットする。スクリプトは HEAD を書き出すので、未コミットの変更は入らない(入らないファイルは、実行時に黄色で出る)。
2. 次を実行する。書き出し先は、既定で Dev の 1 つ上の `AimRacing2026_Portfolio`。変えるときは `-Target`。

```
Tools\Portfolio\sync_portfolio.bat -Commit
```

3. `README.md` の `【要確認】`・`【準備中】` を解消する(実行のたびに残り行数が出る)。
4. 初回だけ: GitHub で空のリポジトリを作り(README・ライセンスなし)、書き出し先で `git push -u origin main`。
5. ビルドを作り、Releases の `v1.0.0` に `AimRacing2026_Build.zip` を添付する。
6. 別の PC では、1〜3 を同じように行う。GitHub に `main` があれば取り込み、新しいコミットがあれば止まる(先に `git pull`)。

動作の決まり:

- 作成者名は `大川翔生`、メールは GitHub の noreply アドレスを、新しく作ったリポジトリにだけ設定する(push すると公開されるため、各 PC の本物のメールは使わない。`-AuthorName`・`-AuthorEmail` で変えられる)。
- マニフェストにないファイルは、書き出し先の `Source/`・`Tools/` から消える(git で戻せる)。
- push、GitHub のリポジトリ作成、Release は行わない(`gh` がない PC があるため)。

確認(`実測`、2026/11/10。スクラッチのフォルダで実行):

- 新規作成とコミット: 追跡ファイル 47 個(`.cs` 34 + `Tools/PerfViewer` 10 + `README.md`・`.gitignore`・`.gitattributes`)。上の「ポートフォリオ用リポジトリの現状」の 47 個と同じ。
- 再実行: 差分なし。
- ローカルの空リポジトリを GitHub の代わりにして、履歴の取り込み(`.git` のないフォルダ)と、新しいコミットがあるときの停止を確認した。
- GitHub は `shoma-ohkawa/AimRacing2026_Portfolio` が 404 のため、「`main` がない」と表示する動作だけ確認した。
- `.bat` から引数が `.ps1` に渡ることを確認した。
- 未確認: 実際の GitHub への push、認証ありの接続、別の PC での実行。

PC ごとの状態(`実測`、2026/11/10):

| PC | `E:\AimRacing\AimRacing2026_Portfolio` |
|---|---|
| 11/07〜08 のメモを書いた PC | `.git` あり。コミット `585d098`、remote 未登録(メモのとおり。この PC では未確認) |
| この PC(Dev は `D:\AimRacing\AimRacing2026_Dev`) | `.git` なし、`README.md` なし。`Source/` 34 本と `Tools/` 10 本だけ。スクリプトはまだ実行していない |

- 履歴を 1 つにするため、**最初に push する PC を 1 台に決める**。ほかの PC で別に作ったリポジトリ(履歴が別)を push すると拒否される。その PC は、フォルダを退避してスクリプトを実行し直せば、GitHub の履歴を取り込める。
- 「開発側との差」の `MultiScreenOutput.cs`・`MultiScreenSourceCamera.cs`(ヘッダだけの差)は、Dev 側にヘッダを足して解消した(2026/11/10。**Dev ではまだ未コミット**)。コミットするまでは、書き出し先にヘッダが入らない。

## 状況の更新(2026/11/10 夕)

追記: この節は、上の記録を書き換えず、その後の変化だけを示す。

| 項目 | 状況 |
|---|---|
| この PC のポートフォリオ用リポジトリ | `E:\AimRacing\AimRacing2026_Portfolio` を、`sync_portfolio` で git 化した。コミットは 2 つ(`f055beb` 書き出し、`9729ad2` README の記入)。追跡ファイル 47 個。remote は登録済みで、**push はしていない** |
| GitHub | `shoma-ohkawa/AimRacing2026_Portfolio` は見つからない(未作成か非公開)。空のリポジトリを作る手順は、ブラウザで行う |
| README | 制作情報・担当内容・操作方法・注意事項・プレイ動画のリンクを記入した。残る印は、実行データの `【準備中】` の 1 行 |
| ビルド | 未作成。この PC では、プロジェクトを開いている Editor が動いている(`実測`)。手順と zip の前に決めることは `Tools/Portfolio/release_v1.0.0.md` |
| 公開前の検索 | 認証情報・IP・メールは 0 件(`実測`) |

「残っていること」の表との対応:

| 順 | 状況 |
|---|---|
| 1 | 未。手順は `release_v1.0.0.md` |
| 2 | 未決。`PublicKey.txt` を入れるか、`nvngx_dlss.dll` を再配布してよいか |
| 3 | 決定済みのまま(`AimRacing2026.exe`、`AimRacing2026_Build.zip`) |
| 4 | 未。公開範囲を決めて、あなたがブラウザで作る |
| 5 | 未。4 のあと、`git push -u origin main` |
| 6 | 未。1 と 5 のあと |
| 7 | 済(`【準備中】` の 1 行を除く)。ビルドと Release のあとで解消する |
| 8 | 未決 |

公開前に決めること(未決):

- 学校名の定数(`日本電子専門学校ゲーム制作研究科`)が、`lap_report.py` と `server.py` に入っている。残すか、置き換えるか。
- コミットの作成者メールは、push すると GitHub で見える。GitHub の noreply アドレスにするか。
- 最初に push する PC を 1 台に決める(履歴が別のリポジトリは、あとから push できない)。

## 状況の更新(2026/11/10 push)

追記: この節は、上の記録を書き換えず、その後の変化だけを示す。

| 項目 | 状況 |
|---|---|
| push | **済み**。`E:\AimRacing\AimRacing2026_Portfolio` の `main`(コミット 2 つ、`53d5e9d` が先頭)を、`https://github.com/shoma-ohkawa/AimRacing2026_Portfolio.git`(公開)へ送った。GitHub の `main` とローカルの `main` は一致(`実測`) |
| GitHub のページ | ルート直下は `Source`・`Tools/PerfViewer`・`README.md`・`.gitignore`・`.gitattributes`。コミット 2 つ(`実測`、ページの表示) |
| コミットのメッセージ | `Co-Authored-By` の行は外した(README のコミットを書き直してから push) |
| 公開した内容 | 学校名の定数と `Tools/PerfViewer/` を含む(2026/11/10 に、残す・公開すると決めた) |
| 未確認 | プレイ動画のリンクを、ログインしていないブラウザで再生できるか。ファイル名 `AimRaicng2026_プレイ動画.mp4` の綴り |

「残っていること」の表との対応: 4・5 は済み。残りは 1(ビルド)、2(zip に入れるもの)、6(Release)、7(`【準備中】` の 1 行)、8(`AutoDemoDriver.cs`)。

## 状況の更新(2026/11/10 決定)

追記: この節は、上の記録を書き換えず、その後の変化だけを示す。

| 項目 | 結果 |
|---|---|
| プレイ動画のリンク | ログインしていないブラウザで視聴できた(あなたが確認) |
| `AutoDemoDriver.cs` | `Source/` に**入れない**と決めた。`manifest.txt` の注釈を直した |
| `PublicKey.txt` | zip に**入れない**と決めた(WIZMO を使わない環境向けのため)。ビルドは出力へコピーするので、zip の前に削除する |
| `nvngx_dlss.dll` | 再配布してよいかを、あなたが確認する。調べた範囲では、条文は見つからなかった(下) |

`nvngx_dlss.dll` について(`実測`・調査の結果):

- Unity 6000.6.5f1 の Editor に、Windows のプレイヤー用として 8 種類のフォルダに入っている。`win64_player_nondevelopment_mono` のものは約 52.2 MB、バージョン `310.5.3.0`、NVIDIA の署名つき(`実測`)。ビルドの出力に入る(Unity の issue tracker にも、使っていなくても入るという報告がある。ページの要約)。
- ローカルの Unity の `ThirdPartyNotices.txt` と HDRP のパッケージに、DLSS・NGX のライセンスの記述は見つからなかった(`実測`)。
- Web では条文自体は見つからなかった。ほかのエンジンでは、DLL をライセンス文書と一緒にアプリに同梱する扱いがある(Unigine の文書、`dlss_wgpu` の説明。どちらも要約で、細部は未確認)。Unity が同梱していても、自分のゲームで再配布してよいとは限らない(検索結果の要約の見方で、NVIDIA や Unity の文書では確認できていない。法的な助言ではない)。
- 確認先: NVIDIA の DLSS SDK のライセンス(`nvngx_dlss.dll` に付くライセンス文書)。
- 外す場合の影響は `未確認`(DLL なしで起動するか、DLSS が無効になるだけか)。試すなら、zip を解凍した先から DLL を消して起動する。

## 状況の更新(2026/11/10 `nvngx_dlss.dll` の方針)

追記: 上の「`nvngx_dlss.dll`」の行を受けた、その後の決定。

- **再配布する方針**にした(2026/11/10)。Unity がビルドに入れる `nvngx_dlss.dll` を、zip にそのまま入れる。
- 再配布の条件は、まだ `未確認`。公開前に、NVIDIA の DLSS SDK のライセンスで、同梱時に必要な表記(著作権表示・ライセンス文書)がないか確認する。必要なら zip と README に入れる。
- Release の本文の下書きに、「DLSS 用の NVIDIA のライブラリ(`nvngx_dlss.dll`)を含みます」を足した(`Tools/Portfolio/release_v1.0.0.md`)。
- 条件が分かって、再配布できないと分かったときは、zip から外して、DLL なしで起動するかを試す。