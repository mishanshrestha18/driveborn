using System.Text;
using Driveborn.Core.Scanning;

namespace Driveborn.Core.Generation;

/// <summary>
/// Deterministic seeding. The same file always produces the same monster, on
/// every machine and every launch - until the file itself changes, at which
/// point the monster changes with it. That is the core promise of the game, so
/// the hash must be stable and must never use <see cref="string.GetHashCode()"/>,
/// which is randomised per process.
/// </summary>
public static class Seed
{
    private const ulong FnvOffset = 14695981039346656037;
    private const ulong FnvPrime = 1099511628211;

    public static ulong Hash(string value)
    {
        var hash = FnvOffset;
        foreach (var b in Encoding.UTF8.GetBytes(value.ToLowerInvariant()))
        {
            hash ^= b;
            hash *= FnvPrime;
        }
        return hash;
    }

    public static ulong Combine(ulong a, ulong b)
    {
        var hash = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
        return hash == 0 ? FnvOffset : hash;
    }

    /// <summary>
    /// A file's identity: path, size and last-write time. Touch the file and it
    /// becomes a different creature next run.
    /// </summary>
    public static ulong ForFile(FileEntry file) =>
        Combine(Hash(file.FullPath), Combine((ulong)file.SizeBytes, (ulong)file.LastWriteUtc.Ticks));

    public static ulong ForFolder(string path) => Hash(path);

    /// <summary>Seed for a daily event - stable for the whole calendar day.</summary>
    public static ulong ForDay(DateTime day, string salt = "") =>
        Combine(Hash(day.ToString("yyyy-MM-dd")), Hash(salt));
}

/// <summary>
/// Small, fast, fully deterministic RNG (xorshift64*). Seeded from
/// <see cref="Seed"/> so world generation is reproducible.
/// </summary>
public sealed class DeterministicRng
{
    private ulong _state;

    public DeterministicRng(ulong seed) => _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    public ulong NextUlong()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return _state * 0x2545F4914F6CDD1DUL;
    }

    /// <summary>Uniform in [0, maxExclusive).</summary>
    public int Next(int maxExclusive) =>
        maxExclusive <= 0 ? 0 : (int)(NextUlong() % (ulong)maxExclusive);

    /// <summary>Uniform in [min, maxExclusive).</summary>
    public int Next(int min, int maxExclusive) =>
        maxExclusive <= min ? min : min + Next(maxExclusive - min);

    public double NextDouble() => (NextUlong() >> 11) * (1.0 / 9007199254740992.0);

    public bool Chance(double probability) => NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
}
