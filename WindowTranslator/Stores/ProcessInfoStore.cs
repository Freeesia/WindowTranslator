namespace WindowTranslator.Stores;

public sealed class ProcessInfoStore(IAutoTargetStore targetStore) : IProcessInfoStoreInternal, IDisposable
{
    private readonly IAutoTargetStore targetStore = targetStore;

    public IntPtr MainWindowHandle { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsMonitor { get; private set; }

    public void SetTargetProcess(IntPtr mainWindowHandle, string name)
    {
        this.MainWindowHandle = mainWindowHandle;
        this.Name = name;
        this.IsMonitor = name.StartsWith("DISPLAY__", StringComparison.OrdinalIgnoreCase);
        this.targetStore.AddTarget(mainWindowHandle, name);
    }

    public void Dispose()
        => this.targetStore.RemoveTarget(this.MainWindowHandle);
}

interface IProcessInfoStoreInternal : IProcessInfoStore
{
    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
