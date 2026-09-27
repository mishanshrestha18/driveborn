using System.Text;

namespace Driveborn.Core.Scanning;

/// <summary>
/// Real disk implementation. Every call opens read-only, shares read/write so it
/// never locks the user's files, and swallows access errors rather than
/// interrupting a run - an unreadable folder is simply an empty room.
/// </summary>
public sealed class FileSystemProbe : IFileSystemProbe
{
    private static readonly string[] TextExtensions =
    {
        ".txt", ".md", ".log", ".json", ".xml", ".yml", ".yaml", ".csv", ".ini", ".cfg",
        ".cs", ".js", ".ts", ".py", ".rs", ".go", ".java", ".c", ".h", ".cpp", ".sql", ".html", ".css"
    };

    public bool DirectoryExists(string path)
    {
        try { return Directory.Exists(path); }
        catch { return false; }
    }

    public IReadOnlyList<FileEntry> EnumerateFiles(string path)
    {
        var results = new List<FileEntry>();
        try
        {
            var dir = new DirectoryInfo(path);
            foreach (var f in dir.EnumerateFiles("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.Offline
            }))
            {
                if (ScanPolicy.IsDeniedFile(f.Name)) continue;

                try
                {
                    results.Add(new FileEntry(
                        f.FullName,
                        f.Name,
                        f.Extension.ToLowerInvariant(),
                        f.Length,
                        f.LastWriteTimeUtc,
                        f.IsReadOnly,
                        f.Attributes.HasFlag(FileAttributes.Hidden)));
                }
                catch (IOException) { /* vanished mid-scan; skip */ }
                catch (UnauthorizedAccessException) { /* skip */ }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
            // An unreadable folder is an empty room, not a crash.
        }
        return results;
    }

    public IReadOnlyList<FolderEntry> EnumerateFolders(string path)
    {
        var results = new List<FolderEntry>();
        try
        {
            var dir = new DirectoryInfo(path);
            foreach (var d in dir.EnumerateDirectories("*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                if (ScanPolicy.IsDeniedFolder(d.FullName)) continue;

                var count = 0;
                try
                {
                    count = d.EnumerateFileSystemInfos("*", new EnumerationOptions
                    {
                        IgnoreInaccessible = true,
                        RecurseSubdirectories = false
                    }).Take(500).Count();
                }
                catch { /* keep zero */ }

                try
                {
                    results.Add(new FolderEntry(d.FullName, d.Name, d.LastWriteTimeUtc, count));
                }
                catch (IOException) { }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or DirectoryNotFoundException or IOException)
        {
        }
        return results;
    }

    public string? ReadFirstLine(string path, int maxChars = 160)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (!TextExtensions.Contains(ext)) return null;
        if (ScanPolicy.IsDeniedFile(Path.GetFileName(path))) return null;

        try
        {
            var info = new FileInfo(path);
            if (info.Length == 0 || info.Length > 8L * 1024 * 1024) return null;

            // Read-only, shared: we must never block the user's own editor.
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            for (var i = 0; i < 8; i++)
            {
                var line = reader.ReadLine();
                if (line is null) break;
                line = line.Trim();
                if (line.Length < 3) continue;
                return line.Length > maxChars ? line[..maxChars] + "..." : line;
            }
        }
        catch { /* unreadable is fine - the entity simply has no lore */ }

        return null;
    }
}
