using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Extensions;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

public class PLaMoOptions : IPluginParam
{
    public const string ModelFileName = "plamo-2-translate-Q4_K_S.gguf";
    public const string ModelUrl = $"https://huggingface.co/mmnga/plamo-2-translate-gguf/resolve/main/{ModelFileName}";

    public static string ModelPath => Path.Combine(PathUtility.UserDir, "plamo", ModelFileName);

    [LocalizedDescription(typeof(Resources), $"{nameof(ContextSize)}_Desc")]
    [Range(512, 32768)]
    public int ContextSize { get; set; } = 2048;

    [Range(-1, 6)]
    [Spinnable]
    [LocalizedDescription(typeof(Resources), $"{nameof(VRAM)}_Desc")]
    public int VRAM { get; set; } = -1;
}

public class PLaMoValidator(ILogger<PLaMoValidator> logger) : ITargetSettingsValidator
{
    private readonly ILogger<PLaMoValidator> logger = logger;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(PLaMoTranslator))
        {
            return ValidateResult.Valid;
        }

        try
        {
            await DownloadModelIfNotExists().ConfigureAwait(false);
            return ValidateResult.Valid;
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("PLaMo", string.Format(Resources.DownloadFailed, ex.Message));
        }
    }

    private async ValueTask DownloadModelIfNotExists()
    {
        var modelPath = PLaMoOptions.ModelPath;
        // すでにモデルファイルが存在する場合は処理をスキップ
        if (File.Exists(modelPath))
            return;

        var modelDir = Path.GetDirectoryName(PLaMoOptions.ModelPath)!;
        Directory.CreateDirectory(modelDir);

        // モデルファイルをダウンロード
        using var httpClient = new HttpClient();
        this.logger.LogInformation("Downloading PLaMo model...");
        await httpClient.DownloadFile(PLaMoOptions.ModelUrl, modelPath, p => this.logger.LogInformation($"Downloading PLaMo model: {p:P2}")).ConfigureAwait(false);
    }
}
