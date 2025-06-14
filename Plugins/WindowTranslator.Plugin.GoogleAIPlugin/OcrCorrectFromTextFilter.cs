using System.Globalization;
using System.Text.Json;
using GenerativeAI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAIPlugin;

public sealed class OcrCorrectFromTextFilter(
    IOptionsSnapshot<LanguageOptions> langOptions,
    IOptionsSnapshot<GoogleAIOptions> googleAiOptions,
    ILogger<OcrCorrectFromTextFilter> logger)
    : OcrCorrectFilterBase<string>(langOptions, googleAiOptions, logger)
{
    protected override CorrectMode TargetMode => CorrectMode.Text;

    protected override string GetSystem(LanguageOptions languageOptions, GoogleAIOptions googleAIOptions)
        => $"""
        あなたは{CultureInfo.GetCultureInfo(languageOptions.Source).DisplayName}の専門家です。
        これから渡される文字列はOCRによって認識されたものであり、誤字や脱字が含まれています。
        次の指示に従って、渡された文字列を修正してください。

        1. 誤字や脱字を、文字形の類似性を考慮して修正してください。
        2. 単語や文章の意味が崩れないように、文字数の変更は最小限にしてください。
        3. 正しい単語や文章に修正した結果のみを出力してください。
        4. 誤字や脱字がない場合は、そのままの文字列を出力してください。
        5. 単語や文章として認識できない場合は、空文字を出力してください。

        <誤字修正の例>
        {googleAIOptions.CorrectSample}
        </誤字修正の例>

        修正する文字列は以下のJsonフォーマットになっています。出力文字列も同じJsonフォーマットで、入力文字列の順序を維持してください。
        <入力テキストのJsonフォーマット>
        ["誤字修正するテキスト1","誤字修正するテキスト2"]
        </入力テキストのJsonフォーマット>
        """;

    protected override ValueTask<IReadOnlyList<string>> GetQueueData(IEnumerable<TextRect> targets, FilterContext context)
        => new([.. targets.Select(t => t.Text)]);

    protected override async Task CorrectCore(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        foreach (var text in texts)
        {
            this.Cache.TryAdd(text, null);
        }
        try
        {
            var json = JsonSerializer.Serialize(texts, DefaultSerializerOptions.GenerateObjectJsonOptions);
            var corrected = await this.Client.GenerateObjectAsync<string[]>(json, cancellationToken)
                .ConfigureAwait(false) ?? [];
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < texts.Count; i++)
            {
                this.Cache[texts[i]] = corrected[i];
            }
        }
        catch (Exception e)
        {
            this.Logger.LogError(e, $"Failed to correct `{texts}`");
        }
    }

    protected override void Dropped(IReadOnlyList<string> texts)
        => this.Logger.LogDebug($"Dropped texts: {string.Join(", ", texts)}");
}
