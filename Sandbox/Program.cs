using System.Reflection;
using System.Text;
using System.Text.Json;
using ConsoleAppFramework;
using DeepL;
using GenerativeAI;
using GenerativeAI.Types;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Windows.Graphics.Imaging;
using WindowTranslator;
using WindowTranslator.Extensions;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.OneOcrPlugin;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

var services = new ServiceCollection();
services.Configure<Secret>(configuration.GetSection("Secret"));
services.Configure<LanguageOptions>(op => { });
services.Configure<BasicOcrParam>(op => { });
services.AddLogging();
using var serviceProvider = services.BuildServiceProvider();
ConsoleApp.ServiceProvider = serviceProvider;

var app = ConsoleApp.Create();
app.Add("DeepL", DeepLTest);
app.Add("GoogleAI", GoogleAITranslateTest);
app.Add("ClipTextRect", ClipTextRect);
app.Add("PLaMo", PLaMoTest);
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

static async Task ClipTextRect([Argument] string imagePath, [FromServices] ILogger<OneOcr> logger, [FromServices] IOptionsSnapshot<LanguageOptions> langOptions, [FromServices] IOptionsSnapshot<BasicOcrParam> ocrParam)
{
    var validator = new OneOcrValidator();
    await validator.Validate(new() { SelectedPlugins = { [nameof(IOcrModule)] = nameof(OneOcr) } }).ConfigureAwait(false);
    var ocr = new OneOcr(logger, langOptions, ocrParam);

    // 画像ファイルの読み込み
    using var fileStream = new FileStream(imagePath, FileMode.Open);
    using var randomAccessStream = fileStream.AsRandomAccessStream();
    var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
    var bitmap = await decoder.GetSoftwareBitmapAsync();

    // OCRの実行
    var textRects = await ocr.RecognizeAsync(bitmap);

    // 画像からテキスト矩形を切り抜き
    var outputDir = Path.Combine(Path.GetDirectoryName(imagePath)!, "clipped");
    Directory.CreateDirectory(outputDir);

    var index = 0;
    foreach (var textRect in textRects)
    {
        // TextRectからRectangleに変換
        var rect = textRect.ToRect();

        // 画像の境界をチェックして調整
        rect.X = Math.Max(0, rect.X);
        rect.Y = Math.Max(0, rect.Y);
        rect.Width = Math.Min(rect.Width, bitmap.PixelWidth - rect.X);
        rect.Height = Math.Min(rect.Height, bitmap.PixelHeight - rect.Y);        // 切り抜いた画像を保存（連番ファイル名）
        var outputFileName = $"text_{index:D3}.jpg";
        var outputPath = Path.Combine(outputDir, outputFileName);

        // 矩形で指定された部分をJPEGバイト配列として取得し、ファイルに書き込み
        var croppedImageData = await bitmap.EncodeToJpegBytes(rect);
        await File.WriteAllBytesAsync(outputPath, croppedImageData);

        Console.WriteLine($"切り抜き画像を保存しました: {outputFileName} (テキスト: \"{textRect.SourceText}\")");
        index++;
    }

    Console.WriteLine($"合計 {textRects.Count()} 個のテキスト矩形を切り抜きました。");
}

static async Task PLaMoTest([Argument] string modelPath, [Argument] string sourceLang = "English", [Argument] string targetLang = "Japanese")
{
    Console.WriteLine($"PLaMo Translation Test");
    Console.WriteLine($"Model: {modelPath}");
    Console.WriteLine($"Source Language: {sourceLang}");
    Console.WriteLine($"Target Language: {targetLang}");
    Console.WriteLine();

    if (!File.Exists(modelPath))
    {
        Console.WriteLine($"Error: Model file not found: {modelPath}");
        return;
    }

    // テスト用の翻訳テキスト
    var testTexts = new[]
    {
        "Hello, how are you?",
        "This is a test.",
        "Good morning!"
    };

    Console.WriteLine("Input texts:");
    foreach (var text in testTexts)
    {
        Console.WriteLine($"  - {text}");
    }
    Console.WriteLine();

    // モデルパラメータの設定
    var modelParams = new ModelParams(modelPath)
    {
        ContextSize = 128,
    };

    Console.WriteLine("Loading model...");
    using var weights = LLamaWeights.LoadFromFile(modelParams);
    Console.WriteLine("Model loaded successfully.");
    Console.WriteLine();

    // JSON形式で入力テキストをまとめる
    var inputJson = JsonSerializer.Serialize(testTexts);
    Console.WriteLine($"Input JSON: {inputJson}");
    Console.WriteLine();

    // PLaMo専用のプロンプトフォーマット
    var prompt = $"""
<|plamo:op|>dataset
translation
<|plamo:op|>input lang={sourceLang}
{inputJson}
<|plamo:op|>output lang={targetLang}

""";

    Console.WriteLine("Prompt:");
    Console.WriteLine("---");
    Console.WriteLine(prompt);
    Console.WriteLine("---");
    Console.WriteLine();

    // 推論の実行
    using var context = weights.CreateContext(modelParams);
    var executor = new StatelessExecutor(weights, modelParams);
    
    var inferenceParams = new InferenceParams
    {
        MaxTokens = 128,
        AntiPrompts = ["<|plamo:op|>"],
    };

    Console.WriteLine("Generating translation...");
    var responseBuilder = new StringBuilder();
    
    await foreach (var token in executor.InferAsync(prompt, inferenceParams))
    {
        responseBuilder.Append(token);
        Console.Write(token); // リアルタイムで出力
    }
    Console.WriteLine();
    Console.WriteLine();

    var response = responseBuilder.ToString().Trim();
    
    Console.WriteLine("Raw response:");
    Console.WriteLine("---");
    Console.WriteLine(response);
    Console.WriteLine("---");
    Console.WriteLine();

    try
    {
        // レスポンスをJSON配列としてパース
        var result = JsonSerializer.Deserialize<string[]>(response);
        if (result != null)
        {
            Console.WriteLine("Parsed translations:");
            for (int i = 0; i < result.Length; i++)
            {
                Console.WriteLine($"  [{i}] {testTexts[i]} => {result[i]}");
            }
        }
        else
        {
            Console.WriteLine("Failed to parse response as JSON array.");
        }
    }
    catch (JsonException ex)
    {
        Console.WriteLine($"JSON parse error: {ex.Message}");
    }
}


record Secret
{
    public required string DeepLAuthKey { get; init; }
    public required string GoogleAIAuthKey { get; init; }
}