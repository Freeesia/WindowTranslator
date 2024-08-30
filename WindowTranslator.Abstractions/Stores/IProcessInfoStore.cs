namespace WindowTranslator.Stores;

public interface IProcessInfoStore
{
    IntPtr MainWindowHandle { get; }
    string Name { get; }

    void SetTargetProcess(IntPtr mainWindowHandle, string name);
}
