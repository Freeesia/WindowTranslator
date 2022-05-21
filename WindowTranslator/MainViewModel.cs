using System;
using System.Diagnostics;
using System.Threading.Tasks;
using PropertyChanged.SourceGenerator;

namespace WindowTranslator;

public partial class MainViewModel
{
    [Notify]
    private IntPtr windowHandle;

    public MainViewModel()
    {
        StartProcess();
    }

    private async void StartProcess()
    {
        var childProc = Process.Start("notepad.exe");
        while (this.WindowHandle == IntPtr.Zero)
        {
            this.WindowHandle = childProc.MainWindowHandle;
            await Task.Delay(500);
        }
    }
}
