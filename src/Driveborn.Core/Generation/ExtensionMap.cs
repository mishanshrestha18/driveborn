using Driveborn.Core.Model;

namespace Driveborn.Core.Generation;

/// <summary>Maps a real file extension onto the family that drives its combat behaviour.</summary>
public static class ExtensionMap
{
    private static readonly Dictionary<string, ExtensionClass> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".exe"] = ExtensionClass.Binary, [".dll"] = ExtensionClass.Binary,
        [".sys"] = ExtensionClass.Binary, [".so"] = ExtensionClass.Binary,
        [".msi"] = ExtensionClass.Binary, [".bin"] = ExtensionClass.Binary,
        [".pdb"] = ExtensionClass.Binary, [".lib"] = ExtensionClass.Binary,

        [".zip"] = ExtensionClass.Archive, [".rar"] = ExtensionClass.Archive,
        [".7z"] = ExtensionClass.Archive, [".tar"] = ExtensionClass.Archive,
        [".gz"] = ExtensionClass.Archive, [".xz"] = ExtensionClass.Archive,
        [".nupkg"] = ExtensionClass.Archive,

        [".iso"] = ExtensionClass.Volume, [".vmdk"] = ExtensionClass.Volume,
        [".vhd"] = ExtensionClass.Volume, [".vhdx"] = ExtensionClass.Volume,
        [".img"] = ExtensionClass.Volume, [".dmg"] = ExtensionClass.Volume,

        [".jpg"] = ExtensionClass.Raster, [".jpeg"] = ExtensionClass.Raster,
        [".png"] = ExtensionClass.Raster, [".gif"] = ExtensionClass.Raster,
        [".bmp"] = ExtensionClass.Raster, [".webp"] = ExtensionClass.Raster,
        [".tif"] = ExtensionClass.Raster, [".tiff"] = ExtensionClass.Raster,
        [".ico"] = ExtensionClass.Raster, [".psd"] = ExtensionClass.Raster,

        [".mp4"] = ExtensionClass.Video, [".mkv"] = ExtensionClass.Video,
        [".mov"] = ExtensionClass.Video, [".avi"] = ExtensionClass.Video,
        [".webm"] = ExtensionClass.Video, [".wmv"] = ExtensionClass.Video,

        [".mp3"] = ExtensionClass.Audio, [".flac"] = ExtensionClass.Audio,
        [".wav"] = ExtensionClass.Audio, [".ogg"] = ExtensionClass.Audio,
        [".m4a"] = ExtensionClass.Audio,

        [".pdf"] = ExtensionClass.Document, [".doc"] = ExtensionClass.Document,
        [".docx"] = ExtensionClass.Document, [".txt"] = ExtensionClass.Document,
        [".md"] = ExtensionClass.Document, [".rtf"] = ExtensionClass.Document,
        [".epub"] = ExtensionClass.Document, [".pptx"] = ExtensionClass.Document,

        [".cs"] = ExtensionClass.Code, [".js"] = ExtensionClass.Code,
        [".ts"] = ExtensionClass.Code, [".py"] = ExtensionClass.Code,
        [".rs"] = ExtensionClass.Code, [".go"] = ExtensionClass.Code,
        [".java"] = ExtensionClass.Code, [".cpp"] = ExtensionClass.Code,
        [".c"] = ExtensionClass.Code, [".h"] = ExtensionClass.Code,
        [".html"] = ExtensionClass.Code, [".css"] = ExtensionClass.Code,
        [".sh"] = ExtensionClass.Code, [".ps1"] = ExtensionClass.Code,

        [".json"] = ExtensionClass.Data, [".xml"] = ExtensionClass.Data,
        [".csv"] = ExtensionClass.Data, [".db"] = ExtensionClass.Data,
        [".sqlite"] = ExtensionClass.Data, [".yml"] = ExtensionClass.Data,
        [".yaml"] = ExtensionClass.Data, [".log"] = ExtensionClass.Data,
        [".xlsx"] = ExtensionClass.Data
    };

    public static ExtensionClass Classify(string extension) =>
        Map.TryGetValue(extension, out var c) ? c : ExtensionClass.Unknown;

    /// <summary>Flavour noun used when naming an entity of this family.</summary>
    public static string TitleFor(ExtensionClass c, EntityKind kind) => (c, kind) switch
    {
        (_, EntityKind.Boss) => "Sovereign",
        (_, EntityKind.Treasure) => "Cache",
        (_, EntityKind.Vault) => "Sealed Vault",
        (_, EntityKind.Mimic) => "Clone",
        (_, EntityKind.Swarm) => "Swarm",
        (_, EntityKind.Wraith) => "Wraith",
        (_, EntityKind.Undead) => "Revenant",
        (ExtensionClass.Binary, _) => "Construct",
        (ExtensionClass.Volume, _) => "Leviathan",
        (ExtensionClass.Archive, _) => "Husk",
        (ExtensionClass.Document, _) => "Scribe",
        (ExtensionClass.Code, _) => "Weaver",
        (ExtensionClass.Data, _) => "Ledger",
        (ExtensionClass.Video, _) => "Projection",
        (ExtensionClass.Audio, _) => "Echo",
        (ExtensionClass.Raster, _) => "Sigil",
        _ => "Fragment"
    };
}
