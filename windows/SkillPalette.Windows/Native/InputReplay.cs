using System.Runtime.InteropServices;

namespace SkillPalette_Windows.Native;

internal static class InputReplay
{
    private const uint InputKeyboard = 1, KeyEventFKeyUp = 0x0002, KeyEventFUnicode = 0x0004;
    private const ushort VkControl = 0x11, VkV = 0x56;
    public static bool RestoreFocus(ForegroundTarget target) => SetForegroundWindow(target.WindowHandle);
    public static InputSendResult SendPasteShortcut() => Send([Keyboard(VkControl), Keyboard(VkV), Keyboard(VkV, KeyEventFKeyUp), Keyboard(VkControl, KeyEventFKeyUp)]);
    public static InputSendResult SendUnicode(char value) => Send([Keyboard(0, KeyEventFUnicode, value), Keyboard(0, KeyEventFUnicode | KeyEventFKeyUp, value)]);
    private static InputSendResult Send(Input[] inputs) => new(SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()), Marshal.GetLastWin32Error());
    private static Input Keyboard(ushort virtualKey, uint flags = 0, char unicode = '\0') => new() { Type = InputKeyboard, Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, ScanCode = unicode, Flags = flags } } };
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort ScanCode; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int Dx; public int Dy; public uint MouseData; public uint Flags; public uint Time; public IntPtr ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int inputSize);
}

internal readonly record struct InputSendResult(uint Sent, int LastError);
