コメント・プランは日本語で出力してください。
PowerShell実行時は極力少ない行数で実行するように、処理を分割してください。

## アーキテクチャ概要

WindowTranslatorは、WindowsアプリのウィンドウテキストをリアルタイムにOCR認識・翻訳してオーバーレイ表示するWPFデスクトップアプリ（.NET 10）です。

### プロジェクト構成

| プロジェクト | 用途 |
|---|---|
| `WindowTranslator` | WPFメインアプリ（UI・オーケストレーション） |
| `WindowTranslator.Abstractions` | プラグインインターフェース・共有型（NuGetパッケージとして公開） |
| `Plugins/WindowTranslator.Plugin.*` | 各種翻訳・OCRプラグイン |
| `WindowTranslator.Wix` | MSIインストーラー生成（WiX Toolset） |

### データフロー

```
ICaptureModule → IOcrModule → IFilterModule（前処理）→ ITranslateModule → IFilterModule（後処理）→ オーバーレイ表示
```

### プラグインシステム

**Weikio.PluginFramework** を使用した動的プラグイン読み込み。プラグインは以下の場所から検出される：
- メインアセンブリ・Abstractionsアセンブリ内
- `./plugins/`（アプリディレクトリ）
- `%AppData%\WindowTranslator\plugins\`（ユーザーディレクトリ）

**プラグインインターフェース一覧：**

| インターフェース | 用途 | ライフタイム |
|---|---|---|
| `ITranslateModule` | テキスト翻訳 | Scoped |
| `IOcrModule` | 画像からテキスト認識 | Scoped |
| `ICaptureModule` | ウィンドウキャプチャ | Scoped |
| `IFilterModule` | 翻訳前後のテキスト加工 | Scoped |
| `IColorModule` | 色変換 | Scoped |
| `ICacheModule` | 翻訳結果キャッシュ | Scoped |
| `IPluginParam` | プラグイン設定パラメータ | Transient |
| `ITargetSettingsValidator` | 設定バリデーション | Transient |

**新規プラグイン作成手順：**
1. `Plugins/WindowTranslator.Plugin.{名前}` にプロジェクトを作成
2. `WindowTranslator.Abstractions` を参照
3. 対象インターフェースを実装
4. `[DisplayName("表示名")]` または `[LocalizedDisplayNameAttribute(typeof(Resources), "キー")]` を付与
5. デフォルト実装にする場合は `[DefaultModule]` を付与
6. 設定パラメータは `IPluginParam` を実装したクラスで定義し、PropertyTools.DataAnnotationsでUI属性を付ける

**プラグイン表示名の解決優先度：** `LocalizedDisplayNameAttribute` > `ResourceManager` > `DisplayName` > クラス名

### 設定システム

- ユーザー設定ファイル: `%UserProfile%\.windowtranslator\settings.json`
- 設定クラス階層: `UserSettings` > `CommonSettings`（共通）/ `TargetSettings`（アプリ別）
- `TargetSettings.SelectedPlugins` にアプリ別のプラグイン選択を保持（キー: インターフェース名）
- `TargetSettings.PluginParams` にプラグイン設定を保持（キー: パラメータクラス名）
- `IOptionsSnapshot<T>` でスコープごとに設定を注入

## ビルド・テスト

```powershell
# ビルド（デバッグ）
dotnet build WindowTranslator.sln

# メインアプリ発行
dotnet publish WindowTranslator -c Release -o publish

# プラグイン発行（例）
dotnet publish Plugins\WindowTranslator.Plugin.DeepLTranslatePlugin -c Release -o publish\plugins\DeepL

# テスト実行（全体）
dotnet test

# テスト実行（単一プロジェクト）
dotnet test Plugins\WindowTranslator.Plugin.ColorThiefPlugin.Tests

# ライセンス収集（dotnet toolのrestore後）
dotnet tool restore
dotnet nuget-license -t -ignore ignore-packages.json -override package-information.json -exclude-projects exclude-projects.json -ji include-projects.json -d licenses -fo licenses\third-party-licenses.txt -f net10.0 -err
```

テストフレームワーク: **xunit** + **Moq**

## 翻訳リソース作成時

* 翻訳元言語は日本語
* 翻訳先言語は英語、ドイツ語、韓国語、中国語（簡体字）、中国語（繁体字）、ベトナム語、マレーシア語、インドネシア語、ブラジルポルトガル語、フランス語、スペイン語、アラビア語、トルコ語、タイ語、ロシア語、フィリピン語、ポーランド語、ペルシア語、チェコ語
* 翻訳先リソースファイルが存在しない場合は、リソースファイルを作成する
* 既存の翻訳テキストは変更しない
  * ただし、翻訳テキストに日本語が入っていた場合は各言語の翻訳に置き換える
* ログメッセージはリソースを作成しない
* 文字化けする可能性が高いので、スクリプトで翻訳リソースの作成は禁止
* `Resources.Designer.cs` はT4テンプレート（`Resources.Designer.tt`）から自動生成されるため手動編集禁止

### 新しい翻訳言語の追加時

* 翻訳元リソース
  * `Properties/Resources.resx`
  * `docs/*.md`
* 翻訳対象
  * `Properties/Resources.*.resx`
  * `docs/*.*.md`
* 各`docs/README.*.md`ファイルに新規言語へのリンクを記載する
* `.github/copilot-instructions.md`に翻訳先言語として追記
* `TargetSettingsViewModel.Languages`に新規言語カルチャーを追加
* `store/store_info.csv`に新規言語列を追加し`ja`列から翻訳する
  * `ja`列がURLなら他の言語列も同じURLを利用する
  * `ja`列が`False`なら他の言語列も同じ`False`を利用する
  * `SearchTerm`は各行40文字以内に収める
  * `SearchTerm`は合計21単語以内に収める

## コード実装時

* 指定された指示には必ず従う
  * ビルドが通らないときも、指示と異なる修正をしてはいけない
* LangVersion: `latest`、Nullable: `enable`、ImplicitUsings: `true` がすべてのプロジェクトで有効（`Directory.Build.props`）
* `IAsyncEnumerable<TextRect>` を `IFilterModule` のパイプラインで使用（ストリーミング処理）
* Windows固有APIは `#if WINDOWS` で条件コンパイル（`WindowTranslator.Abstractions` はクロスプラットフォームビルド対応）

## 指示の理解方針

### 1. 指示に含まれない要素は変更しない

- ユーザーが明示的に変更を求めていないコンポーネント・インターフェース・アーキテクチャには手を加えない
- 「A を削除して B で管理する」→ A の削除と B への移動のみ行う。それ以外の要素（A に関連する属性の設計、C の構造など）は変更しない

### 2. 「管理する場所を変える」は「実装方法を変える」ではない

- 「X側で管理する」=「X（クラス/モジュール）にプロパティ/ロジックを移動する」
- 既存の仕組み（属性の引数、インターフェースの設計など）自体を変えることではない
- 移動先での実装方法を指示なしに変更するのは過剰な解釈

### 3. 指示が曖昧な場合は最小変更を選ぶ

- 複数の解釈が可能な場合、最も小さい変更範囲（字義通り）を選択する
- 「より良い実装」への自発的な変更は禁止
- このファイル（`copilot-instructions.md`）の「指定された指示には必ず従う」を厳守する

### 4. 指示に登場しない変更を行う前に確認する

- 指示に明記されていない既存の仕組みを変えようとしている自分に気づいたら、一度立ち止まる
- 変更範囲が指示から大きく外れると判断した場合は、実装前にユーザーに確認を求める

### 5. PRへの指示はレビューコメントも必ず確認する

- PRに対して指示が出ている場合、`get_comments`（一般コメント）だけでなく `get_review_comments`（インラインレビューコメント）も取得する
- レビューコメントには行単位の具体的な指示が含まれることが多く、見逃すと誤った分析・実装につながる

