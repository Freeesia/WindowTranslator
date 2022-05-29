namespace WindowTranslator.Stores;

public class ProcessInfoStore : IProcessInfoStore
{
    public IntPtr MainWindowHangle { get; private set; }
    public string FileName { get; private set; } = string.Empty;

    public void SetTargetProcess(IntPtr mainWindowHandle, string path)
    {
        this.MainWindowHangle = mainWindowHandle;
        this.FileName = path;
    }
}

public interface IProcessInfoStore
{
    IntPtr MainWindowHangle { get; }
    string FileName { get; }

    void SetTargetProcess(IntPtr mainWindowHandle, string path);
}
