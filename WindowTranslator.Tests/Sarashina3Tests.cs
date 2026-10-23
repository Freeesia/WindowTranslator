extern alias Sarashina3;

using System.Net;
using System.Text;
using System.Text.Json;
using Sarashina3Options = Sarashina3::WindowTranslator.Plugin.Sarashina3Plugin.Sarashina3Options;
using Sarashina3Translator = Sarashina3::WindowTranslator.Plugin.Sarashina3Plugin.Sarashina3Translator;

namespace WindowTranslator.Tests;

public class Sarashina3Tests
{
    [Fact]
    public async Task TranslationUsesDedicatedTranslateApi()
    {
        var handler = new RecordingResponseHandler(
            """{"translated_text":"Hello"}""",
            """{"translated_text":"Goodbye"}""");
        using var httpClient = new HttpClient(handler);
        var translator = new Sarashina3Translator(new Sarashina3Options
        {
            ApiKey = "sarashina-test-key",
        }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, httpClient);

        var translated = await translator.TranslateAsync([
            new TextInfo("こんにちは", null),
            new TextInfo("さようなら", null),
        ]);

        Assert.Equal(["Hello", "Goodbye"], translated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Sarashina3Translator.Endpoint, request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("sarashina-test-key", request.AuthorizationParameter);
        });

        using var firstRequest = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(
            new[] { "text", "source_lang", "target_lang" },
            firstRequest.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("こんにちは", firstRequest.RootElement.GetProperty("text").GetString());
        Assert.Equal("ja", firstRequest.RootElement.GetProperty("source_lang").GetString());
        Assert.Equal("en", firstRequest.RootElement.GetProperty("target_lang").GetString());
    }

    [Fact]
    public async Task SameLanguageReturnsOriginalTextWithoutCallingApi()
    {
        var handler = new RecordingResponseHandler();
        using var httpClient = new HttpClient(handler);
        var translator = new Sarashina3Translator(new Sarashina3Options
        {
            ApiKey = "sarashina-test-key",
        }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "ja",
        }, httpClient);

        Assert.Equal(["そのまま"], await translator.TranslateAsync([new TextInfo("そのまま", null)]));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void MissingApiKeyIsRejected()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<AppUserException>(() => new Sarashina3Translator(
            new Sarashina3Options(),
            new LanguageOptions { Source = "ja-JP", Target = "en-US" },
            httpClient));
    }

    [Fact]
    public void UnsupportedLanguageIsRejected()
    {
        using var httpClient = new HttpClient();
        Assert.Throws<AppUserException>(() => new Sarashina3Translator(
            new Sarashina3Options { ApiKey = "sarashina-test-key" },
            new LanguageOptions { Source = "ja-JP", Target = "zh-CN" },
            httpClient));
    }

    private sealed class RecordingResponseHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        public List<RequestInfo> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            this.Requests.Add(new(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(this.responses.Dequeue(), Encoding.UTF8, "application/json"),
                RequestMessage = request,
            };
        }
    }

    private sealed record RequestInfo(HttpMethod Method, Uri? RequestUri, string? AuthorizationScheme, string? AuthorizationParameter, string Body);
}
