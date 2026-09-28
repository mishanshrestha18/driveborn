using System.Text.RegularExpressions;

namespace Driveborn.Core.Scanning;

/// <summary>
/// The safety gate. Nothing is scanned unless the user explicitly added its root
/// as a Territory, and even then a hard deny-list wins over any allow. This type
/// is the single decision point - the scanner asks it about every path.
///
/// Driveborn never writes to, renames, moves or deletes anything it scans. The
/// probe used by the scanner exposes metadata plus a capped preview only.
/// </summary>
public sealed class ScanPolicy
{
    private readonly List<string> _territories = new();

    /// <summary>
    /// Path fragments that are never scanned, whatever the user picks. Credential
    /// stores, OS internals and package caches - things that are either sensitive
    /// or would make a boring dungeon out of 200k junk files.
    /// </summary>
    private static readonly string[] DeniedFragments =
    {
        @"\windows", @"\program files", @"\programdata",
        @"\appdata\local\microsoft", @"\appdata\locallow",
        @"\$recycle.bin", @"\system volume information",
        @"\.ssh", @"\.gnupg", @"\.aws", @"\.azure", @"\.kube", @"\.docker",
        @"\.config\gh", @"\.password-store", @"\keychains",
        @"\.git\config", @"\.gitconfig",
        @"\node_modules\.bin", @"\.nuget\packages", @"\.cargo\registry",
        @"\onedrivetemp", @"\.vs\", @"\.gradle\caches"
    };

    /// <summary>
    /// File names that are never turned into entities. Secrets should not be
    /// loot, and should not have their first line read for "lore".
    /// </summary>
    private static readonly Regex DeniedFileNames = new(
        @"^(\.env(\..+)?|.*\.pem|.*\.key|.*\.pfx|.*\.p12|.*\.keystore|id_rsa.*|id_ed25519.*|" +
        @"credentials|\.npmrc|\.netrc|secrets?\..*|.*\.kdbx)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Folder names that are hidden from the dungeon. These are not a security
    /// boundary - they are a relevance filter. Version control internals, build
    /// output and package caches are not places a player recognises, and a room
    /// full of them reads as meaningless noise rather than as their own drive.
    /// </summary>
    private static readonly HashSet<string> NoiseFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".hg", ".svn", ".vs", ".idea", ".vscode",
        "bin", "obj", "node_modules", "packages", "vendor",
        "dist", "build", "out", "target", "__pycache__", ".pytest_cache",
        ".venv", "venv", "env", ".tox", ".gradle", ".next", ".nuxt", ".cache",
        "graphify-out", ".claude", "testresults"
    };

    /// <summary>Folders the user has opted in to. Empty means nothing is scannable.</summary>
    public IReadOnlyList<string> Territories => _territories;

    /// <summary>
    /// True when a folder is build or tooling noise that should not become a room.
    /// Unlike <see cref="IsDeniedFolder"/> this is about making the dungeon feel
    /// like the player's own drive, not about safety.
    /// </summary>
    public static bool IsNoiseFolder(string folderName) => NoiseFolders.Contains(folderName);

    /// <summary>Max folder depth walked below a territory root.</summary>
    public int MaxDepth { get; init; } = 12;

    /// <summary>Entities generated per room is capped so a 40k-file folder stays playable.</summary>
    public int MaxEntriesPerFolder { get; init; } = 60;

    /// <summary>
    /// Adds a territory root. Returns false if the path is denied or missing -
    /// the UI surfaces that rather than silently accepting it.
    /// </summary>
    public bool TryAddTerritory(string path, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            reason = "Empty path.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            reason = $"Unusable path: {ex.Message}";
            return false;
        }

        if (IsDeniedFolder(full))
        {
            reason = "That location is permanently excluded (system or credential path).";
            return false;
        }

        if (!Directory.Exists(full))
        {
            reason = "Folder does not exist.";
            return false;
        }

        if (_territories.Any(t => full.Equals(t, StringComparison.OrdinalIgnoreCase)))
        {
            reason = "Already a territory.";
            return false;
        }

        _territories.Add(full);
        return true;
    }

    public void RemoveTerritory(string path) =>
        _territories.RemoveAll(t => t.Equals(path.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase));

    /// <summary>True only if the path sits inside an opted-in territory and is not denied.</summary>
    public bool IsScannable(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        string full;
        try { full = Path.GetFullPath(path); }
        catch { return false; }

        if (IsDeniedFolder(full)) return false;

        return _territories.Any(t =>
            full.Equals(t, StringComparison.OrdinalIgnoreCase) ||
            full.StartsWith(t + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Deny-list check on a folder path. Case-insensitive fragment match.</summary>
    public static bool IsDeniedFolder(string fullPath)
    {
        var sep = Path.DirectorySeparatorChar;
        var norm = fullPath.Replace(Path.AltDirectorySeparatorChar, sep).ToLowerInvariant();
        if (!norm.EndsWith(sep)) norm += sep;
        return DeniedFragments.Any(f => norm.Contains(f + sep) || norm.Contains(f));
    }

    /// <summary>Secrets never become entities, loot or lore.</summary>
    public static bool IsDeniedFile(string fileName) => DeniedFileNames.IsMatch(fileName);
}
