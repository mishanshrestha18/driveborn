namespace Driveborn.Core.Model;

/// <summary>
/// Every tunable number in one place. The whole game is derived from real file
/// metadata, so balance lives or dies on these curves - keep them together.
/// </summary>
public static class GameRules
{
    // ---- Temperament thresholds -------------------------------------------
    public static readonly TimeSpan DormantAge = TimeSpan.FromDays(365 * 3);
    public static readonly TimeSpan HotAge = TimeSpan.FromHours(24);

    // ---- Health curve ------------------------------------------------------
    // HP grows exponentially in the *order of magnitude* of the file, so a 4 KB
    // note is a mosquito and a 5 GB disk image is a wall you fight around.
    public const double HpBase = 6.0;
    public const double HpGrowth = 1.9;
    public const int MinHp = 10;
    public const int MaxHp = 1200;
    public const double BossHpMultiplier = 2.5;

    // ---- Footprint (tiles occupied) ---------------------------------------
    public const long Footprint2Bytes = 16L * 1024 * 1024;    // 16 MB
    public const long Footprint3Bytes = 256L * 1024 * 1024;   // 256 MB
    public const long Footprint4Bytes = 1024L * 1024 * 1024;  // 1 GB
    public const long Footprint5Bytes = 8L * 1024 * 1024 * 1024; // 8 GB

    // ---- Player ------------------------------------------------------------
    public const int StartingIntegrity = 100;
    public const int CyclesPerTurn = 3;
    public const int ParserSlots = 3;
    public const int BaseCacheSlots = 8;

    // ---- Combat ------------------------------------------------------------
    /// <summary>Damage multiplier when a parser matches the target's class.</summary>
    public const double MatchedParserMultiplier = 2.5;

    /// <summary>Damage multiplier when it does not. Mismatched loadouts hurt.</summary>
    public const double MismatchedParserMultiplier = 0.5;

    /// <summary>A matched parser pierces armour entirely.</summary>
    public const bool MatchedParserPiercesArmour = true;

    /// <summary>Bonus multiplier for striking something that is still Dormant.</summary>
    public const double DormantStrikeMultiplier = 3.0;

    // ---- Swarms ------------------------------------------------------------
    /// <summary>Files at or below this size may be merged into a swarm.</summary>
    public const long SwarmMemberMaxBytes = 512 * 1024;
    public const int SwarmMinMembers = 4;

    // ---- Rifts -------------------------------------------------------------
    /// <summary>A folder touched inside this window becomes today's Rift.</summary>
    public static readonly TimeSpan RiftWindow = TimeSpan.FromHours(24);
    public const double RiftLootMultiplier = 2.0;

    /// <summary>Armour by family. Binary and Volume are the hard shells.</summary>
    public static int ArmourFor(ExtensionClass c) => c switch
    {
        ExtensionClass.Volume => 8,
        ExtensionClass.Binary => 6,
        ExtensionClass.Archive => 4,
        ExtensionClass.Video => 3,
        ExtensionClass.Audio => 2,
        ExtensionClass.Document => 2,
        ExtensionClass.Data => 2,
        ExtensionClass.Code => 1,
        ExtensionClass.Raster => 1,
        _ => 2
    };

    /// <summary>Actions an entity takes per round.</summary>
    public static int ActionsFor(Temperament t) => t switch
    {
        Temperament.Hot => 2,
        Temperament.Warm => 1,
        _ => 0 // Dormant things do not act until woken
    };
}
