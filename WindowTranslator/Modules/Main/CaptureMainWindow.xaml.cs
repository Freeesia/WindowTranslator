using Microsoft.VisualStudio.Threading;
using System.Windows;
using System.Windows.Threading;
using WindowTranslator.Stores;
using static PInvoke.User32;

namespace WindowTranslator.Modules.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class CaptureMainWindow : Window
{
    private readonly IProcessInfoStore processInfo;
    private readonly IPresentationService presentationService;
    private readonly DispatcherTimer timer = new();

    public CaptureMainWindow(IProcessInfoStore processInfo, IPresentationService presentationService)
    {
        InitializeComponent();
        this.processInfo = processInfo;
        this.presentationService = presentationService;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => CheckTargetWindow();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.timer.Start();
    }

    private void CheckTargetWindow()
    {
        var windowInfo = WINDOWINFO.Create();
        if (!GetWindowInfo(this.processInfo.MainWindowHangle, ref windowInfo))
        {
            this.timer.Stop();
            this.presentationService.CloseWindowAsync(this).Forget();
            return;
        }
    }
}
