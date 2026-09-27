using Driveborn.Core.Combat;
using Driveborn.Core.Generation;
using Driveborn.Core.Model;
using Driveborn.Core.Scanning;

namespace Driveborn.Core.Run;

/// <summary>
/// Drives one descent: movement, strikes, doors, the enemy phase and extraction.
/// Deliberately UI-free so the whole game can be played and tested headless.
/// </summary>
public sealed class RunEngine
{
    private readonly TerritoryScanner _scanner;
    private readonly RoomBuilder _builder;
    private readonly Func<DateTime> _clock;

    public RunEngine(TerritoryScanner scanner, RoomBuilder builder, Func<DateTime>? clock = null)
    {
        _scanner = scanner;
        _builder = builder;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public RunState? State { get; private set; }

    /// <summary>Raised whenever the current room is replaced, so the view can redraw.</summary>
    public event Action<Room>? RoomChanged;

    // ---- lifecycle ---------------------------------------------------------

    public RunState? Start(string territoryRoot, Player player)
    {
        var snapshot = _scanner.Snapshot(territoryRoot, useCache: false);
        if (snapshot is null) return null;

        var state = new RunState
        {
            TerritoryRoot = Path.GetFullPath(territoryRoot).TrimEnd(Path.DirectorySeparatorChar),
            StartedUtc = _clock(),
            Player = player
        };

        player.Integrity = player.MaxIntegrity;
        player.Carried.Clear();
        player.BeginTurn();

        State = state;
        LoadRoom(snapshot.FullPath, announce: false);
        state.Say($"Descent begins at {snapshot.Name}.");
        return state;
    }

    // ---- navigation --------------------------------------------------------

    public ActionResult Move(int dx, int dy)
    {
        if (State is not { CurrentRoom: { } room } state || state.IsOver)
            return ActionResult.Fail("No active run.");
        if (state.Player.Cycles < 1) return ActionResult.Fail("Out of cycles - end the turn.");

        var target = new GridPoint(state.Player.Position.X + dx, state.Player.Position.Y + dy);
        if (!room.InBounds(target)) return ActionResult.Fail("The wall holds.");

        var tile = room.TileAt(target);

        if (tile.Kind is TileKind.DoorDown or TileKind.StairsUp)
            return Traverse(tile);

        if (tile.Kind == TileKind.Wall) return ActionResult.Fail("The wall holds.");

        var blocker = room.EntityAt(target);
        if (blocker is not null)
            return ActionResult.Fail($"{blocker.Name} blocks the way.");

        state.Player.Position = target;
        Spend(1);
        return ActionResult.Done(string.Empty);
    }

    private ActionResult Traverse(Tile tile)
    {
        var state = State!;
        if (tile.LinkPath is null) return ActionResult.Fail("That way is sealed.");

        // Leaving a room with hostiles still standing costs integrity - you do not
        // get to stroll past a boss.
        var room = state.CurrentRoom!;
        if (!room.IsCleared)
        {
            var toll = Math.Max(2, room.Entities.Count(e => e.IsAlive && e.IsHostile && e.Awake) * 2);
            state.Player.Integrity -= toll;
            state.Say($"You break contact. Pursuit costs {toll} integrity.");
            if (!state.Player.IsAlive) return Die();
        }

        if (!LoadRoom(tile.LinkPath, announce: true))
            return ActionResult.Fail("That folder cannot be read.");

        Spend(1);
        return ActionResult.Done($"You pass into {tile.LinkLabel}.");
    }

    private bool LoadRoom(string folderPath, bool announce)
    {
        var state = State!;
        var snapshot = _scanner.Snapshot(folderPath);
        if (snapshot is null) return false;

        var room = _builder.Build(snapshot, _clock());
        state.CurrentRoom = room;
        state.Player.Position = room.Entrance;

        if (!state.Path.Contains(room.FolderPath, StringComparer.OrdinalIgnoreCase))
            state.Path.Add(room.FolderPath);

        state.DeepestFloor = Math.Max(state.DeepestFloor, room.Depth);

        if (announce)
        {
            var hostiles = room.Entities.Count(e => e.IsHostile && e.IsAlive);
            var dormant = room.Entities.Count(e => e.Temperament == Temperament.Dormant && e.IsAlive);
            state.Say(room.IsRift
                ? $"RIFT: {room.Title} - floor {room.Depth}, {hostiles} hostile, {dormant} dormant."
                : $"{room.Title} - floor {room.Depth}, {hostiles} hostile, {dormant} dormant.");
        }

        RoomChanged?.Invoke(room);
        return true;
    }

    // ---- combat ------------------------------------------------------------

    /// <summary>Strikes a target with the parser in the given slot.</summary>
    public ActionResult Strike(int parserSlot, Entity target)
    {
        if (State is not { CurrentRoom: { } room } state || state.IsOver)
            return ActionResult.Fail("No active run.");

        var parser = state.Player.ParserInSlot(parserSlot);
        if (parser is null) return ActionResult.Fail("That slot is empty.");
        if (!target.IsAlive) return ActionResult.Fail("Already broken.");
        if (target.Hidden) return ActionResult.Fail("Nothing visible there.");

        if (DistanceTo(state.Player.Position, target) > 1)
            return ActionResult.Fail($"{target.Name} is out of reach.");

        if (state.Player.Cycles < target.ReadCost)
            return ActionResult.Fail($"Needs {target.ReadCost} cycles - you have {state.Player.Cycles}.");

        var damage = CombatMath.Strike(parser, target, state.Player.Level, out var matched,
            out var dormantBonus);
        target.Hp -= damage;
        target.Awake = true;
        if (target.Temperament == Temperament.Dormant) target.Temperament = Temperament.Warm;

        var note = matched ? " (matched - armour pierced)" : " (mismatched)";
        if (dormantBonus) note += " (struck while dormant)";
        state.Say($"{parser.Name} hits {target.Name} for {damage}{note}.");

        if (!target.IsAlive) Fell(target, room);

        Spend(target.ReadCost);
        return ActionResult.Done($"{damage} damage.");
    }

    /// <summary>Opens a treasure entity, banking a relic into the run cache.</summary>
    public ActionResult Open(Entity target)
    {
        if (State is not { CurrentRoom: { } room } state || state.IsOver)
            return ActionResult.Fail("No active run.");
        if (target.Kind is not (EntityKind.Treasure or EntityKind.Vault))
            return ActionResult.Fail("That is not a cache.");
        if (DistanceTo(state.Player.Position, target) > 1)
            return ActionResult.Fail("Too far to reach.");
        if (state.Player.CacheFull) return ActionResult.Fail("Cache is full.");
        if (state.Player.Cycles < 1) return ActionResult.Fail("Out of cycles.");

        if (target.Kind == EntityKind.Vault)
            return ActionResult.Fail("Sealed. This vault needs a key.");

        target.Hp = 0;
        var relic = RelicFrom(target, room.IsRift);
        state.Player.Carried.Add(relic);
        state.Say($"Recovered {relic.Name} (worth {relic.Value}).");

        Spend(1);
        return ActionResult.Done(relic.Name);
    }

    private void Fell(Entity target, Room room)
    {
        var state = State!;
        state.EntitiesFelled++;

        var xp = CombatMath.XpFor(target);
        if (state.Player.AwardXp(xp))
            state.Say($"Level {state.Player.Level}. Integrity restored.");

        state.Say($"{target.Name} breaks apart. +{xp} xp.");

        // Bulky or dormant kills leave something worth carrying out.
        if (!state.Player.CacheFull &&
            (target.Kind == EntityKind.Boss || target.Footprint >= 2 ||
             target.Temperament == Temperament.Dormant))
        {
            var relic = RelicFrom(target, room.IsRift);
            state.Player.Carried.Add(relic);
            state.Say($"It drops {relic.Name}.");
        }

        // Archives burst into their children.
        if (target.Class == ExtensionClass.Archive && target.Stack == 1 && target.MaxHp > 60)
            state.Say($"{target.Name} splits - shards scatter across the floor.");

        if (room.IsCleared)
        {
            state.RoomsCleared++;
            state.Say($"{room.Title} is clear.");
        }
    }

    private static Relic RelicFrom(Entity e, bool fromRift) => new()
    {
        Id = $"relic-{e.Id}",
        Name = e.Name,
        SourcePath = e.SourcePath,
        Class = e.Class,
        SizeBytes = e.SizeBytes,
        LastWriteUtc = e.LastWriteUtc,
        Value = CombatMath.LootValue(e, fromRift),
        Inscription = e.Lore,
        FromRift = fromRift
    };

    // ---- turn cycle --------------------------------------------------------

    private void Spend(int cycles)
    {
        var state = State!;
        state.Player.Cycles -= cycles;
        if (state.Player.Cycles <= 0) EndTurn();
    }

    /// <summary>Ends the player phase and runs every awake hostile.</summary>
    public ActionResult EndTurn()
    {
        if (State is not { CurrentRoom: { } room } state || state.IsOver)
            return ActionResult.Fail("No active run.");

        foreach (var entity in room.Entities.Where(e => e.IsAlive && e.IsHostile && e.Awake).ToList())
        {
            var actions = GameRules.ActionsFor(entity.Temperament);
            for (var i = 0; i < actions && state.Player.IsAlive; i++)
                ActFor(entity, room, state);
        }

        if (!state.Player.IsAlive) return Die();

        state.Turn++;
        state.Player.BeginTurn();
        return ActionResult.Done("Turn ends.");
    }

    private void ActFor(Entity entity, Room room, RunState state)
    {
        var distance = DistanceTo(state.Player.Position, entity);

        if (distance <= CombatMath.AttackRange(entity))
        {
            var damage = CombatMath.Retaliate(entity);
            state.Player.Integrity -= damage;
            state.Say($"{entity.Name} strikes you for {damage}.");
            return;
        }

        if (!CombatMath.CanMove(entity)) return;

        var step = StepToward(entity.Position, state.Player.Position);
        if (room.IsPassable(step) && room.EntityAt(step) is null)
            entity.Position = step;
    }

    private ActionResult Die()
    {
        var state = State!;
        state.Outcome = RunOutcome.Died;
        var lost = state.CarriedValue;
        state.Player.Carried.Clear();
        state.Say($"Integrity zero. The descent ends. {lost} in relics lost; xp and map knowledge kept.");
        return ActionResult.Done("You fall.");
    }

    // ---- extraction --------------------------------------------------------

    /// <summary>
    /// Banks the run. Only legal at a territory root - the walk back out is the
    /// decision the whole session builds toward.
    /// </summary>
    public ActionResult Extract()
    {
        if (State is not { } state || state.IsOver) return ActionResult.Fail("No active run.");
        if (!state.CanExtractHere)
            return ActionResult.Fail("You can only extract from the territory root. Climb out.");

        state.Outcome = RunOutcome.Extracted;
        state.Say($"Extracted with {state.Player.Carried.Count} relics worth {state.CarriedValue}.");
        return ActionResult.Done("Extracted.");
    }

    public ActionResult Abandon()
    {
        if (State is not { } state || state.IsOver) return ActionResult.Fail("No active run.");
        state.Outcome = RunOutcome.Abandoned;
        state.Player.Carried.Clear();
        state.Say("Descent abandoned. The haul is gone.");
        return ActionResult.Done("Abandoned.");
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>Chebyshev distance to the nearest tile of a multi-tile body.</summary>
    public static int DistanceTo(GridPoint from, Entity entity) =>
        entity.OccupiedTiles().Min(t => from.ChebyshevTo(t));

    private static GridPoint StepToward(GridPoint from, GridPoint to) => new(
        from.X + Math.Sign(to.X - from.X),
        from.Y + Math.Sign(to.Y - from.Y));
}
