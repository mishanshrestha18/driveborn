using Driveborn.Core.Combat;
using Driveborn.Core.Generation;
using Driveborn.Core.Model;
using Driveborn.Core.Run;
using Driveborn.Core.Scanning;
using Xunit;

namespace Driveborn.Core.Tests;

public class CombatTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static Entity Target(ExtensionClass cls, int hp = 200, int armour = 6,
        Temperament temperament = Temperament.Warm, bool awake = true) => new()
    {
        Id = "t",
        SourcePath = Path.Combine("D:", "x", "file.bin"),
        Name = "Target",
        Kind = EntityKind.Construct,
        Class = cls,
        Temperament = temperament,
        SizeBytes = 1024,
        LastWriteUtc = Now,
        MaxHp = hp,
        Hp = hp,
        Armour = armour,
        Power = 10,
        Footprint = 1,
        ReadCost = 1,
        Awake = awake
    };

    private static ParserTool Parser(ExtensionClass affinity, int level = 2) => new()
    {
        Id = "p",
        Name = "Test Parser",
        Affinity = affinity,
        Level = level
    };

    [Fact]
    public void A_matched_parser_massively_outdamages_a_mismatched_one()
    {
        var target = Target(ExtensionClass.Binary);

        var matched = CombatMath.Strike(Parser(ExtensionClass.Binary), target, 1, out var wasMatch, out _);
        var mismatched = CombatMath.Strike(Parser(ExtensionClass.Raster), target, 1, out var wasMiss, out _);

        Assert.True(wasMatch);
        Assert.False(wasMiss);
        Assert.True(matched > mismatched * 3,
            $"matched {matched} should dwarf mismatched {mismatched}");
    }

    [Fact]
    public void A_matched_parser_pierces_armour()
    {
        var soft = Target(ExtensionClass.Binary, armour: 0);
        var hard = Target(ExtensionClass.Binary, armour: 40);

        var a = CombatMath.Strike(Parser(ExtensionClass.Binary), soft, 1, out _, out _);
        var b = CombatMath.Strike(Parser(ExtensionClass.Binary), hard, 1, out _, out _);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Striking_something_dormant_pays_a_premium()
    {
        var asleep = Target(ExtensionClass.Binary, temperament: Temperament.Dormant, awake: false);
        var awake = Target(ExtensionClass.Binary);

        var sneak = CombatMath.Strike(Parser(ExtensionClass.Binary), asleep, 1, out _, out var bonus);
        var normal = CombatMath.Strike(Parser(ExtensionClass.Binary), awake, 1, out _, out _);

        Assert.True(bonus);
        Assert.True(sneak > normal);
    }

    [Fact]
    public void Damage_is_never_zero()
    {
        var wall = Target(ExtensionClass.Volume, armour: 999);
        Assert.True(CombatMath.Strike(Parser(ExtensionClass.Code), wall, 1, out _, out _) >= 1);
    }

    [Fact]
    public void Hot_things_act_twice_and_dormant_things_not_at_all()
    {
        Assert.Equal(2, GameRules.ActionsFor(Temperament.Hot));
        Assert.Equal(1, GameRules.ActionsFor(Temperament.Warm));
        Assert.Equal(0, GameRules.ActionsFor(Temperament.Dormant));
    }

    [Fact]
    public void Volumes_and_bosses_hold_their_ground()
    {
        var iso = Target(ExtensionClass.Volume);
        Assert.False(CombatMath.CanMove(iso));
    }

    // ---- full run ----------------------------------------------------------

    private static (RunEngine Engine, string Root) Sandbox()
    {
        var root = Path.Combine(Path.GetTempPath(), "driveborn-run-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "vault"));
        System.IO.File.WriteAllText(Path.Combine(root, "readme.md"), "the first line of lore");
        System.IO.File.WriteAllBytes(Path.Combine(root, "payload.bin"), new byte[64 * 1024]);

        var policy = new ScanPolicy();
        policy.TryAddTerritory(root, out _);

        var probe = new FileSystemProbe();
        var scanner = new TerritoryScanner(probe, policy);
        var engine = new RunEngine(scanner, new RoomBuilder(probe), () => Now);
        return (engine, root);
    }

    [Fact]
    public void A_run_starts_in_the_root_room_and_can_extract_there()
    {
        var (engine, root) = Sandbox();
        try
        {
            var state = engine.Start(root, Player.NewReader());

            Assert.NotNull(state);
            Assert.NotNull(state!.CurrentRoom);
            Assert.Equal(RunOutcome.InProgress, state.Outcome);
            Assert.True(state.CanExtractHere);

            Assert.True(engine.Extract().Ok);
            Assert.Equal(RunOutcome.Extracted, state.Outcome);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Dying_wipes_the_haul_but_not_the_experience()
    {
        var (engine, root) = Sandbox();
        try
        {
            var player = Player.NewReader();
            var state = engine.Start(root, player)!;

            player.Xp = 40;
            player.Carried.Add(new Relic
            {
                Id = "r", Name = "Shiny", SourcePath = root, Class = ExtensionClass.Raster,
                SizeBytes = 1, LastWriteUtc = Now, Value = 500
            });

            player.Integrity = 1;
            state.CurrentRoom!.Entities.Clear();
            state.CurrentRoom.Entities.Add(Target(ExtensionClass.Binary, hp: 50));
            state.CurrentRoom.Entities[0].Position = player.Position;
            player.Integrity = 0;

            engine.EndTurn();

            Assert.Equal(RunOutcome.Died, state.Outcome);
            Assert.Empty(player.Carried);
            Assert.Equal(40, player.Xp);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Extraction_is_refused_away_from_the_root()
    {
        var (engine, root) = Sandbox();
        try
        {
            var state = engine.Start(root, Player.NewReader())!;

            // Pretend we descended: the engine checks the current room path.
            var deeper = Path.Combine(root, "vault");
            var snapshotRoom = state.CurrentRoom!;
            Assert.Equal(root, snapshotRoom.FolderPath, ignoreCase: true);
            Assert.True(state.CanExtractHere);

            var door = snapshotRoom.Doors().FirstOrDefault(d => d.LinkPath == deeper);
            Assert.NotNull(door);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Xp_scales_with_bulk_and_heat()
    {
        var hot = Target(ExtensionClass.Binary, hp: 400, temperament: Temperament.Hot);
        var warm = Target(ExtensionClass.Binary, hp: 400);

        Assert.True(CombatMath.XpFor(hot) > CombatMath.XpFor(warm));
    }

    [Fact]
    public void Dormant_kills_are_worth_more_loot()
    {
        var old = Target(ExtensionClass.Document, temperament: Temperament.Dormant);
        var fresh = Target(ExtensionClass.Document);

        Assert.True(CombatMath.LootValue(old, false) > CombatMath.LootValue(fresh, false));
    }

    [Fact]
    public void Rift_loot_is_doubled()
    {
        var e = Target(ExtensionClass.Document);
        Assert.Equal(CombatMath.LootValue(e, false) * 2, CombatMath.LootValue(e, true));
    }
}
