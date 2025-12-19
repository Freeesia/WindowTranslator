using WindowTranslator.Extensions;

namespace WindowTranslator.Modules.Validate;

internal class ValidateViewModel
{
    private readonly IPresentationService presentationService;
    private readonly IEnumerable<ITargetSettingsValidator> validators;
    private readonly TargetSettings settings;

    public ValidateViewModel(IPresentationService presentationService, IEnumerable<ITargetSettingsValidator> validators, TargetSettings settings)
    {
        this.presentationService = presentationService;
        this.validators = validators;
        this.settings = settings;

        _ = ValidateAsync();
    }

    public IReadOnlyList<ValidateResult> Results { get; private set; } = [];

    public async Task ValidateAsync()
    {
        this.Results = await this.validators.ValidateAsync(this.settings);
        await this.presentationService.CloseDialogAsync(true);
    }
}
