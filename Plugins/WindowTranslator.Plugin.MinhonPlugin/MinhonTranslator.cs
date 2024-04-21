using IdentityModel.Client;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.Threading;
using minhon;
using Refit;
using System.ComponentModel;
using System.Threading;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.MinhonPlugin;

[DisplayName("みんなの自動翻訳")]
public class MinhonTranslator : ITranslateModule
{
    private readonly string apiKey;
    private readonly string apiSecret;
    private readonly string name;
    private readonly string sourceLang;
    private readonly string targetLang;
    private readonly IHttpClientFactory clientFactory;
    private readonly AsyncLazy<string> token;
    private readonly IMachineTranslationClient client = RestService.For<IMachineTranslationClient>("https://mt-auto-minhon-mlt.ucri.jgn-x.jp");
    private readonly AsyncSemaphore barrier = new(1);

    public MinhonTranslator(IOptionsSnapshot<MinhonOptions> minhonOptions, IOptionsSnapshot<LanguageOptions> langOptions, IHttpClientFactory clientFactory)
    {
        apiKey = minhonOptions.Value.ApiKey;
        apiSecret = minhonOptions.Value.ApiSecret;
        name = minhonOptions.Value.Name;
        sourceLang = langOptions.Value.Source switch
        {
            "zh-CN" or "zh-TW" => langOptions.Value.Source,
            var t => t[..2],
        };
        targetLang = langOptions.Value.Target switch
        {
            "zh-CN" or "zh-TW" => langOptions.Value.Target,
            var t => t[..2],
        };
        this.clientFactory = clientFactory;
        this.token = new(GetTokenAsync, null);
    }

    public async ValueTask<string[]> TranslateAsync(string[] srcTexts)
    {
        using var r = await barrier.EnterAsync().ConfigureAwait(false);
        var token = await this.token.GetValueAsync().ConfigureAwait(false);
        var response = await client.Translate(
            TranslateMode.TransLM,
            sourceLang,
            targetLang,
            new(apiKey, name, token, string.Join(Environment.NewLine, srcTexts)))
            .ConfigureAwait(false);
        return response
            .ResultSet
            .Result?
            .Information
            .Sentences
            .Select(r => r.TargetText)
            .ToArray()
            ?? throw new InvalidOperationException();
    }

    private async Task<string> GetTokenAsync()
    {
        using var httpClient = clientFactory.CreateClient();
        var response = await httpClient.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
        {
            Address = "https://mt-auto-minhon-mlt.ucri.jgn-x.jp/oauth2/token.php",
            ClientId = apiKey,
            ClientSecret = apiSecret,
        });
        return response.AccessToken ?? throw new InvalidOperationException();

    }
}

public class MinhonOptions : IPluginParam
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
}
