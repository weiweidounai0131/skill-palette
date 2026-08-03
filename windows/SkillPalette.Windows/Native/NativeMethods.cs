using System.Runtime.InteropServices;

namespace SkillPalette_Windows.Native;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    internal static extern uint GetClipboardSequenceNumber();
}
