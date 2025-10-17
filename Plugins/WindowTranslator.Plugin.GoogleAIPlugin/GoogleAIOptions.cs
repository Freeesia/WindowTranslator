using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using GenerativeAI;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAIPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public partial class GoogleAIOptions : IPluginParam
{
    [SelectorStyle(SelectorStyle.ComboBox)]
    public CorrectMode CorrectMode { get; set; }

    public bool WaitCorrect { get; set; }

    [SelectorStyle(SelectorStyle.ComboBox)]
    [JsonConverter(typeof(GoogleAIModelConverter))]
    public GoogleAIModel Model { get; set; } = GoogleAIModel.Gemini20Flash;

    [LocalizedDescription(typeof(Resources), $"{nameof(PreviewModel)}_Desc")]
    public string? PreviewModel { get; set; }

    [DataType(DataType.Password)]
    public string? ApiKey { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? CorrectSample { get; set; }

    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }
}

public enum GoogleAIModel
{
    Gemini20FlashLite,
    Gemini20Flash,
    Gemini25Flash,
    Gemini25Pro,
    Gemini25FlashLite,
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

public static class GoogleAIModelExtensions
{
    public static string GetName(this GoogleAIModel model) => model switch
    {
        GoogleAIModel.Gemini20FlashLite => "models/gemini-2.0-flash-lite",
        GoogleAIModel.Gemini20Flash => GoogleAIModels.Gemini2Flash,
        GoogleAIModel.Gemini25Flash => "models/gemini-2.5-flash",
        GoogleAIModel.Gemini25Pro => "models/gemini-2.5-pro",
        GoogleAIModel.Gemini25FlashLite => "models/gemini-2.5-flash-lite",
        _ => throw new ArgumentOutOfRangeException(nameof(model)),
    };
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

/// <summary>
/// GoogleAIModel用のカスタムコンバーター
/// 古い設定ファイルからの数値デシリアライズと新しい文字列シリアライズをサポートします。
/// </summary>
public class GoogleAIModelConverter : JsonConverter<GoogleAIModel>
{
    public override GoogleAIModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 数値として読み込む（古い設定ファイルの互換性のため）
        if (reader.TokenType == JsonTokenType.Number)
        {
            var numericValue = reader.GetInt32();
            // 古いEnum値をマッピング
            // 0: Gemini15Flash -> Gemini20Flash
            // 1: Gemini15Pro -> Gemini25Pro
            // 2: Gemini20FlashLite (変更なし、ただし新しいインデックスは0)
            // 3: Gemini20Flash (変更なし、ただし新しいインデックスは1)
            // 4: Gemini25Flash (変更なし、ただし新しいインデックスは2)
            // 5: Gemini25Pro (変更なし、ただし新しいインデックスは3)
            // 6: Gemini25FlashLite (変更なし、ただし新しいインデックスは4)
            return numericValue switch
            {
                0 => GoogleAIModel.Gemini20Flash,     // Gemini15Flash -> Gemini20Flash
                1 => GoogleAIModel.Gemini25Pro,       // Gemini15Pro -> Gemini25Pro
                2 => GoogleAIModel.Gemini20FlashLite, // Gemini20FlashLite
                3 => GoogleAIModel.Gemini20Flash,     // Gemini20Flash
                4 => GoogleAIModel.Gemini25Flash,     // Gemini25Flash
                5 => GoogleAIModel.Gemini25Pro,       // Gemini25Pro
                6 => GoogleAIModel.Gemini25FlashLite, // Gemini25FlashLite
                _ => GoogleAIModel.Gemini20Flash,     // デフォルト
            };
        }

        // 文字列として読み込む（新しい設定ファイル）
        if (reader.TokenType == JsonTokenType.String)
        {
            var stringValue = reader.GetString();
            if (Enum.TryParse<GoogleAIModel>(stringValue, out var result))
            {
                return result;
            }
            // 古い名前からの移行をサポート
            return stringValue switch
            {
                "Gemini15Flash" => GoogleAIModel.Gemini20Flash,
                "Gemini15Pro" => GoogleAIModel.Gemini25Pro,
                _ => GoogleAIModel.Gemini20Flash,
            };
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, GoogleAIModel value, JsonSerializerOptions options)
    {
        // 文字列として書き込む（新しい設定ファイル）
        writer.WriteStringValue(value.ToString());
    }
}