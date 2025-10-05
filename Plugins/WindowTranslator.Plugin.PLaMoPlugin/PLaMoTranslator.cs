using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

[DisplayName("PLaMo")]
public sealed class PLaMoTranslator : ITranslateModule, IDisposable
{
    private readonly string sourceLang;
    private readonly string targetLang;
    private readonly LLamaWeights? weights;
    private readonly ModelParams? modelParams;

    public PLaMoTranslator(IOptionsSnapshot<PLaMoOptions> plamoOptions, IOptionsSnapshot<LanguageOptions> langOptions)
    {
        var options = plamoOptions.Value;
        
        // PLaMoモデル用の言語名を取得
        this.sourceLang = GetLanguageName(langOptions.Value.Source);
        this.targetLang = GetLanguageName(langOptions.Value.Target);

        if (string.IsNullOrEmpty(options.ModelPath))
        {
            throw new AppUserException(Resources.ModelPathNotSet);
        }

        if (!File.Exists(options.ModelPath))
        {
            throw new AppUserException(Resources.ModelFileNotFound);
        }

        this.modelParams = new ModelParams(options.ModelPath);

        this.weights = LLamaWeights.LoadFromFile(this.modelParams);
    }

    private static string GetLanguageName(string cultureCode)
    {
        // PLaMoモデルが認識する言語名に変換
        var culture = CultureInfo.GetCultureInfo(cultureCode);
        return culture.TwoLetterISOLanguageName switch
        {
            "ja" => "Japanese",
            "en" => "English",
            "zh" => "Chinese",
            "ko" => "Korean",
            "de" => "German",
            "fr" => "French",
            "es" => "Spanish",
            "it" => "Italian",
            "pt" => "Portuguese",
            "ru" => "Russian",
            "ar" => "Arabic",
            "vi" => "Vietnamese",
            _ => culture.EnglishName.Split(' ')[0] // フォールバック: 英語名の最初の単語
        };
    }

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        if (this.weights is null || this.modelParams is null)
        {
            throw new InvalidOperationException(Resources.ModelNotInitialized);
        }

        // JSON形式で入力テキストをまとめる
        var inputJson = JsonSerializer.Serialize(srcTexts.Select(s => s.SourceText).ToArray());

        // PLaMo専用のプロンプトフォーマット
        var prompt = $"""
            <|plamo:op|>dataset
            translation
            <|plamo:op|>input lang={this.sourceLang}
            {inputJson}
            <|plamo:op|>output lang={this.targetLang}

            """;

        using var context = this.weights.CreateContext(this.modelParams);
        var executor = new StatelessExecutor(this.weights, this.modelParams);
        
        var inferenceParams = new InferenceParams
        {
            MaxTokens = 1024,
            AntiPrompts = ["<|plamo:op|>"],
        };

        var responseBuilder = new StringBuilder();
        
        await foreach (var token in executor.InferAsync(prompt, inferenceParams))
        {
            responseBuilder.Append(token);
        }

        var response = responseBuilder.ToString().Trim();
        
        try
        {
            // レスポンスをJSON配列としてパース
            var result = JsonSerializer.Deserialize<string[]>(response);
            return result ?? [];
        }
        catch (JsonException)
        {
            // JSONパースに失敗した場合は空配列を返す
            return [];
        }
    }

    // PLaMoモデルは用語集をサポートしないため、何もしない
    public ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
        => default;

    // PLaMoモデルはコンテキストをサポートしないため、何もしない
    public void RegisterContext(string context)
    {
    }

    public void Dispose()
    {
        this.weights?.Dispose();
    }
}
