using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PropertyTools.DataAnnotations;
using WindowTranslator.ComponentModel;
using WindowTranslator.Plugin.OrcaRouterPlugin.Properties;
using BrowsableAttribute = System.ComponentModel.BrowsableAttribute;

namespace WindowTranslator.Plugin.OrcaRouterPlugin;

public partial class OrcaRouterOptions : ObservableObject, IPluginParam, IDynamicItemsSource
{
    private CancellationTokenSource? signInCancellation;
    private string? status;

    [property: Browsable(false)]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [NotifyPropertyChangedFor(nameof(Status))]
    [ObservableProperty]
    private string? apiKey;

    [Display(Order = 1)]
    [DynamicItemsSource]
    public string Model { get; set; } = OrcaRouterModels.AutoModel;

    [Display(Order = 2)]
    [JsonIgnore]
    [ReadOnly(true)]
    public string Status => this.status ?? Resources.Text(string.IsNullOrEmpty(this.ApiKey) ? "SignedOut" : "SignedIn");

    [Display(Order = 3)]
    [Height(120)]
    [DataType(DataType.MultilineText)]
    public string? TranslateContext { get; set; }

    [Display(Order = 4)]
    [FileExtensions(Extensions = ".csv")]
    [InputFilePath(".csv", "CSV (.csv)|*.csv")]
    public string? GlossaryPath { get; set; }

    // 認証中にもう一度押した場合は、その認証をキャンセルする。
    [property: Display(Order = 0)]
    [property: JsonIgnore]
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task AccountAsync()
    {
        if (this.signInCancellation is { } pending)
        {
            pending.Cancel();
            return;
        }
        if (!string.IsNullOrEmpty(this.ApiKey))
        {
            this.ApiKey = null;
            SetStatus(null);
            return;
        }
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        this.signInCancellation = cancellation;
        SetStatus(Resources.Text("SigningIn"));
        try
        {
            var key = await OrcaRouterAuthentication.SignInAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            this.ApiKey = key;
            SetStatus(null);
        }
        catch (OperationCanceledException)
        {
            SetStatus(Resources.Text("SignInCancelled"));
        }
        catch (Exception)
        {
            // 認証レスポンスやキーを UI・ログへ出さない。
            SetStatus(Resources.Text("SignInFailed"));
        }
        finally
        {
            this.signInCancellation = null;
        }
    }

    private void SetStatus(string? value)
    {
        this.status = value;
        OnPropertyChanged(nameof(Status));
    }

    public async ValueTask<IReadOnlyList<DynamicItem>> GetItemsAsync(string propertyName, CancellationToken cancellationToken)
    {
        if (propertyName != nameof(Model))
        {
            return [];
        }
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        return await OrcaRouterModels.GetItemsAsync(client, this.ApiKey, this.Model, cancellationToken);
    }
}
