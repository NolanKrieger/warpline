using System.Text.Json;
using Godot;

namespace Warpline;

public sealed class LevelRecord
{
    public int PbTicks { get; set; }
    public string PbRoute { get; set; } = "";
}

public sealed class RunRecord
{
    public int PbTicks { get; set; }
    public List<int> Splits { get; set; } = new();        // cumulative ticks at each level's finish, from the PB run
    public List<int> BestSegments { get; set; } = new();  // fastest ever time for each level inside any run ("golds")

    public int SumOfBest(int levels) =>
        BestSegments.Count >= levels && BestSegments.Take(levels).All(t => t > 0) ? BestSegments.Take(levels).Sum() : 0;
}

public sealed class Settings
{
    public int MasterVolume { get; set; } = 80;       // 0..100
    public int SfxVolume { get; set; } = 80;
    public int MusicVolume { get; set; } = 70;
    public bool Fullscreen { get; set; }
    public bool ReduceMotion { get; set; }
    /// <summary>action -> inputs, each "key:&lt;physical keycode&gt;" or "mouse:&lt;button index&gt;". Missing = defaults.</summary>
    public Dictionary<string, List<string>> Bindings { get; set; } = new();
}

public sealed class SaveData
{
    public int Version { get; set; } = 1;
    public Dictionary<string, LevelRecord> Levels { get; set; } = new();
    public RunRecord Run { get; set; } = new();
    public string Ghost { get; set; } = "pb";         // pb | dev | off
    public Settings Settings { get; set; } = new();
    public HashSet<string> Achievements { get; set; } = new();
}

/// <summary>One JSON save in user:// (or a path given with --save=... for tests). Written atomically.</summary>
public static class Store
{
    public static string Path { get; set; } = "";
    public static SaveData Data { get; private set; } = new();

    public static void Load()
    {
        if (Path.Length == 0) Path = ProjectSettings.GlobalizePath("user://save.json");
        try
        {
            Data = File.Exists(Path) ? JsonSerializer.Deserialize<SaveData>(File.ReadAllText(Path)) ?? new SaveData() : new SaveData();
        }
        catch (Exception e)
        {
            GD.PushWarning($"save unreadable ({e.Message}); starting fresh, old file kept as .bad");
            try { File.Copy(Path, Path + ".bad", true); } catch { }
            Data = new SaveData();
        }
    }

    public static void Save()
    {
        var dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = Path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, Path, true);
    }

    public static LevelRecord? Record(string id) => Data.Levels.TryGetValue(id, out var r) && r.PbTicks > 0 ? r : null;

    /// <summary>Linear unlocks: a level opens once the one before it has been finished.</summary>
    public static bool Unlocked(int index) => index == 0 || (index - 1 < Catalog.Levels.Count && Record(Catalog.Levels[index - 1].Id) != null);

    public static bool AllFinished => Catalog.Levels.All(l => Record(l.Id) != null);
}
