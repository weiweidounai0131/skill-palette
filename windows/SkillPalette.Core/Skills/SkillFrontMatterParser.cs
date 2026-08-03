using System.Text;

namespace SkillPalette.Core.Skills;

/// <summary>Reads only the two front-matter fields the catalog needs.</summary>
internal static class SkillFrontMatterParser
{
    private const int MaximumBytes = 128 * 1024;

    internal static async Task<(string? Name, string? Description)> ParseAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var bounded = new MemoryStream();
        var buffer = new byte[4096];
        var remaining = MaximumBytes;

        while (remaining > 0)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }

        var text = Encoding.UTF8.GetString(bounded.ToArray());
        using var reader = new StringReader(text);
        if (!string.Equals(reader.ReadLine()?.Trim(), "---", StringComparison.Ordinal))
        {
            return (null, null);
        }

        string? name = null;
        string? description = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var trimmed = line.Trim();
            if (trimmed is "---" or "...")
            {
                break;
            }

            if (TryReadScalar(trimmed, "name", out var value))
            {
                name = value;
            }
            else if (TryReadScalar(trimmed, "description", out value))
            {
                description = value;
            }
        }

        return (name, description);
    }

    private static bool TryReadScalar(string line, string key, out string? value)
    {
        value = null;
        if (!line.StartsWith(key, StringComparison.Ordinal) || line.Length <= key.Length || line[key.Length] != ':')
        {
            return false;
        }

        var scalar = line[(key.Length + 1)..].Trim();
        var commentAt = scalar.IndexOf(" #", StringComparison.Ordinal);
        if (commentAt >= 0)
        {
            scalar = scalar[..commentAt].TrimEnd();
        }

        if (scalar.Length >= 2 && ((scalar[0] == '"' && scalar[^1] == '"') || (scalar[0] == '\'' && scalar[^1] == '\'')))
        {
            scalar = scalar[1..^1];
        }

        value = string.IsNullOrWhiteSpace(scalar) ? null : scalar;
        return true;
    }
}
