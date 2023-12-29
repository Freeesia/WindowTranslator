namespace WindowTranslator.ComponentModel;
public readonly struct DisposeAction : IDisposable
{
    private readonly Action action;
    public DisposeAction(Action action)
        => this.action = action;

    public void Dispose() => action();
}
