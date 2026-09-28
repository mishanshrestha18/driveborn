using Driveborn.Core.Generation;
using Driveborn.Core.Model;
using Driveborn.Core.Scanning;

namespace Driveborn.Core.Rifts;

/// <summary>A folder that changed inside the Rift window, offered as today's run.</summary>
public sealed record Rift(
    string FolderPath,
    string Name,
    DateTime LastWriteUtc,
    int EntryCount,
    int Depth,
    bool IsDaily)
{
    public TimeSpan Age(DateTime nowUtc) => nowUtc - LastWriteUtc;
}

/// <summary>
/// The daily hook, and the reason Driveborn needs no content pipeline: the
/// player's own activity generates tomorrow's level. Any folder they actually
/// touched in the last day becomes a Rift - richer loot, gone the next day.
/// </summary>
public sealed class RiftService
{
    private readonly IFileSystemProbe _probe;
    private readonly ScanPolicy _policy;
    private readonly Func<DateTime> _clock;

    /// <summary>Hard cap on folders inspected, so Rift discovery stays sub-second.</summary>
    public int MaxFoldersInspected { get; init; } = 2500;

    /// <summary>How deep below a territory root Rift discovery walks.</summary>
    public int MaxDepth { get; init; } = 5;

    public RiftService(IFileSystemProbe probe, ScanPolicy policy, Func<DateTime>? clock = null)
    {
        _probe = probe;
        _policy = policy;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Finds every recently touched folder across all territories, newest first.
    /// Exactly one is flagged as the day's headline Rift, chosen deterministically
    /// from the date so it does not shuffle on every relaunch.
    /// </summary>
    public IReadOnlyList<Rift> Discover()
    {
        var now = _clock();
        var found = new List<Rift>();
        var inspected = 0;

        foreach (var root in _policy.Territories)
        {
            var queue = new Queue<(string Path, int Depth)>();
            queue.Enqueue((root, 0));

            while (queue.Count > 0 && inspected < MaxFoldersInspected)
            {
                var (path, depth) = queue.Dequeue();
                inspected++;

                // Build output and version-control internals change constantly and
                // would win the Rift lottery every single day, which is both boring
                // and meaningless to the player.
                var folders = _probe.EnumerateFolders(path)
                    .Where(d => !ScanPolicy.IsNoiseFolder(d.Name))
                    .ToList();
                var files = _probe.EnumerateFiles(path);

                var newest = DateTime.MinValue;
                foreach (var f in files) if (f.LastWriteUtc > newest) newest = f.LastWriteUtc;

                if (newest != DateTime.MinValue && (now - newest) <= GameRules.RiftWindow)
                {
                    found.Add(new Rift(
                        FolderPath: path,
                        Name: Path.GetFileName(path) is { Length: > 0 } n ? n : path,
                        LastWriteUtc: newest,
                        EntryCount: files.Count + folders.Count,
                        Depth: depth,
                        IsDaily: false));
                }

                if (depth < MaxDepth)
                    foreach (var child in folders)
                        queue.Enqueue((child.FullPath, depth + 1));
            }
        }

        if (found.Count == 0) return found;

        var ordered = found
            .OrderByDescending(r => r.LastWriteUtc)
            .ThenByDescending(r => r.EntryCount)
            .ToList();

        // Stable pick for the calendar day, biased to the freshest handful.
        var pool = Math.Min(ordered.Count, 5);
        var rng = new DeterministicRng(Seed.ForDay(now.Date, "rift"));
        var chosen = rng.Next(pool);

        ordered[chosen] = ordered[chosen] with { IsDaily = true };

        // Headline Rift leads the list.
        var daily = ordered[chosen];
        ordered.RemoveAt(chosen);
        ordered.Insert(0, daily);

        return ordered;
    }

    /// <summary>Today's headline Rift, or null when nothing was touched recently.</summary>
    public Rift? Daily() => Discover().FirstOrDefault(r => r.IsDaily);
}
