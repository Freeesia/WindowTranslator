using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using WindowTranslator.ComponentModel;

namespace WindowTranslator;
/// <summary>
/// SplashWindow.xaml の相互作用ロジック
/// </summary>
public partial class SplashWindow : Window
{
    private SplashWindow()
        => InitializeComponent();

    public static IDisposable ShowSplash()
    {
        SplashWindow? w = null;
        var t = new Thread(_ =>
        {
            w = new SplashWindow();
            w.Language = XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag);
            w.Closed += (_, _) =>
            {
                w.Dispatcher.BeginInvokeShutdown(DispatcherPriority.SystemIdle);
                Dispatcher.Run();
            };
            w.ShowDialog();
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        return new DisposeAction(() =>
        {
            if (!t.IsAlive)
            {
                return;
            }
            w?.Dispatcher.Invoke(() => w?.Close());
            t.Join();
        });
    }
}
