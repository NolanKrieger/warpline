namespace Warpline.Sim;

/// <summary>Buttons, doors, lasers, receivers and turrets. Stepped once per tick after the robot moves.</summary>
public sealed partial class World
{
    public struct Bullet { public V2 P, V; public int Age; }

    public bool[] DoorOpen = Array.Empty<bool>();
    public bool[] ButtonDown = Array.Empty<bool>();
    public bool[] ButtonActive = Array.Empty<bool>();
    public int[] ButtonTimer = Array.Empty<int>();
    public bool[] ReceiverLit = Array.Empty<bool>();
    public List<(V2 A, V2 B)>[] Beams = Array.Empty<List<(V2, V2)>>();
    public readonly List<Bullet> Bullets = new();
    bool[] _latched = Array.Empty<bool>();
    V2 _prevPos;

    const double BulletRadius = 0.12;
    const int BulletLife = 20 * Tuning.TicksPerSecond;

    void InitMachines()
    {
        var m = Level.Machines;
        DoorOpen = new bool[m.Doors.Count];
        ButtonDown = new bool[m.Buttons.Count];
        ButtonActive = new bool[m.Buttons.Count];
        ButtonTimer = new int[m.Buttons.Count];
        _latched = new bool[m.Buttons.Count];
        ReceiverLit = new bool[m.Receivers.Count];
        Beams = m.Lasers.Select(_ => new List<(V2, V2)>()).ToArray();
        _prevPos = Pos;
        if (m.Lasers.Count > 0) TraceLasers();   // so the first frame already shows the beams
    }

    void UpdateMachines()
    {
        var m = Level.Machines;
        if (!m.Any) return;

        // Buttons
        for (int i = 0; i < m.Buttons.Count; i++)
        {
            var b = m.Buttons[i];
            bool byBody = BodyOverlapsCell(b.X, b.Y), byBullet = !byBody && BulletIn(b.X, b.Y);   // bullets press buttons too
            bool down = byBody || byBullet;
            if (down && !ButtonDown[i] && !ButtonActive[i])
            {
                Events.Add(new SimEvent(SimEventKind.ButtonPressed, new V2(b.X + 0.5, b.Y + 0.5), i));
                if (byBullet) Events.Add(new SimEvent(SimEventKind.BulletPressedButton, new V2(b.X + 0.5, b.Y + 0.5), i));
            }
            ButtonDown[i] = down;
            switch (b.Mode)
            {
                case ButtonMode.Hold: ButtonActive[i] = down; break;
                case ButtonMode.Latch: if (down) _latched[i] = true; ButtonActive[i] = _latched[i]; break;
                case ButtonMode.Timed:
                    if (down) ButtonTimer[i] = b.TimeTicks;
                    else if (ButtonTimer[i] > 0) ButtonTimer[i]--;
                    ButtonActive[i] = ButtonTimer[i] > 0;
                    break;
            }
        }

        // Doors: open while any linked button is active or receiver is lit (receivers from last tick's beams)
        var want = new bool[m.Doors.Count];
        for (int i = 0; i < m.Buttons.Count; i++)
            if (ButtonActive[i]) foreach (var id in m.Buttons[i].Opens) want[m.DoorIndex(id)] = true;
        for (int i = 0; i < m.Receivers.Count; i++)
            if (ReceiverLit[i]) foreach (var id in m.Receivers[i].Opens) want[m.DoorIndex(id)] = true;
        for (int d = 0; d < m.Doors.Count; d++)
        {
            var door = m.Doors[d];
            if (want[d] && !DoorOpen[d])
            {
                DoorOpen[d] = true;
                Events.Add(new SimEvent(SimEventKind.DoorOpened, new V2(door.X + door.W / 2.0, door.Y + door.H / 2.0), d));
            }
            else if (!want[d] && DoorOpen[d] && !BodyOverlapsRect(door.X, door.Y, door.W, door.H))
            {
                // Closing never crushes the robot: a door waits until you're out of it.
                DoorOpen[d] = false;
                Events.Add(new SimEvent(SimEventKind.DoorClosed, new V2(door.X + door.W / 2.0, door.Y + door.H / 2.0), d));
                Bullets.RemoveAll(bl => door.Contains((int)Math.Floor(bl.P.X), (int)Math.Floor(bl.P.Y)));
                if (A is { } pa && ClearanceHitsDoor(pa, d)) { A = null; Events.Add(new SimEvent(SimEventKind.PortalLost, PortalCenter(pa), 0)); }
                if (B is { } pb && ClearanceHitsDoor(pb, d)) { B = null; Events.Add(new SimEvent(SimEventKind.PortalLost, PortalCenter(pb), 1)); }
            }
        }

        // Lasers
        TraceLasers();
        var box = SweptBox(0.1);
        for (int l = 0; l < Beams.Length; l++)
            foreach (var (a, b) in Beams[l])
                if (SegmentHitsBox(a, b, box)) { Die(); return; }

        // Turrets and bullets
        for (int t = 0; t < m.Turrets.Count; t++)
        {
            var tu = m.Turrets[t];
            int t0 = Tick - 1;   // first tick is t0 = 0: phase 0 fires immediately
            if (t0 >= tu.Phase && (t0 - tu.Phase) % tu.Period == 0)
            {
                var dir = new V2(tu.Dx, tu.Dy);
                Bullets.Add(new Bullet { P = new V2(tu.X + 0.5, tu.Y + 0.5) + dir * 0.55, V = dir * tu.Speed });
                Events.Add(new SimEvent(SimEventKind.TurretFired, new V2(tu.X + 0.5, tu.Y + 0.5), t));
            }
        }
        for (int i = Bullets.Count - 1; i >= 0; i--)
        {
            var bl = Bullets[i];
            bool alive = MoveBullet(ref bl);
            bl.Age++;
            if (!alive || bl.Age > BulletLife) { Bullets.RemoveAt(i); continue; }
            Bullets[i] = bl;
            if (Math.Abs(bl.P.X - Pos.X) <= HalfW + BulletRadius - 0.05 && Math.Abs(bl.P.Y - Pos.Y) <= HalfH + BulletRadius - 0.05)
            {
                Die();
                return;
            }
        }
    }

    bool ClearanceHitsDoor(Portal p, int door)
    {
        if (p.OnMover) return false;
        for (int k = p.Start; k < p.Start + Tuning.PortalLength; k++)
        {
            int wx = p.Vertical ? (p.Nx < 0 ? p.Line : p.Line - 1) : k;
            int wy = p.Vertical ? k : (p.Ny < 0 ? p.Line : p.Line - 1);
            int clearance = p.Vertical ? Tuning.PortalClearanceWall : Tuning.PortalClearance;
            for (int dd = 1; dd <= clearance; dd++)
                if (Level.DoorAt(wx + p.Nx * dd, wy + p.Ny * dd) == door) return true;
        }
        return false;
    }

    bool BodyOverlapsCell(int x, int y) => BodyOverlapsRect(x, y, 1, 1);

    bool BulletIn(int x, int y)
    {
        foreach (var bl in Bullets)
            if ((int)Math.Floor(bl.P.X) == x && (int)Math.Floor(bl.P.Y) == y) return true;
        return false;
    }

    bool BodyOverlapsRect(int x, int y, int w, int h) =>
        Pos.X + HalfW > x && Pos.X - HalfW < x + w && Pos.Y + HalfH > y && Pos.Y - HalfH < y + h;

    /// <summary>The body's box over this tick (union of start and end), shrunk a little to be forgiving.</summary>
    (double x0, double y0, double x1, double y1) SweptBox(double shrink)
    {
        var a = TeleportedThisTick ? Pos : _prevPos;
        double hw = HalfW - shrink, hh = HalfH - shrink;
        return (Math.Min(a.X, Pos.X) - hw, Math.Min(a.Y, Pos.Y) - hh, Math.Max(a.X, Pos.X) + hw, Math.Max(a.Y, Pos.Y) + hh);
    }

    static bool SegmentHitsBox(V2 a, V2 b, (double x0, double y0, double x1, double y1) box)
    {
        double sx0 = Math.Min(a.X, b.X) - 0.05, sx1 = Math.Max(a.X, b.X) + 0.05;
        double sy0 = Math.Min(a.Y, b.Y) - 0.05, sy1 = Math.Max(a.Y, b.Y) + 0.05;
        return sx1 > box.x0 && sx0 < box.x1 && sy1 > box.y0 && sy0 < box.y1;
    }

    int ReceiverAt(int x, int y)
    {
        var r = Level.Machines.Receivers;
        for (int i = 0; i < r.Count; i++) if (r[i].X == x && r[i].Y == y) return i;
        return -1;
    }

    /// <summary>Beams are axis-aligned; a beam that meets a portal continues out of the other one (up to 8 hops).</summary>
    void TraceLasers()
    {
        var m = Level.Machines;
        Array.Clear(ReceiverLit);
        for (int l = 0; l < m.Lasers.Count; l++)
        {
            Beams[l].Clear();
            if (m.Lasers[l].IsOn(Tick)) TraceBeam(m.Lasers[l], Beams[l], true);
        }
    }

    /// <summary>Where laser l's beam would go right now, on or off (for the warning line while it is off).</summary>
    public List<(V2 A, V2 B)> PreviewBeam(int l)
    {
        var list = new List<(V2, V2)>();
        TraceBeam(Level.Machines.Lasers[l], list, false);
        return list;
    }

    void TraceBeam(LaserDef las, List<(V2, V2)> beam, bool light)
    {
        var p = new V2(las.X + 0.5, las.Y + 0.5);
        int dx = las.Dx, dy = las.Dy, cx = las.X, cy = las.Y;
        for (int hop = 0; hop < 8; hop++)
        {
            // Walk the grid to the first receiver, solid face or the edge of the world.
            int kind = 0, ri = -1;   // 0 out, 1 receiver, 2 solid face
            V2 q = p;
            for (int step = 0; step < 512; step++)
            {
                cx += dx; cy += dy;
                ri = ReceiverAt(cx, cy);
                if (ri >= 0) { kind = 1; q = new V2(cx + 0.5, cy + 0.5); break; }
                if (cy > Level.H + 2 || cy < -2 || cx < -2 || cx > Level.W + 2) { kind = 0; q = new V2(cx + 0.5, cy + 0.5); break; }
                if (!Solid(cx, cy)) continue;
                kind = 2;
                q = dx > 0 ? new V2(cx, p.Y) : dx < 0 ? new V2(cx + 1, p.Y) : dy > 0 ? new V2(p.X, cy) : new V2(p.X, cy + 1);
                break;
            }
            // A moving panel in the way comes first.
            var dir = new V2(dx, dy);
            var (mv, mt, mnx, mny) = RayMovers(p, dir, (q - p).Length);
            Portal? en, ex;
            if (mv >= 0)
            {
                var hp = p + dir * mt;
                beam.Add((p, hp));
                (en, ex) = PortalOnMoverFace(mv, mnx, mny, mnx != 0 ? hp.Y : hp.X);
                if (en is not { } me || ex is not { } mx) return;
                p = PortalCenter(mx) + mx.T * (hp - PortalCenter(me)).Dot(me.T);
                (dx, dy) = (mx.Nx, mx.Ny);
            }
            else
            {
                beam.Add((p, q));
                if (kind == 1) { if (light) ReceiverLit[ri] = true; return; }
                if (kind == 0) return;
                (en, ex) = PortalOnFace(-dx, -dy, dx > 0 ? cx : dx < 0 ? cx + 1 : dy > 0 ? cy : cy + 1, dx != 0 ? p.Y : p.X);
                if (en is not { } se || ex is not { } sx) return;
                p = PortalCenter(sx) + sx.T * (q - PortalCenter(se)).Dot(se.T);
                (dx, dy) = (sx.Nx, sx.Ny);
            }
            // Continue from the open cell just outside the exit face.
            cx = (int)Math.Floor(p.X + dx * 1e-6) - dx;
            cy = (int)Math.Floor(p.Y + dy * 1e-6) - dy;
            if (dx == 0) cx = (int)Math.Floor(p.X);
            if (dy == 0) cy = (int)Math.Floor(p.Y);
        }
    }

    /// <summary>The portal on a moving panel's face covering tangent coordinate tc, and its partner.</summary>
    (Portal? entry, Portal? exit) PortalOnMoverFace(int mover, int nx, int ny, double tc)
    {
        if (A is not { } a || B is not { } b) return (null, null);
        bool On(Portal p)
        {
            if (p.Mover != mover || p.Nx != nx || p.Ny != ny) return false;
            var (_, start) = PortalWorldFace(p);
            return tc > start && tc < start + Tuning.PortalLength;
        }
        if (On(a)) return (a, b);
        if (On(b)) return (b, a);
        return (null, null);
    }

    /// <summary>The portal on this face covering tangent coordinate tc (strictly inside), and its partner.</summary>
    (Portal? entry, Portal? exit) PortalOnFace(int nx, int ny, int line, double tc)
    {
        if (A is not { } a || B is not { } b) return (null, null);
        bool On(Portal p) => !p.OnMover && p.Nx == nx && p.Ny == ny && p.Line == line && tc > p.Start && tc < p.Start + Tuning.PortalLength;
        if (On(a)) return (a, b);
        if (On(b)) return (b, a);
        return (null, null);
    }

    /// <summary>Moves a bullet one tick. Bullets fly straight and go through portals like everything else.</summary>
    bool MoveBullet(ref Bullet bl)
    {
        double speed = bl.V.Length;
        int n = Math.Max(1, (int)Math.Ceiling(speed * Tuning.Dt / 0.25));
        double h = Tuning.Dt / n;
        for (int i = 0; i < n; i++)
        {
            var next = bl.P + bl.V * h;
            int cx = (int)Math.Floor(next.X), cy = (int)Math.Floor(next.Y);
            int px = (int)Math.Floor(bl.P.X), py = (int)Math.Floor(bl.P.Y);
            if (cy > Level.H + 2 || cy < -2 || cx < -2 || cx > Level.W + 2) return false;
            // Moving panels
            for (int mi = 0; mi < MoverOff.Length; mi++)
            {
                var (x0, y0, x1, y1) = MoverBox(mi);
                bool inNext = next.X > x0 && next.X < x1 && next.Y > y0 && next.Y < y1;
                bool inNow = bl.P.X > x0 && bl.P.X < x1 && bl.P.Y > y0 && bl.P.Y < y1;
                if (!inNext || inNow) continue;
                int sx = Math.Sign(bl.V.X), sy = Math.Sign(bl.V.Y);
                var hitPt = sx > 0 ? new V2(x0, bl.P.Y) : sx < 0 ? new V2(x1, bl.P.Y) : sy > 0 ? new V2(bl.P.X, y0) : new V2(bl.P.X, y1);
                var (me, mx) = PortalOnMoverFace(mi, -sx, -sy, sx != 0 ? bl.P.Y : bl.P.X);
                if (me is not { } men || mx is not { } mex)
                {
                    Events.Add(new SimEvent(SimEventKind.BulletHit, bl.P));
                    return false;
                }
                bl.P = PortalCenter(mex) + mex.T * (hitPt - PortalCenter(men)).Dot(men.T) + mex.N * 0.05;
                bl.V = mex.N * speed;
                goto nextStep;
            }
            if ((cx != px || cy != py) && Solid(cx, cy))
            {
                int dx = Math.Sign(bl.V.X), dy = Math.Sign(bl.V.Y);
                int line = dx > 0 ? cx : dx < 0 ? cx + 1 : dy > 0 ? cy : cy + 1;
                var (entry, exit) = PortalOnFace(-dx, -dy, line, dx != 0 ? bl.P.Y : bl.P.X);
                if (entry is not { } en || exit is not { } ex)
                {
                    Events.Add(new SimEvent(SimEventKind.BulletHit, bl.P));
                    return false;
                }
                var face = dx != 0 ? new V2(line, bl.P.Y) : new V2(bl.P.X, line);
                double s = (face - PortalCenter(en)).Dot(en.T);
                bl.P = PortalCenter(ex) + ex.T * s + ex.N * 0.05;
                bl.V = ex.N * speed;
                continue;
            }
            bl.P = next;
        nextStep:;
        }
        return true;
    }
}
