using Google.Apis.Auth.OAuth2;
using Google.Apis.Script.v1;
using Google.Apis.Script.v1.Data;
using Google.Apis.Services;
using System.Text.Json;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.GoogleAppsSctiptPlugin;

public class GasTranslator : ITranslateModule
{
    private static readonly string[] Scopes = ["https://www.googleapis.com/auth/script.projects"];
    private static readonly string ScriptId = "AKfycbymXadPzkNnpd92ovwULR6gult0W9Vh94ZUgT5-2Ol87He78rWstxknlsAjVOgWRsCtPw";
    private readonly ScriptService scriptService;

    public GasTranslator()
    {
        var credential = GoogleCredential.GetApplicationDefault().CreateScoped(Scopes);
        scriptService = new ScriptService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WindowTranslator",
        });
    }

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
    {
        var request = new ExecutionRequest
        {
            Function = "doPost",
            Parameters =
            [
                new
                {
                    texts = srcTexts.Select(t => t.Text).ToArray(),
                    sourceLanguage = "en",
                    targetLanguage = "ja"
                }
            ]
        };

        var scriptRequest = scriptService.Scripts.Run(request, ScriptId);
        var response = await scriptRequest.ExecuteAsync();

        if (response.Error is {} error)
        {
            throw new Exception($"Script error: {error.Message}");
        }

        var translatedTexts = JsonSerializer.Deserialize<string[]>(response.Response["result"].ToString());
        return translatedTexts ?? [];
    }
}
