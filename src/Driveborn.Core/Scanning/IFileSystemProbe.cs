namespace Driveborn.Core.Scanning;

/// <summary>
/// The only surface through which Driveborn touches a disk. It is deliberately
/// read-only: there is no write, delete, move or create member, so no amount of
/// game logic can modify the player's files. Tests swap in a fake.
/// </summary>
public interface IFileSystemProbe
{
    bool DirectoryExists(string path);

    /// <summary>Files directly inside <paramref name="path"/>. Never recursive.</summary>
    IReadOnlyList<FileEntry> EnumerateFiles(string path);

    /// <summary>Immediate subfolders of <paramref name="path"/>.</summary>
    IReadOnlyList<FolderEntry> EnumerateFolders(string path);

    /// <summary>
    /// First meaningful line of a text file, capped hard, used as lore on a
    /// Document entity. Returns null for anything non-textual or unreadable.
    /// </summary>
    string? ReadFirstLine(string path, int maxChars = 160);
}
