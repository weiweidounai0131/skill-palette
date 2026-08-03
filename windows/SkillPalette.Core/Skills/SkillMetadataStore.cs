using System.Text.Json;

namespace SkillPalette.Core.Skills;

/// <summary>Stores only user curation metadata, never the SKILL.md content.</summary>
public sealed class SkillMetadataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };
    private readonly string _filePath;

    public SkillMetadataStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SkillPalette",
            "skill-metadata.json");
    }

    public async Task<Dictionary<string, SkillMetadata>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, SkillMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var loaded = await JsonSerializer.DeserializeAsync<Dictionary<string, SkillMetadata>>(stream, SerializerOptions, cancellationToken);
        return loaded is null
            ? new Dictionary<string, SkillMetadata>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, SkillMetadata>(loaded, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(IReadOnlyDictionary<string, SkillMetadata> metadata, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, metadata, SerializerOptions, cancellationToken);
            }

            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

public sealed class SkillMetadata
{
    public bool IsFavorite { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public List<string> Tags { get; set; } = [];
}
