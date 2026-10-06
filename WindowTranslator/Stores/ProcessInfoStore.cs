namespace WindowTranslator.Stores;

public sealed class ProcessInfoStore : IProcessInfoStoreInternal
{
    public IntPtr TargetHandle { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void SetTarget(IntPtr targetHandle, string name)
    {
        this.TargetHandle = targetHandle;
        this.Name = name;
    }
}

interface IProcessInfoStoreInternal : IProcessInfoStore
{
    void SetTarget(IntPtr targetHandle, string name);
}
