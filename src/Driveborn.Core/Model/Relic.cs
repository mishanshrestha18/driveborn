namespace Driveborn.Core.Model;

/// <summary>
/// Loot. A relic is a pointer back to a real file the player found - the
/// collection log is therefore a museum of their own drive. We store the path
/// and metadata, never file contents.
/// </summary>
public sealed class Relic
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string SourcePath { get; init; }
    public required ExtensionClass Class { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime LastWriteUtc { get; init; }
    public required int Value { get; init; }

    /// <summary>Set for Raster relics so the UI can show the real thumbnail.</summary>
    public bool CanRenderThumbnail => Class == ExtensionClass.Raster;

    /// <summary>Set for Document relics: the real first line found in the file.</summary>
    public string? Inscription { get; init; }

    /// <summary>True if found inside a Rift - Rift relics are worth more.</summary>
    public bool FromRift { get; init; }
}
