using CommunityToolkit.Mvvm.Input;
using WindowTranslator.Extensions;

namespace WindowTranslator.Modules.Validate;

internal partial class ValidateViewModel(IPresentationService presentationService, IEnumerable<ITargetSettingsValidator> validators, TargetSettings settings)
{
    private readonly IPresentationService presentationService = presentationService;
    private readonly IEnumerable<ITargetSettingsValidator> validators = validators;
    private readonly TargetSettings settings = settings;

    public IReadOnlyList<ValidateResult> Results { get; private set; } = [];

    [RelayCommand]
    public async Task ValidateAsync()
    {
        this.Results = await this.validators.ValidateAsync(this.settings);
        await this.presentationService.CloseDialogAsync(true);
    }
}
