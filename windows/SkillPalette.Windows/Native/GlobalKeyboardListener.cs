using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SkillPalette_Windows.Native;

internal sealed class GlobalKeyboardListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint Vk3 = 0x33;
    private const int VkShift = 0x10;
    private const uint LlkhfInjected = 0x00000010;

    private HookProcedure? _procedure;
    private IntPtr _hook;
    private Func<string, bool> _processMatcher = DefaultProcessMatcher;
    private int _triggerCharacter = '#';

    public event Action<ForegroundTarget, char>? Triggered;

    public bool IsRunning => _hook != IntPtr.Zero;

    public void UpdateProcessMatcher(Func<string, bool> processMatcher) =>
        Volatile.Write(ref _processMatcher, processMatcher ?? DefaultProcessMatcher);

    public void UpdateTriggerCharacter(char triggerCharacter) => Volatile.Write(ref _triggerCharacter, triggerCharacter);

    public void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _procedure = HookCallback;
        _hook = SetWindowsHookEx(WhKeyboardLl, _procedure, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            _procedure = null;
            throw new InvalidOperationException($"无法安装低级键盘监听器。Win32 错误码：{Marshal.GetLastWin32Error()}。");
        }
    }

    public void Stop()
    {
        if (_hook != IntPtr.Zero)
        {
            _ = UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
        _procedure = null;
    }

    public void Dispose() => Stop();

    private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || (message.ToInt32() != WmKeyDown && message.ToInt32() != WmSysKeyDown))
            return CallNextHookEx(_hook, code, message, data);

        var keyboard = Marshal.PtrToStructure<KbdLlHookStruct>(data);
        var triggerCharacter = (char)Volatile.Read(ref _triggerCharacter);
        if ((keyboard.Flags & LlkhfInjected) != 0 || !IsTriggerCharacter(keyboard, triggerCharacter))
            return CallNextHookEx(_hook, code, message, data);

        if (!TryGetForegroundTarget(Volatile.Read(ref _processMatcher), out var target))
            return CallNextHookEx(_hook, code, message, data);

        Triggered?.Invoke(target, triggerCharacter);
        return (IntPtr)1;
    }

    private static bool IsTriggerCharacter(KbdLlHookStruct keyboard, char triggerCharacter)
    {
        if (triggerCharacter == '#' && keyboard.VirtualKeyCode == Vk3 && IsKeyDown(VkShift)) return true;
        var state = new byte[256];
        if (!GetKeyboardState(state)) return false;
        var foreground = GetForegroundWindow();
        var layout = GetKeyboardLayout(GetWindowThreadProcessId(foreground, out _));
        var buffer = new StringBuilder(8);
        var translated = ToUnicodeEx(keyboard.VirtualKeyCode, keyboard.ScanCode, state, buffer, buffer.Capacity, 0, layout);
        return translated == 1 && buffer[0] == triggerCharacter;
    }

    private static bool IsKeyDown(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static bool TryGetForegroundTarget(Func<string, bool> processMatcher, out ForegroundTarget target)
    {
        target = default;
        var window = GetForegroundWindow();
        if (window == IntPtr.Zero) return false;
        _ = GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return false;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            if (process.ProcessName.Equals("SkillPalette", StringComparison.OrdinalIgnoreCase)
                || process.ProcessName.Equals("SkillPalette.Windows", StringComparison.OrdinalIgnoreCase)
                || !processMatcher(process.ProcessName)) return false;
            target = new ForegroundTarget(window, processId, process.ProcessName);
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool DefaultProcessMatcher(string processName) =>
        processName.Contains("codex", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("chatgpt", StringComparison.OrdinalIgnoreCase);

    private delegate IntPtr HookProcedure(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct KbdLlHookStruct { public uint VirtualKeyCode; public uint ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProcedure procedure, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? moduleName);
    [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] keyState);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int ToUnicodeEx(uint virtualKey, uint scanCode, byte[] keyState, StringBuilder buffer, int bufferSize, uint flags, IntPtr keyboardLayout);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}

internal readonly record struct ForegroundTarget(IntPtr WindowHandle, uint ProcessId, string ProcessName);
