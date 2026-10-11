# WindowTranslator の共通作業指示

このファイルはリポジトリ全体に適用する。Codex と GitHub Copilot で共通の作業規約はここを正とする。

## 作業と報告

- コメントと作業プランは日本語で記載する。PR・課題のタイトルと説明も日本語にする。
- GitHub への操作には GitHub プラグインを使用する。`gh` は使用しない。
- PowerShell の処理は短いコマンドに分け、必要な範囲だけ実行する。
- 指示された変更範囲を守る。ビルドエラーを理由に、依頼と異なる修正を加えない。
- 「〇〇に追加して」「〇〇で完結するようにして」は既存の対象への変更を意図している可能性が高い。新規作成を判断する前に、対象の有無と現在の実装を確認する。

### 明示的な禁止指示

- ユーザーの「変更禁止」「変更しないで」「実行禁止」などは、即時に適用される絶対的なスコープ境界として扱う。
- 禁止後は、新規実装だけでなく、取り消し、復元、修正、整形、生成物の更新、ステージ、コミット、push、PR・課題・コメントの更新など、禁止対象の状態を変える操作を行わない。
- 「元に戻すだけ」「誤操作を直すだけ」「必要な後処理」を禁止の例外と推測しない。取り消しや復元も変更として扱う。
- 禁止と別の依頼が同時にある場合、明示的に許可された対象だけを操作する。個別の許可を禁止全体の解除と解釈しない。
- 禁止された変更が既に存在していても勝手に取り消さず、状態を報告する。対象が曖昧なら読み取りだけで確認し、変更前に確認する。

## 構成

WindowTranslator は Windows のウィンドウをキャプチャし、OCR、翻訳、オーバーレイ表示を行う .NET 10 の WPF アプリ。

| 場所 | 役割 |
| --- | --- |
| `WindowTranslator/` | WPF アプリ、UI、処理の統合 |
| `WindowTranslator.Abstractions/` | プラグインのインターフェースと共有型。NuGet パッケージとして公開 |
| `Plugins/WindowTranslator.Plugin.*/` | 翻訳・OCR などのプラグイン |
| `WindowTranslator.Wix/` | WiX による MSI インストーラー |

処理の流れ: `ICaptureModule` → `IOcrModule` → `IFilterModule`（翻訳前）→ `ITranslateModule` → `IFilterModule`（翻訳後）→ 表示。

### プラグイン

- Weikio.PluginFramework でメインアセンブリ、Abstractions アセンブリ、アプリの `./plugins/`、ユーザーディレクトリの `plugins/` と `nuget-plugins/` から読み込む。登録と探索の実装は `WindowTranslator/Program.cs` を確認する。
- `ITranslateModule`、`IOcrModule`、`ICaptureModule`、`IFilterModule`、`IColorModule`、`ICacheModule` は Scoped。`IPluginParam` と `ITargetSettingsValidator` は Transient。
- 新規プラグインは `Plugins/WindowTranslator.Plugin.{名前}/` に作成し、`WindowTranslator.Abstractions` を参照して対象インターフェースを実装する。
- 表示名には `[DisplayName("表示名")]` または `[LocalizedDisplayName(typeof(Resources), "キー")]` を使う。既定モジュールにする場合は `[DefaultModule]` を付ける。
- プラグイン設定は `IPluginParam` 実装で定義し、必要な UI 属性は PropertyTools.DataAnnotations を使う。
- 表示名の解決順は `LocalizedDisplayNameAttribute` → `ResourceManager` → `DisplayNameAttribute` → クラス名。

### 設定

- ユーザー設定は `%UserProfile%\.wt\settings.json`。Debug ビルドでは `%UserProfile%\.wt.debug\settings.json`。パスの定義は `WindowTranslator.Abstractions/PathUtility.cs`。
- `UserSettings` は共通設定 `Common` と対象ごとの設定 `Targets` を持つ。`TargetSettings.SelectedPlugins` のキーはインターフェース名、`PluginParams` のキーは具体的なパラメータ型名。
- 設定は `IOptionsSnapshot<T>` でスコープごとに取得する。対象名付きの設定とプラグインパラメータの結び付けは `WindowTranslator/Program.cs` の既存構成を確認する。

## ビルドとテスト

```powershell
dotnet build WindowTranslator.slnx
dotnet test WindowTranslator.slnx
dotnet test WindowTranslator.Plugin.ColorThiefPlugin.Tests/WindowTranslator.Plugin.ColorThiefPlugin.Tests.csproj
```

テストは xUnit と Moq を使用する。変更箇所に合うテストから実行し、未実行のビルド・テスト・UI 確認を実施済みとして報告しない。

発行とライセンス収集が必要な場合:

```powershell
dotnet publish WindowTranslator -c Release -o publish
dotnet publish Plugins/WindowTranslator.Plugin.DeepLTranslatePlugin -c Release -o publish/plugins/DeepL
dotnet tool restore
dotnet nuget-license -t -ignore ignore-packages.json -override package-information.json -exclude-projects exclude-projects.json -ji include-projects.json -d licenses -fo licenses/third-party-licenses.txt -f net10.0 -err
```

## 翻訳リソース

- 原文は日本語。対象言語は `WindowTranslator/Properties/Resources.*.resx`、`docs/README.*.md`、`store/store_info.csv` の既存言語列を確認する。ファイル名のカルチャー表記は既存ファイルに合わせる。
- 対象言語のリソースファイルがない場合は作成する。既存の翻訳は変更しない。ただし翻訳文に日本語が残っている場合は、その言語の訳に置き換える。
- ログメッセージのための翻訳リソースは作らない。
- 文字化けを避けるため、スクリプトによる翻訳リソースの作成は禁止。`Resources.Designer.cs` は T4 テンプレート `Resources.Designer.tt` の生成物なので手動編集しない。

### 言語を追加するとき

- 原文は `WindowTranslator/Properties/Resources.resx` と `docs/README.md` を参照し、対応する `Resources.*.resx` と `docs/README.*.md` を追加する。各言語の README に新しい言語へのリンクを追加する。
- `WindowTranslator/Modules/Settings/AllSettingsViewModel.cs` の `TargetSettingsViewModel.Languages` にカルチャーを追加する。
- `WindowTranslator.Package/AppxManifest.xml` の `<Resources>` にカルチャーを追加し、MSIX 生成時にマニフェストを検証する。
- `store/store_info.csv` に新言語の列を追加し、`ja` 列から翻訳する。`ja` が URL または `False` の場合は同じ値を使う。`SearchTerm` は各行 40 文字以内、合計 21 単語以内に収める。

## コード実装

- `Directory.Build.props` で `LangVersion=latest`、Nullable、ImplicitUsings が有効。
- `IFilterModule` の翻訳前後の処理は `IAsyncEnumerable<TextRect>` を使用する。
- `WindowTranslator.Abstractions` で Windows 固有 API を扱う場合は `#if WINDOWS` で条件コンパイルする。
