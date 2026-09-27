using Driveborn.Core.Model;
using Driveborn.Core.Scanning;

namespace Driveborn.Core.Generation;

/// <summary>
/// Folder to room. Grid size scales with how much is actually in the folder, the
/// biggest file becomes the floor boss, tiny files are folded into swarms, and
/// every subfolder becomes a labelled door the player can size up before opening.
/// </summary>
public sealed class RoomBuilder
{
    private readonly IFileSystemProbe _probe;

    public RoomBuilder(IFileSystemProbe probe) => _probe = probe;

    public Room Build(FolderSnapshot snapshot, DateTime nowUtc)
    {
        var rng = new DeterministicRng(Seed.ForFolder(snapshot.FullPath));
        var entities = BuildEntities(snapshot, nowUtc);

        var (width, height) = SizeFor(entities, snapshot.Folders.Count);
        var tiles = CarveShell(width, height);

        var isRift = (nowUtc - snapshot.LastWriteUtc) <= GameRules.RiftWindow;

        var room = new Room
        {
            FolderPath = snapshot.FullPath,
            Title = snapshot.Name,
            Depth = snapshot.Depth,
            Width = width,
            Height = height,
            Tiles = tiles,
            ParentPath = snapshot.ParentPath,
            IsRift = isRift
        };

        PlaceDoors(room, snapshot);
        PlaceEntities(room, entities, rng);
        AttachLore(room);

        return room;
    }

    private static List<Entity> BuildEntities(FolderSnapshot snapshot, DateTime nowUtc)
    {
        var entities = new List<Entity>();
        var boss = snapshot.Largest;

        var tiny = snapshot.Files
            .Where(f => f.SizeBytes <= GameRules.SwarmMemberMaxBytes && f != boss)
            .ToList();

        var solo = snapshot.Files
            .Where(f => !tiny.Contains(f))
            .ToList();

        foreach (var file in solo)
            entities.Add(EntityFactory.FromFile(file, nowUtc, isBoss: file == boss && solo.Count > 1));

        // Piles of small files fold into one swarm body, so a folder holding 4000
        // cache files is a single interesting fight instead of 4000 dull ones.
        if (tiny.Count >= GameRules.SwarmMinMembers)
        {
            foreach (var group in tiny.GroupBy(f => ExtensionMap.Classify(f.Extension)))
            {
                var members = group.ToList();
                if (members.Count >= GameRules.SwarmMinMembers)
                    entities.Add(EntityFactory.SwarmFrom(members, snapshot.FullPath, nowUtc));
                else
                    entities.AddRange(members.Select(m => EntityFactory.FromFile(m, nowUtc)));
            }
        }
        else
        {
            entities.AddRange(tiny.Select(m => EntityFactory.FromFile(m, nowUtc)));
        }

        // Files trimmed off by the scanner cap come back as one overflow swarm, so
        // the room still tells the truth about how full the folder really is.
        if (snapshot.TruncatedFileCount > 0)
        {
            var hp = Math.Clamp(6 * snapshot.TruncatedFileCount, GameRules.MinHp, GameRules.MaxHp);
            entities.Add(new Entity
            {
                Id = $"overflow-{Seed.ForFolder(snapshot.FullPath):x8}",
                SourcePath = snapshot.FullPath,
                Name = $"Overflow Swarm ({snapshot.TruncatedFileCount} unread)",
                Kind = EntityKind.Swarm,
                Class = ExtensionClass.Unknown,
                Temperament = Temperament.Warm,
                SizeBytes = 0,
                LastWriteUtc = snapshot.LastWriteUtc,
                MaxHp = hp,
                Hp = hp,
                Armour = 0,
                Power = 4,
                Footprint = 1,
                ReadCost = 1,
                Stack = snapshot.TruncatedFileCount,
                Awake = true
            });
        }

        return entities;
    }

    private static (int Width, int Height) SizeFor(IReadOnlyList<Entity> entities, int doorCount)
    {
        var occupied = entities.Sum(e => e.Footprint * e.Footprint);
        var needed = Math.Max(occupied * 4, 36) + (doorCount * 2);
        var edge = (int)Math.Ceiling(Math.Sqrt(needed));
        var width = Math.Clamp(edge, 9, 26);
        var height = Math.Clamp(edge, 9, 20);
        return (width, height);
    }

    private static Tile[,] CarveShell(int width, int height)
    {
        var tiles = new Tile[width, height];
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
            {
                var isEdge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                tiles[x, y] = new Tile { Kind = isEdge ? TileKind.Wall : TileKind.Floor };
            }
        return tiles;
    }

    private static void PlaceDoors(Room room, FolderSnapshot snapshot)
    {
        // Stairs up sit on the top wall; the player enters on the tile below them.
        var upX = room.Width / 2;
        if (snapshot.ParentPath is not null)
        {
            room.Tiles[upX, 0].Kind = TileKind.StairsUp;
            room.Tiles[upX, 0].LinkPath = snapshot.ParentPath;
            room.Tiles[upX, 0].LinkLabel = "..";
        }
        room.Entrance = new GridPoint(upX, 1);

        // Subfolder doors line the bottom and side walls, biggest folder first.
        // Each carries its real name and entry count, so the route choice is made
        // on real knowledge of the drive rather than a coin flip.
        var slots = new List<GridPoint>();
        for (var x = 2; x < room.Width - 2; x += 2) slots.Add(new GridPoint(x, room.Height - 1));
        for (var y = 2; y < room.Height - 2; y += 2) slots.Add(new GridPoint(0, y));
        for (var y = 2; y < room.Height - 2; y += 2) slots.Add(new GridPoint(room.Width - 1, y));

        var doors = snapshot.Folders.Take(slots.Count).ToList();
        for (var i = 0; i < doors.Count; i++)
        {
            var p = slots[i];
            var tile = room.Tiles[p.X, p.Y];
            tile.Kind = TileKind.DoorDown;
            tile.LinkPath = doors[i].FullPath;
            tile.LinkLabel = doors[i].Name;
            tile.LinkEntryCount = doors[i].EntryCount;
        }
    }

    private static void PlaceEntities(Room room, List<Entity> entities, DeterministicRng rng)
    {
        // Biggest first: hulks need contiguous space, small fry fill the gaps.
        foreach (var entity in entities.OrderByDescending(e => e.Footprint))
        {
            if (TryFindSpot(room, entity, rng, out var spot))
            {
                entity.Position = spot;
                room.Entities.Add(entity);
            }
            // A genuinely full room drops the leftovers rather than overlapping them.
        }
    }

    private static bool TryFindSpot(Room room, Entity entity, DeterministicRng rng, out GridPoint spot)
    {
        var maxX = room.Width - entity.Footprint - 1;
        var maxY = room.Height - entity.Footprint - 1;

        for (var attempt = 0; attempt < 120 && maxX > 1 && maxY > 2; attempt++)
        {
            var candidate = new GridPoint(rng.Next(1, maxX + 1), rng.Next(2, maxY + 1));
            if (Fits(room, entity, candidate))
            {
                spot = candidate;
                return true;
            }
        }

        for (var y = 2; y <= maxY; y++)
            for (var x = 1; x <= maxX; x++)
            {
                var candidate = new GridPoint(x, y);
                if (Fits(room, entity, candidate))
                {
                    spot = candidate;
                    return true;
                }
            }

        spot = default;
        return false;
    }

    private static bool Fits(Room room, Entity entity, GridPoint at)
    {
        for (var dy = 0; dy < entity.Footprint; dy++)
            for (var dx = 0; dx < entity.Footprint; dx++)
            {
                var p = new GridPoint(at.X + dx, at.Y + dy);
                if (!room.InBounds(p)) return false;
                if (room.TileAt(p).Kind != TileKind.Floor) return false;
                if (p == room.Entrance) return false;
                if (room.Entities.Any(e => e.Occupies(p))) return false;
            }
        return true;
    }

    /// <summary>
    /// Scribes carry the real first line of the real file. This is the hook that
    /// turns an old folder into an archaeology site.
    /// </summary>
    private void AttachLore(Room room)
    {
        foreach (var entity in room.Entities.Where(e =>
                     e.Kind is EntityKind.Scribe or EntityKind.Undead &&
                     e.Class is ExtensionClass.Document or ExtensionClass.Code or ExtensionClass.Data))
        {
            entity.Lore = _probe.ReadFirstLine(entity.SourcePath);
        }
    }
}
