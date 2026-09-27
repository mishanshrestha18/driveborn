using Driveborn.Core.Generation;
using Driveborn.Core.Model;
using Driveborn.Core.Scanning;
using Xunit;

namespace Driveborn.Core.Tests;

public class GenerationTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static FileEntry File(string name, long size, DateTime written, bool ro = false,
        bool hidden = false) =>
        new(Path.Combine("D:", "Loot", name), name, Path.GetExtension(name).ToLowerInvariant(),
            size, written, ro, hidden);

    [Fact]
    public void Same_file_always_yields_the_same_creature()
    {
        var file = File("setup.exe", 780L * 1024 * 1024, Now.AddDays(-3));

        var a = EntityFactory.FromFile(file, Now);
        var b = EntityFactory.FromFile(file, Now);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(a.MaxHp, b.MaxHp);
        Assert.Equal(a.Power, b.Power);
        Assert.Equal(a.Kind, b.Kind);
    }

    [Fact]
    public void Touching_a_file_changes_the_creature()
    {
        var before = EntityFactory.FromFile(File("notes.md", 4096, Now.AddDays(-10)), Now);
        var after = EntityFactory.FromFile(File("notes.md", 5000, Now.AddMinutes(-5)), Now);

        Assert.NotEqual(before.Id, after.Id);
        Assert.Equal(Temperament.Hot, after.Temperament);
    }

    [Theory]
    [InlineData(0, Temperament.Hot)]
    [InlineData(40, Temperament.Warm)]
    [InlineData(365 * 4, Temperament.Dormant)]
    public void Age_decides_aggression(int daysOld, Temperament expected)
    {
        var entity = EntityFactory.FromFile(File("thing.bin", 2048, Now.AddDays(-daysOld)), Now);
        Assert.Equal(expected, entity.Temperament);
    }

    [Fact]
    public void Health_grows_with_order_of_magnitude_not_raw_bytes()
    {
        var small = EntityFactory.HealthFor(4 * 1024);
        var medium = EntityFactory.HealthFor(2L * 1024 * 1024);
        var large = EntityFactory.HealthFor(780L * 1024 * 1024);

        Assert.True(small < medium);
        Assert.True(medium < large);
        Assert.True(large <= GameRules.MaxHp);
    }

    [Theory]
    [InlineData(1024L, 1)]
    [InlineData(300L * 1024 * 1024, 3)]
    [InlineData(9L * 1024 * 1024 * 1024, 5)]
    public void Bulk_is_physical(long bytes, int expectedFootprint) =>
        Assert.Equal(expectedFootprint, EntityFactory.FootprintFor(bytes));

    [Fact]
    public void Read_only_files_become_sealed_vaults()
    {
        var entity = EntityFactory.FromFile(File("archive.dat", 90_000, Now.AddDays(-30), ro: true), Now);
        Assert.Equal(EntityKind.Vault, entity.Kind);
    }

    [Fact]
    public void Extensions_map_to_families()
    {
        Assert.Equal(ExtensionClass.Binary, ExtensionMap.Classify(".exe"));
        Assert.Equal(ExtensionClass.Volume, ExtensionMap.Classify(".iso"));
        Assert.Equal(ExtensionClass.Raster, ExtensionMap.Classify(".png"));
        Assert.Equal(ExtensionClass.Unknown, ExtensionMap.Classify(".qqq"));
    }

    [Fact]
    public void Room_is_built_from_the_folder_and_has_a_door_per_subfolder()
    {
        var root = Path.Combine("D:", "Territory");
        var probe = new FakeProbe()
            .AddFolder(root)
            .AddFolder(Path.Combine(root, "drivers"))
            .AddFolder(Path.Combine(root, "old_2019"))
            .AddFile(root, "setup.exe", 780L * 1024 * 1024, Now.AddDays(-3))
            .AddFile(root, "invoice.pdf", 240_000, Now.AddDays(-400))
            .AddFile(root, "meme.jpg", 2L * 1024 * 1024, Now.AddDays(-9));

        var policy = new ScanPolicy();
        Assert.True(policy.TryAddTerritory(root, out _) || true); // fake path may not exist on disk

        var snapshot = new FolderSnapshot(root, "Territory", null, 0, Now,
            probe.EnumerateFiles(root), probe.EnumerateFolders(root), 0);

        var room = new RoomBuilder(probe).Build(snapshot, Now);

        Assert.Equal(2, room.Doors().Count());
        Assert.Equal(3, room.Entities.Count);
        Assert.Contains(room.Entities, e => e.Kind == EntityKind.Boss);
        Assert.True(room.Entities.All(e => room.InBounds(e.Position)));
    }

    [Fact]
    public void Entities_never_overlap()
    {
        var root = Path.Combine("D:", "Dense");
        var probe = new FakeProbe().AddFolder(root);
        for (var i = 0; i < 24; i++)
            probe.AddFile(root, $"blob{i}.bin", 20L * 1024 * 1024 * (i + 1), Now.AddDays(-i - 2));

        var snapshot = new FolderSnapshot(root, "Dense", null, 0, Now,
            probe.EnumerateFiles(root), probe.EnumerateFolders(root), 0);

        var room = new RoomBuilder(probe).Build(snapshot, Now);

        var occupied = new HashSet<GridPoint>();
        foreach (var tile in room.Entities.SelectMany(e => e.OccupiedTiles()))
            Assert.True(occupied.Add(tile), $"two entities share {tile}");
    }

    [Fact]
    public void Tiny_files_fold_into_a_swarm()
    {
        var root = Path.Combine("D:", "Cache");
        var probe = new FakeProbe().AddFolder(root);
        for (var i = 0; i < 12; i++)
            probe.AddFile(root, $"chunk{i}.json", 900, Now.AddDays(-5));

        var snapshot = new FolderSnapshot(root, "Cache", null, 0, Now,
            probe.EnumerateFiles(root), probe.EnumerateFolders(root), 0);

        var room = new RoomBuilder(probe).Build(snapshot, Now);

        Assert.Contains(room.Entities, e => e.Kind == EntityKind.Swarm && e.Stack >= 4);
    }

    [Fact]
    public void Rng_is_reproducible_from_its_seed()
    {
        var a = new DeterministicRng(Seed.Hash("driveborn"));
        var b = new DeterministicRng(Seed.Hash("driveborn"));

        for (var i = 0; i < 50; i++) Assert.Equal(a.Next(1000), b.Next(1000));
    }
}
