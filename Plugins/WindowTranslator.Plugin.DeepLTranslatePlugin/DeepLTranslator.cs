using DeepL;
using Microsoft.Extensions.Options;
using PropertyTools.DataAnnotations;
using System.Text.Json.Serialization;
using WindowTranslator.Modules;
using WindowTranslator.Stores;
using DisplayNameAttribute = System.ComponentModel.DisplayNameAttribute;

namespace WindowTranslator.Plugin.DeepLTranslatePlugin;

[DisplayName("DeepL")]
public class DeepLTranslator(IProcessInfoStore processInfo, IOptionsSnapshot<DeepLOptions> deeplOptions, IOptionsSnapshot<LanguageOptions> langOptions) : ITranslateModule
{
    private readonly Translator translator = new(deeplOptions.Value.AuthKey);
    private readonly string sourceLang = langOptions.Value.Source[..2];
    private readonly string targetLang = langOptions.Value.Target switch
    {
        "en-US" or "en-GB" or "pt-BR" or "pt-PT" => langOptions.Value.Target,
        var t => t[..2],
    };
    private readonly IProcessInfoStore processInfo = processInfo;
    private readonly TextTranslateOptions translateOptions = new();

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        var translated = await translator.TranslateTextAsync(srcTexts, this.sourceLang, this.targetLang, this.translateOptions);
        return translated.Select(t => t.Text).ToArray();
    }

    public async ValueTask RegisterGlossaryAsync(IReadOnlyDictionary<string, string> glossary)
    {
        var list = await this.translator.ListGlossariesAsync().ConfigureAwait(false);
        if (list.FirstOrDefault(g => g.Name == this.processInfo.Name) is { } exist)
        {
            await this.translator.DeleteGlossaryAsync(exist.GlossaryId).ConfigureAwait(false);
        }
        var created = await this.translator.CreateGlossaryAsync(this.processInfo.Name, this.sourceLang, this.targetLang, new(glossary, true)).ConfigureAwait(false);
        this.translateOptions.GlossaryId = created.GlossaryId;
    }
}

public class DeepLOptions : IPluginParam
{
    public string AuthKey { get; set; } = string.Empty;

    [JsonIgnore]
    [Comment]
    public string Comment { get; } = "Translated by DeepL.(https://www.deepl.com/)";
}
