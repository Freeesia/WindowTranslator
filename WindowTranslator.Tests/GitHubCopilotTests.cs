extern alias CopilotPlugin;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GitHub.Copilot;
using CopilotOptions = CopilotPlugin::WindowTranslator.Plugin.GitHubCopilotPlugin.GitHubCopilotOptions;
using CopilotUtility = CopilotPlugin::WindowTranslator.Plugin.GitHubCopilotPlugin.Utility;
using CopilotValidator = CopilotPlugin::WindowTranslator.Plugin.GitHubCopilotPlugin.GitHubCopilotValidator;

namespace WindowTranslator.Tests;

public class GitHubCopilotTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthenticationCheckUsesRuntimeStatus(bool authenticated)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var methods = new List<string>();
        var server = RespondAsync();
        await using (var client = new CopilotClient(new()
        {
            Connection = RuntimeConnection.ForUri($"127.0.0.1:{port}"),
        }))
        {
            if (authenticated)
            {
                await CopilotUtility.EnsureAuthenticatedAsync(client);
            }
            else
            {
                var error = await Assert.ThrowsAsync<AppUserException>(() => CopilotUtility.EnsureAuthenticatedAsync(client));
                Assert.Contains("GitHub", error.Message);
                Assert.Equal(CopilotUtility.AuthenticationRequiredMessage, error.Message);
            }
        }
        await server;
        Assert.Equal(["connect", "auth.getStatus"], methods);

        async Task RespondAsync()
        {
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            while (await ReadMessageAsync(stream, timeout.Token) is { } request)
            {
                using (request)
                {
                    var method = request.RootElement.GetProperty("method").GetString()!;
                    methods.Add(method);
                    object result = method switch
                    {
                        "connect" => new { protocolVersion = 3 },
                        "auth.getStatus" => new { isAuthenticated = authenticated },
                        _ => throw new InvalidOperationException($"Unexpected method: {method}"),
                    };
                    var body = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        jsonrpc = "2.0",
                        id = request.RootElement.GetProperty("id").GetInt64(),
                        result,
                    });
                    await stream.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {body.Length}\r\n\r\n"), timeout.Token);
                    await stream.WriteAsync(body, timeout.Token);
                }
            }
        }
    }

    [Fact]
    public async Task UnselectedPluginDoesNotRequireAuthentication()
        => Assert.True((await new CopilotValidator().Validate(new TargetSettings())).IsValid);

    [Fact]
    public void LoginCommandIsAvailableWithoutBeingSavedInSettings()
    {
        var options = new CopilotOptions();
        Assert.True(options.LoginCommand.CanExecute(null));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options));
        Assert.False(json.RootElement.TryGetProperty(nameof(CopilotOptions.LoginCommand), out _));
        Assert.Equal(options.Model, json.RootElement.GetProperty(nameof(CopilotOptions.Model)).GetString());
    }

    private static async Task<JsonDocument?> ReadMessageAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new StringBuilder();
        var buffer = new byte[1];
        while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            if (await stream.ReadAsync(buffer, cancellationToken) == 0)
            {
                return null;
            }
            header.Append((char)buffer[0]);
        }
        var length = int.Parse(header.ToString().Split(':')[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return JsonDocument.Parse(body);
    }
}
