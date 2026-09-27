namespace Warpline.Sim;

/// <summary>
/// Moving panels. Offsets are a pure function of the tick, so replays stay exact.
/// Order each tick: panels move → the robot riding one is carried → panels push the robot (crush = death) → normal step.
/// </summary>
public sealed partial class World
{
    public V2[] MoverOff = Array.Empty<V2>();
    public V2[] MoverPrevOff = Array.Empty<V2>();
    /// <summary>The panel the robot stood on at the end of the last tick, or -1.</summary>
    public int GroundMover { get; private set; } = -1;

    void InitMovers()
    {
        var m = Level.Machines.Movers;
        MoverOff = m.Select(d => d.OffsetAt(0)).ToArray();
        MoverPrevOff = MoverOff.ToArray();
    }

    public (double x0, double y0, double x1, double y1) MoverBox(int i)
    {
        var d = Level.Machines.Movers[i];
        var o = MoverOff[i];
        return (d.X + o.X, d.Y + o.Y, d.X + o.X + d.W, d.Y + o.Y + d.H);
    }

    public V2 MoverVelocity(int i) => (MoverOff[i] - MoverPrevOff[i]) * Tuning.TicksPerSecond;

    /// <summary>Panel offset blended between the last two ticks, for smooth drawing.</summary>
    public V2 MoverOffsetLerp(int i, double alpha) => MoverPrevOff[i] + (MoverOff[i] - MoverPrevOff[i]) * alpha;

    V2 PortalOffset(Portal p, double alpha = 1) => p.Mover < 0 ? V2.Zero : MoverOffsetLerp(p.Mover, alpha);

    /// <summary>World-space centre of a portal (moving with its panel).</summary>
    public V2 PortalCenter(Portal p) => p.Center + PortalOffset(p);

    /// <summary>World-space end points of a portal; alpha blends panel motion between ticks for drawing.</summary>
    public (V2 a, V2 b) PortalEnds(Portal p, double alpha = 1)
    {
        var o = PortalOffset(p, alpha);
        return (p.EndA + o, p.EndB + o);
    }

    /// <summary>World face line and span start of a portal.</summary>
    (double line, double start) PortalWorldFace(Portal p)
    {
        var o = PortalOffset(p);
        return p.Vertical ? (p.Line + o.X, p.Start + o.Y) : (p.Line + o.Y, p.Start + o.X);
    }

    void StepMovers()
    {
        var movers = Level.Machines.Movers;
        if (movers.Count == 0) return;
        for (int i = 0; i < movers.Count; i++)
        {
            MoverPrevOff[i] = MoverOff[i];
            MoverOff[i] = movers[i].OffsetAt(Tick);
        }
        // Ride: move with the panel you stood on (collisions and portals still apply).
        if (GroundMover >= 0 && Grounded)
        {
            var dlt = MoverOff[GroundMover] - MoverPrevOff[GroundMover];
            if (dlt.Y < 0) { MoveY(dlt.Y); MoveX(dlt.X); }     // going up: lift first
            else { MoveX(dlt.X); MoveY(dlt.Y); }
        }
        // Push: a panel moving into the robot shoves it along; pinned against a wall = crushed.
        for (int i = 0; i < movers.Count && !Dead; i++)
        {
            var (x0, y0, x1, y1) = MoverBox(i);
            if (!BoxOverlaps(x0, y0, x1, y1)) continue;
            var dlt = MoverOff[i] - MoverPrevOff[i];
            if (Math.Abs(dlt.X) >= Math.Abs(dlt.Y) && dlt.X != 0)
                MoveX(dlt.X > 0 ? x1 - (Pos.X - HalfW) + 1e-6 : x0 - (Pos.X + HalfW) - 1e-6);
            else if (dlt.Y != 0)
                MoveY(dlt.Y > 0 ? y1 - (Pos.Y - HalfH) + 1e-6 : y0 - (Pos.Y + HalfH) - 1e-6);
            var b = MoverBox(i);
            if (BoxOverlaps(b.x0, b.y0, b.x1, b.y1) || OverlapsSolid(Pos, HalfW - 0.02, HalfH - 0.02)) Die();
        }
    }

    bool BoxOverlaps(double x0, double y0, double x1, double y1) =>
        Pos.X + HalfW > x0 + 1e-7 && Pos.X - HalfW < x1 - 1e-7 && Pos.Y + HalfH > y0 + 1e-7 && Pos.Y - HalfH < y1 - 1e-7;

    /// <summary>Does this world box touch any static solid (tiles, closed doors) or moving panel?</summary>
    public bool BoxHitsSolid(double x0, double y0, double x1, double y1, int ignoreMover = -1)
    {
        int c0 = (int)Math.Floor(x0 + 1e-9), c1 = (int)Math.Floor(x1 - 1e-9), r0 = (int)Math.Floor(y0 + 1e-9), r1 = (int)Math.Floor(y1 - 1e-9);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
                if (Solid(c, r)) return true;
        for (int i = 0; i < MoverOff.Length; i++)
        {
            if (i == ignoreMover) continue;
            var b = MoverBox(i);
            if (x1 > b.x0 + 1e-9 && x0 < b.x1 - 1e-9 && y1 > b.y0 + 1e-9 && y0 < b.y1 - 1e-9) return true;
        }
        return false;
    }

    /// <summary>
    /// The nearest moving panel face the robot's leading edge crosses when moving by d along an axis:
    /// (panel index, face coordinate), or (-1, 0).
    /// </summary>
    (int mover, double line) MoverHit(bool xAxis, double d)
    {
        int best = -1; double bestLine = 0;
        double hw = HalfW, hh = HalfH;
        for (int i = 0; i < MoverOff.Length; i++)
        {
            var (x0, y0, x1, y1) = MoverBox(i);
            if (xAxis)
            {
                if (Pos.Y + hh <= y0 + 1e-9 || Pos.Y - hh >= y1 - 1e-9) continue;
                if (d > 0 && Pos.X + hw <= x0 + 1e-7 && Pos.X + d + hw > x0 && (best < 0 || x0 < bestLine)) { best = i; bestLine = x0; }
                if (d < 0 && Pos.X - hw >= x1 - 1e-7 && Pos.X + d - hw < x1 && (best < 0 || x1 > bestLine)) { best = i; bestLine = x1; }
            }
            else
            {
                if (Pos.X + hw <= x0 + 1e-9 || Pos.X - hw >= x1 - 1e-9) continue;
                if (d > 0 && Pos.Y + hh <= y0 + 1e-7 && Pos.Y + d + hh > y0 && (best < 0 || y0 < bestLine)) { best = i; bestLine = y0; }
                if (d < 0 && Pos.Y - hh >= y1 - 1e-7 && Pos.Y + d - hh < y1 && (best < 0 || y1 > bestLine)) { best = i; bestLine = y1; }
            }
        }
        return (best, bestLine);
    }

    /// <summary>The robot is about to hit a panel face: go through a portal on it if the body fits.</summary>
    bool TryEnterMover(int mover, int nx, int ny, double lo, double hi)
    {
        if (A is not { } a || B is not { } b) return false;
        bool On(Portal p)
        {
            if (p.Mover != mover || p.Nx != nx || p.Ny != ny) return false;
            var (_, start) = PortalWorldFace(p);
            return lo >= start - Tuning.PortalFit - 1e-9 && hi <= start + Tuning.PortalLength + Tuning.PortalFit + 1e-9;
        }
        if (On(a)) return Transit(a, b);
        if (On(b)) return Transit(b, a);
        return false;
    }

    /// <summary>Ray against an axis-aligned box: entry distance and the normal of the face entered.</summary>
    static bool RayBox(V2 o, V2 d, double x0, double y0, double x1, double y1, out double t, out int nx, out int ny)
    {
        double tmin = double.NegativeInfinity, tmax = double.PositiveInfinity;
        nx = 0; ny = 0; t = 0;
        if (d.X != 0)
        {
            double t1 = (x0 - o.X) / d.X, t2 = (x1 - o.X) / d.X;
            double near = Math.Min(t1, t2), far = Math.Max(t1, t2);
            if (near > tmin) { tmin = near; nx = d.X > 0 ? -1 : 1; ny = 0; }
            tmax = Math.Min(tmax, far);
        }
        else if (o.X <= x0 || o.X >= x1) return false;
        if (d.Y != 0)
        {
            double t1 = (y0 - o.Y) / d.Y, t2 = (y1 - o.Y) / d.Y;
            double near = Math.Min(t1, t2), far = Math.Max(t1, t2);
            if (near > tmin) { tmin = near; nx = 0; ny = d.Y > 0 ? -1 : 1; }
            tmax = Math.Min(tmax, far);
        }
        else if (o.Y <= y0 || o.Y >= y1) return false;
        if (tmin < 0 || tmax < tmin) return false;
        t = tmin;
        return true;
    }

    /// <summary>The nearest panel a ray from o along unit d hits before maxT.</summary>
    (int mover, double t, int nx, int ny) RayMovers(V2 o, V2 d, double maxT)
    {
        int best = -1; double bt = maxT; int bnx = 0, bny = 0;
        for (int i = 0; i < MoverOff.Length; i++)
        {
            var (x0, y0, x1, y1) = MoverBox(i);
            if (RayBox(o, d, x0, y0, x1, y1, out var t, out var nx, out var ny) && t < bt) { best = i; bt = t; bnx = nx; bny = ny; }
        }
        return (best, bt, bnx, bny);
    }

    /// <summary>Fit a portal on a moving panel's face (in the panel's own coordinates).</summary>
    Portal? FitMover(int which, int mi, int nx, int ny, V2 hit)
    {
        var m = Level.Machines.Movers[mi];
        if (!m.Portalable) return null;
        var o = MoverOff[mi];
        bool vertical = nx != 0;
        int lineLocal = vertical ? (nx < 0 ? m.X : m.X + m.W) : (ny < 0 ? m.Y : m.Y + m.H);
        double tcLocal = vertical ? hit.Y - o.Y : hit.X - o.X;
        int lo = vertical ? m.Y : m.X, hi = vertical ? m.Y + m.H : m.X + m.W;
        if (hi - lo < Tuning.PortalLength) return null;
        int clearance = vertical ? Tuning.PortalClearanceWall : Tuning.PortalClearance;
        var other = which == 0 ? B : A;
        var starts = Enumerable.Range(lo, hi - lo - Tuning.PortalLength + 1)
            .Where(st => Math.Abs(st + Tuning.PortalLength / 2.0 - tcLocal) <= Tuning.PortalBump + 1)
            .OrderBy(st => Math.Abs(st + Tuning.PortalLength / 2.0 - tcLocal)).ThenBy(st => st);
        foreach (int start in starts)
        {
            var p = new Portal(nx, ny, lineLocal, start, mi);
            if (other is { } ot && p.Overlaps(ot)) continue;
            var (line, s0) = PortalWorldFace(p);
            double a0 = s0, a1 = s0 + Tuning.PortalLength, b0 = Math.Min(line, line + (vertical ? nx : ny) * clearance), b1 = Math.Max(line, line + (vertical ? nx : ny) * clearance);
            bool blocked = vertical ? BoxHitsSolid(b0, a0, b1, a1, mi) : BoxHitsSolid(a0, b0, a1, b1, mi);
            if (!blocked) return p;
        }
        return null;
    }
}
