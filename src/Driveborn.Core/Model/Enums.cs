namespace Driveborn.Core.Model;

/// <summary>
/// Broad family a file belongs to, derived from its extension. Drives armour,
/// attack pattern and which <see cref="ParserTool"/> counters it.
/// </summary>
public enum ExtensionClass
{
    Unknown = 0,
    Binary,     // .exe .dll .sys .so   - armoured constructs
    Archive,    // .zip .rar .7z        - split into children on death
    Volume,     // .iso .vmdk .vhd      - immobile raid bosses
    Raster,     // .jpg .png .gif       - treasure, renders a real thumbnail
    Video,      // .mp4 .mkv .mov
    Audio,      // .mp3 .flac .wav
    Document,   // .pdf .docx .txt .md  - ranged casters, carry real lore
    Code,       // .cs .js .py .rs
    Data        // .json .csv .db .xml
}

/// <summary>How awake an entity is, derived purely from last-write time.</summary>
public enum Temperament
{
    /// <summary>Older than <see cref="GameRules.DormantAge"/>. Never acts until damaged.</summary>
    Dormant = 0,

    /// <summary>Ordinary file. One action per round.</summary>
    Warm,

    /// <summary>Touched in the last day. Two actions per round.</summary>
    Hot
}

/// <summary>The role an entity plays on the grid.</summary>
public enum EntityKind
{
    Construct = 0, // armoured, slow, high HP
    Scribe,        // ranged caster
    Swarm,         // many tiny bodies
    Mimic,         // duplicate files, disguised as loot
    Undead,        // dormant elite
    Wraith,        // hidden until revealed
    Treasure,      // not hostile - openable
    Vault,         // read-only file, needs a key
    Boss           // largest file on the floor
}

/// <summary>What a tile contains for rendering and movement.</summary>
public enum TileKind
{
    Floor = 0,
    Wall,
    DoorDown,   // a subfolder
    StairsUp,   // the parent folder
    Rubble
}
