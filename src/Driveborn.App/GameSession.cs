using Driveborn.Core.Generation;
using Driveborn.Core.Model;
using Driveborn.Core.Rifts;
using Driveborn.Core.Run;
using Driveborn.Core.Scanning;
using Driveborn.Data;

namespace Driveborn.App;

/// <summary>
/// Wires the pure game core to persistence and hands the window one object to
/// talk to. Everything stateful about a session lives here so the view stays
/// thin and the core stays testable.
/// </summary>
public sealed class GameSession
{
    private readonly SaveStore _store;
    private readonly IFileSystemProbe _probe = new FileSystemProbe();

    public GameSession(SaveStore? store = null)
    {
        _store = store ?? new SaveStore();
        Save = _store.Load();

        Policy = new ScanPolicy();
        foreach (var t in Save.Territories)
            Policy.TryAddTerritory(t, out _);

        Scanner = new TerritoryScanner(_probe, Policy);
        Builder = new RoomBuilder(_probe);
        Rifts = new RiftService(_probe, Policy);
        Engine = new RunEngine(Scanner, Builder);
    }

    public SaveGame Save { get; private set; }
    public ScanPolicy Policy { get; }
    public TerritoryScanner Scanner { get; }
    public RoomBuilder Builder { get; }
    public RiftService Rifts { get; }
    public RunEngine Engine { get; }

    public RunState? Run => Engine.State;
    public bool HasTerritories => Policy.Territories.Count > 0;

    // ---- territories -------------------------------------------------------

    public bool AddTerritory(string path, out string? reason)
    {
        if (!Policy.TryAddTerritory(path, out reason)) return false;

        Save.Territories = Policy.Territories.ToList();
        Persist();
        Scanner.Invalidate();
        return true;
    }

    public void RemoveTerritory(string path)
    {
        Policy.RemoveTerritory(path);
        Save.Territories = Policy.Territories.ToList();
        Persist();
        Scanner.Invalidate();
    }

    // ---- runs --------------------------------------------------------------

    public RunState? BeginDescent(string territoryRoot)
    {
        var player = Save.ToPlayer();
        var state = Engine.Start(territoryRoot, player);
        if (state is null) return null;

        Save.TotalRuns++;
        Persist();
        return state;
    }

    /// <summary>
    /// Folds a finished run into the profile. Relics only enter the permanent
    /// collection on a successful extraction - that is the entire risk model.
    /// </summary>
    public void ConcludeRun()
    {
        if (Run is not { } run || !run.IsOver) return;

        Save.AbsorbPlayer(run.Player);
        Save.DeepestFloor = Math.Max(Save.DeepestFloor, run.DeepestFloor);
        Save.TotalFelled += run.EntitiesFelled;

        foreach (var folder in run.Path)
            if (!Save.ExploredFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                Save.ExploredFolders.Add(folder);

        if (run.Outcome == RunOutcome.Extracted)
        {
            Save.SuccessfulExtractions++;

            foreach (var relic in run.Player.Carried)
            {
                Save.Shards += relic.Value;

                if (!Save.Collection.Any(c => c.SourcePath.Equals(relic.SourcePath,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    Save.Collection.Add(SavedRelic.From(relic));
                }

                if (relic.SizeBytes > Save.LargestFelledBytes)
                {
                    Save.LargestFelledBytes = relic.SizeBytes;
                    Save.LargestFelledName = relic.Name;
                }
            }

            Save.TouchStreak(DateTime.Now);
        }

        Persist();
    }

    /// <summary>Marks today's Rift as claimed so its bonus cannot be re-farmed.</summary>
    public void ClaimRift(Rift rift)
    {
        Save.ClaimedRiftPath = rift.FolderPath;
        Save.ClaimedRiftLocalDate = DateTime.Now.Date;
        Persist();
    }

    public bool RiftAlreadyClaimed(Rift rift) =>
        Save.ClaimedRiftLocalDate?.Date == DateTime.Now.Date &&
        string.Equals(Save.ClaimedRiftPath, rift.FolderPath, StringComparison.OrdinalIgnoreCase);

    public void Persist() => _store.Save(Save);

    public void ResetProfile()
    {
        Save = SaveGame.NewProfile();
        Save.Territories = Policy.Territories.ToList();
        Persist();
    }

    // ---- presentation helpers ---------------------------------------------

    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }

    public static string FormatAge(DateTime lastWriteUtc)
    {
        var age = DateTime.UtcNow - lastWriteUtc;
        if (age.TotalDays >= 365) return $"{(int)(age.TotalDays / 365)}y old";
        if (age.TotalDays >= 1) return $"{(int)age.TotalDays}d old";
        if (age.TotalHours >= 1) return $"{(int)age.TotalHours}h old";
        return "minutes old";
    }
}
