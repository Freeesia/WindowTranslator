namespace WindowTranslator.Stores;

public class ProcessInfoStore : IProcessInfoStore
{
    public IntPtr MainWindowHangle { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void SetTargetProcess(IntPtr mainWindowHandle, string name)
    {
        this.MainWindowHangle = mainWindowHandle;
        this.Name = name;
    }
}

public interface IProcessInfoStore
{
    IntPtr MainWindowHangle { get; }
    string Name { get; }

    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
