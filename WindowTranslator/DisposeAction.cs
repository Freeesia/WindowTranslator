namespace WindowTranslator;
public readonly struct DisposeAction : IDisposable
{
    private readonly Action action;
    public DisposeAction(Action action)
        => this.action = action;

    public void Dispose() => this.action();
}
