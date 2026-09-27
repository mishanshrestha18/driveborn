namespace Driveborn.Core.Model;

/// <summary>
/// A monster, chest or boss on the grid. Every field traces back to one real
/// file; nothing here is invented at random. Hovering an entity in the UI shows
/// <see cref="SourcePath"/> so the game can never lie about what it is.
/// </summary>
public sealed class Entity
{
    public required string Id { get; init; }

    /// <summary>The real, absolute path of the file this entity was grown from.</summary>
    public required string SourcePath { get; init; }

    /// <summary>Display name - the file name, dressed with a title.</summary>
    public required string Name { get; init; }

    public required EntityKind Kind { get; init; }
    public required ExtensionClass Class { get; init; }
    public required Temperament Temperament { get; set; }

    public required long SizeBytes { get; init; }
    public required DateTime LastWriteUtc { get; init; }

    public required int MaxHp { get; init; }
    public int Hp { get; set; }
    public required int Armour { get; init; }
    public required int Power { get; init; }

    /// <summary>Edge length in tiles. A 5 GB image is a 5x5 obstacle.</summary>
    public required int Footprint { get; init; }

    /// <summary>Cycles the player must spend to land one strike on it.</summary>
    public required int ReadCost { get; init; }

    /// <summary>Top-left grid position.</summary>
    public GridPoint Position { get; set; }

    /// <summary>Swarms and archives carry a member count; 1 for everything else.</summary>
    public int Stack { get; init; } = 1;

    /// <summary>True once the player has damaged or revealed it.</summary>
    public bool Awake { get; set; }

    /// <summary>Wraiths stay hidden until revealed.</summary>
    public bool Hidden { get; set; }

    /// <summary>For Document entities: the real first line of the file, if readable.</summary>
    public string? Lore { get; set; }

    public bool IsAlive => Hp > 0;
    public bool IsHostile => Kind is not (EntityKind.Treasure or EntityKind.Vault);

    public IEnumerable<GridPoint> OccupiedTiles()
    {
        for (var dy = 0; dy < Footprint; dy++)
            for (var dx = 0; dx < Footprint; dx++)
                yield return new GridPoint(Position.X + dx, Position.Y + dy);
    }

    public bool Occupies(GridPoint p) =>
        p.X >= Position.X && p.X < Position.X + Footprint &&
        p.Y >= Position.Y && p.Y < Position.Y + Footprint;
}

/// <summary>Integer grid coordinate.</summary>
public readonly record struct GridPoint(int X, int Y)
{
    public int ManhattanTo(GridPoint other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    /// <summary>Chebyshev distance - diagonal adjacency counts as 1.</summary>
    public int ChebyshevTo(GridPoint other) => Math.Max(Math.Abs(X - other.X), Math.Abs(Y - other.Y));

    public override string ToString() => $"({X},{Y})";
}
