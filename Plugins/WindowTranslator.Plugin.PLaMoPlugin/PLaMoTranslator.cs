using System.Globalization;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

public sealed class PLaMoTranslator : ITranslateModule, IDisposable
{
    private readonly string sourceLang;
    private readonly string targetLang;
    private readonly LLamaWeights? weights;
    private readonly ModelParams? modelParams;
    private readonly ILogger<PLaMoTranslator> logger;
    private readonly InferenceParams inferenceParams;

    static PLaMoTranslator()
        => NativeLibraryConfig.LLama.WithSelectingPolicy(LLamaSharpNativeLibrarySelectingPolicy.Instance);

    public PLaMoTranslator(IOptionsSnapshot<PLaMoOptions> plamoOptions, IOptionsSnapshot<LanguageOptions> langOptions, ILogger<PLaMoTranslator> logger)
    {
        var options = plamoOptions.Value;

        // PLaMoモデル用の言語名を取得
        this.sourceLang = GetLanguageName(langOptions.Value.Source);
        this.targetLang = GetLanguageName(langOptions.Value.Target);

        // ダウンロードされたモデルのパスを取得
        var modelPath = PLaMoOptions.ModelPath;

        if (!File.Exists(modelPath))
        {
            throw new AppUserException(Resources.ModelFileNotFound);
        }

        if (!NativeLibraryConfig.LLama.LibraryHasLoaded)
        {
            NativeLibraryConfig.LLama.WithLogCallback(logger);
        }

        this.modelParams = new ModelParams(modelPath)
        {
            ContextSize = (uint)options.ContextSize,
        };
        this.inferenceParams = new InferenceParams
        {
            MaxTokens = 1024,
            AntiPrompts = ["<|plamo:op|>"],
        };

        this.weights = LLamaWeights.LoadFromFile(this.modelParams);
        this.logger = logger;
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



        using var context = this.weights.CreateContext(this.modelParams, this.logger);
        var executor = new StatelessExecutor(this.weights, this.modelParams);
        var responseBuilder = new StringBuilder();
        var responses = new List<string>();

        foreach (var text in srcTexts)
        {
            // PLaMo専用のプロンプトフォーマット
            var prompt = $"""
                <|plamo:op|>dataset
                translation
                <|plamo:op|>input lang={this.sourceLang}
                {text.SourceText}
                <|plamo:op|>output lang={this.targetLang}

                """.ReplaceLineEndings("\n");
            responseBuilder.Clear();
            await foreach (var token in executor.InferAsync(prompt, this.inferenceParams))
            {
                responseBuilder.Append(token);
            }
            var response = responseBuilder.ToString().Trim();
            responses.Add(response);
            this.logger.LogDebug("PLaMo translated: {Original} => {Translated}", text.SourceText, response);
        }

        return [.. responses];
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
