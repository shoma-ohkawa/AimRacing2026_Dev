# 共通基盤(SingletonMonoBehaviour・AppLog)

作成: 2026/10/10(再作成: 2026/10/22) / 作成者: 24CU0110 大川翔生
対象パス: `Assets/#Scripts/Utility/SingletonMonoBehaviour.cs`、`Assets/#Scripts/Utility/AppLog.cs`

常駐マネージャーを 1 つに保つ基底クラスと、リリースで除去できるログ出力。

## 構成

| ファイル | 役割 | 作成 / 更新 |
|---|---|---|
| `SingletonMonoBehaviour.cs` | 常駐マネージャー用の汎用シングルトン基底 | 09/01 / 09/01 |
| `AppLog.cs` | `Debug.Log` のラッパー | 09/03 / 10/20 |

## SingletonMonoBehaviour

| メンバー | 内容 |
|---|---|
| `s_instance` | 唯一のインスタンス。型 `T` ごとに別々に持つ |
| `Instance` | `s_instance` を返す。`null` で未報告なら、未アタッチのエラーを 1 回出す |
| `Awake()` | `CheckInstance()` を呼ぶ |
| `CheckInstance()` | 未登録なら登録して `true`。自分が登録済みなら `true`。別のインスタンスが登録済みなら、自分のコンポーネントだけ `Destroy(this)` して `false` |

- 重複は、コンポーネントだけ破棄する。同じ GameObject の他のコンポーネントは残す。
- 未アタッチのエラーは、型ごとに 1 回。再アタッチで印を戻す。
- `DontDestroyOnLoad` は使わない。常駐は、`Manager` シーンを常に読み込む遷移エンジンが担う。
- 破棄されても、サブクラスの `Awake` の続きと `Start` は実行される。初期化したくないときは、自分で抜ける。

サブクラスの書き方:

```csharp
protected override void Awake()
{
    base.Awake();
    if (Instance != this) { return; }   //重複して破棄された場合は、初期化しない
    //初期化
}
```

### 使っているクラス

| クラス | シーン | 作成 | `base.Awake()` / 重複時の抜け |
|---|---|---|---|
| `GameFlowRunner` | `Manager` | 自作 | `Awake` なし |
| `TransitionFadeController` | `Manager` | 自作 | あり / あり |
| `TitleLogoOverlay` | `Manager` | 自作 | あり / あり |
| `MultiScreenOutput` | `Manager` | 自作 | あり / あり |
| `SoundManager` | `Manager` | 別のメンバー | あり / 独自の確認(同名のタグを探す) |
| `GameManager` | `Manager` | 要確認 | あり / なし |
| `HandleManager` | `Map` | 別のメンバー(要確認) | `Awake` なし |
| `DebugDevice` | ビルド対象の 9 シーンに未配置 | 要確認 | `Awake` なし |

## AppLog

| メソッド | 呼ぶ `Debug` | リリースビルド |
|---|---|---|
| `Log(object)` / `Log(object, Object)` | `Debug.Log` | 呼び出しごと除去(引数の評価もしない) |
| `LogWarning(...)` | `Debug.LogWarning` | 出力される |
| `LogError(...)` | `Debug.LogError` | 出力される |

- `Log` だけに `[Conditional("UNITY_EDITOR")]` と `[Conditional("DEVELOPMENT_BUILD")]` を付ける。
- `LogWarning`・`LogError` は 2026/10/20 に `Conditional` を外した。展示のリリースで不具合が起きたとき、`Player.log` に原因を残すため。
- 本体のスクリプトでは `Debug.Log` ではなく `AppLog` を使う。メッセージの先頭は `[nameof(クラス)]`。
- リリースで残る `LogWarning`・`LogError` は、引数の組み立ても実行される。毎フレーム通る処理では、一度だけ出す・間引く(例: `TireSound`、`DriveSound`、`G923_FFBController`)。
- `LogException`・`LogFormat` に当たるものはない。必要なら素の `Debug.LogException`(リリースでも出る)。
- 使用数(2026/10/10 時点): 自作 23 本は `AppLog` 42 か所・素の `Debug.Log` 0。`Assets/#Scripts` 全体は 124 / 37。`Assets` 全体は 205 / 218。
- 2026/10/20 に、毎フレーム出ていた素の `Debug.Log` 5 か所(`ChairController` 4、`ReturnRoad` 1。10 周で約 246 万行・73.5 MB)を削除した。

### 確認(2026/10/20)

- Unity 6000.6.5f1 でコンパイルして `error CS` 0 件。生成された `Assembly-CSharp.dll` で、`Log` にだけ `Conditional` が付くことを確認した。
- 実機のリリースビルド(自動走行 2 周)で、警告とエラーが `Player.log` に出て、あふれなかった(1,447 行)。
- ハンコン・ランキングサーバー・スマホがない PC では、警告とエラーが出る。展示用 PC で出たら、その機器がつながっていないサインとして読める。

## 未対応・要確認

| 項目 | 内容 |
|---|---|
| 存在確認 | `Instance` を参照すると、未アタッチなら最初の 1 回エラーが出る(例: `MultiScreenSourceCamera`)。存在確認用の API(`HasInstance` など)の案がある |
| `DEVELOPMENT_BUILD` | Unity 6.6 で非推奨(警告 UAC0009)。`AppLog.Log`、`MultiScreenOutput.cs`、`WindowsMonitorInfo.cs` が使う。代わりは `DEBUG`・`UNITY_ENABLE_CHECKS`・`UNITY_INCLUDE_INSTRUMENTATION`、または `Debug.isDebugBuild`。どれが合うかは未調査。今の 6000.6.5f1 では動く |
| 重複時の初期化 | `GameManager` は重複時の確認がなく、`Manager` が複数置かれると初期化が二重に走る恐れ。すべてのサブクラスを `Instance != this` に統一する案 |
| `TCPserver` | `AcceptTcpClient()` が失敗し続けると、待ちなしでログを繰り返す可能性(低リスク。未対応) |
| 静的変数 | Enter Play Mode のドメインリロードを無効にする場合は、リセットが必要(現在は無効にしていないので問題なし) |
| 素の `Debug.Log` | 他のメンバーのスクリプトと外部ライブラリに残る(`Assets/#Scripts` に 37 か所)。扱いは未決 |

## PDCA

担当: 9 名のチーム制作のうち、共通基盤 2 本。期間: 2026/09/01〜09/03。

| | 内容 |
|---|---|
| 目標 | ① 常駐オブジェクトが 1 つ ② 重複で同居コンポーネントを巻き込まない ③ 未配置の原因がすぐ分かる(ログがあふれない) ④ リリースでログの呼び出しと文字列の生成をしない ⑤ `Debug.Log` と同じ書き方で置き換えられる。達成の基準の数値は未記載 |
| 課題 | Additive 構成で常駐が重複・未配置になる / 重複の破棄が同居コンポーネントを巻き込む / 未配置のエラーが毎フレーム出る / 素の `Debug.Log` がリリースでも実行される / 置き換えの手間 |
| 検証・結果 | 8 クラスが継承(`Manager` に 6、`Map` に 1、未配置 1)。3 画面の実機の Development Build で、`AppLog` のログが `Player.log` に出ることを確認(2026/09/29)。リリースでの警告・エラーの出力を確認(2026/10/20) |
| 未記録 | 重複を実際に置いた試験 / リリースで `Log` が出ないことの実機確認 / ログ除去による処理時間・GC の効果 / 展示(TGS2026)での運用結果 |
| 次の候補 | `HasInstance` の用意 / 重複時の初期化の統一 / 素の `Debug.Log` 37 か所の扱いの決定 |
