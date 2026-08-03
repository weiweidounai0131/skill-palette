namespace SkillPalette.Core.Skills;

public enum SkillSource
{
    Codex,
    Agents,
    Custom
}

public sealed record SkillRoot(SkillSource Source, string Path)
{
    public static IReadOnlyList<SkillRoot> CreateDefault()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return [
            new(SkillSource.Codex, System.IO.Path.Combine(profile, ".codex", "skills")),
            new(SkillSource.Agents, System.IO.Path.Combine(profile, ".agents", "skills"))
        ];
    }
}

public sealed record SkillDefinition(
    string Id,
    string Name,
    string Description,
    SkillSource Source,
    string RootPath,
    string RelativePath,
    string FilePath,
    string Family);

public sealed record SkillScanWarning(string Path, string Message);

public sealed record SkillCatalogSnapshot(
    IReadOnlyList<SkillDefinition> Skills,
    IReadOnlyList<SkillScanWarning> Warnings,
    DateTimeOffset CompletedAt);
