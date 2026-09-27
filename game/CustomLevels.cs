using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>The player's own levels: user://levels/&lt;slug&gt;.lvl plus &lt;slug&gt;.route (the author's clear, used as the author ghost).</summary>
public static class CustomLevels
{
    /// <summary>Next to the save file (user://levels normally; the self-test's temp folder under --save).</summary>
    public static string Dir => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Store.Path) ?? ProjectSettings.GlobalizePath("user://"), "levels");

    public sealed record Entry(string Slug, EditLevel Level);

    public static List<Entry> List()
    {
        var list = new List<Entry>();
        if (!System.IO.Directory.Exists(Dir)) return list;
        foreach (var f in System.IO.Directory.GetFiles(Dir, "*.lvl").OrderByDescending(System.IO.File.GetLastWriteTimeUtc))
        {
            try
            {
                var e = EditLevel.FromText(System.IO.File.ReadAllText(f));
                var route = System.IO.Path.ChangeExtension(f, ".route");
                if (System.IO.File.Exists(route)) e.AuthorRoute = System.IO.File.ReadAllText(route);
                list.Add(new Entry(System.IO.Path.GetFileNameWithoutExtension(f), e));
            }
            catch (Exception ex) { GD.PushWarning($"skipping unreadable level {f}: {ex.Message}"); }
        }
        return list;
    }

    public static string NewSlug(string name)
    {
        var baseSlug = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        if (baseSlug.Length == 0) baseSlug = "level";
        if (baseSlug.Length > 40) baseSlug = baseSlug[..40];
        string slug = baseSlug;
        for (int i = 2; System.IO.File.Exists(System.IO.Path.Combine(Dir, slug + ".lvl")); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }

    public static void Save(string slug, EditLevel e)
    {
        System.IO.Directory.CreateDirectory(Dir);
        var path = System.IO.Path.Combine(Dir, slug + ".lvl");
        System.IO.File.WriteAllText(path + ".tmp", e.ToText());
        System.IO.File.Move(path + ".tmp", path, true);
        var route = System.IO.Path.ChangeExtension(path, ".route");
        if (e.AuthorTicks > 0 && e.AuthorRoute.Length > 0) System.IO.File.WriteAllText(route, e.AuthorRoute);
        else if (System.IO.File.Exists(route)) System.IO.File.Delete(route);
    }

    public static void Delete(string slug)
    {
        foreach (var ext in new[] { ".lvl", ".route" })
        {
            var p = System.IO.Path.Combine(Dir, slug + ext);
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }
    }

    /// <summary>Import a share code into the library. Returns the new slug.</summary>
    public static string Import(string code)
    {
        var (text, route) = LevelText.FromCode(code);
        var e = EditLevel.FromText(text);
        if (route != null) e.AuthorRoute = route;
        var slug = NewSlug(e.Name);
        Save(slug, e);
        return slug;
    }

    public static string Code(EditLevel e) => LevelText.ToCode(e.ToText(), e.AuthorTicks > 0 ? e.AuthorRoute : null);

    /// <summary>A playable LevelInfo for a custom level. Its "dev" time and ghost are the author's clear.</summary>
    public static LevelInfo Info(EditLevel e) => new()
    {
        Index = -1,
        Id = "custom:" + e.ContentHash(),
        Level = Level.Parse("custom", e.ToText()),
        DevRoute = e.AuthorRoute.Length > 0 ? Route.Parse(e.AuthorRoute) : new Route(),
        DevTicks = e.AuthorTicks,
        Custom = true,
    };
}
