namespace Driveborn.Core.Scanning;

/// <summary>
/// Walks one folder at a time, on demand. Driveborn deliberately does not index
/// a whole drive up front: rooms are generated lazily as the player opens doors,
/// so entering the game is instant no matter how big the disk is.
/// </summary>
public sealed class TerritoryScanner
{
    private readonly IFileSystemProbe _probe;
    private readonly ScanPolicy _policy;
    private readonly Dictionary<string, FolderSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);

    public TerritoryScanner(IFileSystemProbe probe, ScanPolicy policy)
    {
        _probe = probe;
        _policy = policy;
    }

    public ScanPolicy Policy => _policy;

    /// <summary>Number of folders snapshotted this session.</summary>
    public int CachedFolderCount => _cache.Count;

    /// <summary>
    /// Snapshots a single folder. Returns null if the path is outside every
    /// territory, denied, or gone.
    /// </summary>
    public FolderSnapshot? Snapshot(string folderPath, bool useCache = true)
    {
        if (!_policy.IsScannable(folderPath)) return null;
        if (!_probe.DirectoryExists(folderPath)) return null;

        var full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);

        if (useCache && _cache.TryGetValue(full, out var cached)) return cached;

        var files = _probe.EnumerateFiles(full);
        var truncated = 0;
        if (files.Count > _policy.MaxEntriesPerFolder)
        {
            // Keep the biggest and the newest - the interesting ones - and note
            // how many were left out so the room can show a "swarm overflow".
            truncated = files.Count - _policy.MaxEntriesPerFolder;
            files = files
                .OrderByDescending(f => f.SizeBytes)
                .Take(_policy.MaxEntriesPerFolder / 2)
                .Concat(files
                    .OrderByDescending(f => f.LastWriteUtc)
                    .Take(_policy.MaxEntriesPerFolder / 2))
                .DistinctBy(f => f.FullPath)
                .ToList();
        }

        var folders = _probe.EnumerateFolders(full)
            .Where(d => Depth(d.FullPath) <= _policy.MaxDepth)
            .Where(d => !ScanPolicy.IsNoiseFolder(d.Name))
            .OrderByDescending(d => d.EntryCount)
            .ToList();

        var parent = ParentWithinTerritory(full);

        var snapshot = new FolderSnapshot(
            FullPath: full,
            Name: Path.GetFileName(full) is { Length: > 0 } n ? n : full,
            ParentPath: parent,
            Depth: Depth(full),
            LastWriteUtc: SafeLastWrite(full, folders, files),
            Files: files,
            Folders: folders,
            TruncatedFileCount: truncated);

        _cache[full] = snapshot;
        return snapshot;
    }

    /// <summary>Forgets cached snapshots so the world re-reads the disk.</summary>
    public void Invalidate() => _cache.Clear();

    public void Invalidate(string folderPath) =>
        _cache.Remove(Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar));

    /// <summary>
    /// Depth below the owning territory root. This is the dungeon floor number:
    /// nesting your real folders deeper genuinely means deeper floors.
    /// </summary>
    public int Depth(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var root = _policy.Territories.FirstOrDefault(t =>
            full.StartsWith(t, StringComparison.OrdinalIgnoreCase));
        if (root is null) return 0;

        var relative = full[root.Length..].Trim(Path.DirectorySeparatorChar);
        return relative.Length == 0
            ? 0
            : relative.Count(c => c == Path.DirectorySeparatorChar) + 1;
    }

    /// <summary>Parent folder, but never above a territory root.</summary>
    private string? ParentWithinTerritory(string full)
    {
        if (_policy.Territories.Any(t => full.Equals(t, StringComparison.OrdinalIgnoreCase)))
            return null;

        var parent = Path.GetDirectoryName(full);
        return parent is not null && _policy.IsScannable(parent) ? parent : null;
    }

    private static DateTime SafeLastWrite(string full, IReadOnlyList<FolderEntry> folders,
        IReadOnlyList<FileEntry> files)
    {
        var newest = DateTime.MinValue;
        foreach (var f in files) if (f.LastWriteUtc > newest) newest = f.LastWriteUtc;
        foreach (var d in folders) if (d.LastWriteUtc > newest) newest = d.LastWriteUtc;

        if (newest != DateTime.MinValue) return newest;

        try { return Directory.GetLastWriteTimeUtc(full); }
        catch { return DateTime.UtcNow; }
    }
}
