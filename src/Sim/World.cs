namespace Warpline.Sim;

public enum SimEventKind { Jump, WallJump, Land, PortalPlaced, PortalFizzled, PortalsCleared, Teleport, Death, Finish,
    ButtonPressed, DoorOpened, DoorClosed, TurretFired, BulletHit, PortalLost, Bounce, BulletPressedButton }

/// <summary>Things that happened during one Step, for sound and effects. Which: 0 = cyan (A), 1 = magenta (B).</summary>
public readonly record struct SimEvent(SimEventKind Kind, V2 At, int Which = -1, V2 To = default);

public enum ShotResult { Placed, NotPortalable, Blocked, NoRoom, Miss }

public readonly record struct Shot(ShotResult Result, V2 From, V2 To, Portal? Portal, V2 Normal);

/// <summary>
/// The whole game state for one attempt at one level, stepped at a fixed 120 Hz.
/// Deterministic: the same level and the same input frames always produce the same state.
/// No Godot types here; the game layer only reads this and feeds it InputFrames.
/// </summary>
public sealed partial class World
{
    public Level Level { get; }
    public V2 Pos;            // hitbox centre
    public V2 Vel;
    public bool Crouched;
    public bool Grounded;
    public int Facing = 1;
    public int WallDir;       // -1 / +1 when airborne and touching a wall on that side
    public Portal? A, B;
    public int Tick;
    public bool Dead, Finished;
    public bool TeleportedThisTick;
    public readonly List<SimEvent> Events = new();

    int _coyote, _jumpBuffer, _wallLock, _wallLockDir;
    bool _jumpHeldPrev, _jumpCutArmed, _downHeld;
    /// <summary>Standing on speed gel (from the last ground contact).</summary>
    public bool OnSpeedGel { get; private set; }

    public World(Level level)
    {
        Level = level;
        Pos = level.Spawn;
        Grounded = true;
        InitMovers();
        InitMachines();
    }

    /// <summary>Solid for movement, shots and beams: walls, plus doors while closed.</summary>
    public bool Solid(int x, int y)
    {
        if (Tiles.Solid(Level[x, y])) return true;
        int d = Level.DoorAt(x, y);
        return d >= 0 && !DoorOpen[d];
    }

    public bool PortalableAt(int x, int y) => Tiles.Portalable(Level[x, y]) && Level.DoorAt(x, y) < 0;

    public double HalfW => Tuning.HalfW;
    public double HalfH => Crouched ? Tuning.CrouchHalfH : Tuning.HalfH;
    public bool Sliding => Crouched && Grounded && Math.Abs(Vel.X) > Tuning.CrouchMax;
    public Portal? Get(int which) => which == 0 ? A : B;

    public void Step(InputFrame f)
    {
        Events.Clear();
        TeleportedThisTick = false;
        if (Dead || Finished) return;
        Tick++;
        _prevPos = Pos;
        StepMovers();
        if (Dead) return;

        int dir = (f.Right ? 1 : 0) - (f.Left ? 1 : 0);
        if (dir != 0) Facing = dir;

        bool jumpPressed = f.Jump && !_jumpHeldPrev;
        _jumpHeldPrev = f.Jump;
        if (jumpPressed) _jumpBuffer = Tuning.JumpBufferTicks;
        else if (_jumpBuffer > 0) _jumpBuffer--;

        if (f.FireA) Fire(0, f.Aim);
        else if (f.FireB) Fire(1, f.Aim);

        _downHeld = f.Down;
        UpdateCrouch(f.Down);
        WallDir = Grounded ? 0 : TouchingWall();
        if (_wallLock > 0) _wallLock--;

        Horizontal(dir);
        Jumping(f.Jump);

        Vel = Vel with { Y = Vel.Y + Tuning.Gravity * Tuning.Dt };
        if (!Grounded && WallDir != 0 && dir == WallDir && Vel.Y > Tuning.WallSlideMax)
            Vel = Vel with { Y = Tuning.WallSlideMax };
        if (Vel.Y > Tuning.Terminal) Vel = Vel with { Y = Tuning.Terminal };
        Vel = new V2(Math.Clamp(Vel.X, -Tuning.MaxSpeed, Tuning.MaxSpeed), Math.Clamp(Vel.Y, -Tuning.MaxSpeed, Tuning.MaxSpeed));

        bool wasGrounded = Grounded;
        int riding = GroundMover;
        Move();
        // Leaving a moving panel keeps its speed (momentum is the point of this game).
        if (riding >= 0 && !Grounded && !TeleportedThisTick) Vel += MoverVelocity(riding);
        if (Dead) return;
        UpdateMachines();
        if (Dead) return;
        if (Grounded && !wasGrounded) Events.Add(new SimEvent(SimEventKind.Land, Feet));
        if (wasGrounded && !Grounded && Vel.Y >= 0) _coyote = Tuning.CoyoteTicks;
        else if (!Grounded && _coyote > 0) _coyote--;
        if (Grounded) _coyote = 0;

        Contacts();
    }

    public V2 Feet => new(Pos.X, Pos.Y + HalfH);

    // ---------------------------------------------------------------- movement

    void Horizontal(int dir)
    {
        double vx = Vel.X;
        if (Grounded)
        {
            double speed = Math.Abs(vx);
            bool gel = OnSpeedGel;
            double runMax = gel ? Tuning.SpeedGelMax : Tuning.MaxRun;
            if (Crouched && speed > Tuning.CrouchMax)
                vx = Approach(vx, 0, Tuning.SlideFriction);                       // slide: keep momentum
            else if (speed > runMax && (dir == 0 || dir == Math.Sign(vx)))
                vx = Approach(vx, Math.Sign(vx) * runMax, Tuning.OverMaxDecay);
            else if (dir != 0)
            {
                double max = Crouched ? Tuning.CrouchMax : runMax;
                double accel = vx * dir < 0 ? (gel ? Tuning.SpeedGelTurn : Tuning.TurnAccel) : (gel ? Tuning.SpeedGelAccel : Tuning.GroundAccel);
                vx = Approach(vx, dir * max, accel);
            }
            else vx = Approach(vx, 0, gel ? Tuning.SpeedGelFriction : Tuning.GroundFriction);
        }
        else if (dir != 0 && !(_wallLock > 0 && dir == _wallLockDir))
        {
            if (vx * dir < Tuning.MaxRun) vx = Approach(vx, dir * Tuning.MaxRun, Tuning.AirAccel);
        }
        Vel = Vel with { X = vx };
    }

    void Jumping(bool jumpHeld)
    {
        if (_jumpBuffer > 0)
        {
            if (Grounded || _coyote > 0)
            {
                Vel = Vel with { Y = -Tuning.JumpVelocity };
                _jumpBuffer = 0; _coyote = 0; Grounded = false; _jumpCutArmed = true;
                Events.Add(new SimEvent(SimEventKind.Jump, Feet));
            }
            else if (WallDir != 0)
            {
                Vel = new V2(-WallDir * Tuning.WallJumpX, -Tuning.WallJumpY);
                _wallLock = Tuning.WallJumpLockTicks; _wallLockDir = WallDir;
                Facing = -WallDir;
                _jumpBuffer = 0; _jumpCutArmed = true;
                Events.Add(new SimEvent(SimEventKind.WallJump, Pos + new V2(WallDir * HalfW, 0), WallDir));
            }
        }
        if (_jumpCutArmed && !jumpHeld && Vel.Y < 0)
        {
            Vel = Vel with { Y = Vel.Y * Tuning.JumpCut };
            _jumpCutArmed = false;
        }
        if (Vel.Y >= 0) _jumpCutArmed = false;
    }

    void UpdateCrouch(bool down)
    {
        if (down && !Crouched)
        {
            double feet = Pos.Y + Tuning.HalfH;
            Crouched = true;
            Pos = Pos with { Y = feet - Tuning.CrouchHalfH };
        }
        else if (!down && Crouched)
        {
            double feet = Pos.Y + Tuning.CrouchHalfH;
            var standing = Pos with { Y = feet - Tuning.HalfH };
            if (!OverlapsSolid(standing, Tuning.HalfW, Tuning.HalfH))
            {
                Crouched = false;
                Pos = standing;
            }
        }
    }

    int TouchingWall()
    {
        for (int i = 0; i < MoverOff.Length; i++)
        {
            var (x0, y0, x1, y1) = MoverBox(i);
            if (Pos.Y + HalfH <= y0 || Pos.Y - HalfH >= y1) continue;
            if (Math.Abs(Pos.X + HalfW - x0) < Tuning.WallProbe) return 1;
            if (Math.Abs(Pos.X - HalfW - x1) < Tuning.WallProbe) return -1;
        }
        int r0 = Fl(Pos.Y - HalfH + 1e-9), r1 = Fl(Pos.Y + HalfH - 1e-9);
        int left = Fl(Pos.X - HalfW - Tuning.WallProbe), right = Fl(Pos.X + HalfW + Tuning.WallProbe);
        for (int r = r0; r <= r1; r++)
        {
            if (Solid(right, r)) return 1;
            if (Solid(left, r)) return -1;
        }
        return 0;
    }

    void Move()
    {
        Grounded = false;
        OnSpeedGel = false;
        GroundMover = -1;
        double maxComp = Math.Max(Math.Abs(Vel.X), Math.Abs(Vel.Y)) * Tuning.Dt;
        int n = Math.Max(1, (int)Math.Ceiling(maxComp / Tuning.MaxStep));
        double h = Tuning.Dt / n;
        for (int i = 0; i < n; i++)
        {
            MoveX(Vel.X * h);
            MoveY(Vel.Y * h);
        }
    }

    const double Eps = 1e-9;

    void MoveX(double d)
    {
        if (d == 0) return;
        double hw = HalfW, hh = HalfH;
        double nx = Pos.X + d;
        int r0 = Fl(Pos.Y - hh + Eps), r1 = Fl(Pos.Y + hh - Eps);
        int col = d > 0 ? Fl(nx + hw - Eps) : Fl(nx - hw + Eps);
        bool hit = false;
        for (int r = r0; r <= r1 && !hit; r++) hit = Solid(col, r);
        var (mi, mLine) = MoverHit(true, d);
        if (mi >= 0 && (!hit || (d > 0 ? mLine < col : mLine > col + 1)))
        {
            if (TryEnterMover(mi, d > 0 ? -1 : 1, 0, Pos.Y - hh, Pos.Y + hh)) return;
            Pos = Pos with { X = d > 0 ? mLine - hw : mLine + hw };
            Vel = Vel with { X = 0 };
            return;
        }
        if (!hit) { Pos = Pos with { X = nx }; return; }

        int line = d > 0 ? col : col + 1;
        int nrm = d > 0 ? -1 : 1;
        if (TryEnter(new Portal(nrm, 0, line, 0), Pos.Y - hh, Pos.Y + hh)) return;
        Pos = Pos with { X = d > 0 ? line - hw : line + hw };
        bool bounceWall = false;
        for (int r = r0; r <= r1 && !bounceWall; r++) bounceWall = Level[col, r] == Tile.BounceGel;
        if (bounceWall && !_downHeld && Math.Abs(Vel.X) > Tuning.BounceWallMin)
        {
            Vel = Vel with { X = -Vel.X };
            Facing = Math.Sign(Vel.X);
            Events.Add(new SimEvent(SimEventKind.Bounce, new V2(line, Pos.Y)));
        }
        else Vel = Vel with { X = 0 };
    }

    void MoveY(double d)
    {
        if (d == 0) return;
        double hw = HalfW, hh = HalfH;
        double ny = Pos.Y + d;
        int c0 = Fl(Pos.X - hw + Eps), c1 = Fl(Pos.X + hw - Eps);
        int row = d > 0 ? Fl(ny + hh - Eps) : Fl(ny - hh + Eps);
        bool hit = false;
        for (int c = c0; c <= c1 && !hit; c++) hit = Solid(c, row);
        var (mi, mLine) = MoverHit(false, d);
        if (mi >= 0 && (!hit || (d > 0 ? mLine < row : mLine > row + 1)))
        {
            if (TryEnterMover(mi, 0, d > 0 ? -1 : 1, Pos.X - hw, Pos.X + hw)) return;
            Pos = Pos with { Y = d > 0 ? mLine - hh : mLine + hh };
            Vel = Vel with { Y = 0 };
            if (d > 0) { Grounded = true; GroundMover = mi; }
            else _jumpCutArmed = false;
            return;
        }
        if (!hit) { Pos = Pos with { Y = ny }; return; }

        int line = d > 0 ? row : row + 1;
        int nrm = d > 0 ? -1 : 1;
        if (TryEnter(new Portal(0, nrm, line, 0), Pos.X - hw, Pos.X + hw)) return;
        Pos = Pos with { Y = d > 0 ? line - hh : line + hh };
        bool bounce = false, speedGel = false;
        for (int c = c0; c <= c1; c++)
        {
            var t = Level[c, row];
            bounce |= t == Tile.BounceGel;
            speedGel |= t == Tile.SpeedGel;
        }
        if (bounce && !_downHeld)
        {
            double impact = Math.Abs(Vel.Y);
            if (d > 0)
            {
                double up = Math.Max(impact, Tuning.BounceMin);
                if (_jumpBuffer > 0) { up = Math.Max(impact + Tuning.BounceJumpBoost, Tuning.BounceJumpMin); _jumpBuffer = 0; }
                Vel = Vel with { Y = -up };
                Grounded = false;
            }
            else Vel = Vel with { Y = impact };
            _jumpCutArmed = false;
            Events.Add(new SimEvent(SimEventKind.Bounce, new V2(Pos.X, line)));
            return;
        }
        Vel = Vel with { Y = 0 };
        if (d > 0) { Grounded = true; OnSpeedGel = speedGel; }
        else _jumpCutArmed = false;
    }

    /// <summary>The body is about to hit a face. If a portal covers it and the other portal exists, go through.</summary>
    bool TryEnter(Portal face, double lo, double hi)
    {
        if (A is not { } a || B is not { } b) return false;
        Portal entry, exit;
        if (a.SameFace(face) && a.Fits(lo, hi)) { entry = a; exit = b; }
        else if (b.SameFace(face) && b.Fits(lo, hi)) { entry = b; exit = a; }
        else return false;
        return Transit(entry, exit);
    }

    /// <summary>Move the robot from entry to exit. False (acts as a wall) if something blocks the exit.</summary>
    bool Transit(Portal entry, Portal exit)
    {
        var from = Pos;
        double s = (Pos - PortalCenter(entry)).Dot(entry.T);
        double vin = Vel.Dot(-entry.N);
        double vt = Vel.Dot(entry.T);

        var vOut = exit.N * vin + exit.T * vt;
        if (exit.Ny == -1 && vin < Tuning.FloorExitMinPop)
        {
            vOut = exit.N * Tuning.FloorExitMinPop + exit.T * vt;
            if (Math.Abs(vOut.X) < Tuning.FloorExitNudge) vOut = vOut with { X = Facing * Tuning.FloorExitNudge };
        }

        double tanHalf = exit.Vertical ? HalfH : HalfW;
        double nHalf = exit.Vertical ? HalfW : HalfH;
        double lim = Tuning.PortalLength / 2.0 - tanHalf;
        s = Math.Clamp(s, -lim, lim);
        var dest = PortalCenter(exit) + exit.T * s + exit.N * (nHalf + 1e-6);
        if (BoxHitsSolid(dest.X - HalfW, dest.Y - HalfH, dest.X + HalfW, dest.Y + HalfH)) return false;
        Pos = dest;
        Vel = vOut;
        Grounded = false;
        _jumpCutArmed = false;
        if (vOut.X != 0) Facing = Math.Sign(vOut.X);
        TeleportedThisTick = true;
        Events.Add(new SimEvent(SimEventKind.Teleport, from, entry == A ? 0 : 1, Pos));
        return true;
    }

    // ---------------------------------------------------------------- contacts

    void Contacts()
    {
        double hw = HalfW, hh = HalfH;
        if (Pos.Y - hh > Level.H + 1) { Die(); return; }

        double ins = Tuning.SpikeInset;
        if (Any(Pos, hw - ins, hh - ins, Tile.Spike)) { Die(); return; }
        if ((A != null || B != null) && Any(Pos, hw, hh, Tile.Grill))
        {
            A = null; B = null;
            Events.Add(new SimEvent(SimEventKind.PortalsCleared, Pos));
        }
        if (Any(Pos, hw, hh, Tile.Exit))
        {
            Finished = true;
            Events.Add(new SimEvent(SimEventKind.Finish, Pos));
        }
    }

    void Die()
    {
        Dead = true;
        Events.Add(new SimEvent(SimEventKind.Death, Pos));
    }

    bool Any(V2 p, double hw, double hh, Tile t)
    {
        int c0 = Fl(p.X - hw + Eps), c1 = Fl(p.X + hw - Eps), r0 = Fl(p.Y - hh + Eps), r1 = Fl(p.Y + hh - Eps);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
                if (Level[c, r] == t) return true;
        return false;
    }

    public bool OverlapsSolid(V2 p, double hw, double hh)
    {
        for (int i = 0; i < MoverOff.Length; i++)
        {
            var b = MoverBox(i);
            if (p.X + hw > b.x0 + 1e-9 && p.X - hw < b.x1 - 1e-9 && p.Y + hh > b.y0 + 1e-9 && p.Y - hh < b.y1 - 1e-9) return true;
        }
        int c0 = Fl(p.X - hw + Eps), c1 = Fl(p.X + hw - Eps), r0 = Fl(p.Y - hh + Eps), r1 = Fl(p.Y + hh - Eps);
        for (int r = r0; r <= r1; r++)
            for (int c = c0; c <= c1; c++)
                if (Solid(c, r)) return true;
        return false;
    }

    // ---------------------------------------------------------------- portals

    void Fire(int which, V2 aim)
    {
        var shot = Aim(which, aim);
        if (shot.Result == ShotResult.Placed && shot.Portal is { } p)
        {
            if (which == 0) A = p; else B = p;
            Events.Add(new SimEvent(SimEventKind.PortalPlaced, shot.To, which));
        }
        else Events.Add(new SimEvent(SimEventKind.PortalFizzled, shot.To, which));
    }

    /// <summary>
    /// Where would a shot toward <paramref name="aim"/> land, and would it make a portal?
    /// Pure query: the game uses it for the aim preview, Fire uses it to place.
    /// </summary>
    public Shot Aim(int which, V2 aim)
    {
        var o = Pos;
        var d = (aim - o).Normalized();
        if (d == V2.Zero) d = new V2(Facing, 0);

        int cx = Fl(o.X), cy = Fl(o.Y);
        int sx = Math.Sign(d.X), sy = Math.Sign(d.Y);
        double tdx = sx != 0 ? 1.0 / Math.Abs(d.X) : double.PositiveInfinity;
        double tdy = sy != 0 ? 1.0 / Math.Abs(d.Y) : double.PositiveInfinity;
        double tmx = sx > 0 ? (cx + 1 - o.X) * tdx : sx < 0 ? (o.X - cx) * tdx : double.PositiveInfinity;
        double tmy = sy > 0 ? (cy + 1 - o.Y) * tdy : sy < 0 ? (o.Y - cy) * tdy : double.PositiveInfinity;

        // Moving panels: the nearest one along the ray, compared with the grid hit below.
        var (mv, mt, mnx, mny) = RayMovers(o, d, Tuning.MaxShotRange);
        Shot MoverShot()
        {
            var hp = o + d * mt;
            var n = new V2(mnx, mny);
            if (!Level.Machines.Movers[mv].Portalable) return new Shot(ShotResult.NotPortalable, o, hp, null, n);
            var fit = FitMover(which, mv, mnx, mny, hp);
            return fit is { } fp ? new Shot(ShotResult.Placed, o, hp, fp, n) : new Shot(ShotResult.NoRoom, o, hp, null, n);
        }

        for (int guard = 0; guard < 4096; guard++)
        {
            double t; int nx = 0, ny = 0;
            if (tmx < tmy) { cx += sx; t = tmx; tmx += tdx; nx = -sx; }
            else { cy += sy; t = tmy; tmy += tdy; ny = -sy; }
            var hitPt = o + d * t;
            if (mv >= 0 && mt <= t) return MoverShot();
            if (t > Tuning.MaxShotRange) return new Shot(ShotResult.Miss, o, o + d * Tuning.MaxShotRange, null, V2.Zero);
            if (cy > Level.H + 2) return new Shot(ShotResult.Miss, o, hitPt, null, V2.Zero);
            if (!Solid(cx, cy))
            {
                if (Level[cx, cy] == Tile.Grill) return new Shot(ShotResult.Blocked, o, hitPt, null, new V2(nx, ny));
                continue;
            }
            var n = new V2(nx, ny);
            if (!PortalableAt(cx, cy)) return new Shot(ShotResult.NotPortalable, o, hitPt, null, n);
            var placed = Fit(which, cx, cy, nx, ny, hitPt);
            return placed is { } p
                ? new Shot(ShotResult.Placed, o, hitPt, p, n)
                : new Shot(ShotResult.NoRoom, o, hitPt, null, n);
        }
        return new Shot(ShotResult.Miss, o, o, null, V2.Zero);
    }

    Portal? Fit(int which, int cx, int cy, int nx, int ny, V2 hit)
    {
        bool vertical = nx != 0;
        int line = nx == -1 ? cx : nx == 1 ? cx + 1 : ny == -1 ? cy : cy + 1;
        double tc = vertical ? hit.Y : hit.X;
        int h = vertical ? cy : cx;
        // Space needed in front: a wall portal only has to fit the robot's width (1 tile);
        // floor and ceiling portals have to fit its height (2 tiles).
        int clearance = vertical ? Tuning.PortalClearanceWall : Tuning.PortalClearance;
        var other = which == 0 ? B : A;
        // Slide along the surface: try every span within PortalBump tiles of the hit, closest centre first.
        var starts = new List<int>();
        for (int st = h - Tuning.PortalLength + 1 - Tuning.PortalBump; st <= h + Tuning.PortalBump; st++) starts.Add(st);
        starts.Sort((a, b) =>
        {
            int c = Math.Abs(a + Tuning.PortalLength / 2.0 - tc).CompareTo(Math.Abs(b + Tuning.PortalLength / 2.0 - tc));
            return c != 0 ? c : a.CompareTo(b);   // ties: the lower span, every time (List.Sort is not stable)
        });
        foreach (int start in starts)
        {
            var p = new Portal(nx, ny, line, start);
            if (other is { } o && p.Overlaps(o)) continue;
            bool ok = true;
            for (int k = start; k < start + Tuning.PortalLength && ok; k++)
            {
                int wx = vertical ? cx : k, wy = vertical ? k : cy;
                if (!PortalableAt(wx, wy)) { ok = false; break; }
                for (int dd = 1; dd <= clearance; dd++)
                    if (Solid(wx + nx * dd, wy + ny * dd)) { ok = false; break; }
            }
            // A slid portal must stay on the same continuous surface as the hit (no jumping across gaps).
            if (ok && !Continuous(cx, cy, nx, ny, vertical, h, start)) ok = false;
            if (ok) return p;
        }
        return null;
    }

    /// <summary>Every tile between the hit tile and the span is portalable surface with open space in front.</summary>
    bool Continuous(int cx, int cy, int nx, int ny, bool vertical, int h, int start)
    {
        int lo = Math.Min(h, start), hi = Math.Max(h, start + Tuning.PortalLength - 1);
        for (int k = lo; k <= hi; k++)
        {
            int wx = vertical ? cx : k, wy = vertical ? k : cy;
            if (!PortalableAt(wx, wy) || Solid(wx + nx, wy + ny)) return false;
        }
        return true;
    }

    // ---------------------------------------------------------------- utils

    static int Fl(double v) => (int)Math.Floor(v);
    static double Approach(double v, double target, double accel)
    {
        double step = accel * Tuning.Dt;
        return v < target ? Math.Min(v + step, target) : Math.Max(v - step, target);
    }

    /// <summary>Order-sensitive hash of the full state; equal hashes = identical replays.</summary>
    public ulong StateHash()
    {
        ulong h = 1469598103934665603UL;
        void Mix(long v) { h ^= (ulong)v; h *= 1099511628211UL; }
        Mix(BitConverter.DoubleToInt64Bits(Pos.X)); Mix(BitConverter.DoubleToInt64Bits(Pos.Y));
        Mix(BitConverter.DoubleToInt64Bits(Vel.X)); Mix(BitConverter.DoubleToInt64Bits(Vel.Y));
        Mix(Tick); Mix(Crouched ? 1 : 0); Mix(Grounded ? 1 : 0); Mix(Dead ? 1 : 0); Mix(Finished ? 1 : 0);
        Mix(A?.GetHashCode() ?? 0); Mix(B?.GetHashCode() ?? 0);
        foreach (var o in DoorOpen) Mix(o ? 1 : 0);
        Mix(GroundMover);
        foreach (var b in Bullets) { Mix(BitConverter.DoubleToInt64Bits(b.P.X)); Mix(BitConverter.DoubleToInt64Bits(b.P.Y)); }
        return h;
    }
}

/// <summary>Runs a route headless. Used by tests, the trace tool, and the game to time developer routes.</summary>
public static class Runner
{
    public readonly record struct Result(bool Finished, bool Dead, int Ticks, World World);

    public static Result Run(Level level, IEnumerable<InputFrame> frames, int extraIdleTicks = 0)
    {
        var w = new World(level);
        foreach (var f in frames)
        {
            w.Step(f);
            if (w.Finished || w.Dead) return new Result(w.Finished, w.Dead, w.Tick, w);
        }
        for (int i = 0; i < extraIdleTicks && !w.Finished && !w.Dead; i++) w.Step(InputFrame.None);
        return new Result(w.Finished, w.Dead, w.Tick, w);
    }

    public static string FormatTime(int ticks)
    {
        int ms = (int)Math.Round(ticks * 1000.0 / Tuning.TicksPerSecond);
        return $"{ms / 60000}:{ms / 1000 % 60:00}.{ms % 1000:000}";
    }
}
