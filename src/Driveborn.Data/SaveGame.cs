using Driveborn.Core.Model;

namespace Driveborn.Data;

/// <summary>
/// Everything that survives a run. Note what is *not* here: no file contents, no
/// thumbnails, no directory listings. Relics store a path and metadata so the
/// collection log can point back at the real file, and nothing more.
/// </summary>
public sealed class SaveGame
{
    public int Version { get; set; } = 1;

    // ---- the Reader --------------------------------------------------------
    public int Level { get; set; } = 1;
    public int Xp { get; set; }
    public int MaxIntegrity { get; set; } = GameRules.StartingIntegrity;
    public int CacheSlots { get; set; } = GameRules.BaseCacheSlots;
    public int Shards { get; set; }

    /// <summary>Parsers owned. The first three are the equipped loadout.</summary>
    public List<SavedParser> Parsers { get; set; } = new();

    // ---- world -------------------------------------------------------------
    public List<string> Territories { get; set; } = new();

    /// <summary>Folders the player has stood in, for fog-of-war on the world map.</summary>
    public List<string> ExploredFolders { get; set; } = new();

    /// <summary>Folders fully cleared of hostiles.</summary>
    public List<string> ClearedFolders { get; set; } = new();

    // ---- collection --------------------------------------------------------
    public List<SavedRelic> Collection { get; set; } = new();

    // ---- records and the daily hook ---------------------------------------
    public int DeepestFloor { get; set; }
    public long LargestFelledBytes { get; set; }
    public string? LargestFelledName { get; set; }
    public int TotalRuns { get; set; }
    public int SuccessfulExtractions { get; set; }
    public int TotalFelled { get; set; }

    public int StreakDays { get; set; }
    public int BestStreakDays { get; set; }

    /// <summary>
    /// Set once the player has finished (or skipped) the guided first descent.
    /// The coach can be replayed from the hub at any time.
    /// </summary>
    public bool TutorialCompleted { get; set; }

    /// <summary>Local date of the last completed run, used for the streak.</summary>
    public DateTime? LastPlayedLocalDate { get; set; }

    /// <summary>Rift folder already claimed today, so the bonus cannot be farmed.</summary>
    public string? ClaimedRiftPath { get; set; }
    public DateTime? ClaimedRiftLocalDate { get; set; }

    public bool HasCompletedSetup => Territories.Count > 0;

    /// <summary>Rebuilds a live player from the profile.</summary>
    public Player ToPlayer()
    {
        var player = new Player
        {
            Level = Level,
            Xp = Xp,
            MaxIntegrity = MaxIntegrity,
            Integrity = MaxIntegrity,
            CacheSlots = CacheSlots
        };

        foreach (var p in Parsers.Take(GameRules.ParserSlots))
            player.Parsers.Add(new ParserTool
            {
                Id = p.Id,
                Name = p.Name,
                Affinity = p.Affinity,
                Level = p.Level
            });

        if (player.Parsers.Count == 0) player.Parsers.Add(ParserTool.Starter());
        return player;
    }

    /// <summary>Folds a finished run back into the profile.</summary>
    public void AbsorbPlayer(Player player)
    {
        Level = player.Level;
        Xp = player.Xp;
        MaxIntegrity = player.MaxIntegrity;
        CacheSlots = player.CacheSlots;
    }

    /// <summary>
    /// Advances the daily streak. Same day is a no-op, the next day increments,
    /// anything later resets - standard loss-aversion loop, and the reason the
    /// Rift is worth opening the app for.
    /// </summary>
    public void TouchStreak(DateTime nowLocal)
    {
        var today = nowLocal.Date;
        if (LastPlayedLocalDate is null)
        {
            StreakDays = 1;
        }
        else
        {
            var last = LastPlayedLocalDate.Value.Date;
            if (last == today) return;
            StreakDays = last == today.AddDays(-1) ? StreakDays + 1 : 1;
        }

        LastPlayedLocalDate = today;
        BestStreakDays = Math.Max(BestStreakDays, StreakDays);
    }

    /// <summary>True if the streak will break unless a run happens today.</summary>
    public bool StreakAtRisk(DateTime nowLocal) =>
        LastPlayedLocalDate is { } last && last.Date == nowLocal.Date.AddDays(-1);

    public static SaveGame NewProfile()
    {
        var save = new SaveGame();
        var starter = ParserTool.Starter();
        save.Parsers.Add(new SavedParser
        {
            Id = starter.Id,
            Name = starter.Name,
            Affinity = starter.Affinity,
            Level = starter.Level
        });
        return save;
    }
}

public sealed class SavedParser
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ExtensionClass Affinity { get; set; }
    public int Level { get; set; } = 1;
}

/// <summary>A relic in the permanent collection. Path only - never contents.</summary>
public sealed class SavedRelic
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public ExtensionClass Class { get; set; }
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public int Value { get; set; }
    public string? Inscription { get; set; }
    public bool FromRift { get; set; }
    public DateTime RecoveredUtc { get; set; }

    public static SavedRelic From(Relic r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        SourcePath = r.SourcePath,
        Class = r.Class,
        SizeBytes = r.SizeBytes,
        LastWriteUtc = r.LastWriteUtc,
        Value = r.Value,
        Inscription = r.Inscription,
        FromRift = r.FromRift,
        RecoveredUtc = DateTime.UtcNow
    };
}
