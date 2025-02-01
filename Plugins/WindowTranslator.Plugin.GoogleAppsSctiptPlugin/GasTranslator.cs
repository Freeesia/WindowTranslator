using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;
using ValueTaskSupplement;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAppsSctiptPlugin;

public sealed class GasTranslator : ITranslateModule, IDisposable
{
    private static readonly string[] Scopes = ["https://www.googleapis.com/auth/drive.file", "https://www.googleapis.com/auth/script.scriptapp"];
    private static readonly string DeployId = "AKfycbxe_E9XjeWckgkkbe9mDoc5GyIQX1CaxFD5bBT6J7Y6JmMrG0U7JaQv-D2Nc0NaXI_APQ";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly AsyncLazy<UserCredential> credential;
    private readonly LanguageOptions langOptions;
    private readonly HttpClient client = new()
    {
        BaseAddress = new($"https://script.google.com/macros/s/{DeployId}/exec"),
    };

    public GasTranslator(IOptions<LanguageOptions> langOptions)
    {
        this.langOptions = langOptions.Value;
        this.credential = new(GetCredential);
    }

    public void Dispose()
        => this.client.Dispose();

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var credential = await this.credential.AsValueTask().ConfigureAwait(false);
        if (credential.Token.IsStale)
        {
            await credential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false);
        }
        this.client.DefaultRequestHeaders.Authorization = new(credential.Token.TokenType, credential.Token.AccessToken);
        var req = new TranslateRequest([.. srcTexts.Select(t => t.Text)], this.langOptions.Source, this.langOptions.Target);
        var res = await this.client.PostAsJsonAsync(string.Empty, req, JsonSerializerOptions).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();
        var translatedTexts = await res.Content.ReadFromJsonAsync<string[]>(JsonSerializerOptions).ConfigureAwait(false);
        return translatedTexts ?? [];
    }

    private record TranslateRequest(string[] Texts, string SourceLanguage, string TargetLanguage);

    private async ValueTask<UserCredential> GetCredential()
        => await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets
            {
            },
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(@"StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin")
        ).ConfigureAwait(false);
}
