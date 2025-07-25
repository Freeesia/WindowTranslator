﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Compression;
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
            translated[i] = this.service.Translate(srcTexts[i].SourceText);
        }
        return translated;
    }
}

public class BergamotValidator(IGitHubClient client) : ITargetSettingsValidator
{
    private const string RepoOwner = "mozilla";
    private const string RepoName = "firefox-translations-models";
    private readonly IGitHubClient client = client;

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

    private async ValueTask<bool> DownloadIfNotExists(string src, string dst)
    {
        var langPair = $"{src}{dst}";
        var modelDir = Path.Combine(PathUtility.UserDir, "bergamot", langPair);
        var configPath = Path.Combine(modelDir, "config.yml");

        // すでに設定ファイルが存在する場合は処理をスキップ
        if (File.Exists(configPath))
            return true;

        Directory.CreateDirectory(modelDir);

        var tmpDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpDir);

        // base → base-memory → tiny の順でフォールバック
        var modelPaths = new[] { "models/base", "models/base-memory", "models/tiny" };

        foreach (var modelPath in modelPaths)
        {
            if (await TryDownloadModelFromPath(modelPath, langPair, tmpDir, modelDir, configPath))
                return true;
        }

        // すべてのパスで見つからなかった場合
        return false;
    }

    private async ValueTask<bool> TryDownloadModelFromPath(string modelPath, string langPair, string tmpDir, string modelDir, string configPath)
    {
        try
        {
            var contents = await this.client.Repository.Content.GetAllContents(RepoOwner, RepoName, $"{modelPath}/{langPair}").ConfigureAwait(false);
            var files = await DownloadAndExtractFiles(contents, tmpDir, modelDir);
            await CreateConfigFile(files, configPath);
            return true;
        }
        catch (NotFoundException)
        {
            return false;
        }
    }

    private async ValueTask<List<string>> DownloadAndExtractFiles(IReadOnlyList<RepositoryContent> contents, string tmpDir, string modelDir)
    {
        var files = new List<string>();

        foreach (var content in contents)
        {
            var tmpPath = Path.Combine(tmpDir, content.Name);
            await this.client.DownloadFileAsync(RepoOwner, RepoName, content, tmpPath).ConfigureAwait(false);

            var fileName = await ExtractFileIfNeeded(tmpPath, modelDir);
            files.Add(fileName);
        }

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

file static class Extensions
{
    public static async ValueTask DownloadFileAsync(this IGitHubClient client, string owner, string repo, RepositoryContent content, string path)
    {
        var res = await client.Connection.GetRawStream(new(content.DownloadUrl), ReadOnlyDictionary<string, string>.Empty).ConfigureAwait(false);
        res.HttpResponse.IsSuccessStatusCode();
        if (res.HttpResponse.Body is string lfs && lfs.StartsWith("version https://git-lfs.github.com/spec/v1", StringComparison.Ordinal))
        {
            var url = $"https://media.githubusercontent.com/media/{owner}/{repo}/refs/heads/main/{content.Path}";
            await client.DownloadFileAsync(new(url), path).ConfigureAwait(false);
        }
        else if (res.HttpResponse.Body is string d)
        {
            await using var fs = File.Create(path);
            await using var st = new StreamWriter(fs);
            await st.WriteAsync(d).ConfigureAwait(false);
        }
        else if (res.HttpResponse.Body is Stream stream)
        {
            await using var fs = File.Create(path);
            await stream.CopyToAsync(fs).ConfigureAwait(false);
        }
    }

    public static async ValueTask DownloadFileAsync(this IGitHubClient client, Uri url, string path)
    {
        var res = await client.Connection.GetRawStream(url, ReadOnlyDictionary<string, string>.Empty).ConfigureAwait(false);
        res.HttpResponse.IsSuccessStatusCode();
        using var st = res.Body;
        await using var fs = File.Create(path);
        await st.CopyToAsync(fs).ConfigureAwait(false);
    }
}