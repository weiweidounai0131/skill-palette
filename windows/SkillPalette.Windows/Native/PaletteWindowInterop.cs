using System.Runtime.InteropServices;
using Windows.Graphics;

namespace SkillPalette_Windows.Native;

internal static class PaletteWindowInterop
{
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L, WsExAppWindow = 0x00040000L;

    internal static void HideFromTaskbar(IntPtr window)
    {
        var style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        _ = SetWindowLongPtr(window, GwlExStyle, new IntPtr((style | WsExToolWindow) & ~WsExAppWindow));
    }

    internal static PointInt32 PlaceAboveTarget(IntPtr targetWindow, int width, int height)
    {
        if (!GetWindowRect(targetWindow, out var target)) return PlaceNearCursor(width, height);
        var anchor = new Point { X = (target.Left + target.Right) / 2, Y = target.Bottom };
        var monitor = MonitorFromPoint(anchor, 2);
        var info = new MonitorInfo { Size = (uint)Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return PlaceNearCursor(width, height);
        var work = info.WorkArea;
        var minX = work.Left + 12;
        var maxX = work.Right - width - 12;
        var minY = work.Top + 12;
        var maxY = work.Bottom - height - 12;
        var x = maxX < minX ? work.Left : Math.Clamp(anchor.X - width / 2, minX, maxX);
        var y = maxY < minY ? work.Top : Math.Clamp(target.Bottom - height - 72, minY, maxY);
        return new PointInt32(x, y);
    }

    internal static void SetTopmost(IntPtr window, bool value) =>
        _ = SetWindowPos(window, value ? new IntPtr(-1) : new IntPtr(-2), 0, 0, 0, 0, 0x0001 | 0x0002);

    internal static bool IsForeground(IntPtr window) => GetForegroundWindow() == window;

    internal static void ActivateWindow(IntPtr window, IntPtr targetWindow)
    {
        var currentThread = GetCurrentThreadId();
        var foreground = GetForegroundWindow();
        var foregroundThread = foreground == IntPtr.Zero ? 0u : GetWindowThreadProcessId(foreground, out _);
        var targetThread = targetWindow == IntPtr.Zero ? 0u : GetWindowThreadProcessId(targetWindow, out _);
        var attachedToForeground = foregroundThread != 0 && foregroundThread != currentThread && AttachThreadInput(currentThread, foregroundThread, true);
        var attachedToTarget = targetThread != 0 && targetThread != currentThread && targetThread != foregroundThread && AttachThreadInput(currentThread, targetThread, true);
        _ = ShowWindow(window, 1);
        _ = SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0040);
        _ = BringWindowToTop(window);
        _ = SetActiveWindow(window);
        _ = SetForegroundWindow(window);
        _ = SetFocus(window);
        if (GetForegroundWindow() != window)
        {
            keybd_event(0x12, 0, 0, UIntPtr.Zero);
            keybd_event(0x12, 0, 0x0002, UIntPtr.Zero);
            _ = SetForegroundWindow(window);
            _ = SetActiveWindow(window);
            _ = SetFocus(window);
        }
        if (attachedToTarget) _ = AttachThreadInput(currentThread, targetThread, false);
        if (attachedToForeground) _ = AttachThreadInput(currentThread, foregroundThread, false);
    }

    private static PointInt32 PlaceNearCursor(int width, int height)
    {
        _ = GetCursorPos(out var point);
        return new PointInt32(point.X - width / 2, point.Y - height - 18);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr window);
    [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public uint Size; public Rect MonitorArea; public Rect WorkArea; public uint Flags; }
}
