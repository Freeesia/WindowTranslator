# WindowTranslator 外部プラグイン開発ガイド

## 概要

WindowTranslator は外部プラグインによる機能拡張をサポートしています。
NuGet パッケージとしてプラグインを公開することで、他のユーザーがアプリ内から簡単にインストールできます。

## クイックスタート

### 1. プロジェクト作成

```bash
dotnet new classlib -n WindowTranslator.Plugin.YourPlugin
cd WindowTranslator.Plugin.YourPlugin
```

### 2. .csproj を設定

最小構成の `.csproj` 例:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDynamicLoading>true</EnableDynamicLoading>

    <!-- NuGet パッケージ情報 -->
    <PackageId>WindowTranslator.Plugin.YourPlugin</PackageId>
    <Title>Your Plugin</Title>
    <Version>1.0.0</Version>
    <Authors>YourName</Authors>
    <Description>プラグインストアに表示する具体的な説明文</Description>
    <PackageProjectUrl>https://github.com/YourName/YourPlugin</PackageProjectUrl>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <!-- この タグ が必須です（アプリ内一覧への表示条件） -->
    <PackageTags>$(PackageTags);windowtranslator-plugin</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
    <None Update="README.md" Pack="true" PackagePath="\" />

    <!-- WindowTranslator.Abstractions を NuGet から参照 -->
    <PackageReference Include="WindowTranslator.Abstractions" Version="x.y.z" ExcludeAssets="runtime" />

    <!-- ホスト側で提供されるパッケージは ExcludeAssets="runtime" を設定 -->
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" ExcludeAssets="runtime" />
    <PackageReference Include="Microsoft.Extensions.Options" ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

> **重要**: `<PackageTags>` に `windowtranslator-plugin` を含めることで、
> WindowTranslator アプリ内のプラグインストアに表示されます。
>
> `<Title>`、`<Description>`、`<Authors>`、プロジェクトURL、ライセンス情報は
> プラグインストアの一覧・詳細に表示されます。利用者が機能と提供元を判断できる
> 内容を設定してください。
>
> `WindowTranslator.Abstractions` の依存バージョン範囲は、インストール先の
> WindowTranslator との互換性判定に使用されます。サポートする最も古い
> `WindowTranslator.Abstractions` のバージョンを指定してください。

### README の多言語化

プラグインストアは、README 内のカルチャー名だけで構成された第2レベル見出しを
言語セクションとして認識します。README は1ファイルのまま、必要な言語だけを
次のように記載してください。

```markdown
# Your Plugin

## ja

日本語の説明です。

## 機能

- 機能A

## en

English description.

## Features

- Feature A
```

アプリのUIカルチャーとの完全一致、親言語、英語、先頭セクションの順で表示する
言語を選択します。`## 機能`や`## Features`など、カルチャー名ではない見出しは
言語セクション内の通常の見出しとして扱われます。カルチャー名は`ja`、`pt-BR`、
`zh-Hans`のように、.NETの正規表記と大文字・小文字まで一致させてください。
正規形のカルチャー名を持つ第2レベル見出しはREADME全体で言語境界として扱うため、
コード例など別の用途には使用しないでください。

リポジトリ内の公式プラグインは、既存のUI翻訳に合わせて次の22言語を1つの
READMEに収録しています。

`ja`、`en`、`ar`、`cs`、`de`、`es`、`fa`、`fil`、`fr`、`hi`、`hu`、`id`、
`ko`、`ms`、`pl`、`pt-BR`、`ru`、`th`、`tr`、`vi`、`zh-CN`、`zh-TW`

外部プラグインで全言語を用意する必要はありません。収録していないUI言語には
英語セクションが表示されます。

### 3. プラグインを実装

対象のインターフェースを実装します:

| インターフェース | 用途 |
|---|---|
| `ITranslateModule` | テキスト翻訳 |
| `IOcrModule` | 画像からテキスト認識 |
| `ICaptureModule` | ウィンドウキャプチャ |
| `IFilterModule` | 翻訳前後のテキスト加工 |
| `IColorModule` | 色変換 |
| `ICacheModule` | 翻訳結果キャッシュ |

実際の実装例は
[DeepLTranslator.cs](../Plugins/WindowTranslator.Plugin.DeepLTranslatePlugin/DeepLTranslator.cs)
と
[WindowTranslator.Plugin.DeepLTranslatePlugin.csproj](../Plugins/WindowTranslator.Plugin.DeepLTranslatePlugin/WindowTranslator.Plugin.DeepLTranslatePlugin.csproj)
を参照してください。

### 4. パッケージをビルドして NuGet に公開

```bash
dotnet pack -c Release -o ./nupkg
dotnet nuget push ./nupkg/WindowTranslator.Plugin.YourPlugin.1.0.0.nupkg \
  --api-key YOUR_API_KEY \
  --source https://api.nuget.org/v3/index.json
```

## プラグイン設定パラメータ

プラグインに設定画面を追加するには `IPluginParam` を実装します:

```csharp
using PropertyTools.DataAnnotations;
using WindowTranslator;

public class MyPluginParam : IPluginParam
{
    [Category("API設定")]
    [DisplayName("APIキー")]
    public string ApiKey { get; set; } = string.Empty;

    [Category("翻訳設定")]
    [DisplayName("翻訳元言語")]
    public string SourceLanguage { get; set; } = "ja";
}
```

## デフォルトモジュールの指定

プラグインをデフォルトとして使用させるには `[DefaultModule]` 属性を付与します:

```csharp
[DefaultModule]
[DisplayName("My 翻訳")]
public class MyTranslateModule : ITranslateModule { ... }
```

## プラグインインストール先

インストールされたプラグインは以下のフォルダに配置されます:

- NuGetからインストール: `%USERPROFILE%\.wt\nuget-plugins\{PackageId}\`
- ユーザーが手動で配置: `%USERPROFILE%\.wt\plugins\`

NuGetのインストール・更新・アンインストールでは、手動配置用フォルダの内容を変更しません。

## アプリからインストールする

1. WindowTranslator の設定を開きます。
2. 「プラグイン」タブを選択します。
3. 利用するプラグインの「インストール」を選択します。
   プレリリース版を利用する場合は、そのプラグインの「プレリリース」にチェックを入れます。
4. インストール完了後のダイアログから WindowTranslator を再起動します。

NuGetパッケージで宣言されたランタイム依存関係も再帰的に取得されます。
同じ依存パッケージに両立しないバージョン条件がある場合は、既存の
プラグイン配置を変更せずにインストールを中止します。

保存済みのモジュール選択やプラグイン設定パラメータだけを根拠に、パッケージが
自動インストールされることはありません。インストールはプラグインストアで
利用者が明示的に実行した場合だけ行われます。

プラグイン情報は WindowTranslator の起動後にバックグラウンドで更新されます。
プラグインをインストールした WindowTranslator と現在のメジャーバージョンが異なる場合、
そのプラグインは互換性がないものとして起動時のロード対象から除外されます。
また、インストール時に記録された `WindowTranslator.Abstractions` の依存バージョン範囲を
現在のホストが満たさない場合も、起動時のロード対象から除外されます。
互換バージョンを再インストールすると、次回起動から再び利用できます。

アンインストールするとパッケージは読み込み対象から除外され、管理フォルダの実体は
次回の WindowTranslator 起動時に削除されます。実行中に読み込まれたプラグインを
停止するにも、WindowTranslator の再起動が必要です。

## 注意事項

- プラグインは WindowTranslator と同じ `net10.0` をターゲットにしてください。`net11.0` など、ホストより新しいTFMは読み込めません
- `<EnableDynamicLoading>true</EnableDynamicLoading>` を必ず設定してください
- ホスト側で既に提供されているパッケージは `ExcludeAssets="runtime"` を設定し、DLL を重複させないようにしてください
- 通常のランタイム依存は `PackageReference` として宣言してください
- `ProjectReference` は通常、参照先プロジェクトへの NuGet 依存としてパッケージ化されます。参照先をNuGetへ公開しない場合は、`PrivateAssets="all"` を設定したうえで必要なDLLをプラグインパッケージへ同梱し、参照先が必要とする `PackageReference` もプラグイン側で宣言してください
- パッケージ固有の追加ファイルは、実行時に必要な相対ディレクトリを保って `lib/net10.0/` に含めてください
