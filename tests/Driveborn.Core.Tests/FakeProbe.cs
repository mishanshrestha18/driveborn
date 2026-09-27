using Driveborn.Core.Scanning;

namespace Driveborn.Core.Tests;

/// <summary>
/// In-memory file system so the whole game can be tested without touching a real
/// disk. The probe interface has no write members, which is the point: there is
/// nothing here to fake destructively either.
/// </summary>
public sealed class FakeProbe : IFileSystemProbe
{
    private readonly Dictionary<string, List<FileEntry>> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<FolderEntry>> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _lore = new(StringComparer.OrdinalIgnoreCase);

    public FakeProbe AddFolder(string path)
    {
        _files.TryAdd(path, new List<FileEntry>());
        _folders.TryAdd(path, new List<FolderEntry>());

        var parent = Path.GetDirectoryName(path);
        if (parent is not null && _folders.ContainsKey(parent))
        {
            _folders[parent].Add(new FolderEntry(path, Path.GetFileName(path), DateTime.UtcNow, 0));
        }
        return this;
    }

    public FakeProbe AddFile(string folder, string name, long size, DateTime lastWriteUtc,
        bool readOnly = false, bool hidden = false, string? lore = null)
    {
        AddFolder(folder);
        var full = Path.Combine(folder, name);
        _files[folder].Add(new FileEntry(full, name, Path.GetExtension(name).ToLowerInvariant(),
            size, lastWriteUtc, readOnly, hidden));
        if (lore is not null) _lore[full] = lore;
        return this;
    }

    public bool DirectoryExists(string path) => _folders.ContainsKey(path);

    public IReadOnlyList<FileEntry> EnumerateFiles(string path) =>
        _files.TryGetValue(path, out var f) ? f : Array.Empty<FileEntry>();

    public IReadOnlyList<FolderEntry> EnumerateFolders(string path) =>
        _folders.TryGetValue(path, out var d) ? d : Array.Empty<FolderEntry>();

    public string? ReadFirstLine(string path, int maxChars = 160) =>
        _lore.TryGetValue(path, out var line) ? line : null;
}
