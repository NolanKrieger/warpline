using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Warpline.Sim;

/// <summary>Writing levels back to text (the editor's save format) and packing them into share codes.</summary>
public static class LevelText
{
    static string F(double v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    static string Sec(int ticks) => F(ticks / (double)Tuning.TicksPerSecond);
    static string Dir(int dx, int dy) => dx < 0 ? "left" : dx > 0 ? "right" : dy < 0 ? "up" : "down";

    /// <summary>Full level text: header, grid, machines. Level.Parse(ToText(l)) reproduces the level.</summary>
    public static string ToText(Level l, int authorTicks = 0)
    {
        var sb = new StringBuilder();
        sb.Append("name: ").Append(l.Name).Append('\n');
        if (l.Hint.Length > 0) sb.Append("hint: ").Append(l.Hint).Append('\n');
        if (authorTicks > 0) sb.Append("author: ").Append(authorTicks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("---\n");
        int sx = (int)Math.Floor(l.Spawn.X), sy = (int)Math.Floor(l.Spawn.Y + Tuning.HalfH - 1e-9);
        for (int y = 0; y < l.H; y++)
        {
            for (int x = 0; x < l.W; x++)
                sb.Append(x == sx && y == sy && l[x, y] == Tile.Empty ? 'S' : Tiles.ToChar(l[x, y]));
            sb.Append('\n');
        }
        var m = l.Machines;
        if (m.Any || m.Movers.Count > 0)
        {
            sb.Append("---\n");
            foreach (var d in m.Doors) sb.Append($"door {d.Id} x={d.X} y={d.Y} w={d.W} h={d.H}\n");
            foreach (var b in m.Buttons)
                sb.Append($"button {b.Id} x={b.X} y={b.Y} opens={string.Join(',', b.Opens)} mode={b.Mode.ToString().ToLowerInvariant()}")
                  .Append(b.Mode == ButtonMode.Timed ? $" time={Sec(b.TimeTicks)}" : "").Append('\n');
            foreach (var z in m.Lasers)
                sb.Append($"laser {z.Id} x={z.X} y={z.Y} dir={Dir(z.Dx, z.Dy)}")
                  .Append(z.Period > 0 ? $" period={Sec(z.Period)} on={Sec(z.On)} phase={Sec(z.Phase)}" : "").Append('\n');
            foreach (var r in m.Receivers) sb.Append($"receiver {r.Id} x={r.X} y={r.Y} opens={string.Join(',', r.Opens)}\n");
            foreach (var t in m.Turrets)
                sb.Append($"turret {t.Id} x={t.X} y={t.Y} dir={Dir(t.Dx, t.Dy)} period={Sec(t.Period)} phase={Sec(t.Phase)} speed={F(t.Speed)}\n");
            foreach (var v in m.Movers)
            {
                sb.Append($"mover {v.Id} x={v.X} y={v.Y} w={v.W} h={v.H}");
                sb.Append(v.Orbit > 0 ? $" orbit={F(v.Orbit)}" : $" to={v.X + v.Dx},{v.Y + v.Dy}");
                sb.Append($" period={Sec(v.Period)} phase={Sec(v.Phase)} kind={(v.Portalable ? "panel" : "metal")}\n");
            }
        }
        return sb.ToString();
    }

    /// <summary>The "author:" header (best clear by the level's maker), or 0.</summary>
    public static int AuthorTicks(string text)
    {
        foreach (var line in text.Replace("\r", "").Split('\n'))
        {
            if (line.Trim() == "---") break;
            if (line.StartsWith("author:") && int.TryParse(line[7..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)) return t;
        }
        return 0;
    }

    const string Prefix = "WL1-";
    const char RouteSep = '\u001e';

    /// <summary>Pack a level (and optionally the author's route, for the author ghost) into a copy-paste code.</summary>
    public static string ToCode(string levelText, string? authorRoute = null)
    {
        var payload = Encoding.UTF8.GetBytes(levelText + (authorRoute is { Length: > 0 } ? RouteSep + authorRoute : ""));
        using var ms = new MemoryStream();
        using (var br = new BrotliStream(ms, CompressionLevel.SmallestSize, leaveOpen: true)) br.Write(payload);
        return Prefix + Convert.ToBase64String(ms.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Unpack a share code. Throws FormatException with a readable message when it isn't one.</summary>
    public static (string levelText, string? authorRoute) FromCode(string code)
    {
        code = new string(code.Where(c => !char.IsWhiteSpace(c)).ToArray());
        if (!code.StartsWith(Prefix)) throw new FormatException("not a Warpline level code (should start with WL1-)");
        var b64 = code[Prefix.Length..].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
        byte[] raw;
        try { raw = Convert.FromBase64String(b64); }
        catch (FormatException) { throw new FormatException("the code is damaged (not valid base64)"); }
        string text;
        try
        {
            using var br = new BrotliStream(new MemoryStream(raw), CompressionMode.Decompress);
            using var outMs = new MemoryStream();
            br.CopyTo(outMs);
            text = Encoding.UTF8.GetString(outMs.ToArray());
        }
        catch (Exception) { throw new FormatException("the code is damaged (can't unpack it)"); }
        int sep = text.IndexOf(RouteSep);
        return sep < 0 ? (text, null) : (text[..sep], text[(sep + 1)..]);
    }
}
