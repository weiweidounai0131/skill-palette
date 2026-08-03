using System.Text.Json;

namespace SkillPalette_Windows.Services;

internal sealed class TargetRuleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SkillPalette");
    private string FilePath => Path.Combine(_directory, "target-rules.json");

    public async Task<TargetRuleSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return TargetRuleSettings.CreateDefault();
            await using var stream = File.OpenRead(FilePath);
            var settings = await JsonSerializer.DeserializeAsync<TargetRuleSettings>(stream, JsonOptions);
            return settings is { Rules.Count: > 0 } ? settings.Normalize() : TargetRuleSettings.CreateDefault();
        }
        catch { return TargetRuleSettings.CreateDefault(); }
    }

    public async Task SaveAsync(TargetRuleSettings settings)
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

internal sealed class TargetRuleSettings
{
    public bool TargetOnlyMode { get; set; } = true;
    public List<TargetAppRule> Rules { get; set; } = [];

    public static TargetRuleSettings CreateDefault() => new()
    {
        Rules =
        [
            new TargetAppRule { Id = "codex", ProcessName = "codex", IsBuiltIn = true, IsEnabled = true },
            new TargetAppRule { Id = "chatgpt", ProcessName = "chatgpt", IsBuiltIn = true, IsEnabled = true }
        ]
    };

    public TargetRuleSettings Normalize()
    {
        Rules = Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.ProcessName))
            .Select(rule =>
            {
                rule.ProcessName = NormalizeProcessName(rule.ProcessName);
                return rule;
            })
            .GroupBy(rule => rule.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        return this;
    }

    public bool Matches(string processName) => !TargetOnlyMode || Rules.Any(rule => rule.IsEnabled && string.Equals(rule.ProcessName, NormalizeProcessName(processName), StringComparison.OrdinalIgnoreCase));

    public static string NormalizeProcessName(string processName) => Path.GetFileNameWithoutExtension(processName.Trim());
}

internal sealed class TargetAppRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsEnabled { get; set; } = true;
    public string ProcessName { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }
}
