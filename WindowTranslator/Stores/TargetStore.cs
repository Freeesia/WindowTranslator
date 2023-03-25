using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace WindowTranslator.Stores;
public sealed class TargetStore : ITargetStore, IDisposable
{
    private readonly static string targetsPath = Path.Combine(PathUtility.UserDir, "autoTargets.json");
    private readonly IOptionsMonitor<UserSettings> userSettings;
    private readonly List<string> targets;
    private readonly List<IntPtr> activeWindows = new();

    public TargetStore(IOptionsMonitor<UserSettings> userSettings)
    {
        this.userSettings = userSettings;
        if (File.Exists(targetsPath))
        {
            using var fs = File.OpenRead(targetsPath);
            this.targets = JsonSerializer.Deserialize<List<string>>(fs) ?? new();
        }
        else
        {
            this.targets = new();
        }
    }

    public void Dispose()
    {
        var settings = this.userSettings.CurrentValue;
        if (settings.IsEnableAutoTarget)
        {
            Directory.CreateDirectory(PathUtility.UserDir);
            using var fs = File.Create(targetsPath);
            JsonSerializer.Serialize(fs, this.targets.Distinct());
        }
    }

    public bool IsTarget(IntPtr windowHandle, string name)
    {
        if (this.activeWindows.Contains(windowHandle))
        {
            return false;
        }
        var settings = this.userSettings.CurrentValue;
        if (settings.AutoTargets.Contains(name))
        {
            return true;
        }
        return settings.IsEnableAutoTarget && this.targets.Contains(name);
    }

    public void AddTarget(IntPtr windowHandle, string name)
    {
        this.targets.Add(name);
        this.activeWindows.Add(windowHandle);
    }

    public void RemoveTarget(IntPtr windowHandle)
        => this.activeWindows.Remove(windowHandle);
}

public interface ITargetStore
{
    void AddTarget(IntPtr windowHandle, string name);
    bool IsTarget(IntPtr windowHandle, string name);
    void RemoveTarget(IntPtr windowHandle);
}