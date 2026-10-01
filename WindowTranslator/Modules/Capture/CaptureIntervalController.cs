namespace WindowTranslator.Modules.Capture;

internal sealed class CaptureIntervalController(double seconds, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private long? completedTimestamp;

    public bool CanStart => this.completedTimestamp is not { } completed
        || this.timeProvider.GetElapsedTime(completed).TotalSeconds >= seconds;

    public void Complete() => this.completedTimestamp = this.timeProvider.GetTimestamp();

    public void Reset() => this.completedTimestamp = null;
}
