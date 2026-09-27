using System.Globalization;
using System.Text;
using Warpline.Sim;

namespace Warpline;

/// <summary>One machine in the editor: a flat bag of fields that serializes to a level object line.</summary>
public sealed class EditObj
{
    public string Kind = "door";          // door | button | laser | receiver | turret | mover
    public int X, Y, W = 1, H = 1;
    public int Ch = 1;                    // channel 1..4: buttons/receivers open every door on their channel
    public int Dx = 1, Dy;                // direction (laser, turret) or travel (mover)
    public string Mode = "latch";         // button: hold | latch | timed
    public double Time = 3, Period = 2, On = 1, Phase, Speed = 18, Orbit;
    public bool Metal;                    // mover kind

    public EditObj Clone() => (EditObj)MemberwiseClone();
    public bool Covers(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>
/// The editor's working copy of a level: a char grid plus machines. Converts to and from the level text format;
/// Level.Parse on ToText() is the source of truth for validity.
/// </summary>
public sealed class EditLevel
{
    public string Name = "Untitled", Hint = "";
    public int W, H;
    public char[,] Grid;
    public List<EditObj> Objects = new();
    public int AuthorTicks;
    public string AuthorRoute = "";

    public EditLevel(int w = 80, int h = 33)
    {
        W = w; H = h;
        Grid = new char[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Grid[x, y] = x < 2 || x >= w - 2 || y < 2 || y >= h - 4 ? '#' : '.';
        Grid[5, h - 5] = 'S';
        Grid[w - 6, h - 5] = 'E'; Grid[w - 6, h - 6] = 'E';
    }

    public char this[int x, int y]
    {
        get => x >= 0 && y >= 0 && x < W && y < H ? Grid[x, y] : '#';
        set { if (x >= 0 && y >= 0 && x < W && y < H) Grid[x, y] = value; }
    }

    /// <summary>Paint a cell. Spawn is unique: painting S moves it.</summary>
    public void Paint(int x, int y, char c)
    {
        if (x < 0 || y < 0 || x >= W || y >= H) return;
        if (c == 'S')
            for (int yy = 0; yy < H; yy++)
                for (int xx = 0; xx < W; xx++)
                    if (Grid[xx, yy] == 'S') Grid[xx, yy] = '.';
        Grid[x, y] = c;
    }

    public void Resize(int w, int h)
    {
        w = Math.Clamp(w, 24, 400); h = Math.Clamp(h, 16, 80);
        var g = new char[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                g[x, y] = x < W && y < H ? Grid[x, y] : (y >= H ? '#' : '.');
        Grid = g; W = w; H = h;
        Objects.RemoveAll(o => o.X >= w || o.Y >= h);
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string Dir(int dx, int dy) => dx < 0 ? "left" : dx > 0 ? "right" : dy < 0 ? "up" : "down";

    /// <summary>Level text. includeAuthor=false gives the content used for hashing (name/hint/author don't matter).</summary>
    public string ToText(bool includeAuthor = true)
    {
        var sb = new StringBuilder();
        sb.Append("name: ").Append(Name.Replace('\n', ' ')).Append('\n');
        if (Hint.Length > 0) sb.Append("hint: ").Append(Hint.Replace('\n', ' ')).Append('\n');
        if (includeAuthor && AuthorTicks > 0) sb.Append("author: ").Append(AuthorTicks).Append('\n');
        sb.Append("---\n");
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++) sb.Append(Grid[x, y]);
            sb.Append('\n');
        }
        if (Objects.Count > 0)
        {
            sb.Append("---\n");
            var doors = Objects.Where(o => o.Kind == "door").ToList();
            string DoorId(EditObj d) => $"c{d.Ch}d{doors.IndexOf(d) + 1}";
            string Opens(int ch)
            {
                var ids = doors.Where(d => d.Ch == ch).Select(DoorId).ToList();
                return ids.Count > 0 ? string.Join(',', ids) : "";
            }
            int n = 0;
            foreach (var o in Objects)
            {
                n++;
                switch (o.Kind)
                {
                    case "door": sb.Append($"door {DoorId(o)} x={o.X} y={o.Y} w={o.W} h={o.H}\n"); break;
                    case "button":
                        sb.Append($"button b{n} x={o.X} y={o.Y}").Append(Opens(o.Ch) is { Length: > 0 } ob ? $" opens={ob}" : "")
                          .Append($" mode={o.Mode}").Append(o.Mode == "timed" ? $" time={F(o.Time)}" : "").Append('\n');
                        break;
                    case "receiver": sb.Append($"receiver r{n} x={o.X} y={o.Y}").Append(Opens(o.Ch) is { Length: > 0 } orr ? $" opens={orr}" : "").Append('\n'); break;
                    case "laser":
                        sb.Append($"laser l{n} x={o.X} y={o.Y} dir={Dir(o.Dx, o.Dy)}").Append(o.Period > 0 ? $" period={F(o.Period)} on={F(o.On)} phase={F(o.Phase)}" : "").Append('\n');
                        break;
                    case "turret": sb.Append($"turret t{n} x={o.X} y={o.Y} dir={Dir(o.Dx, o.Dy)} period={F(Math.Max(0.1, o.Period))} phase={F(o.Phase)} speed={F(o.Speed)}\n"); break;
                    case "mover":
                        sb.Append($"mover m{n} x={o.X} y={o.Y} w={o.W} h={o.H}")
                          .Append(o.Orbit > 0 ? $" orbit={F(o.Orbit)}" : $" to={o.X + o.Dx},{o.Y + o.Dy}")
                          .Append($" period={F(Math.Max(0.5, o.Period))} phase={F(o.Phase)} kind={(o.Metal ? "metal" : "panel")}\n");
                        break;
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>Stable identity of the playable content (for PBs): changes whenever the level itself changes.</summary>
    public string ContentHash() => Hash(ToText(false).Split("---", 2)[1]);

    public static string Hash(string s)
    {
        ulong h = 1469598103934665603UL;
        foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
        return h.ToString("x16");
    }

    public static EditLevel FromText(string text)
    {
        var level = Level.Parse("edit", text);   // validates and parses machines
        var lines = text.Replace("\r", "").Split('\n');
        var e = new EditLevel(level.W, level.H) { Name = level.Name, Hint = level.Hint, AuthorTicks = LevelText.AuthorTicks(text) };
        int i = Array.FindIndex(lines, l => l.Trim() == "---") + 1;
        for (int y = 0; y < level.H; y++)
        {
            var row = i + y < lines.Length ? lines[i + y] : "";
            for (int x = 0; x < level.W; x++) e.Grid[x, y] = x < row.Length ? (row[x] == ' ' ? '.' : row[x]) : '.';
        }
        var m = level.Machines;
        int ChOf(string doorId)
        {
            int idx = m.DoorIndex(doorId);
            if (doorId.Length > 1 && doorId[0] == 'c' && char.IsDigit(doorId[1])) return Math.Clamp(doorId[1] - '0', 1, 4);
            return idx < 0 ? 1 : idx % 4 + 1;
        }
        foreach (var d in m.Doors) e.Objects.Add(new EditObj { Kind = "door", X = d.X, Y = d.Y, W = d.W, H = d.H, Ch = ChOf(d.Id) });
        foreach (var b in m.Buttons)
            e.Objects.Add(new EditObj { Kind = "button", X = b.X, Y = b.Y, Ch = b.Opens.Length > 0 ? ChOf(b.Opens[0]) : 1, Mode = b.Mode.ToString().ToLowerInvariant(), Time = b.TimeTicks / 120.0 });
        foreach (var r in m.Receivers) e.Objects.Add(new EditObj { Kind = "receiver", X = r.X, Y = r.Y, Ch = r.Opens.Length > 0 ? ChOf(r.Opens[0]) : 1 });
        foreach (var l in m.Lasers) e.Objects.Add(new EditObj { Kind = "laser", X = l.X, Y = l.Y, Dx = l.Dx, Dy = l.Dy, Period = l.Period / 120.0, On = l.On / 120.0, Phase = l.Phase / 120.0 });
        foreach (var t in m.Turrets) e.Objects.Add(new EditObj { Kind = "turret", X = t.X, Y = t.Y, Dx = t.Dx, Dy = t.Dy, Period = t.Period / 120.0, Phase = t.Phase / 120.0, Speed = t.Speed });
        foreach (var v in m.Movers)
            e.Objects.Add(new EditObj { Kind = "mover", X = v.X, Y = v.Y, W = v.W, H = v.H, Dx = v.Dx, Dy = v.Dy, Period = v.Period / 120.0, Phase = v.Phase / 120.0, Orbit = v.Orbit, Metal = !v.Portalable });
        return e;
    }

    /// <summary>Problems that stop the level from being played, or empty.</summary>
    public string Problem()
    {
        int spawns = 0, exits = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { if (Grid[x, y] == 'S') spawns++; if (Grid[x, y] == 'E') exits++; }
        if (spawns == 0) return "place a spawn (S)";
        if (exits == 0) return "place an exit";
        try { Level.Parse("check", ToText()); }
        catch (FormatException ex) { return ex.Message.Replace("level check: ", ""); }
        return "";
    }
}
