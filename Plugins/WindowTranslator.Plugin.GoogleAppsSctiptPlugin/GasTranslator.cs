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
    private const string DeployId = "AKfycbyyy3RHgU6oRiiBKcKbxi3dOQWvSOzanhUuK_S5uy37YHkXy-53i_T8cvPKtYfj9BECBw";
    private static readonly string[] Scopes = [
        "https://www.googleapis.com/auth/script.scriptapp",
    ];
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
            BaseAddress = new($"https://script.googleapis.com/v1/scripts/{deployId}:run"),
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
            var sourceTexts = srcTexts.Select(t => t.SourceText).ToArray();
            var req = new ScriptRunRequest("translate", [this.langOptions.Source.GetLangCode(), this.langOptions.Target.GetLangCode(), sourceTexts]);
            var res = await this.client.PostAsJsonAsync(string.Empty, req, JsonSerializerOptions).ConfigureAwait(false);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
            {
                await this.authStore.ClearAsync().ConfigureAwait(false);
                throw new(Resources.PermissionDenied);
            }
            res.EnsureSuccessStatusCode();
            var scriptResponse = await res.Content.ReadFromJsonAsync<ScriptRunResponse>(JsonSerializerOptions).ConfigureAwait(false);
            if (scriptResponse?.Error is { } error)
            {
                this.logger.LogWarning("Google翻訳のスクリプト実行エラー: {Message}", error.Message);
                throw new InvalidOperationException(Resources.UnexpectedResponse);
            }
            return scriptResponse?.Response?.Result ?? [];
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

    private record ScriptRunRequest(string Function, object[] Parameters);
    private record ScriptRunResponse(bool Done, ScriptExecutionResponse? Response, ScriptRunError? Error);
    private record ScriptExecutionResponse(string[]? Result);
    private record ScriptRunError(int Code, string Message, string Status);

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
        Span<byte> buf = stackalloc byte[48];
        Rfc2898DeriveBytes.Pbkdf2(password, salt.AsSpan(), buf, 10000, HashAlgorithmName.SHA256);
        aes.Key = buf[..32].ToArray();
        aes.IV = buf[32..].ToArray();
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