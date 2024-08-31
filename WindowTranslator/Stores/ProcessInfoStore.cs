namespace WindowTranslator.Stores;

public sealed class ProcessInfoStore(ITargetStore targetStore) : IProcessInfoStoreInternal, IDisposable
{
    private readonly ITargetStore targetStore = targetStore;

    public IntPtr MainWindowHandle { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void SetTargetProcess(IntPtr mainWindowHandle, string name)
    {
        this.MainWindowHandle = mainWindowHandle;
        this.Name = name;
        this.targetStore.AddTarget(mainWindowHandle, name);
    }

    public void Dispose()
        => this.targetStore.RemoveTarget(this.MainWindowHandle);
}

interface IProcessInfoStoreInternal : IProcessInfoStore
{
    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
