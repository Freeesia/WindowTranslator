using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Duende.IdentityModel.Client;
using WindowTranslator.Plugin.OrcaRouterPlugin.Properties;

namespace WindowTranslator.Plugin.OrcaRouterPlugin;

internal static class OrcaRouterAuthentication
{
    private const string Authority = "https://www.orcarouter.ai";
    private const string Referral = "ref_a519c896910b1a7646fd";

    public static async Task<string> SignInAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        return await SignInAsync(client, uri => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }), cancellationToken);
    }

    internal static async Task<string> SignInAsync(HttpClient client, Action<Uri> openBrowser, CancellationToken cancellationToken)
    {
        var endpoints = await DiscoverAsync(client, cancellationToken);
        var verifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        using var listener = CreateListener();
        var callback = listener.Prefixes.Single();
        var parameters = new Parameters();
        parameters.Add("callback_url", callback);
        parameters.Add("code_challenge", CreateChallenge(verifier));
        parameters.Add("code_challenge_method", "S256");
        parameters.Add("state", state);
        parameters.Add("app_name", "WindowTranslator");
        parameters.Add("scope", "api");
        parameters.Add("ref", Referral);
        var url = new RequestUrl(endpoints.Authorization.AbsoluteUri).Create(parameters);
        cancellationToken.ThrowIfCancellationRequested();
        openBrowser(new Uri(url));
        var code = await ReceiveCodeAsync(listener, state, cancellationToken);
        using var response = await client.PostAsJsonAsync(endpoints.Token, new { code, code_verifier = verifier }, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("key", out var value) || value.ValueKind != JsonValueKind.String
            || value.GetString() is not { Length: > 8 } key || !key.StartsWith("sk-orca-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Invalid OrcaRouter key response.");
        }
        return key;
    }

    internal static string CreateChallenge(string verifier)
        => Base64Url.EncodeToString(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    internal static async Task<(Uri Authorization, Uri Token)> DiscoverAsync(HttpClient client, CancellationToken cancellationToken)
    {
        var discovery = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
        {
            Address = Authority,
            Policy = new DiscoveryPolicy
            {
                RequireKeySet = false, // API キーを交換する OAuth フローであり、ID Token は使用しない。
                // 現在の Discovery はリバースプロキシ内の http URL を返す。
                // 以下で正規ホスト・ポートを検証し、HTTPS の URL のみを使用する。
                ValidateIssuerName = false,
                ValidateEndpoints = false,
            },
        }, cancellationToken);
        if (discovery.IsError || NormalizeEndpoint(discovery.Issuer).AbsoluteUri.TrimEnd('/') != Authority
            || !discovery.CodeChallengeMethodsSupported.Contains("S256"))
        {
            throw new InvalidOperationException("Invalid OrcaRouter discovery response.");
        }
        return (NormalizeEndpoint(discovery.AuthorizeEndpoint), NormalizeEndpoint(discovery.TokenEndpoint));
    }

    internal static Uri NormalizeEndpoint(string? endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || uri.Host != "www.orcarouter.ai"
            || !uri.IsDefaultPort || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.Query.Length != 0)
        {
            throw new InvalidOperationException("Unexpected OrcaRouter endpoint.");
        }
        return new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri;
    }

    private static HttpListener CreateListener()
    {
        for (var attempt = 0; ; attempt++)
        {
            // OS に空きポートを選ばせる。HttpListener へ引き渡す間に占有されたら再試行する。
            var socket = new TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            var port = ((IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/callback/");
            try
            {
                listener.Start();
                return listener;
            }
            catch (HttpListenerException) when (attempt < 4)
            {
                listener.Close();
            }
            catch
            {
                listener.Close();
                throw;
            }
        }
    }

    private static async Task<string> ReceiveCodeAsync(HttpListener listener, string state, CancellationToken cancellationToken)
    {
        while (true)
        {
            var context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            using var response = context.Response;
            var query = context.Request.QueryString;
            if (context.Request.HttpMethod != "GET" || context.Request.Url?.AbsolutePath != "/callback/"
                || query.GetValues("state") is not { Length: 1 } values || values[0] != state)
            {
                response.StatusCode = (int)HttpStatusCode.BadRequest;
                continue;
            }
            var code = query.GetValues("code");
            var error = query.GetValues("error");
            response.ContentType = "text/plain; charset=utf-8";
            response.Headers["Cache-Control"] = "no-store";
            var body = Encoding.UTF8.GetBytes(Resources.Text("ReturnToApp"));
            response.ContentLength64 = body.Length;
            try
            {
                await response.OutputStream.WriteAsync(body, cancellationToken);
            }
            catch (HttpListenerException)
            {
                // ブラウザーが接続を閉じても、受け取ったコードは処理する。
            }
            if (error is ["access_denied"])
            {
                throw new OperationCanceledException();
            }
            if (error is not null || code is not { Length: 1 } || string.IsNullOrWhiteSpace(code[0]))
            {
                throw new InvalidOperationException("Invalid OrcaRouter callback.");
            }
            return code[0];
        }
    }
}
