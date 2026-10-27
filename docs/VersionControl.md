# バージョン管理(Git・Git LFS)の準備

作成: 2026/10/23(更新: 2026/10/27) / 作成者: 24CU0110 大川翔生
対象: プロジェクト直下の `.gitattributes`・`.gitignore`、Unity の設定、Sourcetree でのリポジトリの作り方

Git(Sourcetree)でプロジェクトを管理するための準備。Git LFS の対象を決め、管理に入れないものを除き、リポジトリを作る手順をまとめた。**リポジトリは 2026/10/23 に作成済み**(ブランチ `main`、リモート `origin` は GitHub の `shoma-ohkawa/AimRacing2026_Dev`)。`.gitignore` は 2026/10/27 に追加した(`6048fa2`)。ただし、**実際の `.gitattributes` は、この文書の設計と違う**。差は末尾の「追記: 2026/10/27 実際のリポジトリの状態」。

- 確認: `実測`(数値で確認した)、`コード`(ソースを読んだ)、`推定`、`未確認`。
- 結果: 追加される約 5,700 ファイルのうち、Git LFS が 1,331 ファイル・約 3.3 GB、通常の Git が 4,364 ファイル・108.5 MB(`実測`。2026/10/23。ドライランの集計で、この文書を足す前。実際のリポジトリの数は末尾の追記)。

## 用意したもの

| ファイル | 内容 |
|---|---|
| `.gitattributes` | 改行の規則と、Git LFS の対象 |
| `.gitignore` | 管理に入れないもの |
| `.gitkeep`(6 個) | 空フォルダを残すための空ファイル(下の「事前確認の結果」) |
| `docs/VersionControl.md` | この文書 |

## Git LFS の対象形式

集計の方法: `.gitignore` を適用したあとに追加される全ファイルを、プロジェクトの外に作った一時リポジトリでドライランし、`filter=lfs` になるものを数えた(`実測`)。

| 種類 | 拡張子 | 数 | 容量(MB) | 主な場所 |
|---|---|---|---|---|
| 画像 | `.png` | 716 | 1,503.6 | `Assets` 全体 |
| 画像 | `.tga` | 56 | 379.4 | OP の建物ほかのテクスチャ |
| 画像 | `.exr` / `.hdr` / `.jpg` / `.tif` | 3 / 1 / 17 / 1 | 23.2 / 22.5 / 18.1 / 0.1 | 空の画像、車両モデルのテクスチャ |
| 音声 | `.wav` | 283 | 749.2 | `Assets/Sounds`(41 本、493 MB)と `FMOD_2024/Assets`(242 本、256 MB) |
| 音声 | `.bank` | 4 | 9.4 | `Assets/StreamingAssets`、`FMOD_2024/Build` |
| 3D モデル | `.fbx`(39)と `.FBX`(114) | 153 | 51.4 | `Assets` |
| フォント | `.otf` / `.ttf` | 2 / 1 | 5.9 / 0.3 | `Assets/TextMesh Pro/Fonts` |
| TextMesh Pro のフォントアセット | `*SDF.asset` | 3 | 261.4 | 2 本が 100 MB 超(128.0 MB と 131.2 MB) |
| ネイティブプラグイン | `.a` / `.so` / `.dll` / `.bc` | 18 / 25 / 31 / 2 | 153.3 / 37.7 / 31.0 / 21.1 | `Assets/Plugins/FMOD` の各 OS 用ほか |
| ネイティブプラグイン | 拡張子なし(macOS 用)/ `.lib` / `.jar` | 3 / 1 / 1 | 11.8 / 0.0 / 0.0 | `Assets/Plugins/FMOD/platforms/mac` ほか |
| SpeedTree | `.st` | 1 | 0.8 | `Assets/###Map2026/Tree` |
| 計測のログ | `PerfRecords/**/*.log` | 9 | 82.7 | `Player.log`(最大 70.1 MB) |
| **合計** | | **1,331** | **3,362.9** | `Assets` 3,019.6 / `FMOD_2024` 260.7 / `PerfRecords` 82.7 / `Packages` 0.0 |

- 現在は 0 件だが、入れたときの LFS 漏れを防ぐために先に書いた形式: `.jpeg` `.tiff` `.bmp` `.gif` `.psd` `.dds` / `.mp3` `.ogg` `.aif` `.aiff` `.flac` / `.mp4` `.mov` `.avi` `.webm` `.mpg` `.mpeg` / `.obj` `.blend` `.dae` `.3ds` `.max` `.ma` `.mb` `.c4d` `.glb` / `.dylib` / `.zip` `.7z` `.rar` `.gz` `.tar` `.unitypackage`。
- 選び方: バイナリで差分もマージもできない形式を、拡張子単位で入れた。Git の指定は拡張子単位なので、小さな PNG も LFS になる。
- `*SDF.asset` は YAML だが、アトラス画像を文字列で抱えて大きい。GitHub は 100 MB を超える通常のファイルの push を拒否するため、LFS にした。
- `.fbx` と `.FBX` は別々に書く(拡張子の大文字小文字が混在しているため)。Windows の `git lfs track` の表示は小文字になり、`*.fbx` が 2 行出る。表示だけの違い。
- 集計の結果、LFS に入らない 10 MB 以上のファイルは 2 つだけ: `Assets/!Scenes/MapEnvironment.unity`(14.2 MB)、`Assets/###Map2020/##FBX/Tree/Old_AllTree.prefab`(12.0 MB)。どちらもテキスト。LFS に入らないバイナリは、FMOD の `.pkf` が 1 本(0.02 MB)だけ。

## LFS にしないもの

| 種類 | 理由 |
|---|---|
| `.meta`・`.unity`・`.prefab`・`.mat`・`.asset`(SDF 以外)・`.controller`・`.playable` など Unity の YAML | テキストで差分が取れ、マージできる。LFS にするとマージできなくなる |
| `.cs`・`.shader`・`.hlsl`・`.md`・`.json`・`.xml`・`.py`・`.bat` | テキスト |
| `.csv`(計測記録を含む) | CLAUDE.md の決めどおり、計測記録は通常の Git。1 本 0.4 MB 以下(周回の記録)で、`.log` だけが大きい。増えて重くなったら LFS への切り替えを検討する(履歴の書き換えが要るので、早めに決める) |
| `.fspro`・`.js`(FMOD Studio) | テキスト |

## 管理に入れないもの(`.gitignore`)

| 対象 | 理由 |
|---|---|
| `Library/` `Temp/` `Obj/` `Logs/` `UserSettings/` | Unity が作り直す、またはこの PC だけの情報 |
| `Build/` | ビルドの成果物(`nvngx_dlss.dll` 52 MB、`UnityPlayer.dll` 37 MB など)。`Build/run_game_perf.bat` は `Tools/PerfViewer` のコピー |
| `*.csproj` `*.slnx` ほか | Unity が作り直す Visual Studio のファイル(10 個)。`.vsconfig` は残す |
| 直下の `*.log` | `fmod_editor.log` など。`PerfRecords` の中のログは対象外 |
| `FMOD_2024/.unsaved/` | FMOD Studio の一時ファイル |
| `.claude/settings.local.json` | Claude Code の個人設定 |
| `__pycache__/` `*.pyc` | Python の実行時ファイル |
| `.vs/` `.idea/` `.vscode/` `.DS_Store` `Thumbs.db` ほか | IDE・OS が作るもの |

- ドライランで無視された項目は 16 件: `Library/`、`Logs/`、`UserSettings/`、`Build/`、`FMOD_2024/.unsaved/`、Visual Studio のファイル 10、`fmod_editor.log`(`実測`)。
- **`FMOD_2024/Build` は管理に入れる。** `FMODStudioSettings.asset` が `sourceBankPath: FMOD_2024/Build` を指しているため(`コード`)。`.bank` が 2 本だけで小さい。
- Project Zip(`ProjectZipWhitelist.cs`)と同じ考え方にそろえた。違いは、`PerfRecords` を Git に入れる点(CLAUDE.md の決め。Zip には入らない)。

## 改行

| 対象 | 規則 | 理由 |
|---|---|---|
| 既定 | `text=auto`。リポジトリの中は LF、作業フォルダは各自の設定(Windows は CRLF) | `.cs`(476 本)、`.shader`、`.md` は CRLF で書かれている |
| Unity の YAML(`.meta` `.unity` `.prefab` `.mat` `.asset` ほか) | LF に固定 | Unity が LF で書く。`.meta` 2,840 本、`.asset`、`.prefab`、`.mat`、`.unity` はすべて LF(`実測`) |
| `.bat` `.cmd` | CRLF に固定 | LF だとバッチが誤動作することがある |
| `.sh` | LF に固定 | |
| `.csv` | 変換しない | LF と CRLF が混在(43 本のうち CRLF が 3 本)。計測記録を 1 バイトも変えないため |
| LFS の対象 | 変換しない | |

- 確認: 見本ファイルの一時リポジトリで、`.cs` は「リポジトリ LF・作業フォルダ CRLF」、`.unity` は LF 指定、`.csv` は混在のまま保存されることを確認した(`実測`)。
- この PC の `core.autocrlf` は `true`。この規則は `core.autocrlf` に関わらず効く設計だが、`true` 以外の PC では未確認。

## 事前確認の結果

| 項目 | 結果 | 確認 |
|---|---|---|
| Unity のメタファイル | `Visible Meta Files`(`VersionControlSettings.asset`) | コード |
| Asset Serialization | `Force Text`(`m_SerializationMode: 2`) | コード |
| 機密情報 | `Assets/#Scripts` と `ProjectSettings` を検索(password・secret・api key・token・秘密鍵)。該当は URL の中の `secrets` の文字と、空欄の 2 項目(`ps4NPTitleSecret`、`metroCertificatePassword`)だけ。`Assets/Plugins` などの他のコード、シーン、プレハブの中は未検索 | 実測(範囲は限定) |
| `PublicKey.txt`(直下、32 バイト) | WIZMO(椅子)のプラグインが、ビルドの出力へコピーする公開鍵(`WIZMOTools.cs` の 23 行)。ビルドと椅子に必要なので管理に入れる。リモートは非公開にする | コード |
| 空フォルダ(6 個) | Git は空フォルダを保存しない。Unity はフォルダごとに `.meta` を作るので、クローンすると「フォルダのない `.meta`」の警告になる。先頭が `.` のファイルは Unity が無視する(`.meta` が作られない)ので、`.gitkeep` を置いた。この版の Unity での動作は未確認 | コード・未確認 |
| パスの長さ | 最長 115 文字(`Assets` 内)。日本語名のファイルが 322 本。`core.longpaths true` を設定する | 実測 |
| 埋め込みパッケージ | `Packages/unity-ffb-master` は自前の `.gitattributes`(`* text=auto`)と `.gitignore` を持つ。そのフォルダの中だけで効く。`UNITYFFB.dll` は LFS に入る | 実測 |
| ツール | Git 2.55.0、Git LFS 3.7.1。グローバル設定に `filter.lfs` と `core.autocrlf=true` がある。Sourcetree 3.4.32 がインストール済み。Sourcetree が使う Git(埋め込みかシステムか)は未確認 | 実測 |
| ディスク | D: に 235.7 GB の空き。LFS は `.git/lfs` にもコピーを持つので、作業フォルダ(約 3.4 GB)に加えて約 3.3 GB が要る | 実測・推定 |

`.gitkeep` を置いた空フォルダ:

- `Assets/Resources`
- `Assets/###Map2022/people/People/Texture`
- `Assets/###Map2026/###Map2025/2025_Members/Aim_NewMountain_Morimoto/0911_`
- `Assets/###Map2026/SplineCable/FBX`
- `Assets/DrivingTools/AutoDriving/Editor`
- `Assets/Plugins/Windows`

## リポジトリの作り方

作成済み(2026/10/23)。以下は、作り直すときと、別の PC・別のリポジトリで作るときの手順。実際の作成との違いは、末尾の「追記: 2026/10/27 実際のリポジトリの状態」。プロジェクトのフォルダは PC ごとに違う(この PC は `C:\Production\AimRacing2026_Dev`。2026/10/23 の手順は `D:\AimRacing\AimRacing2026_Dev`)。

### 始める前に

1. リモートを先に作る(GitHub、Bitbucket、GitLab など)。**非公開**にし、README・`.gitignore`・ライセンスは追加せず、空で作る。LFS の容量の上限を確認する(下の「容量と上限」)。
2. Unity Editor を閉じる。
3. Sourcetree の「ツール > オプション > Git」で、システムの Git(`C:\Program Files\Git`)を使う設定にする。メニュー名は 3.4.32 で未確認。

### 手順

PowerShell で、プロジェクトのフォルダに移動してから行う。最初のコミットは、Sourcetree ではなくコマンドで行う(3.4 GB・約 5,700 ファイルを一度に扱うので、Sourcetree の画面更新が重くなりやすい。推定)。

1. プロジェクトのフォルダに移動する。

```
cd C:\Production\AimRacing2026_Dev
```

2. リポジトリを作る。ブランチ名は `main`。

```
git init -b main
```

3. 長いパスを許可する。

```
git config core.longpaths true
```

4. 日本語のファイル名をそのまま表示する。

```
git config core.quotepath false
```

5. このリポジトリに Git LFS のフックを入れる。

```
git lfs install --local
```

6. 最初に、規則のファイルだけをステージする。

```
git add .gitattributes .gitignore
```

7. 規則のファイルだけをコミットする。

```
git commit -m "Add .gitattributes and .gitignore"
```

8. LFS の対象のパターンが並ぶことを確認する。

```
git lfs track
```

9. 全部をステージする。3.4 GB を読むので時間がかかる(所要時間は未記録)。

```
git add .
```

10. LFS に入ったファイルを確認する。1,331 行前後(追加したファイルで変わる)。

```
git lfs ls-files
```

11. 初回コミットを作る。

```
git commit -m "Initial commit"
```

12. リモートを登録する。`<URL>` はリモートのリポジトリの URL。

```
git remote add origin <URL>
```

13. 送る。LFS の約 3.3 GB が一緒に上がる。途中で切れたら、同じコマンドをもう一度実行する(再開の動作は未確認)。

```
git push -u origin main
```

14. 確認として、別のフォルダへクローンする。

```
git clone <URL> D:\Temp\verify
```

15. クローンしたフォルダで、LFS のファイルが実体になっていることを確認する。約 44 MB なら実体、130 バイト前後ならポインタのまま。

```
(Get-Item "D:\Temp\verify\Assets\Sounds\BGM\2026\GAME BGM3.wav").Length
```

### Sourcetree への登録

手順 11 のあとで、Sourcetree の「Add(既存のローカルリポジトリを追加)」で `C:\Production\AimRacing2026_Dev` を選ぶ。以降のコミット・プル・プッシュは Sourcetree でよい。メニュー名は 3.4.32 で未確認。

## 他のメンバーが取り込む手順

1. Git for Windows を入れる(Git LFS が入っている)。
2. 1 回だけ実行する。

```
git lfs install
```

3. クローンする(約 3.3 GB。所要時間は未記録)。

```
git clone <URL>
```

4. Unity Hub で 6000.6.5f1 を入れ、クローンしたフォルダを開く。初回は `Library` を作るため、インポートに時間がかかる(未記録)。
5. Visual Studio は、`.vsconfig` の構成(ゲーム開発 / Unity)を入れる。

## 運用のルール

- **バイナリを足す前に、拡張子が `.gitattributes` にあるか確認する。** 新しい拡張子なら、先に追加してコミットしてから、ファイルを足す。

```
git lfs track "*.拡張子"
```

- LFS に入れ忘れてコミットしたときは、push 前なら次で直せる。履歴を書き換えるので、他のメンバーに渡したあとは全員の調整が要る。

```
git lfs migrate import --include="*.拡張子"
```

- `.meta` は、アセットと一緒にコミットする。移動・名前変更・削除は、Explorer ではなく Unity の Project ウィンドウで行う(`.meta` が孤立するため)。
- シーンとプレハブは、同じファイルを 2 人で同時に編集しない。YAML のマージは難しい。担当を分けるか、下の UnityYAMLMerge を使う。
- `Edit > Project Settings > Editor` の `Version Control Mode`(`Visible Meta Files`)と `Asset Serialization`(`Force Text`)は変えない。
- コミットの前に、Unity で保存する(`File > Save Project`)。
- 計測記録は、`PerfRecords\<PC>\` にフォルダごと置いてコミットする(CLAUDE.md の「計測記録」)。`Player.log` は自動で LFS に入る。

## UnityYAMLMerge の設定(任意)

競合したシーン・プレハブを、Unity のツールで統合する。自動では動かさず、競合したときに `git mergetool` で呼ぶ。**未検証**(実際の競合で試していない)。

`.git/config` に次を足す。パスは、Unity のインストール先に合わせる(この PC では次のパスに存在する: `実測`)。

```
[merge]
	tool = unityyamlmerge
[mergetool "unityyamlmerge"]
	trustExitCode = false
	cmd = 'C:/Program Files/Unity/Hub/Editor/6000.6.5f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"
```

競合したら、次を実行する。

```
git mergetool
```

## 容量と上限

- Git LFS の合計は約 3.3 GB。クローンのたびに全部を取得する。
- ホスティング先ごとの、LFS の容量・転送量の上限と 1 ファイルの上限は**未確認**。リポジトリを作る前に確認する。
- 減らす候補(いずれも未実施):
  - `sf-pro-rounded-regular SDF.asset`(63 文字しかないのに 8192×8192 のアトラスで 128 MB)を、小さいアトラスで作り直す(`CurrentStateAnalysis.md` の U-1)。
  - 未参照のアセット(約 306 MB)の整理(`CurrentStateAnalysis.md` の X-1)。
  - `Assets/Sounds` と `FMOD_2024/Assets` の wav の重複(未調査)。

## 未確認

- 実際のリポジトリでの、初回コミット・push・クローンの所要時間(コミットと push は済んでいるが、時間は未記録)。
- Sourcetree 3.4.32 が使う Git と、LFS ファイルの表示。
- リモートの LFS の上限。
- クローンしたあとに、Unity で開いてエラーなく読み込めること(`.gitkeep` と LFS の実体化を含む)。
- `core.autocrlf` が `true` 以外の PC での改行。
- UnityYAMLMerge。
- 機密情報の検索は、`Assets/#Scripts` と `ProjectSettings` だけ。

## PDCA

担当: 9 名のチーム制作のうち、バージョン管理の準備。期間: 2026/10/23。

| | 内容 |
|---|---|
| 目標 | ① 大きなバイナリを LFS に入れ、通常の Git を軽く保つ ② 生成物・個人の設定・機密を入れない ③ Unity の `.meta` と YAML を壊さない ④ 誰でも同じ手順で作れる。達成の基準の数値は未記載 |
| 課題 | `Assets` が 3.1 GB・5,038 ファイル / 100 MB を超えるテキスト(フォントアセット)が 2 本 / 拡張子の大文字小文字が混在(`.FBX` が 114 本)/ 空フォルダ 6 個 / 改行が種類ごとに違う(`.cs` は CRLF、`.meta` は LF)/ `Build/` に 100 MB 級の成果物 / 埋め込みパッケージが自前の `.gitattributes` を持つ |
| 検証・結果 | プロジェクトの外の一時リポジトリでドライランした。追加される 5,695 ファイル(この文書を足す前)のうち、LFS が 1,331 ファイル・3,362.9 MB、通常が 4,364 ファイル・108.5 MB。LFS に入らない 50 MB 以上のファイルは 0。LFS に入らないバイナリは `.pkf` が 1 本(0.02 MB)。見本ファイルの一時リポジトリで、LFS のポインタ化、改行、無視を確認した |
| 未記録 | 実リポジトリでの所要時間 / クローンの実行(push は済み。時間は未記録) / クローン後の Unity での読み込み / リモートの LFS の上限 / UnityYAMLMerge の動作 |
| 次の候補 | 設計どおりの `.gitattributes` にするかの判断(下の追記。`.gitignore` は 2026/10/27 に追加済み) / `FMOD_2024/.unsaved/` を追跡から外すかの判断 / フォントアセットの縮小 / 未参照アセットの整理 / wav の重複の調査 / UnityYAMLMerge の試験 |

## 追記: 2026/10/27 実際のリポジトリの状態

追記: 2026/10/27 / 作成者: 24CU0110 大川翔生
上の本文(設計と、2026/10/23 のドライランの数)は書き換えない。この節は、作成後のリポジトリを `git` で調べた結果。確認は `実測`(コマンドの出力。2026/10/27)。`.gitignore` を `6048fa2` でコミットしたあとの状態に直した。

### 状態

| 項目 | 内容 |
|---|---|
| 作成 | 2026/10/23。ブランチ `main`。最初のコミットは `61dcfdb`「AimRacing2026 初回コミット」。以降のコミットは `git log` で見る |
| リモート | `origin` = GitHub の `shoma-ohkawa/AimRacing2026_Dev`。`main` は `origin/main` を追跡する。push の状態は、`git status -sb` で見る(ローカルの記録を見るだけで、リモートには問い合わせない)。公開範囲は未確認 |
| 場所 | この PC は `C:\Production\AimRacing2026_Dev`。本文の手順は `D:\AimRacing\AimRacing2026_Dev`(同じ PC かは未確認) |
| 追跡するファイル | 5,928 個(`.gitignore` の追加後。追加前は 5,927 個)。うち LFS が 1,309 個・3,197.1 MB(本文と同じ単位。作業フォルダの実体の合計)。`Library`・`Build`・`UserSettings` は 0 個 |
| LFS のフック | `post-checkout`・`post-commit`・`post-merge`・`pre-push` が入っている |
| ツール | Git 2.55.0、Git LFS 3.7.1。`core.autocrlf=true`(グローバル設定) |
| 空フォルダ | `.gitkeep` 6 個すべてが追跡されている |

### 本文の設計との差

| 項目 | 本文(設計) | 実際 | 影響 |
|---|---|---|---|
| コミット | 規則のファイルだけの 1 つ目(手順 7)と、初回コミット(手順 11) | 1 つ(`.gitattributes` を含む) | — |
| `.gitattributes` | 改行の規則と、LFS の対象 | 19 行。LFS の指定だけで、改行の規則(`text=auto` など)がない。本文にない `*.ai` がある | `.cs`・`.meta` などの改行は、各 PC の `core.autocrlf` 次第 |
| LFS の対象 | 1,331 ファイル・3,362.9 MB | 1,309 ファイル・3,197.1 MB。差の 22 個(165.9 MB)は、`.a` 18・`.st` 1・macOS 用の拡張子なし 3。指定がなく、通常の Git に入っている | 最大が 15.2 MB なので、GitHub の 100 MB の制限には掛からない。通常の Git が 165.9 MB 重くなる |
| 先に書いた形式 | `.jpeg`・`.psd`・`.mp3`・`.mp4`・`.obj`・`.zip` など 33 形式(現在は 0 件) | 33 形式とも指定がない | 足すと、LFS に入らず通常の Git に入る |
| `.gitignore` | 直下に置く | 2026/10/27 に追加(`6048fa2`)。33 行で、「管理に入れないもの」の表の項目を入れた。`.gitignore` だけで無視される未追跡は 16 件(`.vs/`・`Build/`・`Library/`・`Logs/`・`UserSettings/`、Visual Studio のファイル 10 個、`fmod_editor.log`)で、`.git/info/exclude` のときと同じ | 他の PC がクローンしても、除外の規則が届く(クローンでの確認は未実施) |
| 除外の置き場 | — | `.git/info/exclude`(この PC だけ)に 10 行: `/Library/` `/Temp/` `/Obj/` `/Logs/` `/UserSettings/` `/Build/` `/.vs/` `/*.csproj` `/*.slnx` `/*.log` | `.gitignore` が同じ規則を持つので、この 10 行は要らない(消していない。残しても害はない)。`FMOD_2024/.unsaved/`・`.claude/settings.local.json`・`__pycache__/`・`*.pyc` などは `.gitignore` だけにある |
| 追跡済みの除外対象 | 管理に入れない | `FMOD_2024/.unsaved/` の 231 個(0.43 MB、LFS ではない)が、初回コミットから追跡されている | `.gitignore` に載せても、追跡は止まらない。FMOD Studio が保存するたびに、変更として出る。外すなら別のコミットで `git rm --cached`(履歴は書き換えない)。未決 |
| `git config` | `core.longpaths true`・`core.quotepath false`(手順 3・4) | local・global・system のどれにもない | 日本語名の表示と、長いパス(最長 115 文字)への影響は未確認 |

- 別の PC でクローンしても、`.gitignore` により `Library`・`Build` などは未追跡として出ない(`推定`。クローンでの確認は未実施)。
- `.gitattributes` の差の対応(本文どおりに入れる、実際に合わせて本文を直す、など)は未決。`.gitattributes` の LFS の指定を足しても、すでに通常の Git に入った 22 個は LFS にならない(履歴の書き換え、`git lfs migrate import` が要る。本文の「運用のルール」)。
- 未確認: リモートの公開範囲、別フォルダへのクローンと LFS の実体化(手順 14・15)、Sourcetree への登録。
