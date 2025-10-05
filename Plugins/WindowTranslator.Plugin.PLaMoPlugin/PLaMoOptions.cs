using System.ComponentModel.DataAnnotations;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Modules;
using WindowTranslator.Plugin.PLaMoPlugin.Properties;

namespace WindowTranslator.Plugin.PLaMoPlugin;

public class PLaMoOptions : IPluginParam
{
    [LocalizedDescription(typeof(Resources), $"{nameof(ModelPath)}_Desc")]
    [InputFilePath(".gguf", "GGUF Model (*.gguf)|*.gguf")]
    public string? ModelPath { get; set; }

    [LocalizedDescription(typeof(Resources), $"{nameof(GpuLayerCount)}_Desc")]
    [Range(0, 100)]
    public int GpuLayerCount { get; set; } = 0;

    [LocalizedDescription(typeof(Resources), $"{nameof(ContextSize)}_Desc")]
    [Range(512, 32768)]
    public int ContextSize { get; set; } = 2048;
}

public class PLaMoOptionsValidator : ITargetSettingsValidator
{
    public ValueTask<ValidateResult> Validate(TargetSettings settings)
    {
        var op = settings.PluginParams.GetValueOrDefault(nameof(PLaMoOptions)) as PLaMoOptions;
        
        // モデルパスが設定されていない場合は無効
        if (string.IsNullOrEmpty(op?.ModelPath))
        {
            // 翻訳モジュールで利用しない場合は無条件で有効
            if (settings.SelectedPlugins[nameof(ITranslateModule)] != nameof(PLaMoTranslator))
            {
                return ValueTask.FromResult(ValidateResult.Valid);
            }
            return ValueTask.FromResult(ValidateResult.Invalid("PLaMo", Resources.ModelPathNotSet));
        }

        // モデルファイルが存在しない場合は無効
        if (!File.Exists(op.ModelPath))
        {
            return ValueTask.FromResult(ValidateResult.Invalid("PLaMo", Resources.ModelFileNotFound));
        }

        return ValueTask.FromResult(ValidateResult.Valid);
    }
}
