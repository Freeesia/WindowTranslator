namespace WindowTranslator.Stores;

public sealed class ProcessInfoStore : IProcessInfoStore, IDisposable
{
    private readonly ITargetStore targetStore;

    public IntPtr MainWindowHangle { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public ProcessInfoStore(ITargetStore targetStore)
        => this.targetStore = targetStore;

    public void SetTargetProcess(IntPtr mainWindowHandle, string name)
    {
        this.MainWindowHangle = mainWindowHandle;
        this.Name = name;
        this.targetStore.AddTarget(mainWindowHandle, name);
    }

    public void Dispose()
        => this.targetStore.RemoveTarget(this.MainWindowHangle);
}

public interface IProcessInfoStore
{
    IntPtr MainWindowHangle { get; }
    string Name { get; }

    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
