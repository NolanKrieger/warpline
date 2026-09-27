using Godot;
using Warpline.Sim;

namespace Warpline;

public sealed class LevelInfo
{
    public required int Index { get; init; }
    public required string Id { get; init; }
    public required Level Level { get; init; }
    public required Route DevRoute { get; init; }
    public required int DevTicks { get; init; }
    public bool Custom { get; init; }
    public string Number => Custom ? "ED" : (Index + 1).ToString("00");
}

/// <summary>Loads levels/index.txt and each level's .lvl + developer .route (the dev ghost and the dev time).</summary>
public static class Catalog
{
    public static List<LevelInfo> Levels { get; } = new();

    public static void Load()
    {
        if (Levels.Count > 0) return;
        var index = Godot.FileAccess.GetFileAsString("res://levels/index.txt");
        int i = 0;
        foreach (var raw in index.Split('\n'))
        {
            var id = raw.Trim();
            if (id.Length == 0 || id.StartsWith('#')) continue;
            var level = Level.Parse(id, Godot.FileAccess.GetFileAsString($"res://levels/{id}.lvl"));
            var route = Route.Parse(Godot.FileAccess.GetFileAsString($"res://levels/{id}.route"));
            var result = Runner.Run(level, route.Frames());
            if (!result.Finished) GD.PushWarning($"dev route for {id} does not finish");
            Levels.Add(new LevelInfo { Index = i++, Id = id, Level = level, DevRoute = route, DevTicks = result.Finished ? result.Ticks : 0 });
        }
    }
}
