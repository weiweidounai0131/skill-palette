using Microsoft.Win32;

namespace SkillPalette_Windows.Services;

/// <summary>
/// Registers the portable executable for the current user's Windows sign-in.
/// No administrator permission is required.
/// </summary>
internal sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SkillPalette";

    public void SetEnabled(bool isEnabled)
    {
        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException("无法访问当前用户的 Windows 启动项。");

        if (isEnabled)
        {
            runKey.SetValue(ValueName, BuildCommand(), RegistryValueKind.String);
        }
        else
        {
            runKey.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string BuildCommand()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("无法确定 Skill Palette 的启动位置。");
        }

        return $"\"{executablePath}\" --background";
    }
}
