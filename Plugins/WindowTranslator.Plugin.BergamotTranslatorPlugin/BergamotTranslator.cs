using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using BergamotTranslatorSharp;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.BergamotTranslatorPlugin.Properties;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

[DefaultModule]
[DisplayName("Bergamot")]
public sealed class BergamotTranslator : ITranslateModule, IDisposable
{
    private readonly BlockingService service;

    public BergamotTranslator(IOptionsSnapshot<LanguageOptions> langOptions)
    {
        if (!SystemUtility.IsX64Machine())
        {
            throw new AppUserException(Resources.NotAvailableArch);
        }
        var src = langOptions.Value.Source[..2];
        var dst = langOptions.Value.Target[..2];
        var path = Path.Combine(SystemUtility.ModelsPath, $"{src}{dst}", "config.yml");
        if (File.Exists(path))
        {
            this.service = new BlockingService(path);
            return;
        }
        var path1 = Path.Combine(SystemUtility.ModelsPath, $"{src}en", "config.yml");
        var path2 = Path.Combine(SystemUtility.ModelsPath, $"en{dst}", "config.yml");
        if (File.Exists(path1) && File.Exists(path2))
        {
            this.service = new BlockingService(path1, path2);
            return;
        }
        throw new AppUserException(Resources.ModelNotFound);
    }

    public void Dispose()
        => this.service?.Dispose();

    public async ValueTask<string[]> TranslateAsync(TextInfo[] srcTexts)
        => await Task.Run(() => Translate(srcTexts)).ConfigureAwait(false);

    private string[] Translate(TextInfo[] srcTexts)
    {
        var translated = new string[srcTexts.Length];
        for (var i = 0; i < srcTexts.Length; i++)
        {
            translated[i] = this.service.Translate(srcTexts[i].SourceText);
        }
        return translated;
    }
}

public class BergamotValidator(ILogger<BergamotValidator> logger) : ITargetSettingsValidator
{
    // mozilla/firefox-translations-models はアーカイブされ、モデルはGCSバケットに移行された。
    // モデル一覧はランタイムに db/models.json から取得し、モデルの更新に追従する。
    private const string BucketName = "moz-fx-translations-data--303e-prod-translations-data";
    private const string ModelsJsonObject = "db/models.json";

    private static volatile GcsModelsRoot? _cachedModels;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private static readonly StorageClient StorageClient = StorageClient.CreateUnauthenticated();
    private static readonly string[] ArchitecturePriority = ["base", "base-memory", "tiny"];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly ILogger<BergamotValidator> logger = logger;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(BergamotTranslator))
        {
            return ValidateResult.Valid;
        }

        if (!SystemUtility.IsX64Machine())
        {
            return ValidateResult.Invalid("Bergamot", Resources.NotAvailableArch);
        }

        var src = settings.Language.Source[..2];
        var dst = settings.Language.Target[..2];

        try
        {
            if (await DownloadIfNotExists(src, dst).ConfigureAwait(false))
            {
                return ValidateResult.Valid;
            }
            else if (src == "en" || dst == "en")
            {
                var srcLang = CultureInfo.GetCultureInfo(settings.Language.Source).DisplayName;
                var dstLang = CultureInfo.GetCultureInfo(settings.Language.Target).DisplayName;
                return ValidateResult.Invalid("Bergamot", string.Format(Resources.NotFoundCompatModel, srcLang, dstLang));
            }

            if (!await DownloadIfNotExists(src, "en").ConfigureAwait(false))
            {
                var srcLang = CultureInfo.GetCultureInfo(settings.Language.Source).DisplayName;
                return ValidateResult.Invalid("Bergamot", string.Format(Resources.NotFoundFromModel, srcLang));
            }
            if (!await DownloadIfNotExists("en", dst).ConfigureAwait(false))
            {
                var dstLang = CultureInfo.GetCultureInfo(settings.Language.Target).DisplayName;
                return ValidateResult.Invalid("Bergamot", string.Format(Resources.NotFoundToModel, dstLang));
            }

            return ValidateResult.Valid;
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("Bergamot", string.Format(Resources.DownloadFaild, ex.Message));
        }
    }

    private async ValueTask<GcsModelsRoot> GetModelsIndexAsync()
    {
        if (_cachedModels is not null)
            return _cachedModels;

        await Semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cachedModels is not null)
                return _cachedModels;

            this.logger.LogInformation("Downloading models index from GCS...");
            using var stream = new MemoryStream();
            await StorageClient.DownloadObjectAsync(BucketName, ModelsJsonObject, stream).ConfigureAwait(false);
            stream.Position = 0;
            _cachedModels = await JsonSerializer.DeserializeAsync<GcsModelsRoot>(stream, JsonOptions).ConfigureAwait(false)
                ?? throw new InvalidOperationException("models.jsonの解析に失敗しました");
            this.logger.LogInformation("Downloaded models index");
            return _cachedModels;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private async ValueTask<bool> DownloadIfNotExists(string src, string dst)
    {
        var langPair = $"{src}{dst}";
        var hyphenated = $"{src}-{dst}";
        var modelDir = Path.Combine(SystemUtility.ModelsPath, langPair);
        var configPath = Path.Combine(modelDir, "config.yml");

        // すでに設定ファイルが存在する場合は処理をスキップ
        if (File.Exists(configPath))
            return true;

        var modelsIndex = await GetModelsIndexAsync().ConfigureAwait(false);
        if (!modelsIndex.Models.TryGetValue(hyphenated, out var modelList))
            return false;

        // base → base-memory → tiny の順でフォールバック、Releaseモデルを優先
        GcsModel? model = null;
        foreach (var arch in ArchitecturePriority)
        {
            model = modelList.FirstOrDefault(m => m.Architecture == arch && m.ReleaseStatus == "Release")
                ?? modelList.FirstOrDefault(m => m.Architecture == arch);
            if (model is not null)
                break;
        }

        if (model is null)
            return false;

        Directory.CreateDirectory(modelDir);
        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);

        var files = await DownloadAndExtractFiles(model, tmpDir, modelDir).ConfigureAwait(false);
        await CreateConfigFile(files, configPath).ConfigureAwait(false);
        return true;
    }

    private async ValueTask<List<string>> DownloadAndExtractFiles(GcsModel model, string tmpDir, string modelDir)
    {
        var files = new List<string>();

        async Task<string> DownloadAndExtract(string gcsObjectPath)
        {
            var fileName = Path.GetFileName(gcsObjectPath);
            var tmpPath = Path.Combine(tmpDir, fileName);
            this.logger.LogInformation("Downloading {FileName}...", fileName);
            using (var fs = File.Create(tmpPath))
            {
                await StorageClient.DownloadObjectAsync(BucketName, gcsObjectPath, fs).ConfigureAwait(false);
            }
            this.logger.LogInformation("Downloaded {FileName}", fileName);
            return await ExtractFileIfNeeded(tmpPath, modelDir).ConfigureAwait(false);
        }

        files.Add(await DownloadAndExtract(model.Files.Model.Path).ConfigureAwait(false));

        if (model.Files.LexicalShortlist?.Path is string lexPath)
            files.Add(await DownloadAndExtract(lexPath).ConfigureAwait(false));

        if (model.Files.Vocab?.Path is string vocabPath)
            files.Add(await DownloadAndExtract(vocabPath).ConfigureAwait(false));

        if (model.Files.SrcVocab?.Path is string srcVocabPath)
            files.Add(await DownloadAndExtract(srcVocabPath).ConfigureAwait(false));

        if (model.Files.TrgVocab?.Path is string trgVocabPath)
            files.Add(await DownloadAndExtract(trgVocabPath).ConfigureAwait(false));

        return files;
    }

    private static async ValueTask<string> ExtractFileIfNeeded(string tmpPath, string modelDir)
    {
        if (Path.GetExtension(tmpPath) == ".gz")
        {
            var fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(tmpPath));
            var dstPath = Path.Combine(modelDir, fileName);

            using var st = File.OpenRead(tmpPath);
            using var gz = new GZipStream(st, CompressionMode.Decompress);
            using var fs = File.Create(dstPath);
            await gz.CopyToAsync(fs).ConfigureAwait(false);

            return fileName;
        }
        else
        {
            var fileName = Path.GetFileName(tmpPath);
            var dstPath = Path.Combine(modelDir, fileName);
            File.Move(tmpPath, dstPath, true);

            return fileName;
        }
    }

    private static async ValueTask CreateConfigFile(List<string> files, string configPath)
    {
        var modelFile = files.FirstOrDefault(f => f.StartsWith("model"));
        var vocabPath = files.FirstOrDefault(f => f.StartsWith("vocab"));
        var srcVocabPath = files.FirstOrDefault(f => f.Contains("srcvocab"));
        var trgVocabPath = files.FirstOrDefault(f => f.Contains("trgvocab"));
        var shortListPath = files.FirstOrDefault(f => f.StartsWith("lex"));

        var config = CreateConfig(
            modelFile ?? throw new InvalidOperationException("modelデータが存在しません"),
            srcVocabPath ?? vocabPath ?? throw new InvalidOperationException("vocabデータが存在しません"),
            trgVocabPath ?? vocabPath ?? throw new InvalidOperationException("vocabデータが存在しません"),
            shortListPath ?? throw new InvalidOperationException("shortListデータが存在しません"),
            GetModelPrecision(modelFile));

        await File.WriteAllTextAsync(configPath, config).ConfigureAwait(false);
    }

    private static string GetModelPrecision(string modelFile)
        => modelFile?.Split('.') switch
        {
            [.., "intgemm", "alphas", "bin"] => "int8shiftAlphaAll",
            [.., "intgemm8", "bin"] => "int8shiftAll",
            _ => throw new InvalidOperationException($"Unknown model name pattern: {modelFile}")
        };

    private static string CreateConfig(string modelPath, string srcVocabPath, string trgVocabPath, string shortListPath, string precision) => $"""
        # These Marian options are set according to
        # https://github.com/mozilla/firefox-translations/blob/main/extension/controller/translation/translationWorker.js
        # to imitate production setting

        relative-paths: true
        models:
          - {modelPath}
        vocabs:
          - {srcVocabPath}
          - {trgVocabPath}
        shortlist:
          - {shortListPath}
          - false
        beam-size: 1
        normalize: 1.0
        word-penalty: 0
        max-length-break: 128
        mini-batch-words: 1024
        workspace: 128
        max-length-factor: 2.0
        skip-cost: true
        cpu-threads: 0
        quiet: true
        quiet-translation: true
        gemm-precision: {precision}
        alignment: soft
        """;
}

/// <summary>GCS の db/models.json のルート構造</summary>
internal record GcsModelsRoot(
    string BaseUrl,
    Dictionary<string, List<GcsModel>> Models
);

/// <summary>特定の言語ペア・アーキテクチャのモデルエントリ</summary>
internal record GcsModel(
    string Architecture,
    string ReleaseStatus,
    string SourceLanguage,
    string TargetLanguage,
    GcsModelFiles Files
);

/// <summary>モデルに含まれるファイル群</summary>
internal record GcsModelFiles(
    GcsModelFile Model,
    GcsModelFile? LexicalShortlist,
    GcsModelFile? Vocab,
    GcsModelFile? SrcVocab,
    GcsModelFile? TrgVocab
);

/// <summary>GCSオブジェクトパスを持つファイル参照</summary>
internal record GcsModelFile(
    string Path
);