namespace WindowTranslator.Stores;

public sealed class ProcessInfoStore : IProcessInfoStoreInternal
{
    public IntPtr MainWindowHandle { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void SetTargetProcess(IntPtr mainWindowHandle, string name)
    {
        this.MainWindowHandle = mainWindowHandle;
        this.Name = name;
    }
}

interface IProcessInfoStoreInternal : IProcessInfoStore
{
    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
