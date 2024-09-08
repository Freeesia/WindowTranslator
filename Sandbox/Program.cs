using System.Reflection;
using ConsoleAppFramework;
using DeepL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var configuration = new ConfigurationBuilder()
    .AddUserSecrets(Assembly.GetExecutingAssembly())
    .Build();

var services = new ServiceCollection();
services.Configure<Secret>(configuration.GetSection("Secret"));
using var serviceProvider = services.BuildServiceProvider();
ConsoleApp.ServiceProvider = serviceProvider;

var app = ConsoleApp.Create();
app.Add("DeepL", DeepLTest);
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

record Secret
{
    public required string DeepLAuthKey { get; init; }
}