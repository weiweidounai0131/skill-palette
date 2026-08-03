using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SkillPalette_Windows.Native;

internal static class ForegroundWindowInspector
{
    public static ForegroundWindowInfo GetCurrent()
    {
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return new ForegroundWindowInfo("未检测到前台窗口", 0);
        _ = GetWindowThreadProcessId(window, out var processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return new ForegroundWindowInfo(process.ProcessName, processId);
        }
        catch { return new ForegroundWindowInfo("无法识别前台应用", processId); }
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

internal readonly record struct ForegroundWindowInfo(string ProcessName, uint ProcessId);
