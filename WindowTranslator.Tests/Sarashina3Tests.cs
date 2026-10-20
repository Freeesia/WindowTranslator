extern alias Sarashina3;

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text;
using System.Text.Json;
using OpenAI;
using OpenAI.Chat;
using Sarashina3Options = Sarashina3::WindowTranslator.Plugin.Sarashina3Plugin.Sarashina3Options;
using Sarashina3Translator = Sarashina3::WindowTranslator.Plugin.Sarashina3Plugin.Sarashina3Translator;

namespace WindowTranslator.Tests;

public class Sarashina3Tests
{
    [Fact]
    public async Task TranslationUsesSarashinaEndpointAndPreservesInputOrder()
    {
        var handler = new RecordingResponseHandler(
            """{"id":"test","object":"chat.completion","created":0,"model":"sarashina3-translate","choices":[{"index":0,"message":{"role":"assistant","content":"  Hello  "},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""",
            """{"id":"test","object":"chat.completion","created":0,"model":"sarashina3-translate","choices":[{"index":0,"message":{"role":"assistant","content":"  Goodbye  "},"finish_reason":"stop"}],"usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}""");
        using var httpClient = new HttpClient(handler);
        var client = new ChatClient(Sarashina3Translator.Model, new ApiKeyCredential("sarashina-test-key"), new OpenAIClientOptions
        {
            Endpoint = new Uri(Sarashina3Translator.Endpoint),
            Transport = new HttpClientPipelineTransport(httpClient),
            RetryPolicy = new ClientRetryPolicy(0),
        });
        var translator = new Sarashina3Translator(new Sarashina3Options
        {
            TranslateContext = "A conversation in a game.",
        }, new LanguageOptions
        {
            Source = "ja-JP",
            Target = "en-US",
        }, client);
        await translator.RegisterGlossaryAsync(new Dictionary<string, string> { ["こんにちは"] = "Hi" });
        translator.RegisterContext("The character is speaking.");

        var translated = await translator.TranslateAsync([
            new TextInfo("こんにちは", "Casual tone."),
            new TextInfo("さようなら", null),
        ]);

        Assert.Equal(["Hello", "Goodbye"], translated);
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal($"{Sarashina3Translator.Endpoint}/chat/completions", request.RequestUri?.AbsoluteUri);
            Assert.Equal("Bearer", request.AuthorizationScheme);
            Assert.Equal("sarashina-test-key", request.AuthorizationParameter);
        });
        using var firstRequest = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(Sarashina3Translator.Model, firstRequest.RootElement.GetProperty("model").GetString());
        var systemPrompt = firstRequest.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("Japanese (Japan)", systemPrompt);
        Assert.Contains("English (United States)", systemPrompt);
        Assert.Contains("A conversation in a game.", systemPrompt);
        Assert.Contains("The character is speaking.", systemPrompt);
        Assert.Contains("こんにちは => Hi", systemPrompt);
    }

    [Fact]
    public void MissingApiKeyIsRejected()
    {
        Assert.Throws<AppUserException>(() => new Sarashina3Translator(
            new Sarashina3Options(),
            new LanguageOptions { Source = "ja-JP", Target = "en-US" }));
    }

    private sealed class RecordingResponseHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> responses = new(responses);

        public List<RequestInfo> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await Assert.IsAssignableFrom<HttpContent>(request.Content).ReadAsStringAsync(cancellationToken);
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
