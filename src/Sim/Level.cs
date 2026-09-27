using System.Text;

namespace Warpline.Sim;

public enum Tile : byte { Empty, Panel, Metal, Spike, Grill, Exit, SpeedGel, BounceGel }

public static class Tiles
{
    public static bool Solid(Tile t) => t is Tile.Panel or Tile.Metal or Tile.SpeedGel or Tile.BounceGel;
    public static bool Portalable(Tile t) => t == Tile.Panel;

    public static Tile FromChar(char c) => c switch
    {
        '#' => Tile.Panel,
        'X' => Tile.Metal,
        '^' => Tile.Spike,
        ':' => Tile.Grill,
        'E' => Tile.Exit,
        '=' => Tile.SpeedGel,
        '%' => Tile.BounceGel,
        _ => Tile.Empty,
    };

    public static char ToChar(Tile t) => t switch
    {
        Tile.Panel => '#',
        Tile.Metal => 'X',
        Tile.Spike => '^',
        Tile.Grill => ':',
        Tile.Exit => 'E',
        Tile.SpeedGel => '=',
        Tile.BounceGel => '%',
        _ => '.',
    };
}

/// <summary>
/// A level: a tile grid plus a spawn point. Text format:
/// <code>
/// name: Level Name
/// hint: optional one-line hint
/// ---
/// XXXXXXXX
/// X..S..EX
/// XXXXXXXX
/// </code>
/// '#' portal panel, 'X' metal (no portals), '^' spikes, ':' fizzler grill, 'E' exit, 'S' spawn, '.' empty,
/// '=' speed gel (fast, slippery floor), '%' bounce gel (keeps your speed and throws you back). Gel never takes portals.
/// Outside the grid: walls on the sides and top, open air below (falling out = death).
/// An optional second "---" is followed by machine lines (doors, buttons, lasers, receivers, turrets): see <see cref="Machines.Parse"/>.
/// </summary>
public sealed class Level
{
    public string Id { get; }
    public string Name { get; }
    public string Hint { get; }
    public int W { get; }
    public int H { get; }
    /// <summary>Spawn position as the hitbox centre (standing, feet on the floor under 'S').</summary>
    public V2 Spawn { get; }
    public Machines Machines { get; }
    private readonly Tile[] _cells;
    private readonly short[] _doorAt;   // door index + 1 per cell, 0 = none

    public Level(string id, string name, string hint, int w, int h, Tile[] cells, V2 spawn, Machines? machines = null)
    {
        Id = id; Name = name; Hint = hint; W = w; H = h; _cells = cells; Spawn = spawn;
        Machines = machines ?? new Machines();
        _doorAt = new short[w * h];
        for (int i = 0; i < Machines.Doors.Count; i++)
        {
            var d = Machines.Doors[i];
            for (int y = d.Y; y < d.Y + d.H; y++)
                for (int x = d.X; x < d.X + d.W; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h) _doorAt[y * w + x] = (short)(i + 1);
        }
    }

    /// <summary>Index of the door covering this cell, or -1.</summary>
    public int DoorAt(int x, int y) => x < 0 || y < 0 || x >= W || y >= H ? -1 : _doorAt[y * W + x] - 1;

    public Tile this[int x, int y]
    {
        get
        {
            if (y >= H) return Tile.Empty;             // pits
            if (x < 0 || x >= W || y < 0) return Tile.Metal;
            return _cells[y * W + x];
        }
    }

    public static Level Parse(string id, string text)
    {
        var lines = text.Replace("\r", "").Split('\n');
        string name = id, hint = "";
        int i = 0;
        for (; i < lines.Length; i++)
        {
            var l = lines[i];
            if (l.Trim() == "---") { i++; break; }
            if (l.StartsWith("name:")) name = l[5..].Trim();
            else if (l.StartsWith("hint:")) hint = l[5..].Trim();
        }
        var rest = lines.Skip(i).Select(l => l.TrimEnd()).ToList();
        int sep = rest.FindIndex(l => l.Trim() == "---");
        var rows = sep >= 0 ? rest.Take(sep).ToList() : rest;
        var objectLines = sep >= 0 ? rest.Skip(sep + 1) : Enumerable.Empty<string>();
        while (rows.Count > 0 && rows[^1].Length == 0) rows.RemoveAt(rows.Count - 1);
        if (rows.Count == 0) throw new FormatException($"level {id}: no grid");
        int w = rows.Max(r => r.Length), h = rows.Count;
        var cells = new Tile[w * h];
        int sx = -1, sy = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                char c = x < rows[y].Length ? rows[y][x] : '.';
                if (c == 'S') { sx = x; sy = y; }
                cells[y * w + x] = Tiles.FromChar(c);
            }
        if (sx < 0) throw new FormatException($"level {id}: no spawn 'S'");
        // Feet on the first solid tile at or below S.
        int fy = sy;
        while (fy < h && !Tiles.Solid(cells[fy * w + sx])) fy++;
        if (fy >= h) throw new FormatException($"level {id}: no floor under spawn");
        var spawn = new V2(sx + 0.5, fy - Tuning.HalfH);
        var machines = Machines.Parse(objectLines, id);
        // A door owns its cells: whatever the grid drew there is replaced by the door.
        foreach (var d in machines.Doors)
            for (int y = d.Y; y < d.Y + d.H; y++)
                for (int x = d.X; x < d.X + d.W; x++)
                    if (x >= 0 && y >= 0 && x < w && y < h) cells[y * w + x] = Tile.Empty;
        return new Level(id, name, hint, w, h, cells, spawn, machines);
    }

    public string GridText()
    {
        var sb = new StringBuilder();
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++) sb.Append(Tiles.ToChar(this[x, y]));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
