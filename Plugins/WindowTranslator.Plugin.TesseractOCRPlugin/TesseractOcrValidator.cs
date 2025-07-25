﻿using Octokit;
using TesseractOCR.Enums;
using WindowTranslator.Modules;

namespace WindowTranslator.Plugin.TesseractOCRPlugin;

public class TesseractOcrValidator(IGitHubClient client) : ITargetSettingsValidator
{
    private readonly IGitHubClient client = client;

    public async ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        // 翻訳モジュールで利用しない場合は無条件で有効
        if (settings.SelectedPlugins[nameof(IOcrModule)] != nameof(TesseractOcr))
        {
            return ValidateResult.Valid;
        }

        Directory.CreateDirectory(TesseractOcr.DataDir);
        var langData = LanguageHelper.EnumToString(TesseractOcr.ConvertLanguage(settings.Language.Source)) + ".traineddata";
        var langDataPath = Path.Combine(TesseractOcr.DataDir, langData);
        if (File.Exists(langDataPath))
        {
            return ValidateResult.Valid;
        }

        // IGitHubClient を利用して `tesseract-ocr/tessdata_best` リポジトリからeng.traineddataをダウンロードする
        var contents = await client.Repository.Content.GetRawContent("tesseract-ocr", "tessdata_best", langData);
        await using var fs = File.Create(langDataPath);
        await fs.WriteAsync(contents).ConfigureAwait(false);
        return ValidateResult.Valid;
    }
}
