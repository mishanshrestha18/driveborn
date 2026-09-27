namespace Driveborn.Core.Model;

/// <summary>A single grid cell.</summary>
public sealed class Tile
{
    public required TileKind Kind { get; set; }

    /// <summary>For <see cref="TileKind.DoorDown"/>: the real subfolder path it leads to.</summary>
    public string? LinkPath { get; set; }

    /// <summary>For doors: the folder name shown on the door.</summary>
    public string? LinkLabel { get; set; }

    /// <summary>Number of entries behind the door - the player's threat preview.</summary>
    public int LinkEntryCount { get; set; }

    public bool Walkable => Kind is TileKind.Floor or TileKind.DoorDown or TileKind.StairsUp or TileKind.Rubble;
}

/// <summary>
/// One folder, rendered as a fightable room. Width and height scale with how
/// much is actually in the folder.
/// </summary>
public sealed class Room
{
    public required string FolderPath { get; init; }
    public required string Title { get; init; }
    public required int Depth { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required Tile[,] Tiles { get; init; }

    public List<Entity> Entities { get; } = new();

    /// <summary>Set when this folder was touched inside the Rift window.</summary>
    public bool IsRift { get; init; }

    /// <summary>Parent folder path, or null at a territory root.</summary>
    public string? ParentPath { get; init; }

    public GridPoint Entrance { get; set; }

    public bool InBounds(GridPoint p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public Tile TileAt(GridPoint p) => Tiles[p.X, p.Y];

    public Entity? EntityAt(GridPoint p) =>
        Entities.FirstOrDefault(e => e.IsAlive && !e.Hidden && e.Occupies(p));

    /// <summary>True when nothing hostile is left standing.</summary>
    public bool IsCleared => !Entities.Any(e => e.IsAlive && e.IsHostile);

    public IEnumerable<Tile> Doors()
    {
        for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
                if (Tiles[x, y].Kind == TileKind.DoorDown)
                    yield return Tiles[x, y];
    }

    /// <summary>Can the player stand here right now?</summary>
    public bool IsPassable(GridPoint p) =>
        InBounds(p) && TileAt(p).Walkable && EntityAt(p) is null;
}
