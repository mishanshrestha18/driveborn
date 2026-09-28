using Driveborn.Core.Scanning;
using Driveborn.Core.Tutorial;
using Xunit;

namespace Driveborn.Core.Tests;

public class NoiseFolderTests
{
    [Theory]
    [InlineData(".git")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("node_modules")]
    [InlineData("__pycache__")]
    [InlineData("graphify-out")]
    [InlineData(".vs")]
    public void Build_and_tooling_folders_never_become_rooms(string name) =>
        Assert.True(ScanPolicy.IsNoiseFolder(name), $"{name} should be filtered out");

    [Theory]
    [InlineData("Photos")]
    [InlineData("src")]
    [InlineData("2019 backup")]
    [InlineData("Documents")]
    public void Real_folders_still_become_rooms(string name) =>
        Assert.False(ScanPolicy.IsNoiseFolder(name));

    [Fact]
    public void Noise_folders_are_hidden_from_doors()
    {
        var root = Path.Combine("D:", "Repo");
        var probe = new FakeProbe()
            .AddFolder(root)
            .AddFolder(Path.Combine(root, "src"))
            .AddFolder(Path.Combine(root, ".git"))
            .AddFolder(Path.Combine(root, "obj"))
            .AddFile(root, "readme.md", 4000, DateTime.UtcNow.AddDays(-5));

        var visible = probe.EnumerateFolders(root)
            .Where(d => !ScanPolicy.IsNoiseFolder(d.Name))
            .Select(d => d.Name)
            .ToList();

        Assert.Equal(new[] { "src" }, visible);
    }

    [Fact]
    public void Noise_filtering_is_not_a_safety_boundary()
    {
        // A noise folder is merely uninteresting; the deny-list is what protects
        // anything. Keeping these distinct matters - conflating them would make it
        // tempting to relax one and silently weaken the other.
        Assert.True(ScanPolicy.IsNoiseFolder("bin"));
        Assert.False(ScanPolicy.IsDeniedFolder(@"D:\Projects\app\bin"));
    }
}

public class TutorialCoachTests
{
    [Fact]
    public void Coach_is_dormant_until_started()
    {
        var coach = new TutorialCoach();
        Assert.False(coach.IsActive);
        Assert.Null(coach.Current);
    }

    [Fact]
    public void Starting_shows_the_first_card()
    {
        var coach = new TutorialCoach();
        coach.Start();

        Assert.True(coach.IsActive);
        Assert.NotNull(coach.Current);
        Assert.Equal(1, coach.StepNumber);
    }

    [Fact]
    public void An_unrelated_action_does_not_advance_a_waiting_step()
    {
        var coach = new TutorialCoach();
        coach.Start();
        coach.Advance(); // step 2 waits for Moved

        var before = coach.StepNumber;
        coach.Notify(TutorialTrigger.Extracted);

        Assert.Equal(before, coach.StepNumber);
    }

    [Fact]
    public void The_awaited_action_advances_the_step()
    {
        var coach = new TutorialCoach();
        coach.Start();
        coach.Advance(); // step 2 waits for Moved

        var before = coach.StepNumber;
        coach.Notify(TutorialTrigger.Moved);

        Assert.Equal(before + 1, coach.StepNumber);
    }

    [Fact]
    public void Manual_steps_ignore_triggers_entirely()
    {
        var coach = new TutorialCoach();
        coach.Start(); // step 1 is manual

        coach.Notify(TutorialTrigger.Moved);
        coach.Notify(TutorialTrigger.Killed);

        Assert.Equal(1, coach.StepNumber);
    }

    [Fact]
    public void Every_step_can_be_reached_by_pressing_Next()
    {
        // The player must never be able to get stuck on a lesson their own folder
        // cannot demonstrate - e.g. a room with nothing their parsers match.
        var coach = new TutorialCoach();
        coach.Start();

        for (var i = 0; i < coach.StepCount; i++) coach.Advance();

        Assert.True(coach.IsFinished);
        Assert.False(coach.IsActive);
        Assert.Null(coach.Current);
    }

    [Fact]
    public void Stopping_ends_it_immediately()
    {
        var coach = new TutorialCoach();
        coach.Start();
        coach.Stop();

        Assert.False(coach.IsActive);
        Assert.Null(coach.Current);
    }

    [Fact]
    public void Changed_fires_on_every_transition()
    {
        var coach = new TutorialCoach();
        var fired = 0;
        coach.Changed += () => fired++;

        coach.Start();
        coach.Advance();
        coach.Stop();

        Assert.Equal(3, fired);
    }

    [Fact]
    public void Every_card_has_real_text()
    {
        var coach = new TutorialCoach();
        coach.Start();

        while (coach.Current is { } step)
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Title));
            Assert.True(step.Body.Length > 40, $"step '{step.Title}' is too thin to teach anything");
            coach.Advance();
        }
    }
}
