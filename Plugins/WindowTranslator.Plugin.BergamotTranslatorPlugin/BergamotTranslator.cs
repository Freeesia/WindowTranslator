using System.ComponentModel;
using BergamotTranslatorSharp;
using Microsoft.Extensions.Options;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.BergamotTranslatorPlugin;

[DisplayName("Bergamot")]
public class BergamotTranslator : ITranslateModule
{
    private readonly BlockingService service;

    public BergamotTranslator(IOptionsSnapshot<LanguageOptions> langOptions)
    {
        var src = langOptions.Value.Source[..2];
        var dst = langOptions.Value.Target[..2];
        var path = Path.Combine(PathUtility.UserDir, "bergamot", $"{src}{dst}", "config.yml");
        this.service = new BlockingService(path);
    }

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
    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(BergamotTranslator))
        {
            return ValidateResult.Valid;
        }

        var src = settings.Language.Source[..2];
        var dst = settings.Language.Target[..2];
        var path = Path.Combine(PathUtility.UserDir, "bergamot", $"{src}{dst}", "config.yml");
        if (File.Exists(path))
        {
            return ValidateResult.Valid;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // TODO: モデルのダウンロード

        var config = CreateConfig(string.Empty, string.Empty, string.Empty, string.Empty, "float32");
        await File.WriteAllTextAsync(path, config).ConfigureAwait(false);

        return ValidateResult.Valid;
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