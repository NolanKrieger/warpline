using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>Static level art, drawn once per level load.</summary>
public partial class TileLayer : Node2D
{
    public const float T = WorldView.T;
    Level? _level;

    public void SetLevel(Level level) { _level = level; QueueRedraw(); }

    public override void _Draw()
    {
        if (_level is not { } L) return;
        for (int y = 0; y < L.H; y++)
            for (int x = 0; x < L.W; x++)
                if (!Tiles.Solid(L[x, y])) DrawRect(new Rect2(x * T - 1, y * T - 1, 2, 2), Pal.BgDot);

        for (int y = 0; y < L.H; y++)
            for (int x = 0; x < L.W; x++)
            {
                var t = L[x, y];
                var r = new Rect2(x * T, y * T, T, T);
                switch (t)
                {
                    case Tile.Panel: DrawPanel(L, x, y, r); break;
                    case Tile.Metal: DrawMetal(L, x, y, r); break;
                    case Tile.SpeedGel: DrawGel(L, x, y, r, Pal.SpeedGel); break;
                    case Tile.BounceGel: DrawGel(L, x, y, r, Pal.BounceGel); break;
                    case Tile.Spike: DrawSpike(L, x, y, r); break;
                    case Tile.Grill:
                        DrawRect(r, Pal.Grill with { A = 0.07f });
                        for (float gx = 3; gx < T; gx += 6) DrawLine(r.Position + new Vector2(gx, 0), r.Position + new Vector2(gx, T), Pal.Grill with { A = 0.22f }, 1);
                        break;
                    case Tile.Exit:
                        DrawRect(r, Pal.Exit with { A = 0.13f });
                        if (L[x, y - 1] != Tile.Exit) DrawRect(new Rect2(r.Position.X - 3, r.Position.Y - 3, T + 6, 4), Pal.Exit);
                        DrawRect(new Rect2(r.Position.X - 3, r.Position.Y, 3, T), Pal.Exit);
                        DrawRect(new Rect2(r.End.X, r.Position.Y, 3, T), Pal.Exit);
                        break;
                }
            }
    }

    // Outside the grid counts as closed for drawing, so the level's outer skin isn't painted as a surface.
    static bool Open(Level L, int x, int y) => x >= 0 && y >= 0 && x < L.W && y < L.H && !Tiles.Solid(L[x, y]);

    void DrawPanel(Level L, int x, int y, Rect2 r)
    {
        bool exposed = Open(L, x - 1, y) || Open(L, x + 1, y) || Open(L, x, y - 1) || Open(L, x, y + 1);
        if (!exposed)
        {
            // Deep inside a wall: only surfaces matter, so the core stays dark and the white skin reads as "portal here".
            DrawRect(r, Pal.PanelCore);
            DrawRect(new Rect2(r.Position.X, r.End.Y - 1, T, 1), Pal.PanelCoreLine);
            DrawRect(new Rect2(r.End.X - 1, r.Position.Y, 1, T), Pal.PanelCoreLine);
            return;
        }
        DrawRect(r, Pal.Panel);
        DrawRect(new Rect2(r.Position.X, r.End.Y - 3, T, 3), Pal.PanelShade);    // seams between panels
        DrawRect(new Rect2(r.End.X - 2, r.Position.Y, 2, T), Pal.PanelShade);
        Edges(L, x, y, r, Pal.PanelEdge, 3);
    }

    void DrawMetal(Level L, int x, int y, Rect2 r)
    {
        DrawRect(r, Pal.Metal);
        bool inside = !Open(L, x - 1, y) && !Open(L, x + 1, y) && !Open(L, x, y - 1) && !Open(L, x, y + 1);
        if (inside) DrawRect(r, Pal.MetalDark with { A = 0.55f });
        Edges(L, x, y, r, Pal.MetalLight, 3);
    }

    /// <summary>Gel: a dark block with a thick glossy coat on every exposed face.</summary>
    void DrawGel(Level L, int x, int y, Rect2 r, Color c)
    {
        DrawRect(r, c.Darkened(0.6f));
        Edges(L, x, y, r, c, 12);
        Edges(L, x, y, new Rect2(r.Position + new Vector2(3, 3), r.Size - new Vector2(6, 6)), c.Lightened(0.5f), 3);
        if (Open(L, x, y + 1))   // drips under a gel ceiling
            DrawRect(new Rect2(r.Position.X + 6 + (x * 7 % 13), r.End.Y, 4, 5 + (x * 3 % 5)), c);
    }

    void Edges(Level L, int x, int y, Rect2 r, Color c, float w)
    {
        if (Open(L, x, y - 1)) DrawRect(new Rect2(r.Position.X, r.Position.Y, T, w), c);
        if (Open(L, x, y + 1)) DrawRect(new Rect2(r.Position.X, r.End.Y - w, T, w), c);
        if (Open(L, x - 1, y)) DrawRect(new Rect2(r.Position.X, r.Position.Y, w, T), c);
        if (Open(L, x + 1, y)) DrawRect(new Rect2(r.End.X - w, r.Position.Y, w, T), c);
    }

    void DrawSpike(Level L, int x, int y, Rect2 r)
    {
        // Point away from whatever the spikes are mounted on.
        Vector2 up = Tiles.Solid(L[x, y + 1]) ? Vector2.Up : Tiles.Solid(L[x, y - 1]) ? Vector2.Down
                   : Tiles.Solid(L[x - 1, y]) ? Vector2.Right : Tiles.Solid(L[x + 1, y]) ? Vector2.Left : Vector2.Up;
        var side = new Vector2(-up.Y, up.X);
        var c = r.GetCenter();
        var baseMid = c - up * (T / 2);
        for (int i = 0; i < 3; i++)
        {
            float o = (i - 1) * T / 3;
            var b0 = baseMid + side * (o - T / 6);
            var b1 = baseMid + side * (o + T / 6);
            var tip = baseMid + side * o + up * (T * 0.8f);
            DrawColoredPolygon(new[] { b0, b1, tip }, Pal.Spike);
        }
    }
}

/// <summary>
/// Faint architectural shapes behind the level, moving at half camera speed. Drawn once per level;
/// only the node position changes per frame. Never uses cyan, magenta or red (those mean something).
/// </summary>
public partial class BgLayer : Node2D
{
    const float K = 0.5f;   // parallax: moves at half the camera's speed
    Level? _level;
    public Vector2 CameraCenter { set { Visible = _level != null && !Store.Data.Settings.ReduceMotion; Position = value * (1 - K) - new Vector2(960, 540) * (1 - K); } }

    public Level? Level
    {
        set { _level = value; Visible = value != null; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (_level is not { } L) return;
        var rng = new Random(L.Id.Aggregate(17, (h, c) => h * 31 + c));
        float w = L.W * WorldView.T * K + 1920, h = L.H * WorldView.T * K + 1080;
        var line = new Color(0.75f, 0.82f, 0.95f, 0.045f);
        var fill = new Color(0.75f, 0.82f, 0.95f, 0.018f);
        for (int i = 0; i < 26; i++)
        {
            float rw = 120 + (float)rng.NextDouble() * 520, rh = 80 + (float)rng.NextDouble() * 420;
            var r = new Rect2((float)rng.NextDouble() * w - rw / 2, (float)rng.NextDouble() * h - rh / 2, rw, rh);
            if (rng.Next(3) == 0) DrawRect(r, fill);
            DrawRect(r, line, false, 2);
            if (rng.Next(2) == 0)   // a bracket of short ticks along one edge
                for (float x = r.Position.X + 12; x < r.End.X - 12; x += 24) DrawLine(new Vector2(x, r.End.Y), new Vector2(x, r.End.Y - 8), line, 2);
        }
        for (int i = 0; i < 14; i++)
        {
            float x = (float)rng.NextDouble() * w, y = (float)rng.NextDouble() * h, len = 200 + (float)rng.NextDouble() * 700;
            DrawLine(new Vector2(x, y), new Vector2(x + len, y - len), line, 2);
            DrawLine(new Vector2(x + 18, y), new Vector2(x + 18 + len, y - len), line with { A = 0.025f }, 2);
        }
    }
}

/// <summary>Everything that moves: portals, robot, ghost, aim preview, particles, crosshair.</summary>
public partial class WorldView : Node2D
{
    public const float T = 32f;
    public TileLayer Tiles { get; } = new();
    public Session? Session;
    public bool ShowAim = true;
    public bool ShowCrosshair = true;
    public Vector2 AimPx;
    readonly StyleBoxFlat _box = new() { CornerDetail = 4 };

    struct Particle { public Vector2 P, V; public float Life, Max, Size; public Color C; public bool Ring; }
    readonly List<Particle> _parts = new();
    readonly List<(Vector2 p, float t)> _trail = new();
    float _time, _runPhase;
    float _squash;                      // >0 squash (landing), <0 stretch (jump); decays to 0
    Portal? _lastA, _lastB;
    float _openA = 1, _openB = 1;       // 0..1 portal opening animation
    readonly Random _rng = new(3);

    public override void _Ready()
    {
        Tiles.ShowBehindParent = true;   // static art under the moving things
        AddChild(Tiles);
    }

    public static Vector2 Px(V2 v) => new((float)(v.X * T), (float)(v.Y * T));

    public Vector2 PlayerPx()
    {
        if (Session is not { } s) return Vector2.Zero;
        float a = (float)Engine.GetPhysicsInterpolationFraction();
        var p = s.Started ? s.PrevPos + (s.World.Pos - s.PrevPos) * a : s.World.Pos;
        return Px(p);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        for (int i = _parts.Count - 1; i >= 0; i--)
        {
            var p = _parts[i];
            p.Life -= dt;
            if (p.Life <= 0) { _parts.RemoveAt(i); continue; }
            p.P += p.V * dt;
            if (!p.Ring) p.V = p.V * (1 - 3 * dt) + new Vector2(0, 600) * dt;
            _parts[i] = p;
        }
        _squash = Mathf.MoveToward(_squash, 0, dt * 7);
        _openA = Math.Min(1, _openA + dt * 9);
        _openB = Math.Min(1, _openB + dt * 9);
        if (Session is { } s)
        {
            if (s.World.A != _lastA) { if (s.World.A != null) _openA = 0; _lastA = s.World.A; }
            if (s.World.B != _lastB) { if (s.World.B != null) _openB = 0; _lastB = s.World.B; }
            var speed = s.World.Vel.Length;
            if (s.World.OnSpeedGel && Math.Abs(s.World.Vel.X) > Tuning.MaxRun + 2 && _rng.Next(3) == 0)
            {
                var feet = PlayerPx() + new Vector2(-Math.Sign((float)s.World.Vel.X) * 8, (float)s.World.HalfH * T);
                _parts.Add(new Particle { P = feet, V = new Vector2(-(float)s.World.Vel.X * 6, -80 - _rng.Next(80)), Life = 0.3f, Max = 0.3f, C = Pal.SpeedGel, Size = 3 });
            }
            if (s.World.Grounded && Math.Abs(s.World.Vel.X) > 0.5) _runPhase += dt * (float)Math.Abs(s.World.Vel.X) * 1.6f;
            if (!Store.Data.Settings.ReduceMotion) _trail.Add((PlayerPx(), _time));
            while (_trail.Count > 0 && _time - _trail[0].t > 0.14f) _trail.RemoveAt(0);
            if (speed < Tuning.MaxRun + 3) _trail.Clear();
        }
        QueueRedraw();
    }

    // ---------------------------------------------------------------- effects API

    public void Burst(V2 at, Color c, int n, float speed, float life = 0.45f, float size = 4)
    {
        var p = Px(at);
        for (int i = 0; i < n; i++)
        {
            double a = _rng.NextDouble() * Math.PI * 2;
            float s = speed * (0.35f + (float)_rng.NextDouble() * 0.65f);
            _parts.Add(new Particle { P = p, V = new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * s, Life = life, Max = life, C = c, Size = size });
        }
    }

    public void RingAt(V2 at, Color c, float speed = 260, float life = 0.35f)
        => _parts.Add(new Particle { P = Px(at), V = new Vector2(speed, 0), Life = life, Max = life, C = c, Ring = true });

    public void ClearEffects() { _parts.Clear(); _trail.Clear(); _squash = 0; }
    public void Squash() => _squash = Store.Data.Settings.ReduceMotion ? 0 : 1;
    public void Stretch() => _squash = Store.Data.Settings.ReduceMotion ? 0 : -0.8f;

    // ---------------------------------------------------------------- drawing

    public override void _Draw()
    {
        if (Session is not { } s) return;
        var w = s.World;
        DrawAnimatedTiles(w.Level);
        DrawMachinesBack(w);

        if (s.Ghost is { } g && s.Started)
        {
            float a = (float)Engine.GetPhysicsInterpolationFraction();
            var gp = Px(s.PrevGhostPos + (g.Pos - s.PrevGhostPos) * a);
            if (g.A is { } ga) DrawPortal(g, ga, Pal.Cyan, 0.3f);
            if (g.B is { } gb) DrawPortal(g, gb, Pal.Magenta, 0.3f);
            if (!g.Finished) DrawRobot(gp, (float)g.HalfH, g.Facing, 0, g.Grounded, false, Vector2.Right * g.Facing, 0.33f, true);
        }

        DrawMovers(w);
        if (w.A is { } pa) DrawPortal(w, pa, Pal.Cyan, 1f, Ease(_openA));
        if (w.B is { } pb) DrawPortal(w, pb, Pal.Magenta, 1f, Ease(_openB));

        var pp = PlayerPx();
        var aimDir = (AimPx - pp).Normalized();
        if (ShowAim && !w.Dead && !w.Finished) DrawAim(w);

        for (int i = 0; i < _trail.Count; i++)
        {
            float k = (i + 1f) / (_trail.Count + 1f);
            var c = Pal.Cyan.Lerp(Pal.Magenta, k) with { A = 0.18f * k };
            float hh = (float)w.HalfH * T;
            _box.BgColor = c; _box.SetCornerRadiusAll(6);
            DrawStyleBox(_box, new Rect2(_trail[i].p.X - 11, _trail[i].p.Y - hh, 22, hh * 2));
        }

        if (!w.Dead)
        {
            float q = _squash;
            var scale = new Vector2(1 + 0.22f * q, 1 - 0.22f * q);
            // Lean into horizontal speed a little.
            float lean = Mathf.Clamp((float)w.Vel.X / 60f, -1, 1) * 0.12f * (w.Grounded ? 0.4f : 1f);
            var feet = pp + new Vector2(0, (float)w.HalfH * T);
            DrawSetTransform(feet, lean, scale);
            DrawRobot(new Vector2(0, -(float)w.HalfH * T), (float)w.HalfH, w.Facing, _runPhase, w.Grounded, w.Sliding, aimDir.Rotated(-lean), 1f, false);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        DrawMachinesFront(w);

        foreach (var p in _parts)
        {
            float k = p.Life / p.Max;
            if (p.Ring) DrawArc(p.P, (1 - k) * p.V.X * p.Max + 4, 0, Mathf.Tau, 32, p.C with { A = k * 0.9f }, 3, true);
            else DrawRect(new Rect2(p.P - Vector2.One * p.Size / 2, Vector2.One * p.Size), p.C with { A = Math.Min(1, k * 1.5f) });
        }

        if (ShowCrosshair) DrawCrosshair(w);
    }

    void DrawAnimatedTiles(Level L)
    {
        // Only the visible part of the level.
        var inv = GetCanvasTransform().AffineInverse();
        var vr = GetViewportRect();
        var tl = inv * vr.Position; var br = inv * vr.End;
        int x0 = Math.Max(0, (int)(tl.X / T) - 1), x1 = Math.Min(L.W - 1, (int)(br.X / T) + 1);
        int y0 = Math.Max(0, (int)(tl.Y / T) - 1), y1 = Math.Min(L.H - 1, (int)(br.Y / T) + 1);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var t = L[x, y];
                if (t == Tile.Grill)
                {
                    float sweep = ((_time * 1.3f + x * 0.07f) % 1.0f) * T;
                    DrawRect(new Rect2(x * T, y * T + sweep - 2, T, 4), Pal.Grill with { A = 0.18f });
                }
                else if (t == Tile.Exit && L[x, y - 1] != Tile.Exit)
                {
                    var c = new Vector2(x * T + T / 2, y * T - 18 - pulse * 6);
                    DrawColoredPolygon(new[] { c + new Vector2(-8, -8), c + new Vector2(8, -8), c + new Vector2(0, 4) }, Pal.Exit with { A = 0.6f + 0.4f * pulse });
                }
            }
    }

    static float Ease(float t) => 1 - (1 - t) * (1 - t) * (1 - t);

    void DrawPortal(World w, Portal p, Color c, float alpha, float open = 1f)
    {
        var (ea, eb) = w.PortalEnds(p, Engine.GetPhysicsInterpolationFraction());
        var a = Px(ea); var b = Px(eb);
        if (open < 1f) { var mid = (a + b) / 2; a = mid + (a - mid) * open; b = mid + (b - mid) * open; }
        var n = new Vector2(p.Nx, p.Ny);
        float wob = 1.5f * Mathf.Sin(_time * 9);
        DrawPolygon(new[] { a, b, b + n * (18 + wob), a + n * (18 + wob) },
            new[] { c with { A = 0.55f * alpha }, c with { A = 0.55f * alpha }, c with { A = 0 }, c with { A = 0 } });
        DrawLine(a + n * 3, b + n * 3, c with { A = alpha }, 6, true);
        DrawLine(a + n * 1.5f, b + n * 1.5f, Colors.White with { A = 0.85f * alpha }, 2, true);
        if (c == Pal.Magenta)
        {
            // Shape cue: magenta has square end caps, cyan round ones.
            DrawRect(new Rect2(a + n * 3 - Vector2.One * 4.5f, Vector2.One * 9), c with { A = alpha });
            DrawRect(new Rect2(b + n * 3 - Vector2.One * 4.5f, Vector2.One * 9), c with { A = alpha });
        }
        else
        {
            DrawCircle(a + n * 3, 5, c with { A = alpha });
            DrawCircle(b + n * 3, 5, c with { A = alpha });
        }
        // Particles drifting out of the surface.
        var along = (b - a);
        for (int i = 0; i < 4; i++)
        {
            float k = (_time * 0.9f + i * 0.25f) % 1f;
            float s = ((i * 0.37f + _time * 0.21f) % 1f);
            var q = a + along * s + n * (4 + k * 18);
            DrawRect(new Rect2(q - Vector2.One * 1.5f, Vector2.One * 3), c with { A = (1 - k) * 0.8f * alpha });
        }
    }

    void DrawAim(World w)
    {
        var aim = new V2(AimPx.X / T, AimPx.Y / T);
        var shotA = w.Aim(0, aim);
        var shotB = w.Aim(1, aim);
        var shot = shotA.Result == ShotResult.Placed ? shotA : shotB;
        var from = PlayerPx();
        var to = Px(shot.To);
        if (shot.Result == ShotResult.Miss) to = from + (to - from).LimitLength(260);
        DrawDashedLine(from, to, Colors.White with { A = 0.22f }, 2, 8);
        if (shot.Result == ShotResult.Placed && shot.Portal is { } p)
        {
            var (ea, eb) = w.PortalEnds(p);
            var a = Px(ea); var b = Px(eb);
            var n = new Vector2(p.Nx, p.Ny);
            DrawLine(a + n * 5, b + n * 5, Colors.White with { A = 0.75f }, 2, true);
            DrawLine(a + n * 5, a + n * 12, Colors.White with { A = 0.75f }, 2, true);
            DrawLine(b + n * 5, b + n * 12, Colors.White with { A = 0.75f }, 2, true);
        }
        else if (shot.Result is ShotResult.NotPortalable or ShotResult.NoRoom or ShotResult.Blocked)
        {
            DrawLine(to + new Vector2(-7, -7), to + new Vector2(7, 7), Pal.Spike, 3, true);
            DrawLine(to + new Vector2(-7, 7), to + new Vector2(7, -7), Pal.Spike, 3, true);
        }
    }

    void DrawCrosshair(World w)
    {
        var m = AimPx;
        DrawArc(m, 11, Mathf.Pi / 2 + 0.25f, Mathf.Pi * 1.5f - 0.25f, 16, Pal.Cyan with { A = w.A != null ? 1f : 0.4f }, w.A != null ? 4 : 2, true);
        DrawArc(m, 11, -Mathf.Pi / 2 + 0.25f, Mathf.Pi / 2 - 0.25f, 16, Pal.Magenta with { A = w.B != null ? 1f : 0.4f }, w.B != null ? 4 : 2, true);
        DrawCircle(m, 2, Colors.White);
    }

    /// <summary>The robot: head with visor, torso, legs, a portal gun arm and an antenna.</summary>
    void DrawRobot(Vector2 c, float halfH, int facing, float phase, bool grounded, bool sliding, Vector2 aim, float alpha, bool ghost)
    {
        float h = halfH * 2 * T, top = c.Y - h / 2, bottom = c.Y + h / 2;
        bool crouched = halfH < Tuning.HalfH - 0.01;
        Color body = ghost ? Colors.White with { A = alpha } : Pal.Robot;
        Color shade = ghost ? Colors.White with { A = alpha * 0.6f } : Pal.RobotShade;
        Color dark = ghost ? Colors.White with { A = alpha * 0.25f } : Pal.RobotDark;

        float headH = 15, legH = crouched ? 0 : 9;
        float torsoTop = top + headH - 1, torsoBottom = bottom - legH;
        if (!crouched)
        {
            float lift = grounded ? Mathf.Sin(phase) * 3 : 2;
            DrawRect(new Rect2(c.X - 8, bottom - legH + Math.Max(0, lift) * -1, 6, legH), shade);
            DrawRect(new Rect2(c.X + 2, bottom - legH + Math.Max(0, -lift) * -1, 6, legH), shade);
        }
        _box.BgColor = body; _box.SetCornerRadiusAll(4);
        DrawStyleBox(_box, new Rect2(c.X - 10, torsoTop, 20, Math.Max(5, torsoBottom - torsoTop)));
        if (!ghost) DrawRect(new Rect2(c.X - 6, torsoTop + 4, 12, 2), shade);

        // antenna
        var ab = new Vector2(c.X - facing * 6, top + 1);
        DrawLine(ab, ab + new Vector2(-facing * 3, -7), dark with { A = ghost ? alpha : 1 }, 2);
        DrawCircle(ab + new Vector2(-facing * 3, -8), 2.5f, ghost ? body : Pal.Cyan);

        // head + visor + eye
        _box.BgColor = body; _box.SetCornerRadiusAll(5);
        DrawStyleBox(_box, new Rect2(c.X - 12, top, 24, headH));
        if (!ghost)
        {
            _box.BgColor = Pal.RobotDark; _box.SetCornerRadiusAll(3);
            DrawStyleBox(_box, new Rect2(c.X - 9 + facing * 1.5f, top + 3, 18, 8));
            DrawRect(new Rect2(c.X + facing * 4 - 3, top + 5.5f, 6, 3), new Color("aef4ff"));
        }

        // portal gun arm, pointing at the aim
        if (!ghost)
        {
            var sh = new Vector2(c.X, torsoTop + 5);
            var tip = sh + aim * 15;
            DrawLine(sh, tip, Pal.RobotDark, 6, true);
            DrawLine(sh, tip - aim * 3, Pal.RobotShade, 3, true);
            DrawCircle(tip, 3, Colors.White);
        }

        if (sliding && !ghost)
            for (int i = 0; i < 3; i++)
                DrawRect(new Rect2(c.X - facing * (10 + i * 7) - 2, bottom - 3 - (i % 2) * 2, 4, 2), Pal.PanelShade with { A = 0.7f });
    }
}
