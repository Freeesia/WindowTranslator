using System.Runtime.Versioning;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;

namespace WindowTranslator;

/// <summary>
/// ウィンドウに関連するユーティリティメソッドを提供します。
/// </summary>
public static class WindowUtility
{
    /// <summary>
    /// 指定したウィンドウを所有するプロセスの ID を取得します。
    /// </summary>
    /// <param name="windowHandle">ウィンドウハンドル。</param>
    /// <param name="processId">取得したプロセス ID。</param>
    /// <returns>プロセス ID を取得できた場合は <see langword="true"/>。</returns>
    [SupportedOSPlatform("windows5.0")]
    public static unsafe bool TryGetProcessId(IntPtr windowHandle, out int processId)
    {
        uint nativeProcessId = 0;
        var threadId = GetWindowThreadProcessId((HWND)windowHandle, &nativeProcessId);
        processId = unchecked((int)nativeProcessId);
        return threadId != 0;
    }
}
