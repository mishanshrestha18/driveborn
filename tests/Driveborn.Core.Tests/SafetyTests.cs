using Driveborn.Core.Scanning;
using Xunit;

namespace Driveborn.Core.Tests;

/// <summary>
/// These tests exist because the app points itself at a real person's drive. If
/// any of them go red, the app should not ship.
/// </summary>
public class SafetyTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files\Something")]
    [InlineData(@"C:\Users\me\.ssh")]
    [InlineData(@"C:\Users\me\.aws\cli")]
    [InlineData(@"C:\Users\me\AppData\Local\Microsoft\Edge")]
    [InlineData(@"D:\$RECYCLE.BIN")]
    [InlineData(@"C:\System Volume Information")]
    public void System_and_credential_paths_are_permanently_denied(string path) =>
        Assert.True(ScanPolicy.IsDeniedFolder(path), $"{path} should be denied");

    [Theory]
    [InlineData(@"D:\Projects\app")]
    [InlineData(@"E:\Photos\2019")]
    public void Ordinary_user_folders_are_allowed(string path) =>
        Assert.False(ScanPolicy.IsDeniedFolder(path));

    [Theory]
    [InlineData(".env")]
    [InlineData(".env.production")]
    [InlineData("id_rsa")]
    [InlineData("server.pem")]
    [InlineData("cert.pfx")]
    [InlineData("vault.kdbx")]
    [InlineData("credentials")]
    public void Secret_files_never_become_entities(string name) =>
        Assert.True(ScanPolicy.IsDeniedFile(name), $"{name} should be excluded");

    [Theory]
    [InlineData("holiday.jpg")]
    [InlineData("notes.md")]
    [InlineData("setup.exe")]
    public void Ordinary_files_are_fair_game(string name) =>
        Assert.False(ScanPolicy.IsDeniedFile(name));

    [Fact]
    public void Nothing_is_scannable_until_a_territory_is_added()
    {
        var policy = new ScanPolicy();
        Assert.False(policy.IsScannable(@"D:\Projects"));
        Assert.Empty(policy.Territories);
    }

    [Fact]
    public void Scanning_is_confined_to_opted_in_roots()
    {
        var policy = new ScanPolicy();
        var root = Path.Combine(Path.GetTempPath(), "driveborn-test-root");
        Directory.CreateDirectory(root);

        Assert.True(policy.TryAddTerritory(root, out _));

        Assert.True(policy.IsScannable(root));
        Assert.True(policy.IsScannable(Path.Combine(root, "inner")));
        Assert.False(policy.IsScannable(Path.GetTempPath()));
        Assert.False(policy.IsScannable(@"C:\Windows"));

        Directory.Delete(root);
    }

    [Fact]
    public void A_denied_folder_cannot_be_added_as_a_territory()
    {
        var policy = new ScanPolicy();
        Assert.False(policy.TryAddTerritory(@"C:\Windows\System32", out var reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void The_probe_interface_exposes_no_way_to_modify_a_disk()
    {
        var members = typeof(IFileSystemProbe).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(members, m =>
            m.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Move", StringComparison.OrdinalIgnoreCase) ||
            m.Contains("Create", StringComparison.OrdinalIgnoreCase));
    }
}
