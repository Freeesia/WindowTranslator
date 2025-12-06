using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ValueTaskSupplement;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.GoogleAppsSctiptPlugin.Properties;

namespace WindowTranslator.Plugin.GoogleAppsSctiptPlugin;

public sealed class GasTranslator : ITranslateModule, IDisposable
{
    private const string DeployId = "AKfycbxe_E9XjeWckgkkbe9mDoc5GyIQX1CaxFD5bBT6J7Y6JmMrG0U7JaQv-D2Nc0NaXI_APQ";
    private static readonly string[] Scopes = ["https://www.googleapis.com/auth/drive.file", "https://www.googleapis.com/auth/script.scriptapp"];
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly FileDataStore authStore = new(@"StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin");
    private readonly LanguageOptions langOptions;
    private readonly bool isPublicScript;
    private readonly AsyncLazy<UserCredential> credential;
    private readonly HttpClient client;
    private readonly ILogger<GasTranslator> logger;

    public GasTranslator(IOptionsSnapshot<LanguageOptions> langOptions, IOptionsSnapshot<GasOptions> gasOptions, ILogger<GasTranslator> logger)
    {
        this.logger = logger;
        this.langOptions = langOptions.Value;
        this.isPublicScript = string.IsNullOrEmpty(gasOptions.Value.DeployId);
        this.credential = new(GetCredential);
        var deployId = this.isPublicScript ? DeployId : gasOptions.Value.DeployId;
        this.client = new()
        {
            BaseAddress = new($"https://script.google.com/macros/s/{deployId}/exec"),
        };
    }

    public string Name => nameof(GasTranslator);

    public void Dispose()
        => this.client.Dispose();

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        try
        {
            if (this.isPublicScript)
            {
                var credential = await this.credential.AsValueTask().ConfigureAwait(false);
                if (credential.Token.IsStale)
                {
                    await credential.RefreshTokenAsync(CancellationToken.None).ConfigureAwait(false);
                }
                this.client.DefaultRequestHeaders.Authorization = new(credential.Token.TokenType, credential.Token.AccessToken);
            }
            var req = new TranslateRequest([.. srcTexts.Select(t => t.SourceText)], this.langOptions.Source.GetLangCode(), this.langOptions.Target.GetLangCode());
            var res = await this.client.PostAsJsonAsync(string.Empty, req, JsonSerializerOptions).ConfigureAwait(false);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                await this.authStore.ClearAsync().ConfigureAwait(false);
                throw new(Resources.PermissionDenied);
            }
            res.EnsureSuccessStatusCode();
            var translatedTexts = await res.Content.ReadFromJsonAsync<string[]>(JsonSerializerOptions).ConfigureAwait(false);
            return translatedTexts ?? [];
        }
        // Jsonエラーということは指定した以外のレスポンスが返ってきた（上限到達等でHTMLが返る可能性）
        catch (JsonException e)
        {
            this.logger.LogWarning("Google翻訳から予期しないレスポンスが返されました");
            throw new InvalidOperationException(Resources.UnexpectedResponse, e);
        }
        catch (TokenResponseException e) when (e.Error.Error == "access_denied")
        {
            this.logger.LogWarning("Google翻訳の認証に失敗しました");
            throw new AppUserException(Resources.PermissionDenied);
        }
    }

    private record TranslateRequest(string[] Texts, string SourceLanguage, string TargetLanguage);

    private async ValueTask<UserCredential> GetCredential()
        => await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets
            {
                ClientId = "156744888502-89gavii6pir2u26r1mb4lmomrsksmut9.apps.googleusercontent.com",
                ClientSecret = Decrypt("PgYVCapRv9Jd/ReWKysH9bjx6M5oWXQaOW3QuxC/KmPVS9ReIMHnHg2fls/KdQZP"),
            },
            Scopes,
            "user",
            CancellationToken.None,
            authStore
        ).ConfigureAwait(false);

    private static string Decrypt(string cipherTextBase64)
    {
        var password = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "DecryptKey")
            .Value;
        if (string.IsNullOrEmpty(password))
        {
            password = Environment.GetEnvironmentVariable("WindowTranslator_DecryptKey") ?? throw new("DecryptKey is not found.");
        }
        // 暗号文をバイト配列に変換
        byte[] cipherBytes = Convert.FromBase64String(cipherTextBase64);

        // 固定または属性等で管理する salt
        byte[] salt = Encoding.UTF8.GetBytes("wX9&7QjrkK%@");
        using var aes = Aes.Create();
        // PBKDF2 を使って、パスワードから鍵と IV を生成
        aes.Key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 32);
        aes.IV = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA256, 16);
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}

public class GasOptions : IPluginParam
{

    [DataType(DataType.Password)]
    public string DeployId { get; set; } = string.Empty;
}

file static class Extensions
{
    public static string GetLangCode(this string target)
        => target switch
        {
            "pt-BR" or "pt-PT" => target,
            "zh-Hant" => "zh-TW",
            "zh-Hans" => "zh-CN",
            var t => t[..2],
        };
}