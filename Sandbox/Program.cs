using System.Reflection;
using System.Text.Json;
using ConsoleAppFramework;
using DeepL;
using GenerativeAI;
using GenerativeAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

var services = new ServiceCollection();
services.Configure<Secret>(configuration.GetSection("Secret"));
services.Configure<LanguageOptions>(op => { });
services.Configure<BasicOcrParam>(op => { });
using var serviceProvider = services.BuildServiceProvider();
ConsoleApp.ServiceProvider = serviceProvider;

var app = ConsoleApp.Create();
app.Add("DeepL", DeepLTest);
app.Add("GoogleAI", GoogleAITranslateTest);
app.Add("ClipTextRect", ClipTextRect);
app.Run(args);

static async Task DeepLTest([FromServices] IOptions<Secret> secret)
{
    var translator = new Translator(secret.Value.DeepLAuthKey);
    var results = await translator.TranslateTextAsync(
        ["Bath? Me too."],
        "en",
        "ja",
        new()
        {
            Context = """
            This line is said by the male character.
            Like his Uncle Landen, he is a woodworker and runs the Carpenter's Shop in The Eastern Road.
            He has a thoughtful and calm personality.
            His first person is "ボク".
            """
        }).ConfigureAwait(false);
    Console.WriteLine(results[0].Text);
}

static async Task GoogleAITranslateTest([FromServices] IOptions<Secret> secret)
{
    var googleAI = new GoogleAi(secret.Value.GoogleAIAuthKey);
    var client = googleAI.CreateGenerativeModel(
            GoogleAIModels.Gemini2Flash,
            safetyRatings: [
                new(){ Category = HarmCategory.HARM_CATEGORY_HARASSMENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_HATE_SPEECH, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_SEXUALLY_EXPLICIT, Threshold =HarmBlockThreshold.BLOCK_NONE},
                new(){ Category = HarmCategory.HARM_CATEGORY_DANGEROUS_CONTENT, Threshold =HarmBlockThreshold.BLOCK_NONE},
            ]
        );
    var systemInstruction = """
        あなたは英語(アメリカ)から日本語(日本)へ翻訳する専門家です。
        翻訳にあたって以下の点を考慮してください。

        翻訳するテキストは全体を通して、以下の背景や文脈があるものして翻訳してください。
        <背景>
        This is a nostalgic farming / life sim RPG game like Harvest Moon.
        It is set in rural medieval Europe.
        </背景>

        入力テキストは以下のJsonフォーマットになっています。
        各textの内容はペアとなるcontextの文脈を考慮して翻訳してください。
        contextに一人称が指定されている場合は、漢字、ひらがな、カタカナの表記を変更せずに一人称をそのまま使ってください。
        <入力テキストのJsonフォーマット>
        [{"text":"翻訳対象のテキスト1", "context": "翻訳対象のテキスト1の文脈"}, {"text":"翻訳対象のテキスト2", "context": "翻訳対象のテキスト2の文脈"}]
        </入力テキストのJsonフォーマット>
        
        出力は以下の文字列型の配列のJsonフォーマットです。
        入力されたテキストの順序を維持して翻訳したテキストを出力してください。
        <出力テキストのJsonフォーマット>
        ["翻訳したテキスト1", "翻訳したテキスト2"]
        </出力テキストのJsonフォーマット>

        入力された英語(アメリカ)のテキストを日本語(日本)へ翻訳して出力ください。
        """;
    var input = new[] {
        new {
            Text = "Bath? Me too.",
            Context = """
            This line is said by the male character.
            Like his Uncle Landen, he is a woodworker and runs the Carpenter's Shop in The Eastern Road.
            He has a thoughtful and calm personality.
            His first person is "儂" in Kanji.
            """ } };
    var req = new GenerateContentRequest()
    {
        Contents = [RequestExtensions.FormatGenerateContentInput(JsonSerializer.Serialize(input, client.GenerateObjectJsonSerializerOptions))],
        SystemInstruction = RequestExtensions.FormatSystemInstruction(systemInstruction),
    };
    var translated = await client.GenerateObjectAsync<string[]>(req).ConfigureAwait(false) ?? [];
    Console.WriteLine(translated[0]);
}

static async Task ClipTextRect(string imagePath, [FromServices]ILogger<OneOcr> logger, [FromServices]IOptionsSnapshot<LanguageOptions> langOptions, [FromServices] IOptionsSnapshot<BasicOcrParam> ocrParam)
{
    var validator = new OneOcrValidator();
    await validator.Validate(null!);
    var ocr = new OneOcr(logger, langOptions, ocrParam);

    // 画像ファイルの読み込み

    // ocrの実行

    // 画像からテキスト矩形を切り抜き

    // 切り抜いた画像を保存
}


record Secret
{
    public required string DeepLAuthKey { get; init; }
    public required string GoogleAIAuthKey { get; init; }
}