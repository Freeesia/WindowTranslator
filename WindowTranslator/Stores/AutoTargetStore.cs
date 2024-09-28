using Microsoft.Extensions.Options;
using System.IO;
using System.Text.Json;

namespace WindowTranslator.Stores;
sealed class AutoTargetStore : IAutoTargetStore, IDisposable
{
    private readonly static string targetsPath = Path.Combine(PathUtility.UserDir, "autoTargets.json");
    private readonly IOptionsMonitor<CommonSettings> settings;
    private readonly List<IntPtr> activeWindows = [];

    public ISet<string> AutoTargets { get; }

    public AutoTargetStore(IOptionsMonitor<CommonSettings> userSettings)
    {
        this.settings = userSettings;
        if (File.Exists(targetsPath))
        {
            using var fs = File.OpenRead(targetsPath);
            this.AutoTargets = JsonSerializer.Deserialize<HashSet<string>>(fs) ?? [];
        }
        else
        {
            this.AutoTargets = new HashSet<string>();
        }
    }

    public void Dispose()
    {
        var settings = this.settings.CurrentValue;
        if (settings.IsEnableAutoTarget)
        {
            Save();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(PathUtility.UserDir);
        using var fs = File.Create(targetsPath);
        JsonSerializer.Serialize(fs, this.AutoTargets.Distinct());
    }

    public bool IsAutoTarget(IntPtr windowHandle, string name)
    {
        if (this.activeWindows.Contains(windowHandle))
        {
            return false;
        }
        var settings = this.settings.CurrentValue;
        return settings.IsEnableAutoTarget && this.AutoTargets.Contains(name);
    }

    public void AddTarget(IntPtr windowHandle, string name)
    {
        this.AutoTargets.Add(name);
        this.activeWindows.Add(windowHandle);
    }

    public void RemoveTarget(IntPtr windowHandle)
        => this.activeWindows.Remove(windowHandle);
}

public interface IAutoTargetStore
{
    ISet<string> AutoTargets { get; }
    void AddTarget(IntPtr windowHandle, string name);
    bool IsAutoTarget(IntPtr windowHandle, string name);
    void RemoveTarget(IntPtr windowHandle);
    void Save();
}