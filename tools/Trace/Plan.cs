using System.Globalization;
using Warpline.Sim;

/// <summary>
/// Compiles a readable "plan" into an exact route by simulating as it goes. Dev tool only; the game reads .route.
/// Line: <c>keys [A:x,y|B:x,y] (for N | until cond[&amp;cond...])</c>
/// keys: any of L R J D, or '-' for none. Conditions: x&gt;=v x&lt;=v y&gt;=v y&lt;=v vx&gt;=v vx&lt;=v vy&gt;=v vy&lt;=v,
/// tick&gt;=v, grounded, air, wall, wallL, wallR, tele, finished, open=door, closed=door, lit=receiver,
/// laseron=laser, laseroff=laser. The shot fires on the first tick of the line.
/// </summary>
static class Plan
{
    public static (List<InputFrame> frames, World w) Compile(Level level, string text, TextWriter log)
    {
        var w = new World(level);
        var frames = new List<InputFrame>();
        int lineNo = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            lineNo++;
            var line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var tok = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (tok.Count == 0) continue;
            string keys = tok[0];
            bool l = keys.Contains('L'), r = keys.Contains('R'), j = keys.Contains('J'), d = keys.Contains('D');
            bool fa = false, fb = false; V2 aim = V2.Zero;
            int k = 1;
            if (k < tok.Count && (tok[k].StartsWith("A:") || tok[k].StartsWith("B:")))
            {
                var xy = tok[k][2..].Split(',');
                aim = new V2(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
                if (tok[k][0] == 'A') fa = true; else fb = true;
                k++;
            }
            int forN = -1; string[] conds = Array.Empty<string>();
            if (k < tok.Count && tok[k] == "for") forN = int.Parse(tok[k + 1]);
            else if (k < tok.Count && tok[k] == "until") conds = string.Join("", tok.Skip(k + 1)).Split('&');
            else forN = 1;

            int n = 0;
            while (true)
            {
                var f = new InputFrame(l, r, j, d, n == 0 && fa, n == 0 && fb, aim);
                w.Step(f);
                frames.Add(f);
                n++;
                foreach (var e in w.Events)
                    if (e.Kind is SimEventKind.PortalFizzled or SimEventKind.Death)
                        log.WriteLine($"  plan line {lineNo}: {e.Kind} at tick {w.Tick} pos {w.Pos}");
                if (w.Dead || w.Finished) return (frames, w);
                if (forN >= 0 && n >= forN) break;
                if (forN < 0 && conds.All(c => Holds(w, c))) break;
                if (n > 3000) { log.WriteLine($"  plan line {lineNo}: '{raw.Trim()}' never satisfied (pos {w.Pos} vel {w.Vel})"); return (frames, w); }
            }
            log.WriteLine($"  L{lineNo,-3} t{w.Tick,5} pos {w.Pos} vel {w.Vel} {(w.Grounded ? "ground" : "air")}{(w.WallDir != 0 ? " wall" + w.WallDir : "")}   {raw.Trim()}");
        }
        return (frames, w);
    }

    static bool Holds(World w, string c)
    {
        c = c.Trim();
        switch (c)
        {
            case "grounded": return w.Grounded;
            case "air": return !w.Grounded;
            case "wall": return w.WallDir != 0;
            case "wallL": return w.WallDir == -1;
            case "wallR": return w.WallDir == 1;
            case "tele": return w.TeleportedThisTick;
            case "finished": return w.Finished;
        }
        if (c.StartsWith("open=")) return w.DoorOpen[w.Level.Machines.DoorIndex(c[5..])];
        if (c.StartsWith("closed=")) return !w.DoorOpen[w.Level.Machines.DoorIndex(c[7..])];
        if (c.StartsWith("lit=")) return w.ReceiverLit[w.Level.Machines.Receivers.FindIndex(r => r.Id == c[4..])];
        if (c.StartsWith("laseron=")) return w.Beams[w.Level.Machines.Lasers.FindIndex(r => r.Id == c[8..])].Count > 0;
        if (c.StartsWith("laseroff=")) return w.Beams[w.Level.Machines.Lasers.FindIndex(r => r.Id == c[9..])].Count == 0;
        foreach (var op in new[] { ">=", "<=" })
        {
            int i = c.IndexOf(op, StringComparison.Ordinal);
            if (i < 0) continue;
            string v = c[..i];
            double val = double.Parse(c[(i + 2)..], CultureInfo.InvariantCulture);
            double cur;
            if (v.StartsWith("mx.") || v.StartsWith("my."))
            {
                int mi = w.Level.Machines.Movers.FindIndex(m => m.Id == v[3..]);
                var box = w.MoverBox(mi);
                cur = v[1] == 'x' ? box.x0 : box.y0;
            }
            else cur = v switch { "x" => w.Pos.X, "y" => w.Pos.Y, "vx" => w.Vel.X, "vy" => w.Vel.Y, "tick" => w.Tick, _ => throw new FormatException("bad cond " + c) };
            return op == ">=" ? cur >= val : cur <= val;
        }
        throw new FormatException("bad cond " + c);
    }
}
