using System.Globalization;

namespace Warpline.Sim;

public enum ButtonMode { Hold, Latch, Timed }

/// <summary>A door: a rectangle of cells that is solid (and not portalable) while closed.</summary>
public sealed record DoorDef(string Id, int X, int Y, int W, int H)
{
    public bool Contains(int x, int y) => x >= X && x < X + W && y >= Y && y < Y + H;
}

/// <summary>A button occupies one open cell; touching it presses it. Opens every door in Opens.</summary>
public sealed record ButtonDef(string Id, int X, int Y, string[] Opens, ButtonMode Mode, int TimeTicks);

/// <summary>A laser emitter in an open cell, firing along (Dx, Dy). On for On ticks of every Period (Period 0 = always).</summary>
public sealed record LaserDef(string Id, int X, int Y, int Dx, int Dy, int Period, int On, int Phase)
{
    public bool IsOn(int tick) => Period <= 0 || ((tick + Phase) % Period + Period) % Period < On;
    /// <summary>Ticks until the beam turns on (0 when on). Drives the warm-up flicker.</summary>
    public int TicksToOn(int tick)
    {
        if (IsOn(tick)) return 0;
        int t = ((tick + Phase) % Period + Period) % Period;
        return Period - t;
    }
}

/// <summary>A laser receiver in an open cell: lit while a beam reaches it, opening its doors.</summary>
public sealed record ReceiverDef(string Id, int X, int Y, string[] Opens);

/// <summary>A turret in an open cell, firing a bullet along (Dx, Dy) every Period ticks (offset by Phase).</summary>
public sealed record TurretDef(string Id, int X, int Y, int Dx, int Dy, int Period, int Phase, double Speed);

/// <summary>
/// A moving panel: a W×H block at (X, Y) that glides to (X + Dx, Y + Dy) and back every Period ticks (eased at the ends).
/// Portalable panels carry portals with them. The robot rides on top, gets pushed, and is crushed if pinned.
/// </summary>
public sealed record MoverDef(string Id, int X, int Y, int W, int H, int Dx, int Dy, int Period, int Phase, bool Portalable, double Orbit = 0)
{
    /// <summary>
    /// Offset from the rest position at a given tick: an eased ping-pong along (Dx, Dy), or — with Orbit &gt; 0 —
    /// a circle of that radius (the panel stays upright, like a Ferris-wheel car). Deterministic maths only.
    /// </summary>
    public V2 OffsetAt(int tick)
    {
        if (Period <= 0) return V2.Zero;
        double u = (((tick + Phase) % Period + Period) % Period) / (double)Period;
        if (Orbit > 0) return new V2(Orbit * (DetMath.CosTurns(u) - 1), Orbit * DetMath.SinTurns(u));
        if (Dx == 0 && Dy == 0) return V2.Zero;
        double v = u < 0.5 ? u * 2 : 2 - u * 2;
        double f = v * v * (3 - 2 * v);
        return new V2(Dx * f, Dy * f);
    }
}

public sealed class Machines
{
    public List<DoorDef> Doors { get; } = new();
    public List<ButtonDef> Buttons { get; } = new();
    public List<LaserDef> Lasers { get; } = new();
    public List<ReceiverDef> Receivers { get; } = new();
    public List<TurretDef> Turrets { get; } = new();
    public List<MoverDef> Movers { get; } = new();
    public bool Any => Doors.Count + Buttons.Count + Lasers.Count + Receivers.Count + Turrets.Count > 0;

    public int DoorIndex(string id) => Doors.FindIndex(d => d.Id == id);

    /// <summary>
    /// Object lines after the grid, one per line: <c>kind id key=value ...</c>
    /// <code>
    /// door d1 x=40 y=20 w=1 h=4
    /// button b1 x=38 y=28 opens=d1 mode=timed time=3.5
    /// laser l1 x=50 y=3 dir=down period=2 on=1 phase=0.5      (seconds; period 0 = always on)
    /// receiver r1 x=70 y=10 opens=d2
    /// turret t1 x=80 y=10 dir=left period=1.5 phase=0 speed=22
    /// mover m1 x=40 y=10 w=4 h=1 to=40,4 period=4 phase=0 kind=panel|metal   (period = there and back)
    /// mover w1 x=60 y=12 w=3 h=1 orbit=6 period=8                           (circles its rest point, stays upright)
    /// </code>
    /// </summary>
    public static Machines Parse(IEnumerable<string> lines, string levelId)
    {
        var m = new Machines();
        foreach (var raw in lines)
        {
            var line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var tok = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tok.Length == 0) continue;
            if (tok.Length < 2) throw new FormatException($"level {levelId}: object line '{raw}' needs a kind and an id");
            var kv = new Dictionary<string, string>();
            foreach (var t in tok.Skip(2))
            {
                var p = t.Split('=', 2);
                if (p.Length != 2) throw new FormatException($"level {levelId}: bad field '{t}' in '{raw}'");
                kv[p[0]] = p[1];
            }
            int I(string k) => int.Parse(kv[k], CultureInfo.InvariantCulture);
            double D(string k, double def) => kv.TryGetValue(k, out var v) ? double.Parse(v, CultureInfo.InvariantCulture) : def;
            int Ticks(string k, double def) => (int)Math.Round(D(k, def) * Tuning.TicksPerSecond);
            string[] Opens() => kv.TryGetValue("opens", out var o) ? o.Split(',') : Array.Empty<string>();
            (int, int) Dir() => kv.TryGetValue("dir", out var d) ? d switch
            {
                "left" => (-1, 0), "right" => (1, 0), "up" => (0, -1), "down" => (0, 1),
                _ => throw new FormatException($"level {levelId}: dir must be left/right/up/down in '{raw}'"),
            } : throw new FormatException($"level {levelId}: '{raw}' needs dir=");
            try
            {
                switch (tok[0])
                {
                    case "door": m.Doors.Add(new DoorDef(tok[1], I("x"), I("y"), kv.ContainsKey("w") ? I("w") : 1, kv.ContainsKey("h") ? I("h") : 1)); break;
                    case "button":
                        var mode = kv.TryGetValue("mode", out var md) ? md switch
                        {
                            "hold" => ButtonMode.Hold, "latch" => ButtonMode.Latch, "timed" => ButtonMode.Timed,
                            _ => throw new FormatException($"level {levelId}: mode must be hold/latch/timed"),
                        } : ButtonMode.Latch;
                        m.Buttons.Add(new ButtonDef(tok[1], I("x"), I("y"), Opens(), mode, Ticks("time", 3)));
                        break;
                    case "laser":
                        var (ldx, ldy) = Dir();
                        m.Lasers.Add(new LaserDef(tok[1], I("x"), I("y"), ldx, ldy, Ticks("period", 0), Ticks("on", 0), Ticks("phase", 0)));
                        break;
                    case "receiver": m.Receivers.Add(new ReceiverDef(tok[1], I("x"), I("y"), Opens())); break;
                    case "turret":
                        var (tdx, tdy) = Dir();
                        m.Turrets.Add(new TurretDef(tok[1], I("x"), I("y"), tdx, tdy, Math.Max(1, Ticks("period", 1.5)), Ticks("phase", 0), D("speed", 22)));
                        break;
                    case "mover":
                        int tx = I("x"), ty = I("y");
                        if (kv.TryGetValue("to", out var toStr))
                        {
                            var to = toStr.Split(',');
                            tx = int.Parse(to[0], CultureInfo.InvariantCulture); ty = int.Parse(to[1], CultureInfo.InvariantCulture);
                        }
                        bool portalable = !kv.TryGetValue("kind", out var kind) || kind == "panel";
                        m.Movers.Add(new MoverDef(tok[1], I("x"), I("y"), kv.ContainsKey("w") ? I("w") : 2, kv.ContainsKey("h") ? I("h") : 1,
                            tx - I("x"), ty - I("y"), Math.Max(1, Ticks("period", 4)), Ticks("phase", 0), portalable, D("orbit", 0)));
                        break;
                    default: throw new FormatException($"level {levelId}: unknown object kind '{tok[0]}'");
                }
            }
            catch (KeyNotFoundException e) { throw new FormatException($"level {levelId}: '{raw}' is missing a field ({e.Message})"); }
        }
        foreach (var id in m.Buttons.SelectMany(b => b.Opens).Concat(m.Receivers.SelectMany(r => r.Opens)))
            if (m.DoorIndex(id) < 0) throw new FormatException($"level {levelId}: opens unknown door '{id}'");
        return m;
    }
}
