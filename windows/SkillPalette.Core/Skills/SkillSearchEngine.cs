using System.Text;

namespace SkillPalette.Core.Skills;

public sealed record SkillSearchResult(SkillDefinition Skill, SkillMetadata Metadata, int Score);
public sealed record SkillSearchOptions(
    string? Query = null,
    string? Category = null,
    bool FavoritesOnly = false,
    bool PrioritizeQuickAccess = true);

public sealed class SkillSearchEngine
{
    public IReadOnlyList<SkillSearchResult> Search(
        SkillCatalogSnapshot snapshot,
        IReadOnlyDictionary<string, SkillMetadata> metadata,
        SkillSearchOptions? options = null)
    {
        options ??= new SkillSearchOptions();
        var normalizedQuery = Normalize(options.Query);
        var results = new List<SkillSearchResult>();

        foreach (var skill in snapshot.Skills)
        {
            metadata.TryGetValue(skill.Id, out var value);
            value ??= new SkillMetadata();
            if (normalizedQuery.Length == 0 && options.Category is { Length: > 0 } category && !string.Equals(skill.Family, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedQuery.Length == 0 && options.FavoritesOnly && !value.IsFavorite)
            {
                continue;
            }

            var score = Score(skill, value, normalizedQuery);
            if (score >= 0)
            {
                results.Add(new SkillSearchResult(skill, value, score));
            }
        }

        if (normalizedQuery.Length == 0 && options.PrioritizeQuickAccess)
        {
            return results.OrderByDescending(result => result.Metadata.IsFavorite)
                .ThenByDescending(result => result.Metadata.LastUsedAt)
                .ThenBy(result => result.Skill.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(result => result.Skill.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return results.OrderByDescending(result => result.Score)
            .ThenBy(result => result.Skill.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(result => result.Skill.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> Categories(SkillCatalogSnapshot snapshot) => snapshot.Skills
        .Select(skill => skill.Family)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static int Score(SkillDefinition skill, SkillMetadata metadata, string query)
    {
        if (query.Length == 0)
        {
            return 0;
        }

        var name = Normalize(skill.Name);
        if (name == query) return 1000;
        if (name.StartsWith(query, StringComparison.Ordinal)) return 900;
        if (name.Contains(query, StringComparison.Ordinal)) return 800;
        if (metadata.Tags.Any(tag => Normalize(tag).Contains(query, StringComparison.Ordinal))) return 700;
        if (Normalize(skill.Description).Contains(query, StringComparison.Ordinal)) return 600;
        return IsEligibleForFuzzyMatch(query) && IsFuzzyMatch(name, query) ? 400 : -1;
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    private static bool IsEligibleForFuzzyMatch(string query) => query.Length >= 3 && query.All(char.IsAsciiLetterOrDigit);

    private static bool IsFuzzyMatch(string name, string query)
    {
        var compactName = new string(name.Where(char.IsAsciiLetterOrDigit).ToArray());
        if (compactName.Length == 0)
        {
            return false;
        }

        var threshold = Math.Max(1, query.Length / 4);
        return EditDistance(compactName, query) <= threshold;
    }

    private static int EditDistance(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
