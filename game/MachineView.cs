using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>Drawing for doors, buttons, lasers, receivers, turrets and bullets. Red is only ever used for what kills.</summary>
public partial class WorldView
{
    // Checked under protanopia/deuteranopia/tritanopia simulation: every pair stays ≥ ΔE 21 apart
    // (lime was replaced by rust: yellow/lime collapsed to ΔE 2.6 for deuteranopes). Pips show the number too.
    static readonly Color[] Channel = { new("ffe066"), new("7aa2ff"), new("c46b00"), new("eef1f5") };

    /// <summary>Channel number as dots, so links never depend on colour alone.</summary>
    void Pips(Vector2 at, int ch, Color c)
    {
        for (int i = 0; i < ch; i++) DrawCircle(at + new Vector2((i - (ch - 1) / 2f) * 6, 0), 2.2f, c);
    }

    static int ChannelOf(Machines m, int door)
    {
        var id = m.Doors[door].Id;
        return id.Length > 1 && id[0] == 'c' && char.IsDigit(id[1]) ? id[1] - '0' : door % Channel.Length + 1;
    }

    public static Color ChannelColor(int ch) => Channel[(Math.Max(1, ch) - 1) % Channel.Length];

    /// <summary>
    /// A door's colour: its channel (editor ids look like c2d5) or, for hand-written levels, its index.
    /// Buttons and receivers borrow the colour of the first door they open.
    /// </summary>
    static Color DoorColor(Machines m, int door)
    {
        var id = m.Doors[door].Id;
        if (id.Length > 1 && id[0] == 'c' && char.IsDigit(id[1])) return ChannelColor(id[1] - '0');
        return Channel[door % Channel.Length];
    }
    static Color LinkColor(Machines m, string[] opens) => opens.Length > 0 ? DoorColor(m, m.DoorIndex(opens[0])) : Pal.TextDim;

    void DrawMachinesBack(World w)
    {
        var m = w.Level.Machines;
        if (!m.Any) return;
        for (int d = 0; d < m.Doors.Count; d++) DrawDoor(w, m, d);
        for (int i = 0; i < m.Buttons.Count; i++) DrawButton(w, m, i);
        for (int i = 0; i < m.Receivers.Count; i++) DrawReceiver(w, m, i);
    }

    void DrawMachinesFront(World w)
    {
        var m = w.Level.Machines;
        if (!m.Any) return;
        for (int l = 0; l < m.Lasers.Count; l++) DrawLaser(w, m, l);
        for (int t = 0; t < m.Turrets.Count; t++) DrawTurret(m.Turrets[t]);
        foreach (var b in w.Bullets)
        {
            var p = Px(b.P);
            var back = -new Vector2((float)b.V.X, (float)b.V.Y).Normalized() * 14;
            DrawLine(p, p + back, Pal.Spike with { A = 0.35f }, 5, true);
            DrawCircle(p, 5.5f, Pal.Spike);
            DrawCircle(p, 2.2f, Colors.White);
        }
    }

    /// <summary>Moving panels: a rail showing the track, then the block at its blended position.</summary>
    void DrawMovers(World w)
    {
        var movers = w.Level.Machines.Movers;
        float alpha = (float)Engine.GetPhysicsInterpolationFraction();
        for (int i = 0; i < movers.Count; i++)
        {
            var m = movers[i];
            var rest = new Vector2((m.X + m.W / 2f) * T, (m.Y + m.H / 2f) * T);
            if (m.Orbit > 0)
            {
                var hub = rest - new Vector2((float)m.Orbit * T, 0);
                DrawArc(hub, (float)m.Orbit * T, 0, Mathf.Tau, 64, Pal.TextDim with { A = 0.3f }, 3, true);
                DrawCircle(hub, 6, Pal.TextDim with { A = 0.5f });
            }
            else
            {
                var far = rest + new Vector2(m.Dx * T, m.Dy * T);
                DrawDashedLine(rest, far, Pal.TextDim with { A = 0.35f }, 3, 10);
                DrawCircle(rest, 4, Pal.TextDim with { A = 0.5f });
                DrawCircle(far, 4, Pal.TextDim with { A = 0.5f });
            }
            var o = w.MoverOffsetLerp(i, alpha);
            var r = new Rect2((float)((m.X + o.X) * T), (float)((m.Y + o.Y) * T), m.W * T, m.H * T);
            if (m.Portalable)
            {
                DrawRect(r, Pal.Panel);
                for (int x = 1; x < m.W; x++) DrawLine(new Vector2(r.Position.X + x * T, r.Position.Y), new Vector2(r.Position.X + x * T, r.End.Y), Pal.PanelShade, 2);
                for (int y = 1; y < m.H; y++) DrawLine(new Vector2(r.Position.X, r.Position.Y + y * T), new Vector2(r.End.X, r.Position.Y + y * T), Pal.PanelShade, 2);
                DrawRect(r, Pal.PanelEdge, false, 3);
            }
            else
            {
                DrawRect(r, Pal.Metal);
                DrawRect(r, Pal.MetalLight, false, 3);
            }
            // little rollers so it reads as a machine, not a wall
            DrawCircle(r.GetCenter(), 5, Pal.MetalLight);
            DrawCircle(r.GetCenter(), 2, Pal.Bg);
        }
    }

    void DrawDoor(World w, Machines m, int d)
    {
        var door = m.Doors[d];
        var c = DoorColor(m, d);
        var r = new Rect2(door.X * T, door.Y * T, door.W * T, door.H * T);
        if (!w.DoorOpen[d])
        {
            DrawRect(r, new Color("2c313c"));
            // chevrons
            for (float y = r.Position.Y + 6; y < r.End.Y - 4; y += 14)
                DrawLine(new Vector2(r.Position.X + 5, y), new Vector2(r.End.X - 5, y + 6), c with { A = 0.35f }, 3);
            DrawRect(r.Grow(-1.5f), c, false, 3);
            Pips(new Vector2(r.GetCenter().X, r.Position.Y + 8), ChannelOf(m, d), c);
        }
        else
        {
            DrawRect(r, c with { A = 0.05f });
            DrawRect(r.Grow(-1.5f), c with { A = 0.3f }, false, 2);
        }
        // Timed doors: a bar that runs down with the linked timed button.
        for (int i = 0; i < m.Buttons.Count; i++)
        {
            var b = m.Buttons[i];
            if (b.Mode != ButtonMode.Timed || !b.Opens.Contains(door.Id) || w.ButtonTimer[i] <= 0) continue;
            float k = w.ButtonTimer[i] / (float)b.TimeTicks;
            DrawRect(new Rect2(r.Position.X - 6, r.End.Y - r.Size.Y * k, 4, r.Size.Y * k), c);
        }
    }

    void DrawButton(World w, Machines m, int i)
    {
        var b = m.Buttons[i];
        var c = LinkColor(m, b.Opens);
        float x = b.X * T, y = b.Y * T;
        bool active = w.ButtonActive[i], down = w.ButtonDown[i];
        // Mount on whatever solid neighbour there is (floor first).
        Rect2 plate, basePlate;
        float th = down ? 3 : 7;
        if (w.Solid(b.X, b.Y + 1)) { basePlate = new Rect2(x + 4, y + T - 3, T - 8, 3); plate = new Rect2(x + 7, y + T - 3 - th, T - 14, th); }
        else if (w.Solid(b.X, b.Y - 1)) { basePlate = new Rect2(x + 4, y, T - 8, 3); plate = new Rect2(x + 7, y + 3, T - 14, th); }
        else if (w.Solid(b.X - 1, b.Y)) { basePlate = new Rect2(x, y + 4, 3, T - 8); plate = new Rect2(x + 3, y + 7, th, T - 14); }
        else { basePlate = new Rect2(x + T - 3, y + 4, 3, T - 8); plate = new Rect2(x + T - 3 - th, y + 7, th, T - 14); }
        DrawRect(basePlate, Pal.MetalLight);
        DrawRect(plate, active ? c : c.Darkened(0.45f));
        if (active) DrawRect(plate.Grow(3), c with { A = 0.18f });
        if (b.Opens.Length > 0) Pips(new Vector2(x + T / 2, y + T / 2 - 4), ChannelOf(m, m.DoorIndex(b.Opens[0])), c);
        if (b.Mode == ButtonMode.Timed)
            DrawArc(new Vector2(x + T / 2, y + T / 2), 6, 0, Mathf.Tau, 12, c with { A = 0.5f }, 1.5f, true);
    }

    void DrawReceiver(World w, Machines m, int i)
    {
        var r = m.Receivers[i];
        var c = LinkColor(m, r.Opens);
        var ctr = new Vector2(r.X * T + T / 2, r.Y * T + T / 2);
        bool lit = w.ReceiverLit[i];
        DrawCircle(ctr, 13, new Color("1d2129"));
        DrawArc(ctr, 13, 0, Mathf.Tau, 24, c, 3, true);
        DrawArc(ctr, 7, 0, Mathf.Tau, 16, c with { A = 0.6f }, 2, true);
        if (lit)
        {
            DrawCircle(ctr, 6, c);
            DrawArc(ctr, 17 + 2 * Mathf.Sin(_time * 12), 0, Mathf.Tau, 24, c with { A = 0.5f }, 2, true);
        }
    }

    void DrawLaser(World w, Machines m, int l)
    {
        var las = m.Lasers[l];
        var ctr = new Vector2(las.X * T + T / 2, las.Y * T + T / 2);
        var dir = new Vector2(las.Dx, las.Dy);
        bool on = w.Beams[l].Count > 0;
        int toOn = las.TicksToOn(w.Tick);
        if (on)
        {
            foreach (var (a, b) in w.Beams[l])
            {
                var pa = Px(a); var pb = Px(b);
                DrawLine(pa, pb, Pal.Spike with { A = 0.22f }, 12, true);
                DrawLine(pa, pb, Pal.Spike, 4, true);
                DrawLine(pa, pb, Colors.White with { A = 0.85f }, 1.5f, true);
            }
        }
        else if (las.Period > 0)
        {
            // Warning line where the beam will be; it brightens in the last half second.
            float warn = toOn < 60 ? 0.35f + 0.3f * Mathf.Sin(_time * 40) : 0.12f;
            foreach (var (a, b) in w.PreviewBeam(l)) DrawDashedLine(Px(a), Px(b), Pal.Spike with { A = warn }, 2, 10);
        }
        // emitter body
        var body = new Rect2(ctr - new Vector2(11, 11), new Vector2(22, 22));
        DrawRect(body, new Color("1d2129"));
        DrawRect(body, Pal.MetalLight, false, 2);
        DrawCircle(ctr + dir * 8, 5, on ? Pal.Spike : Pal.Spike with { A = 0.35f });
    }

    void DrawTurret(TurretDef t)
    {
        var ctr = new Vector2(t.X * T + T / 2, t.Y * T + T / 2);
        var dir = new Vector2(t.Dx, t.Dy);
        DrawLine(ctr, ctr + dir * 17, Pal.MetalLight, 8);
        _box.BgColor = new Color("1d2129"); _box.SetCornerRadiusAll(6);
        DrawStyleBox(_box, new Rect2(ctr - new Vector2(12, 12), new Vector2(24, 24)));
        DrawRect(new Rect2(ctr - new Vector2(12, 12), new Vector2(24, 24)), Pal.MetalLight, false, 2);
        DrawCircle(ctr + dir * 4, 4, Pal.Spike);
    }
}
