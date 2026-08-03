using System.Runtime.InteropServices;

namespace SkillPalette_Windows.Services;

/// <summary>
/// Keeps Skill Palette available from the notification area while the main window is hidden.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint TrayCallbackMessage = 0x8000 + 87;
    private const uint WmLButtonDoubleClick = 0x0203;
    private const uint WmRButtonUp = 0x0205;
    private const uint WmContextMenu = 0x007B;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCommand = 0x0100;
    private const uint MfString = 0x0000;
    private const uint ImageIcon = 1;
    private const uint LrLoadFromFile = 0x0010;

    private readonly IntPtr _windowHandle;
    private readonly IntPtr _iconHandle;
    private readonly SubclassProc _subclassProc;
    private readonly Action[] _actions;
    private bool _disposed;

    public TrayIconService(
        IntPtr windowHandle,
        Action openSkillLibrary,
        Action rescan,
        Action openSettings,
        Action openAbout,
        Action exit)
    {
        _windowHandle = windowHandle;
        _actions = [openSkillLibrary, rescan, openSettings, openAbout, exit];
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        _iconHandle = LoadImage(
            IntPtr.Zero,
            iconPath,
            ImageIcon,
            GetSystemMetrics(49),
            GetSystemMetrics(50),
            LrLoadFromFile);
        if (_iconHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("无法加载 Skill Palette 图标。");
        }

        _subclassProc = WindowProcedure;
        if (!SetWindowSubclass(_windowHandle, _subclassProc, TrayIconId, UIntPtr.Zero))
        {
            DestroyIcon(_iconHandle);
            throw new InvalidOperationException("无法初始化 Skill Palette 托盘菜单。");
        }

        var iconData = CreateIconData();
        if (!ShellNotifyIcon(0, ref iconData))
        {
            RemoveWindowSubclass(_windowHandle, _subclassProc, TrayIconId);
            DestroyIcon(_iconHandle);
            throw new InvalidOperationException("无法将 Skill Palette 添加到通知区域。");
        }

        iconData.uVersion = 4;
        ShellNotifyIcon(4, ref iconData);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        var iconData = CreateIconData();
        ShellNotifyIcon(2, ref iconData);
        RemoveWindowSubclass(_windowHandle, _subclassProc, TrayIconId);
        DestroyIcon(_iconHandle);
    }

    private IntPtr WindowProcedure(
        IntPtr window,
        uint message,
        UIntPtr wParam,
        IntPtr lParam,
        UIntPtr subclassId,
        UIntPtr referenceData)
    {
        if (message == TrayCallbackMessage)
        {
            // NOTIFYICON_VERSION_4 puts the notification code in the low word
            // and the icon identifier in the high word.
            var mouseMessage = unchecked((uint)lParam.ToInt64()) & 0xFFFF;
            if (mouseMessage == WmLButtonDoubleClick)
            {
                _actions[2]();
            }
            else if (mouseMessage == WmRButtonUp || mouseMessage == WmContextMenu)
            {
                ShowMenu();
            }

            return IntPtr.Zero;
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        try
        {
            AppendMenu(menu, MfString, 1, "Skill 库");
            AppendMenu(menu, MfString, 2, "重新扫描");
            AppendMenu(menu, MfString, 3, "设置");
            AppendMenu(menu, MfString, 4, "关于");
            AppendMenu(menu, MfString, 5, "退出");

            GetCursorPos(out var point);
            SetForegroundWindow(_windowHandle);
            var command = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
            if (command is >= 1 and <= 5)
            {
                _actions[command - 1]();
            }
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private NOTIFYICONDATA CreateIconData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _windowHandle,
        uID = TrayIconId,
        uFlags = 0x0001 | 0x0002 | 0x0004,
        uCallbackMessage = TrayCallbackMessage,
        hIcon = _iconHandle,
        szTip = "Skill Palette",
    };

    private delegate IntPtr SubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr subclassId, UIntPtr referenceData);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;

        public uint uVersion
        {
            set => uTimeoutOrVersion = value;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ShellNotifyIcon(uint message, ref NOTIFYICONDATA data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int desiredWidth, int desiredHeight, uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool SetWindowSubclass(IntPtr window, SubclassProc procedure, uint subclassId, UIntPtr referenceData);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc procedure, uint subclassId);

    [DllImport("comctl32.dll")]
    private static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, uint itemId, string text);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rectangle);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
