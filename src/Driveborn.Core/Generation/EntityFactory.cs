using Driveborn.Core.Model;
using Driveborn.Core.Scanning;

namespace Driveborn.Core.Generation;

/// <summary>
/// Turns one real file into one creature. All stats come from metadata that the
/// player can verify by hovering the entity - size becomes bulk, extension
/// becomes armour, age becomes aggression.
/// </summary>
public static class EntityFactory
{
    public static Entity FromFile(FileEntry file, DateTime nowUtc, bool isBoss = false)
    {
        var rng = new DeterministicRng(Seed.ForFile(file));
        var cls = ExtensionMap.Classify(file.Extension);
        var temperament = TemperamentFor(file.LastWriteUtc, nowUtc);
        var kind = KindFor(cls, temperament, file, isBoss, rng);

        var hp = HealthFor(file.SizeBytes, isBoss);
        var footprint = FootprintFor(file.SizeBytes);
        var armour = GameRules.ArmourFor(cls) + (isBoss ? 3 : 0);

        // Power scales with bulk but far more gently than HP, so big things are
        // walls to chew through rather than one-shot machines.
        var power = Math.Clamp(3 + (int)(Math.Sqrt(hp) * 0.9) + rng.Next(0, 3), 3, 60);

        return new Entity
        {
            Id = $"{Seed.ForFile(file):x16}",
            SourcePath = file.FullPath,
            Name = NameFor(file, cls, kind),
            Kind = kind,
            Class = cls,
            Temperament = temperament,
            SizeBytes = file.SizeBytes,
            LastWriteUtc = file.LastWriteUtc,
            MaxHp = hp,
            Hp = hp,
            Armour = armour,
            Power = power,
            Footprint = footprint,
            ReadCost = ReadCostFor(footprint),
            Hidden = file.IsHidden && kind == EntityKind.Wraith,
            Awake = temperament != Temperament.Dormant
        };
    }

    /// <summary>
    /// Folds a pile of tiny files into one swarm body, so a folder with 4000
    /// cache files is one interesting fight instead of 4000 boring ones.
    /// </summary>
    public static Entity SwarmFrom(IReadOnlyList<FileEntry> members, string folderPath, DateTime nowUtc)
    {
        var rng = new DeterministicRng(Seed.Combine(Seed.ForFolder(folderPath), (ulong)members.Count));
        var totalBytes = members.Sum(m => m.SizeBytes);
        var newest = members.Max(m => m.LastWriteUtc);
        var cls = members
            .GroupBy(m => ExtensionMap.Classify(m.Extension))
            .OrderByDescending(g => g.Count())
            .First().Key;

        var hp = Math.Clamp(12 * members.Count, GameRules.MinHp, GameRules.MaxHp);

        return new Entity
        {
            Id = $"swarm-{Seed.ForFolder(folderPath):x8}-{members.Count}",
            SourcePath = folderPath,
            Name = $"{ExtensionMap.TitleFor(cls, EntityKind.Swarm)} of {members.Count}",
            Kind = EntityKind.Swarm,
            Class = cls,
            Temperament = TemperamentFor(newest, nowUtc),
            SizeBytes = totalBytes,
            LastWriteUtc = newest,
            MaxHp = hp,
            Hp = hp,
            Armour = 0,
            Power = Math.Clamp(2 + members.Count / 2, 2, 24),
            Footprint = 1,
            ReadCost = 1,
            Stack = members.Count,
            Awake = true
        };
    }

    public static Temperament TemperamentFor(DateTime lastWriteUtc, DateTime nowUtc)
    {
        var age = nowUtc - lastWriteUtc;
        if (age >= GameRules.DormantAge) return Temperament.Dormant;
        if (age <= GameRules.HotAge) return Temperament.Hot;
        return Temperament.Warm;
    }

    /// <summary>
    /// HP grows with the file's order of magnitude, not its raw size - otherwise
    /// a 5 GB ISO would have a million hit points.
    /// </summary>
    public static int HealthFor(long sizeBytes, bool isBoss = false)
    {
        var bytes = Math.Max(sizeBytes, 1024);
        var magnitude = Math.Log10(bytes) - 2.0;
        var hp = GameRules.HpBase * Math.Pow(GameRules.HpGrowth, magnitude);
        if (isBoss) hp *= GameRules.BossHpMultiplier;
        return (int)Math.Clamp(Math.Round(hp), GameRules.MinHp, GameRules.MaxHp * (isBoss ? 3 : 1));
    }

    /// <summary>Edge length in tiles. Bulk is physical: big files block the room.</summary>
    public static int FootprintFor(long sizeBytes) => sizeBytes switch
    {
        < GameRules.Footprint2Bytes => 1,
        < GameRules.Footprint3Bytes => 2,
        < GameRules.Footprint4Bytes => 3,
        < GameRules.Footprint5Bytes => 4,
        _ => 5
    };

    /// <summary>Cycles spent per strike. Chewing a huge file eats the whole turn.</summary>
    public static int ReadCostFor(int footprint) => Math.Clamp(1 + (footprint - 1) / 2, 1, 3);

    private static EntityKind KindFor(ExtensionClass cls, Temperament temperament,
        FileEntry file, bool isBoss, DeterministicRng rng)
    {
        if (isBoss) return EntityKind.Boss;
        if (file.IsReadOnly) return EntityKind.Vault;
        if (file.IsHidden) return EntityKind.Wraith;

        if (cls == ExtensionClass.Raster && rng.Chance(0.75)) return EntityKind.Treasure;
        if (temperament == Temperament.Dormant) return EntityKind.Undead;

        return cls switch
        {
            ExtensionClass.Binary or ExtensionClass.Volume => EntityKind.Construct,
            ExtensionClass.Document or ExtensionClass.Data => EntityKind.Scribe,
            _ => EntityKind.Construct
        };
    }

    private static string NameFor(FileEntry file, ExtensionClass cls, EntityKind kind)
    {
        var stem = Path.GetFileNameWithoutExtension(file.Name);
        if (stem.Length > 28) stem = stem[..28] + "...";
        return $"{stem} · {ExtensionMap.TitleFor(cls, kind)}";
    }
}
