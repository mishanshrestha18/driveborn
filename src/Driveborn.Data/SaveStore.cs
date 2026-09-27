using System.Text.Json;
using System.Text.Json.Serialization;

namespace Driveborn.Data;

/// <summary>
/// Profile persistence. Plain JSON under LocalAppData, written atomically via a
/// temp file so a crash mid-save cannot leave a shredded profile. No database
/// engine, no network, no telemetry - the save never leaves the machine.
/// </summary>
public sealed class SaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;

    public SaveStore(string? overridePath = null)
    {
        _path = overridePath ?? DefaultPath();
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public string Path_ => _path;

    public static string DefaultPath() => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Driveborn", "profile.json");

    public SaveGame Load()
    {
        try
        {
            if (!File.Exists(_path)) return SaveGame.NewProfile();

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json)) return SaveGame.NewProfile();

            return JsonSerializer.Deserialize<SaveGame>(json, Options) ?? SaveGame.NewProfile();
        }
        catch (Exception)
        {
            // A corrupt profile should never block play. Keep the bad file aside
            // so it can be inspected, and start fresh.
            TryQuarantine();
            return SaveGame.NewProfile();
        }
    }

    public void Save(SaveGame save)
    {
        var json = JsonSerializer.Serialize(save, Options);
        var temp = _path + ".tmp";

        File.WriteAllText(temp, json);

        if (File.Exists(_path)) File.Replace(temp, _path, null);
        else File.Move(temp, _path);
    }

    private void TryQuarantine()
    {
        try
        {
            if (File.Exists(_path))
                File.Move(_path, _path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}", overwrite: true);
        }
        catch { /* best effort */ }
    }
}
