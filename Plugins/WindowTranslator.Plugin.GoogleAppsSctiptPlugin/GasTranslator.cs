using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Options;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ValueTaskSupplement;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAppsSctiptPlugin;

[DisplayName("Google Apps Script")]
public sealed class GasTranslator : ITranslateModule, IDisposable
{
    private const string DeployId = "AKfycbxe_E9XjeWckgkkbe9mDoc5GyIQX1CaxFD5bBT6J7Y6JmMrG0U7JaQv-D2Nc0NaXI_APQ";
    private static readonly string[] Scopes = ["https://www.googleapis.com/auth/drive.file", "https://www.googleapis.com/auth/script.scriptapp"];
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly LanguageOptions langOptions;
    private readonly bool isPublicScript;
    private readonly AsyncLazy<UserCredential> credential;
    private readonly HttpClient client;

    public GasTranslator(IOptions<LanguageOptions> langOptions, IOptions<GasOptions> gasOptions)
    {
        this.langOptions = langOptions.Value;
        this.isPublicScript = string.IsNullOrEmpty(gasOptions.Value.DeployId);
        this.credential = new(GetCredential);
        var deployId = this.isPublicScript ? DeployId : gasOptions.Value.DeployId;
        this.client = new()
        {
            BaseAddress = new($"https://script.google.com/macros/s/{deployId}/exec"),
        };
    }

    public void Dispose()
        => this.client.Dispose();

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
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
                ClientId = "156744888502-89gavii6pir2u26r1mb4lmomrsksmut9.apps.googleusercontent.com",
                ClientSecret = Decrypt("PgYVCapRv9Jd/ReWKysH9bjx6M5oWXQaOW3QuxC/KmPVS9ReIMHnHg2fls/KdQZP"),
            },
            Scopes,
            "user",
            CancellationToken.None,
            new FileDataStore(@"StudioFreesia\WindowTranslator\GoogleAppsScriptPlugin")
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

        // PBKDF2 を使って、パスワードから鍵と IV を生成
        using var pdb = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
        byte[] key = pdb.GetBytes(32); // 256 ビット（AES-256 用）
        byte[] iv = pdb.GetBytes(16);  // 128 ビット（IV のサイズ）

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(cipherBytes);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var reader = new StreamReader(cs);
        return reader.ReadToEnd();
    }
}

[DisplayName("Google Apps Script")]
public class GasOptions : IPluginParam
{

    [DataType(DataType.Password)]
    [DisplayName("スクリプトのデプロイID")]
    public string DeployId { get; set; } = string.Empty;
}
