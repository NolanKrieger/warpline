using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>
/// Where achievements (and later cloud saves) go. Local always; a Steam implementation plugs in here once the
/// game has a Steamworks App ID (see docs/STEAM.md) — ids below are the API names to create on the partner site.
/// </summary>
public interface IPlatform
{
    string Name { get; }
    void Unlock(string id);
}

public sealed class LocalPlatform : IPlatform
{
    public string Name => "local";
    public void Unlock(string id) { }
}

public static class Achievements
{
    public sealed record Def(string Id, string Name, string Description);

    public static readonly Def[] All =
    {
        new("FIRST_STEPS", "Boot Sequence", "Finish the first level."),
        new("SPEEDY_THING", "Speedy Thing Goes In", "Come out of a portal faster than 40 tiles a second."),
        new("TERMINAL", "Terminal Velocity", "Fall as fast as anything can fall."),
        new("HALFWAY", "Halfway There", "Finish ten levels."),
        new("ALL_LEVELS", "Warpline", "Finish every level."),
        new("DEV_ONE", "Developer", "Beat a developer time."),
        new("GOLD_ALL", "Gold Rush", "Earn gold or better on every level."),
        new("DEV_ALL", "Dev Kit", "Earn the dev medal on every level."),
        new("FULL_RUN", "Full Send", "Finish a full-game run."),
        new("DEATHLESS", "Clean Run", "Finish a full-game run without dying or restarting."),
        new("SUB_DEV_RUN", "Faster Than Us", "Finish a full-game run faster than all the dev times added up."),
        new("RETURN_TO_SENDER", "Return to Sender", "Press a button with a bullet you sent through your portals."),
        new("ARCHITECT", "Architect", "Clear the check on a level you built."),
        new("SHARED", "Word of Mouth", "Share a level code, or play one someone shared."),
    };

    public static IPlatform Platform { get; set; } = new LocalPlatform();
    public static event Action<Def>? Unlocked;

    public static bool Has(string id) => Store.Data.Achievements.Contains(id);

    public static void Unlock(string id)
    {
        if (Has(id)) return;
        var def = All.FirstOrDefault(d => d.Id == id);
        if (def == null) { GD.PushWarning("unknown achievement " + id); return; }
        Store.Data.Achievements.Add(id);
        Store.Save();
        Platform.Unlock(id);
        Unlocked?.Invoke(def);
    }

    /// <summary>Re-check the save-derived ones (after any finish or run).</summary>
    public static void CheckProgress()
    {
        var levels = Catalog.Levels;
        int done = levels.Count(l => Store.Record(l.Id) != null);
        if (done >= 1 && Store.Record(levels[0].Id) != null) Unlock("FIRST_STEPS");
        if (done >= 10) Unlock("HALFWAY");
        if (done == levels.Count) Unlock("ALL_LEVELS");
        var medals = levels.Select(l => Store.Record(l.Id) is { } r ? Medals.For(r.PbTicks, l.DevTicks) : Medal.None).ToList();
        if (medals.Any(m => m == Medal.Dev)) Unlock("DEV_ONE");
        if (medals.All(m => m >= Medal.Gold)) Unlock("GOLD_ALL");
        if (medals.All(m => m == Medal.Dev)) Unlock("DEV_ALL");
    }
}
