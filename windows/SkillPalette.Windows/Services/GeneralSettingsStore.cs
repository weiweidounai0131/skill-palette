using System.Text.Json;

namespace SkillPalette_Windows.Services;

internal sealed class GeneralSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkillPalette");
    private string FilePath => Path.Combine(_directory, "general-settings.json");

    public async Task<GeneralSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return new GeneralSettings();
            await using var stream = File.OpenRead(FilePath);
            return (await JsonSerializer.DeserializeAsync<GeneralSettings>(stream, JsonOptions) ?? new GeneralSettings()).Normalize();
        }
        catch { return new GeneralSettings(); }
    }

    public async Task SaveAsync(GeneralSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var temporaryPath = FilePath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings.Normalize(), JsonOptions);
            await stream.FlushAsync();
        }
        File.Move(temporaryPath, FilePath, true);
    }
}

internal sealed class GeneralSettings
{
    public string TriggerCharacter { get; set; } = "#";
    public string InvocationPrefix { get; set; } = "@";
    public bool IsListeningEnabled { get; set; } = true;
    public bool IsLaunchAtStartupEnabled { get; set; } = true;

    public GeneralSettings Normalize()
    {
        TriggerCharacter = IsValidTrigger(TriggerCharacter) ? TriggerCharacter.Trim() : "#";
        InvocationPrefix = string.IsNullOrWhiteSpace(InvocationPrefix) ? "@" : InvocationPrefix.Trim();
        return this;
    }

    public static bool IsValidTrigger(string? value) =>
        value is { Length: 1 } && !char.IsControl(value[0]) && !char.IsWhiteSpace(value[0]);
}
