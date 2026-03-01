using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Messaging;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowTranslator.Stores;
using static Windows.Win32.PInvoke;

namespace WindowTranslator.Modules.Main;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class CaptureMainWindow
{
    private readonly IProcessInfoStore processInfo;
    private readonly DispatcherTimer timer = new();

    public CaptureMainWindow(IProcessInfoStore processInfo)
    {
        InitializeComponent();
        this.processInfo = processInfo;
        this.timer.Interval = TimeSpan.FromMilliseconds(10);
        this.timer.Tick += (s, e) => CheckTargetWindow();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        this.timer.Start();
        StrongReferenceMessenger.Default.Register<CaptureMainWindow, CloseMessage>(this, CloseIfViewModel);
    }

    private void CheckTargetWindow()
    {
        // ディスプレイの場合はウィンドウチェックをスキップ
        if (this.processInfo.IsMonitor)
        {
            return;
        }
        
        var windowInfo = new WINDOWINFO() { cbSize = (uint)Marshal.SizeOf<WINDOWINFO>() };
        if (!GetWindowInfo((HWND)this.processInfo.MainWindowHandle, ref windowInfo))
        {
            this.Close();
            return;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        this.timer.Stop();
        StrongReferenceMessenger.Default.Unregister<CloseMessage>(this);
    }

    private static void CloseIfViewModel(CaptureMainWindow w, CloseMessage m)
    {
        if (w.DataContext == m.ViewModel)
        {
            w.Close();
        }
    }
}
