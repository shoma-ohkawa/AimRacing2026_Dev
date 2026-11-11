# Release v1.0.0 の準備(下書き)

作成: 2026/11/10(更新: 2026/11/12) / 作成者: 24CU0110 大川翔生
対象: ポートフォリオ用リポジトリの Releases(`v1.0.0`)

GitHub の Releases に載せるものと、ビルドから zip までの手順、公開の記録。2026/11/12 に Release `v1.0.0` を公開した(末尾の「実施の記録」)。

- 確認: `実測`(コマンドの出力で確認した)、`コード`(ソースを読んだ)、`文書`(既存の docs の記録)、`推定`、`未確認`。

## Release の設定

| 項目 | 値 |
|---|---|
| タグ | `v1.0.0`(対象は `main`) |
| タイトル | `AimRacing2026 Portfolio Build` |
| 添付 | `AimRacing2026_Build.zip` の 1 つ |
| Latest | ON |

## 本文(貼り付け用)

````markdown
ポートフォリオ確認用の実行データです。

## 起動方法

1. `AimRacing2026_Build.zip` をダウンロードして解凍します。
2. `AimRacing2026.exe` を起動します。

## 操作

通常プレイは、キーボードまたはゲームパッドでできます。操作は README の「操作方法」を見てください。
G923 ハンドルコントローラーと WIZMO モーションチェアは、展示用の追加機能です。

## 注意

- G923 や WIZMO がない環境では、FFB の反力とモーションチェアの連動は体験できません。
- WIZMO を使う場合は、安全を確認してから起動してください。
- DLSS 用の NVIDIA のライブラリ(`nvngx_dlss.dll`)を含みます。
- 公開用のため、開発用のファイルと認証情報は含めていません。
````

最後の行は、zip の中身を確認してから貼る(下の「zip の前に決めること」)。

## ビルドの手順

設定(2026/11/10 時点):

| 項目 | 値 | 確認 |
|---|---|---|
| Unity | 6000.6.5f1(`C:\Program Files\Unity\Hub\Editor\6000.6.5f1`) | `実測` |
| シーン | 9 つすべて有効(Load・Splash・Opening・Menu・Manager・Map・MapEnvironment・Loading・Result) | `実測` |
| スクリプトのバックエンド | Standalone の指定がなく、既定の Mono | `コード`(`ProjectSettings.asset` に `Android` の指定だけある) |
| Development Build | OFF にする | `推定`: 公開したビルドの `boot.config` にデバッグ接続の指定がない。Editor の設定は未確認 |
| 保存名 | `AimRacing2026.exe`(`productName` の `AimRacing2026_Unity6.3` は変えない) | `文書`(`docs/Temp_PortfolioRepository.md`) |
| 出力先 | プロジェクト直下の `Build\`(`.gitignore` の `/Build/` で管理外)。公開したビルドは `E:\Build`(プロジェクトの外) | `コード`(前半)/ `実測`(後半) |

- プロジェクトを開いている Editor がある PC では、先に Editor を閉じる(`実測`: この PC は 2026/11/10 に開いていた)。
- `Assets/#Scripts/Editor/AimRacingV2Build.cs` は使わない。出力先が別のユーザーのデスクトップに固定されている(`コード`)。

Editor を閉じて、コマンドで作る(`未確認`。このコマンドは実行していない。公開したビルドを何で作ったかは未記録):

```
"C:\Program Files\Unity\Hub\Editor\6000.6.5f1\Editor\Unity.exe" -batchmode -quit -projectPath "<プロジェクトのパス>" -buildWindows64Player "<プロジェクトのパス>\Build\AimRacing2026.exe" -logFile "<プロジェクトのパス>\Build\build.log"
```

Editor で作る場合: Build Profiles で Windows を選び、Development Build を OFF にして、`Build\AimRacing2026.exe` として保存する。

作った直後に確認すること:

1. `Build\build.log` の末尾が成功になっている(コマンドの場合)。
2. `AimRacing2026.exe` を起動して、Opening から Result まで通る。

## zip の前に決めること

| 項目 | 内容 | 状況 |
|---|---|---|
| `PublicKey.txt` | WIZMO の公開鍵。ビルド時に出力へコピーされる(`文書`) | **入れない**(2026/11/10 決定。WIZMO を使わない環境向けのため)。zip の作成時に `--exclude` で除いた(2026/11/12。元のフォルダからは削除していない) |
| `nvngx_dlss.dll` | DLSS のライブラリ(NVIDIA の署名つき、バージョン 310.5.3.0、約 52 MB)。Unity 6000.6.5f1 が、Windows のプレイヤーのビルドに入れる(`実測`: Editor の `PlaybackEngines\windowsstandalonesupport\Variations\*\nvngx_dlss.dll`) | **入れる方針**(2026/11/10 決定。Unity がビルドに入れるものを、そのまま配布する)。再配布の条件は `未確認`: ローカルの Unity にはライセンス文書がなく、Web でも条文は見つからなかった。公開前に、NVIDIA の DLSS SDK のライセンスで、同梱時に必要な表記(著作権表示・ライセンス文書)がないか確認する。必要なら zip と README に入れる。2026/11/12 に、ビルドの中身そのままで zip に入れて公開した(ライセンス文書は足していない)。条件の確認は未了 |
| `*_DoNotShip`・`*_ButDontShipItWithYourGame` のフォルダ | Unity が出力する、配布しないフォルダ | zip から外す(下のコマンドで除く)。公開したビルドにあったのは `AimRacing2026_BackUpThisFolder_ButDontShipItWithYourGame`(0.2 MB)だけで、`*_DoNotShip` はなかった(`実測`) |
| `Player.log`・計測の記録 | 実行後に出る | zip に入れない(ビルドの出力先ではなく `LocalLow` に出る: `文書`) |

zip を作る(2026/11/12 に、公開したビルド `E:\Build` で実行した。約 89 秒で 1.18 GiB。`実測`):

```
tar -a -c -f AimRacing2026_Build.zip -C Build --exclude "PublicKey.txt" --exclude "*_DoNotShip" --exclude "*_ButDontShipItWithYourGame" .
```

- zip の出力先は、ビルドのフォルダの外にする(中に作ると、zip 自身が入る)。実際は `C:\WINDOWS\system32\tar.exe` を指定し、`E:\Build_Release\` に出力した。
- GitHub の Releases は、添付 1 つが 2 GiB 未満。zip は 1,271,372,698 バイト(1.18 GiB)で収まった(`実測`)。
- 作った zip は、別のフォルダに解凍して照合した。246 ファイルすべてが、元と SHA-256 で一致し、`PublicKey.txt` と除外したフォルダは含まれない(`実測`)。**`AimRacing2026.exe` の起動と、`PublicKey.txt` がない状態での Opening から Result までは、未確認**(2026/11/12 時点で、確認した記録がない)。

## Release を作る

1. 前提: ポートフォリオ用リポジトリが GitHub に push 済み。
2. GitHub のリポジトリで Releases → Draft a new release。タグ `v1.0.0` を作り、タイトルと本文を貼って、zip を添付する。
3. README の `【準備中】` を解消して、push する(済: 2026/11/12、`d8f1ef1`)。

## 実施の記録(2026/11/12)

確認は `実測`(GitHub のページと API の表示、コマンドの出力。2026/11/12)。

| 項目 | 結果 |
|---|---|
| 公開 | `v1.0.0` を公開。タイトル `AimRacing2026 Portfolio Build`、Latest、pre-release ではない。2026/11/12 01:20(JST。API の `published_at` は 2026-11-11 16:20:19 UTC) |
| タグ | `main` の `9d503cc`(「AimRacing2026_Dev 238a986 から同期」)を指す |
| 添付 | `AimRacing2026_Build.zip`(1,271,372,698 バイト、アップロード済み)。ページの「Assets 3」は、zip と、GitHub が自動で付ける Source code(zip・tar.gz) |
| SHA-256 | GitHub が記録した値と、ローカルの zip の値(`15856BCD4196B268737D09DC56BB4B33EF229A7683006BA0B8B6D0D1500DF81A`)が一致。ダウンロードして照合したのではない |
| 本文 | 上の貼り付け用と同じ(`nvngx_dlss.dll` を含む旨の行を含む) |
| README | 実行データの `【準備中】` を解消(`d8f1ef1`)。`d8f1ef1` 時点の README に `【準備中】`・`【要確認】` は 0 件 |
| zip の場所 | `E:\Build_Release\AimRacing2026_Build.zip`(ローカル) |

未確認:

- 公開した zip の起動(Opening から Result まで通ること)。`PublicKey.txt` がない状態での動作。ログインしていないブラウザでのダウンロード。
- `nvngx_dlss.dll` の再配布の条件(NVIDIA の DLSS SDK のライセンス)。再配布できないと分かったときは、zip から外して作り直し、Release の添付を差し替える。DLL なしで起動するかは未確認。
- タグ `v1.0.0` は README の修正前の `9d503cc` のまま。zip の内容には関係しない。
