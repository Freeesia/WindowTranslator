using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAIPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public sealed record GoogleAIModelItem(string Value, string DisplayName);

public partial class GoogleAIOptions : ObservableObject, IPluginParam
{
    private readonly GoogleAIModelCatalog modelCatalog = new();
    private IReadOnlyList<GoogleAIModelItem> modelItems = GoogleAIModelCatalog.DefaultModelItems;

    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    public bool WaitCorrect { get; set; }

    [property: SelectorStyle(SelectorStyle.ComboBox)]
    [property: ItemsSourceProperty(nameof(ModelItems))]
    [property: DisplayMemberPath(nameof(GoogleAIModelItem.DisplayName))]
    [property: SelectedValuePath(nameof(GoogleAIModelItem.Value))]
    [ObservableProperty]
    private string model = "gemini-3.8-flash";

    [System.ComponentModel.Browsable(false)]
    [JsonIgnore]
    public IReadOnlyList<GoogleAIModelItem> ModelItems
    {
        get => this.modelItems;
        private set => SetProperty(ref this.modelItems, value);
    }

    [property: DataType(DataType.Password)]
    [ObservableProperty]
    private string? apiKey;

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }

    partial void OnApiKeyChanged(string? value)
        => _ = RefreshModelItemsAsync();

    partial void OnModelChanged(string value)
    {
        var migratedModel = GoogleAIModelCatalog.MigrateLegacyModel(value);
        if (!string.Equals(migratedModel, value, StringComparison.Ordinal))
        {
            this.Model = migratedModel;
            return;
        }

        this.ModelItems = this.ModelItems.EnsureSelectedModel(value);
    }

    private async Task RefreshModelItemsAsync()
    {
        var items = await this.modelCatalog.RefreshAsync(this.ApiKey, this.Model, CancellationToken.None);
        if (items is not null)
        {
            this.ModelItems = items.EnsureSelectedModel(this.Model);
        }
    }
}

public enum CorrectMode
{
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(None)}")]
    None,
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(Text)}")]
    Text,
    [LocalizedDescription(typeof(Resources), $"{nameof(CorrectMode)}_{nameof(Image)}")]
    Image,
}

public class GoogleAIValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var op = settings.PluginParams.GetValueOrDefault(nameof(GoogleAIOptions)) as GoogleAIOptions;
        // APIキーが設定されている場合は有効
        if (!string.IsNullOrEmpty(op?.ApiKey))
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        // 翻訳モジュールでも補正も利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(GoogleAITranslator) && (op?.CorrectMode ?? CorrectMode.None) == CorrectMode.None)
        {
            return ValueTask.FromResult(ValidateResult.Valid);
        }

        return ValueTask.FromResult(ValidateResult.Invalid("Gemini", """
            翻訳モジュールに「Gemini翻訳」が選択もしくは認識補正が有効化されています。

            Geminiの利用にはAPIキーが必要です。
            「対象ごとの設定」→「Gemini設定」タブのAPIキーを設定してください。

            APIキーはGeminiの[APIキーページ](https://aistudio.google.com/app/apikey)から取得できます。
            """));
    }
}

internal sealed class GoogleAIModelCatalog
{
    private int refreshVersion;

    public static IReadOnlyList<GoogleAIModelItem> DefaultModelItems { get; } =
    [
        // 翻訳とOCRで使う通常のコンテンツ生成モデル
        new("gemini-3.8-flash", "Gemini 3.8 Flash"),
        new("gemini-3.7-flash", "Gemini 3.7 Flash"),
        new("gemini-3.6-flash", "Gemini 3.6 Flash"),
        new("gemini-3.5-flash", "Gemini 3.5 Flash"),
        new("gemini-3.5-flash-lite", "Gemini 3.5 Flash Lite"),
        new("gemini-3.1-flash-lite", "Gemini 3.1 Flash Lite"),
        new("gemini-3.1-pro-preview", "Gemini 3.1 Pro"),
        new("gemini-3-flash-preview", "Gemini 3 Flash"),

        // Gemini 2.5モデル（提供継続中）
        new("gemini-2.5-flash", "Gemini 2.5 Flash"),
        new("gemini-2.5-flash-lite", "Gemini 2.5 Flash Lite"),
        new("gemini-2.5-pro", "Gemini 2.5 Pro"),
    ];

    public static string MigrateLegacyModel(string value)
        => value switch
        {
            // 以前の列挙型で保存されていた設定をモデルIDへ移行する。
            "Gemini15Flash" => "gemini-2.5-flash-lite",
            "Gemini15Pro" => "gemini-2.5-pro",
            "Gemini20FlashLite" => "gemini-3.5-flash-lite",
            "Gemini20Flash" => "gemini-3.8-flash",
            "Gemini25Flash" => "gemini-2.5-flash",
            "Gemini25Pro" => "gemini-2.5-pro",
            "Gemini25FlashLite" => "gemini-2.5-flash-lite",
            "0" => "gemini-2.5-flash-lite",
            "1" => "gemini-2.5-pro",
            "2" => "gemini-3.5-flash-lite",
            "3" => "gemini-3.8-flash",
            "4" => "gemini-2.5-flash",
            "5" => "gemini-2.5-pro",
            "6" => "gemini-2.5-flash-lite",
            _ => value,
        };

    public async Task<IReadOnlyList<GoogleAIModelItem>?> RefreshAsync(string? apiKey, string selectedModel, CancellationToken cancellationToken)
    {
        var currentRefreshVersion = Interlocked.Increment(ref this.refreshVersion);
        try
        {
            var items = new List<GoogleAIModelItem>(DefaultModelItems);
            if (!string.IsNullOrEmpty(apiKey))
            {
                var googleAI = new GenerativeAI.GoogleAi(apiKey);
                if (await googleAI.ListModelsAsync(cancellationToken: cancellationToken).ConfigureAwait(false) is { Models: { } models })
                {
                    var knownValues = new HashSet<string>(DefaultModelItems.Select(item => item.Value), StringComparer.Ordinal);
                    foreach (var model in models.Where(model => model.SupportedGenerationMethods?.Contains("generateContent") == true))
                    {
                        var name = model.Name.StartsWith("models/", StringComparison.Ordinal)
                            ? model.Name["models/".Length..]
                            : model.Name;
                        if (knownValues.Add(name))
                        {
                            items.Add(new(name, model.DisplayName ?? name));
                        }
                    }
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (currentRefreshVersion != Volatile.Read(ref this.refreshVersion))
            {
                return null;
            }

            return items.EnsureSelectedModel(selectedModel)
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
        }
        catch (Exception)
        {
            // 候補取得に失敗した場合は、呼び出し元の候補と選択値を維持する。
            System.Diagnostics.Trace.WriteLine("Failed to refresh Google AI models.");
            return null;
        }
    }
}

file static class Extensions
{
    public static IReadOnlyList<GoogleAIModelItem> EnsureSelectedModel(this IReadOnlyList<GoogleAIModelItem> items, string? selectedModel)
    {
        if (string.IsNullOrEmpty(selectedModel) || items.Any(item => string.Equals(item.Value, selectedModel, StringComparison.Ordinal)))
        {
            return items;
        }

        return [.. items, new(selectedModel, selectedModel)];
    }
}