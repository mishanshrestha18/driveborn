namespace Driveborn.Core.Scanning;

/// <summary>Metadata for one real file. No contents, no handles held.</summary>
public sealed record FileEntry(
    string FullPath,
    string Name,
    string Extension,
    long SizeBytes,
    DateTime LastWriteUtc,
    bool IsReadOnly,
    bool IsHidden);

/// <summary>Metadata for one real subfolder.</summary>
public sealed record FolderEntry(
    string FullPath,
    string Name,
    DateTime LastWriteUtc,
    int EntryCount);

/// <summary>Everything the generator needs about one folder.</summary>
public sealed record FolderSnapshot(
    string FullPath,
    string Name,
    string? ParentPath,
    int Depth,
    DateTime LastWriteUtc,
    IReadOnlyList<FileEntry> Files,
    IReadOnlyList<FolderEntry> Folders,
    int TruncatedFileCount)
{
    public long TotalBytes => Files.Sum(f => f.SizeBytes);
    public FileEntry? Largest => Files.MaxBy(f => f.SizeBytes);
}
