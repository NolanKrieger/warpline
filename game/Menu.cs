using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>Title and level select. Real buttons, so mouse and keyboard (arrows + Enter) both work.</summary>
public partial class Menu : Control
{
    public Main Game = null!;
    VBoxContainer _list = null!;
    float _time;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _list = new VBoxContainer { Position = new Vector2(150, 372), CustomMinimumSize = new Vector2(820, 0) };
        _list.AddThemeConstantOverride("separation", 8);
        AddChild(_list);
    }

    public void Rebuild()
    {
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        _list.AddChild(grid);
        Button? first = null, last = null;
        foreach (var li in Catalog.Levels)
        {
            bool open = Store.Unlocked(li.Index);
            var rec = Store.Record(li.Id);
            var medal = rec == null ? Medal.None : Medals.For(rec.PbTicks, li.DevTicks);
            var card = Card(li.Number, li.Level.Name, !open ? "LOCKED" : rec == null ? "new" : $"{Runner.FormatTime(rec.PbTicks)} {Pal.MedalName(medal)}",
                open, rec == null ? Pal.TextDim : Pal.MedalColor(medal));
            int idx = li.Index;
            card.Pressed += () => Game.StartLevel(idx, false);
            grid.AddChild(card);
            if (open) last = card;
            if (open && rec == null && first == null) first = card;   // focus the next level to beat
        }
        _list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var run = Store.Data.Run;
        int sob = run.SumOfBest(Catalog.Levels.Count);
        var rb = Row("FULL GAME RUN", Store.AllFinished ? (run.PbTicks > 0 ? Runner.FormatTime(run.PbTicks) + (sob > 0 ? $"   SoB {Runner.FormatTime(sob)}" : "") : "no run yet") : "finish every level first",
            Store.AllFinished, Pal.Exit);
        rb.Pressed += () => Game.StartLevel(0, true);
        _list.AddChild(rb);
        var pair1 = new HBoxContainer(); pair1.AddThemeConstantOverride("separation", 8);
        var ed = Row("EDITOR", $"{CustomLevels.List().Count} saved", true, Pal.Cyan, 406);
        ed.Pressed += () => Game.OpenLibrary();
        var st = Row("SETTINGS", "", true, Pal.TextDim, 406);
        st.Pressed += () => Game.OpenSettings();
        pair1.AddChild(ed); pair1.AddChild(st);
        _list.AddChild(pair1);
        var pair2 = new HBoxContainer(); pair2.AddThemeConstantOverride("separation", 8);
        var ac = Row("ACHIEVEMENTS", $"{Store.Data.Achievements.Count}/{Achievements.All.Length}", true, Pal.MedalColor(Medal.Gold), 406);
        ac.Pressed += () => Game.ShowAchievements();
        var cr = Row("CREDITS", "", true, Pal.TextDim, 406);
        cr.Pressed += () => Game.ShowCredits();
        pair2.AddChild(ac); pair2.AddChild(cr);
        _list.AddChild(pair2);
        var q = Row("QUIT", "", true, Pal.TextDim);
        q.Pressed += () => GetTree().Quit();
        _list.AddChild(q);
        (first ?? last ?? rb).CallDeferred(Control.MethodName.GrabFocus);

        // Medal tally: 1 point bronze ... 4 points dev, per level.
        _points = Catalog.Levels.Sum(l => Store.Record(l.Id) is { } r ? (int)Medals.For(r.PbTicks, l.DevTicks) : 0);
        _maxPoints = Catalog.Levels.Count * (int)Medal.Dev;
    }

    int _points, _maxPoints;

    /// <summary>A level card: number, name, and PB + medal (or LOCKED / new).</summary>
    static Button Card(string number, string name, string status, bool enabled, Color statusColor)
    {
        var b = Ui.Button("", 196, 20);
        b.CustomMinimumSize = new Vector2(196, 70);
        b.Disabled = !enabled;
        var num = Ui.Label(number, 15, Pal.Mono, enabled ? Pal.TextDim : Pal.TextDim with { A = 0.5f });
        num.Position = new Vector2(14, 6);
        b.AddChild(num);
        var nm = Ui.Label(name, 19, Pal.Black, enabled ? Pal.Text : Pal.TextDim with { A = 0.5f });
        nm.Position = new Vector2(38, 3);
        nm.ClipText = true;
        nm.Size = new Vector2(150, 28);
        b.AddChild(nm);
        var st = Ui.Label(status, 15, Pal.Mono, enabled ? statusColor : Pal.TextDim with { A = 0.5f });
        st.Position = new Vector2(14, 38);
        b.AddChild(st);
        return b;
    }

    Button Row(string left, string right, bool enabled, Color rightColor, float width = 820)
    {
        var b = Ui.Button(left, width);
        b.Disabled = !enabled;
        b.CustomMinimumSize = new Vector2(width, 56);
        if (right.Length > 0) Ui.RightLabel(b, right, enabled ? rightColor : Pal.TextDim with { A = 0.5f });
        return b;
    }

    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }

    public override void _Draw()
    {
        DrawRect(new Rect2(0, 0, 1920, 1080), Pal.Bg);
        for (int x = 0; x < 1920; x += 32)
            for (int y = 0; y < 1080; y += 32)
                DrawRect(new Rect2(x - 1, y - 1, 2, 2), Pal.BgDot);
        // Two portals on the right, with a robot mid-flight between them.
        float wob = 3 * Mathf.Sin(_time * 3);
        Portal(new Vector2(1500, 250), Vector2.Down, Pal.Magenta, wob);
        Portal(new Vector2(1500, 900), Vector2.Up, Pal.Cyan, -wob);
        float k = (_time * 0.8f) % 1f;
        var p = new Vector2(1500, 900 - k * 650);
        for (int i = 0; i < 6; i++)
            DrawRect(new Rect2(p.X - 11, p.Y - 22 + i * 18, 22, 44), Pal.Cyan.Lerp(Pal.Magenta, i / 6f) with { A = 0.12f * (6 - i) / 6f });
        var sb = new StyleBoxFlat { BgColor = Pal.Robot }; sb.SetCornerRadiusAll(5);
        DrawStyleBox(sb, new Rect2(p.X - 12, p.Y - 22, 24, 15));
        DrawStyleBox(sb, new Rect2(p.X - 10, p.Y - 8, 20, 22));
        DrawRect(new Rect2(p.X - 8, p.Y - 19, 16, 8), Pal.RobotDark);
        DrawRect(new Rect2(p.X + 1, p.Y - 16.5f, 6, 3), new Color("aef4ff"));

        DrawString(Pal.Black, new Vector2(144, 230), "WARPLINE", HorizontalAlignment.Left, -1, 128, Pal.Text);
        DrawRect(new Rect2(150, 252, 330, 8), Pal.Cyan);
        DrawRect(new Rect2(480, 252, 330, 8), Pal.Magenta);
        DrawString(Pal.Bold, new Vector2(150, 310), "2D portal speedrun  ·  speed in = speed out", HorizontalAlignment.Left, -1, 28, Pal.TextDim);
        if (_maxPoints > 0)
            DrawString(Pal.Mono, new Vector2(150, 346), $"medal points {_points} / {_maxPoints}", HorizontalAlignment.Left, -1, 20, Pal.MedalColor(Medal.Gold) with { A = _points > 0 ? 1 : 0.5f });

        string[] help =
        {
            "A / D  run", "Space  jump · off walls too", "S  crouch · slide keeps speed", "Left click  cyan portal",
            "Right click  magenta portal", "R  instant restart", "G  ghost: PB / dev / off", "Esc  pause · F11  fullscreen",
        };
        for (int i = 0; i < help.Length; i++)
            DrawString(Pal.Regular, new Vector2(1060, 470 + i * 38), help[i], HorizontalAlignment.Left, -1, 24, Pal.TextDim);
    }

    void Portal(Vector2 c, Vector2 n, Color col, float wob)
    {
        var a = c + new Vector2(-64, 0); var b = c + new Vector2(64, 0);
        DrawPolygon(new[] { a, b, b + n * (40 + wob), a + n * (40 + wob) }, new[] { col with { A = 0.5f }, col with { A = 0.5f }, col with { A = 0 }, col with { A = 0 } });
        DrawLine(a, b, col, 8, true);
        DrawLine(a, b, Colors.White with { A = 0.8f }, 2, true);
    }
}
