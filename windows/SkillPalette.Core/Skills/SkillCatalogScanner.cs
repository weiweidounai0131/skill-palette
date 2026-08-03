using System.Text;

namespace SkillPalette.Core.Skills;

public sealed class SkillCatalogScanner
{
    public async Task<SkillCatalogSnapshot> ScanAsync(
        IEnumerable<SkillRoot>? roots = null,
        CancellationToken cancellationToken = default)
    {
        var skills = new List<SkillDefinition>();
        var warnings = new List<SkillScanWarning>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots ?? SkillRoot.CreateDefault())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(root.Path))
            {
                continue;
            }

            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                foreach (var filePath in Directory.EnumerateFiles(root.Path, "SKILL.md", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fullPath = Path.GetFullPath(filePath);
                    if (!seenPaths.Add(fullPath))
                    {
                        continue;
                    }

                    try
                    {
                        var (declaredName, description) = await SkillFrontMatterParser.ParseAsync(fullPath, cancellationToken);
                        var relativePath = Path.GetRelativePath(root.Path, fullPath);
                        var name = declaredName ?? new DirectoryInfo(Path.GetDirectoryName(fullPath)!).Name;
                        var family = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                        skills.Add(new SkillDefinition(
                            Id: fullPath.ToUpperInvariant(),
                            Name: name,
                            Description: description ?? string.Empty,
                            Source: root.Source,
                            RootPath: Path.GetFullPath(root.Path),
                            RelativePath: relativePath,
                            FilePath: fullPath,
                            Family: family));
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DecoderFallbackException)
                    {
                        warnings.Add(new SkillScanWarning(fullPath, ex.Message));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(new SkillScanWarning(root.Path, ex.Message));
            }
        }

        var prefixCounts = skills
            .Select(skill => FamilyPrefix(skill.Name))
            .Where(prefix => prefix is not null)
            .GroupBy(prefix => prefix!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        var categorized = skills
            .Select(skill => skill with { Family = CategoryName(skill, prefixCounts) });

        var ordered = categorized
            .OrderBy(skill => skill.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(skill => skill.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new SkillCatalogSnapshot(ordered, warnings, DateTimeOffset.Now);
    }

    private static string CategoryName(SkillDefinition skill, IReadOnlyDictionary<string, int> prefixCounts)
    {
        var folders = skill.RelativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .SkipLast(1)
            .ToArray();

        if (folders.Length > 1)
        {
            return string.Equals(folders[0], ".system", StringComparison.OrdinalIgnoreCase) ? "内置" : folders[0];
        }

        var prefix = FamilyPrefix(skill.Name);
        return prefix is not null && prefixCounts.GetValueOrDefault(prefix) > 1 ? prefix : "其他";
    }

    private static string? FamilyPrefix(string name)
    {
        var delimiter = name.IndexOfAny(['-', ':']);
        return delimiter > 0 ? name[..delimiter].ToLowerInvariant() : null;
    }
}
