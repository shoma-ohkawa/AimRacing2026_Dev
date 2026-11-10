# Release v1.0.0 の準備(下書き)

作成: 2026/11/10 / 作成者: 24CU0110 大川翔生
対象: ポートフォリオ用リポジトリの Releases(`v1.0.0`)

GitHub の Releases に載せるものと、ビルドから zip までの手順。ビルドは未実行(2026/11/10 時点)。

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
| Development Build | OFF にする | `未確認`(Editor の Build Profiles で見る) |
| 保存名 | `AimRacing2026.exe`(`productName` の `AimRacing2026_Unity6.3` は変えない) | `文書`(`docs/Temp_PortfolioRepository.md`) |
| 出力先 | プロジェクト直下の `Build\`(`.gitignore` の `/Build/` で管理外) | `コード` |

- プロジェクトを開いている Editor がある PC では、先に Editor を閉じる(`実測`: この PC は 2026/11/10 に開いていた)。
- `Assets/#Scripts/Editor/AimRacingV2Build.cs` は使わない。出力先が別のユーザーのデスクトップに固定されている(`コード`)。

Editor を閉じて、コマンドで作る(`未確認`。実行したことがない):

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
| `PublicKey.txt` | WIZMO の公開鍵。ビルド時に出力へコピーされる(`文書`) | **入れない**(2026/11/10 決定。WIZMO を使わない環境向けのため)。zip を作る前に、`Build\` から削除する |
| `nvngx_dlss.dll` | DLSS のライブラリ(NVIDIA の署名つき、バージョン 310.5.3.0、約 52 MB)。Unity 6000.6.5f1 が、Windows のプレイヤーのビルドに入れる(`実測`: Editor の `PlaybackEngines\windowsstandalonesupport\Variations\*\nvngx_dlss.dll`) | **入れる方針**(2026/11/10 決定。Unity がビルドに入れるものを、そのまま配布する)。再配布の条件は `未確認`: ローカルの Unity にはライセンス文書がなく、Web でも条文は見つからなかった。公開前に、NVIDIA の DLSS SDK のライセンスで、同梱時に必要な表記(著作権表示・ライセンス文書)がないか確認する。必要なら zip と README に入れる |
| `*_DoNotShip`・`*_ButDontShipItWithYourGame` のフォルダ | Unity が出力する、配布しないフォルダ | zip から外す(下のコマンドで除く) |
| `Player.log`・計測の記録 | 実行後に出る | zip に入れない(ビルドの出力先ではなく `LocalLow` に出る: `文書`) |

zip を作る(`Build\` で試した結果ではなく、ダミーのフォルダで除外の動作だけ `実測`):

```
tar -a -c -f AimRacing2026_Build.zip -C Build --exclude "*_DoNotShip" --exclude "*_ButDontShipItWithYourGame" .
```

- GitHub の Releases は、添付 1 つが 2 GiB 未満。zip の容量は `未確認`(ビルド後に見る)。
- 作った zip は、別のフォルダに解凍して、`AimRacing2026.exe` が起動することを確認してから添付する。`PublicKey.txt` がない状態で、Opening から Result まで通ることも確認する(`未確認`)。

## Release を作る

1. 前提: ポートフォリオ用リポジトリが GitHub に push 済み。
2. GitHub のリポジトリで Releases → Draft a new release。タグ `v1.0.0` を作り、タイトルと本文を貼って、zip を添付する。
3. README の `【準備中】` を解消して、push する。
