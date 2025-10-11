using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

public class PLaMoOptions : IPluginParam
{
    [LocalizedDescription(typeof(Resources), $"{nameof(ContextSize)}_Desc")]
    [Range(512, 32768)]
    public int ContextSize { get; set; } = 2048;
}

public class PLaMoValidator : ITargetSettingsValidator
{
    private const string ModelUrl = "https://huggingface.co/mmnga/plamo-2-translate-gguf/resolve/main/plamo-2-translate-Q4_K_M.gguf";
    private const string ModelFileName = "plamo-2-translate-Q4_K_M.gguf";

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

    private static async ValueTask DownloadModelIfNotExists()
    {
        var modelDir = Path.Combine(PathUtility.UserDir, "plamo");
        var modelPath = Path.Combine(modelDir, ModelFileName);

        // すでにモデルファイルが存在する場合は処理をスキップ
        if (File.Exists(modelPath))
            return;

        Directory.CreateDirectory(modelDir);

        // モデルファイルをダウンロード
        using var httpClient = new HttpClient();
        using var response = await httpClient.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        await using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 8192, useAsync: true);
        await contentStream.CopyToAsync(fileStream).ConfigureAwait(false);
    }

    public static string GetModelPath()
    {
        var modelDir = Path.Combine(PathUtility.UserDir, "plamo");
        return Path.Combine(modelDir, ModelFileName);
    }
}
