namespace WindowTranslator.ComponentModel;
public readonly struct DisposeAction(Action action) : IDisposable
{
    private readonly Action action = action;

    public void Dispose() => action();
}
