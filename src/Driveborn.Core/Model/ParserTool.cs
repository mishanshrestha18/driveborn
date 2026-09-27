namespace Driveborn.Core.Model;

/// <summary>
/// The player's weapon. A parser is tuned to one <see cref="ExtensionClass"/>:
/// matched, it pierces armour and hits for <see cref="GameRules.MatchedParserMultiplier"/>;
/// mismatched, it barely scratches. Because the dungeon is the player's own disk,
/// they can reason about which folders need which loadout - that is the whole
/// strategic layer.
/// </summary>
public sealed class ParserTool
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ExtensionClass Affinity { get; init; }
    public required int Level { get; init; }

    /// <summary>Base damage before match/mismatch multipliers.</summary>
    public int BaseDamage => 12 + (Level * 8);

    public bool Matches(ExtensionClass target) => Affinity == target;

    public static ParserTool Starter() => new()
    {
        Id = "parser-binary-1",
        Name = "Binary Parser",
        Affinity = ExtensionClass.Binary,
        Level = 1
    };

    public static string DisplayFor(ExtensionClass c) => c switch
    {
        ExtensionClass.Binary => "Binary Parser",
        ExtensionClass.Raster => "Raster Parser",
        ExtensionClass.Video => "Stream Parser",
        ExtensionClass.Audio => "Waveform Parser",
        ExtensionClass.Document => "Glyph Parser",
        ExtensionClass.Archive => "Unpack Parser",
        ExtensionClass.Volume => "Mount Parser",
        ExtensionClass.Code => "Syntax Parser",
        ExtensionClass.Data => "Schema Parser",
        _ => "Null Parser"
    };
}
