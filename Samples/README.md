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
    <Version>1.0.0</Version>
    <Authors>YourName</Authors>
    <Description>説明文</Description>
    <!-- この タグ が必須です（アプリ内一覧への表示条件） -->
    <PackageTags>windowtranslator-plugin</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>

  <ItemGroup>
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

```csharp
using System.ComponentModel;
using WindowTranslator.Modules;

[DisplayName("MyPlugin 翻訳")]
public class MyTranslateModule : ITranslateModule
{
    public async IAsyncEnumerable<string> TranslateAsync(
        IAsyncEnumerable<string> texts,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var text in texts.WithCancellation(cancellationToken))
        {
            yield return await MyTranslateApiAsync(text, cancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
```

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

- Windows: `%USERPROFILE%\.wt\plugins\{PackageId}\`

## 注意事項

- プラグインは .NET 10 以上をターゲットにしてください
- `<EnableDynamicLoading>true</EnableDynamicLoading>` を必ず設定してください
- ホスト側で既に提供されているパッケージは `ExcludeAssets="runtime"` を設定し、DLL を重複させないようにしてください
- プラグインに必要な独自の依存 DLL はすべて `lib/net10.0/` フォルダに含めてください
