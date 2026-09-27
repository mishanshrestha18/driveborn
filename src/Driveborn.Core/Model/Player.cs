namespace Driveborn.Core.Model;

/// <summary>
/// The Reader - a process crawling the file system. Integrity is HP, Cycles are
/// action points, and the parser loadout decides which folders are survivable.
/// </summary>
public sealed class Player
{
    public int MaxIntegrity { get; set; } = GameRules.StartingIntegrity;
    public int Integrity { get; set; } = GameRules.StartingIntegrity;
    public int Cycles { get; set; } = GameRules.CyclesPerTurn;
    public int MaxCycles { get; set; } = GameRules.CyclesPerTurn;

    public int Level { get; set; } = 1;
    public int Xp { get; set; }

    public GridPoint Position { get; set; }

    /// <summary>Equipped parsers, capped at <see cref="GameRules.ParserSlots"/>.</summary>
    public List<ParserTool> Parsers { get; } = new();

    /// <summary>Loot picked up this run. Lost entirely if the run ends in death.</summary>
    public List<Relic> Carried { get; } = new();

    public int CacheSlots { get; set; } = GameRules.BaseCacheSlots;

    public bool IsAlive => Integrity > 0;
    public bool CacheFull => Carried.Count >= CacheSlots;

    public int XpToNextLevel => 60 * Level * Level;

    public ParserTool? ParserInSlot(int slot) =>
        slot >= 0 && slot < Parsers.Count ? Parsers[slot] : null;

    /// <summary>Best parser the player is carrying for a given target family.</summary>
    public ParserTool? BestParserFor(ExtensionClass target) =>
        Parsers.Where(p => p.Matches(target)).OrderByDescending(p => p.Level).FirstOrDefault()
        ?? Parsers.OrderByDescending(p => p.Level).FirstOrDefault();

    public void BeginTurn() => Cycles = MaxCycles;

    /// <summary>Awards XP and levels up, returning true if a level was gained.</summary>
    public bool AwardXp(int amount)
    {
        Xp += amount;
        var levelled = false;
        while (Xp >= XpToNextLevel)
        {
            Xp -= XpToNextLevel;
            Level++;
            MaxIntegrity += 12;
            Integrity = MaxIntegrity;
            levelled = true;
        }
        return levelled;
    }

    public static Player NewReader()
    {
        var p = new Player();
        p.Parsers.Add(ParserTool.Starter());
        return p;
    }
}
