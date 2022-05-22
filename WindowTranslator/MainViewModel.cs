using CommunityToolkit.Mvvm.ComponentModel;

namespace WindowTranslator;

[ObservableObject]
public sealed partial class MainViewModel
{
    public IntPtr WindowHandle { get; }

    [ObservableProperty]
    private string ocrText = string.Empty;

    public MainViewModel(IntPtr windowHandle)
    {
        this.WindowHandle = windowHandle;
    }
}
