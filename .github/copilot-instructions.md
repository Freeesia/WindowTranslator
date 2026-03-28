コメント・プランは日本語で出力してください。
PRのタイトルと説明は日本語で記載してください。
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


