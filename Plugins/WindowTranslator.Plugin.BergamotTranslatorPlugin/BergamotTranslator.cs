using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
using System.Reflection;
using BergamotTranslatorSharp;
using Microsoft.Extensions.Options;
using Octokit;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;

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
            throw new Exception($"ご利用のPCではBergamotを利用できません");
        }
        var src = langOptions.Value.Source[..2];
        var dst = langOptions.Value.Target[..2];
        var path = Path.Combine(PathUtility.UserDir, "bergamot", $"{src}{dst}", "config.yml");
        if (File.Exists(path))
        {
            this.service = new BlockingService(path);
            return;
        }
        var path1 = Path.Combine(PathUtility.UserDir, "bergamot", $"{src}en", "config.yml");
        var path2 = Path.Combine(PathUtility.UserDir, "bergamot", $"en{dst}", "config.yml");
        if (File.Exists(path1) && File.Exists(path2))
        {
            this.service = new BlockingService(path1, path2);
            return;
        }
        throw new Exception("Bergamot モデルが存在しないため、翻訳できません。");
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
            translated[i] = this.service.Translate(srcTexts[i].Text);
        }
        return translated;
    }
}

public class BergamotValidator : ITargetSettingsValidator
{
    private static readonly GitHubClient GitHubClient = new(CreateHeader());
    private const string RepoOwner = "mozilla";
    private const string RepoName = "firefox-translations-models";

    private static ProductHeaderValue CreateHeader()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetName();
        return new(name.Name, name.Version?.ToString());
    }

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(BergamotTranslator))
        {
            return ValidateResult.Valid;
        }

        if (!SystemUtility.IsX64Machine())
        {
            return ValidateResult.Invalid("対象外のPC", $"ご利用のPCではBergamotを利用できません");
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
                return ValidateResult.Invalid("Bergamot モデル", $"No model data was found that can be translated from {srcLang} to {dstLang}. Translation is not available for this language pair.");
            }

            if (!await DownloadIfNotExists(src, "en").ConfigureAwait(false))
            {
                var srcLang = CultureInfo.GetCultureInfo(settings.Language.Source).DisplayName;
                return ValidateResult.Invalid("Bergamot モデル", $"{srcLang}から翻訳できるモデルデータが見つかりませんでした。この言語の翻訳は利用できません。");
            }
            if (!await DownloadIfNotExists("en", dst).ConfigureAwait(false))
            {
                var dstLang = CultureInfo.GetCultureInfo(settings.Language.Target).DisplayName;
                return ValidateResult.Invalid("Bergamot モデル", $"{dstLang}へ翻訳できるモデルデータが見つかりませんでした。この言語の翻訳は利用できません。");
            }

            return ValidateResult.Valid;
        }
        catch (Exception ex)
        {
            return ValidateResult.Invalid("Bergamot モデル",
                $"モデルファイルのダウンロードに失敗しました。\n{ex.Message}");
        }
    }

    private static async ValueTask<bool> DownloadIfNotExists(string src, string dst)
    {
        var langPair = $"{src}{dst}";
        var modelDir = Path.Combine(PathUtility.UserDir, "bergamot", langPair);
        var configPath = Path.Combine(modelDir, "config.yml");

        if (File.Exists(configPath))
        {
            return true;
        }

        Directory.CreateDirectory(modelDir);

        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);

        try
        {
            var contents = await GitHubClient.Repository.Content.GetAllContents(RepoOwner, RepoName, $"models/prod/{langPair}").ConfigureAwait(false);

            var files = new List<string>();
            foreach (var content in contents)
            {
                var path = Path.Combine(tmpDir, content.Name);
                var url = $"https://media.githubusercontent.com/media/{RepoOwner}/{RepoName}/refs/heads/main/{content.Path}";
                await GitHubClient.DownloadFileAsync(new(url), path).ConfigureAwait(false);
                if (Path.GetExtension(path) == ".gz")
                {
                    var dstPath = Path.Combine(modelDir, Path.GetFileNameWithoutExtension(content.Name));
                    using var st = File.OpenRead(path);
                    using var gz = new GZipStream(st, CompressionMode.Decompress);
                    using var fs = File.Create(dstPath);
                    await gz.CopyToAsync(fs).ConfigureAwait(false);
                    files.Add(Path.GetFileNameWithoutExtension(content.Name));
                }
                else
                {
                    var dstPath = Path.Combine(modelDir, content.Name);
                    File.Move(path, dstPath, true);
                    files.Add(content.Name);
                }
            }

            var modelPath = files.FirstOrDefault(f => f.StartsWith("model"));
            var vocabPath = files.FirstOrDefault(f => f.StartsWith("vocab"));
            var srcVocabPath = files.FirstOrDefault(f => f.Contains("srcvocab"));
            var trgVocabPath = files.FirstOrDefault(f => f.Contains("trgvocab"));
            var shortListPath = files.FirstOrDefault(f => f.StartsWith("lex"));

            var config = CreateConfig(
                modelPath ?? throw new InvalidOperationException("modelデータが存在しません"),
                srcVocabPath ?? vocabPath ?? throw new InvalidOperationException("vocabデータが存在しません"),
                trgVocabPath ?? vocabPath ?? throw new InvalidOperationException("vocabデータが存在しません"),
                shortListPath ?? throw new InvalidOperationException("shortListデータが存在しません"),
                modelPath.Split('.') switch
                {
                    [.., "intgemm", "alphas", "bin"] => "int8shiftAlphaAll",
                    [.., "intgemm8", "bin"] => "int8shiftAll",
                    _ => throw new InvalidOperationException($"Unknown model name pattern: ${modelPath}")
                });
            await File.WriteAllTextAsync(configPath, config).ConfigureAwait(false);
        }
        catch (NotFoundException)
        {
            return false;
        }
        return true;
    }

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

file static class Extensions
{
    public static async ValueTask DownloadFileAsync(this GitHubClient client, Uri url, string path)
    {
        var res = await client.Connection.GetRawStream(url, ReadOnlyDictionary<string, string>.Empty).ConfigureAwait(false);
        res.HttpResponse.IsSuccessStatusCode();
        using var st = res.Body;
        await using var fs = File.Create(path);
        await st.CopyToAsync(fs).ConfigureAwait(false);
    }
}