using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>Timer, splits, prompts and the finish card, drawn directly (no scene files to keep in sync).</summary>
public partial class Hud : Control
{
    public Main Game = null!;
    float _time;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    float _introAt = -10, _flashAt = -10, _splitAt = -10;
    Color _flash;
    int? _splitDelta;
    bool _splitGold;

    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }

    public void Intro() => _introAt = _time;
    public void Flash(Color c) { if (Store.Data.Settings.ReduceMotion) return; _flash = c; _flashAt = _time; }

    public void SplitPopup(int? delta, bool gold) { _splitDelta = delta; _splitGold = gold; _splitAt = _time; }

    static string Fmt(int ticks) => Runner.FormatTime(ticks);
    static string Delta(int d)
    {
        int ms = (int)Math.Round(Math.Abs(d) * 1000.0 / Tuning.TicksPerSecond);
        return (d < 0 ? "−" : "+") + (ms >= 60000 ? $"{ms / 60000}:{ms / 1000 % 60:00}.{ms % 1000 / 10:00}" : $"{ms / 1000}.{ms % 1000 / 10:00}");
    }

    void Text(string s, Vector2 pos, Font f, int size, Color c, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1)
    {
        // Dark outline: the HUD often sits over white portal panels.
        DrawStringOutline(f, pos, s, align, width, size, Math.Max(6, size / 5), new Color(0.03f, 0.04f, 0.06f, 0.85f * c.A));
        DrawString(f, pos, s, align, width, size, c);
    }

    public override void _Draw() => DrawPlaying();

    void DrawPlaying()
    {
        if (Game.Session is not { } s || !Game.Playing) return;
        var info = s.Info;
        float W = 1920;

        // No full-width band: the top rows of a level stay visible (and aimable) under the HUD.
        DrawRect(new Rect2(W / 2 - 170, 6, 340, 72), new Color(0.03f, 0.04f, 0.06f, 0.6f));

        // Left: level
        Text($"{info.Number}", new Vector2(28, 52), Pal.Mono, 26, Pal.TextDim);
        Text(info.Level.Name.ToUpperInvariant(), new Vector2(78, 52), Pal.Black, 30, Pal.Text);

        // Centre: level timer + comparison
        var rec = Store.Record(info.Id);
        int t = s.World.Tick;
        Color timerColor = s.Finished ? Pal.Exit : Pal.Text;
        Text(Fmt(t), new Vector2(0, 50), Pal.Mono, 46, timerColor, HorizontalAlignment.Center, W);
        string devName = info.Custom ? "AUTHOR" : "DEV";
        string sub = rec != null ? $"PB {Fmt(rec.PbTicks)}" : info.DevTicks > 0 ? $"{devName} {Fmt(info.DevTicks)}" : "";
        if (rec != null && info.DevTicks > 0)
        {
            var m = Medals.For(rec.PbTicks, info.DevTicks);
            sub += $"   ·   {(m == Medal.Dev ? devName : Pal.MedalName(m))}";
        }
        Text(sub, new Vector2(0, 72), Pal.Mono, 17, Pal.TextDim, HorizontalAlignment.Center, W);

        // Right: full-game run clock, or the ghost + medal targets
        if (Game.RunMode)
        {
            Text("RUN " + Fmt(Game.RunTicks), new Vector2(W - 520, 50), Pal.Mono, 34, Pal.Text, HorizontalAlignment.Right, 492);
            DrawSplits();
        }
        else if (info.DevTicks > 0)
        {
            Text($"GOLD {Fmt(Medals.GoldTicks(info.DevTicks))}   {(info.Custom ? "AUTHOR" : "DEV")} {Fmt(info.DevTicks)}", new Vector2(W - 620, 46), Pal.Mono, 17, Pal.TextDim, HorizontalAlignment.Right, 592);
        }

        // Bottom
        if (!s.Started && !s.Finished && !Game.Trailer)
        {
            float a = 0.55f + 0.45f * Mathf.Sin(_time * 5);
            Text("MOVE OR SHOOT TO START", new Vector2(0, 980), Pal.Bold, 26, Pal.Text with { A = a }, HorizontalAlignment.Center, W);
            if (info.Level.Hint.Length > 0) Text(info.Level.Hint, new Vector2(0, 1024), Pal.Regular, 22, Pal.TextDim, HorizontalAlignment.Center, W);
        }
        string ghost = s.GhostLabel.Length > 0 ? $"ghost {s.GhostLabel}" : "ghost off";
        if (!Game.Trailer) Text($"R restart   ·   Esc pause / settings   ·   G {ghost}", new Vector2(W - 760, 1062), Pal.Regular, 18, Pal.TextDim with { A = 0.8f }, HorizontalAlignment.Right, 740);
        if (Game.Autoplay && !Game.Trailer) Text("DEV ROUTE PLAYBACK", new Vector2(24, 1062), Pal.Bold, 18, Pal.Exit);

        // Level name card for the first moments of a level.
        float ia = _time - _introAt;
        if (ia < 1.6f)
        {
            float a = ia < 1.1f ? 1 : 1 - (ia - 1.1f) / 0.5f;
            float slide = Math.Min(1, ia / 0.18f);
            var x = 60 - (1 - slide) * 80;
            DrawRect(new Rect2(0, 430, 700 * slide, 150), new Color(0, 0, 0, 0.45f * a));
            DrawRect(new Rect2(0, 430, 8, 150), Pal.Cyan with { A = a });
            Text($"LEVEL {info.Number}", new Vector2(x, 480), Pal.Mono, 24, Pal.TextDim with { A = a });
            Text(info.Level.Name.ToUpperInvariant(), new Vector2(x, 548), Pal.Black, 58, Pal.Text with { A = a });
        }

        // Split popup (full-game run).
        float sa = _time - _splitAt;
        if (sa < 1.6f)
        {
            float a = sa < 1.1f ? 1 : 1 - (sa - 1.1f) / 0.5f;
            string txt = _splitDelta is { } d ? Delta(d) : "split";
            Color c = _splitGold ? Pal.MedalColor(Medal.Gold) : _splitDelta is { } d2 && d2 <= 0 ? Pal.Ahead : Pal.Behind;
            Text(txt + (_splitGold ? "  GOLD" : ""), new Vector2(0, 118), Pal.Mono, 30, c with { A = a }, HorizontalAlignment.Center, W);
        }

        float fa = _time - _flashAt;
        if (fa < 0.18f) DrawRect(new Rect2(0, 0, 1920, 1080), _flash with { A = _flash.A * (1 - fa / 0.18f) });

        if (!Game.Paused && !Game.Trailer && Game.Result is { } r) DrawResult(r);
    }

    void DrawSplits()
    {
        var pb = Store.Data.Run;
        float x = 1920 - 430, y = 104;
        DrawRect(new Rect2(x - 16, y - 26, 420, 64 + 30 * Catalog.Levels.Count), new Color(0, 0, 0, 0.35f));
        int sob = pb.SumOfBest(Catalog.Levels.Count);
        Text("sum of best", new Vector2(x, y + Catalog.Levels.Count * 30 + 6), Pal.Regular, 17, Pal.TextDim);
        Text(sob > 0 ? Fmt(sob) : "—", new Vector2(x + 180, y + Catalog.Levels.Count * 30 + 6), Pal.Mono, 17, Pal.TextDim, HorizontalAlignment.Right, 200);
        for (int i = 0; i < Catalog.Levels.Count; i++)
        {
            var li = Catalog.Levels[i];
            bool done = i < Game.RunSplits.Count, current = i == Game.RunLevel && !done;
            Color c = current ? Pal.Text : done ? Pal.Text with { A = 0.85f } : Pal.TextDim;
            Text($"{li.Number} {li.Level.Name}", new Vector2(x, y + i * 30), Pal.Bold, 19, c);
            if (done)
            {
                int split = Game.RunSplits[i];
                bool gold = i < Game.RunGolds.Count && Game.RunGolds[i];
                Text(Fmt(split), new Vector2(x + 180, y + i * 30), Pal.Mono, 19, gold ? Pal.MedalColor(Medal.Gold) : c, HorizontalAlignment.Right, 200);
                if (i < pb.Splits.Count)
                {
                    int d = split - pb.Splits[i];
                    Text(Delta(d), new Vector2(x + 90, y + i * 30), Pal.Mono, 17, d <= 0 ? Pal.Ahead : Pal.Behind, HorizontalAlignment.Right, 80);
                }
            }
            else if (i < pb.Splits.Count)
                Text(Fmt(pb.Splits[i]), new Vector2(x + 180, y + i * 30), Pal.Mono, 19, Pal.TextDim, HorizontalAlignment.Right, 200);
        }
    }

    void Card(float h)
    {
        var r = new Rect2(1920 / 2f - 380, 1080 / 2f - h / 2, 760, h);
        DrawRect(new Rect2(0, 0, 1920, 1080), new Color(0, 0, 0, 0.45f));
        DrawRect(r, new Color("12151d"));
        DrawRect(r, Pal.Cyan with { A = 0.9f }, false, 3);
        DrawRect(new Rect2(r.Position.X, r.Position.Y, r.Size.X / 2, 5), Pal.Cyan);
        DrawRect(new Rect2(r.Position.X + r.Size.X / 2, r.Position.Y, r.Size.X / 2, 5), Pal.Magenta);
    }

    void DrawResult(Main.ResultInfo r)
    {
        Card(420);
        float y = 1080 / 2f - 210;
        Text(r.Title, new Vector2(0, y + 76), Pal.Black, 40, Pal.Text, HorizontalAlignment.Center, 1920);
        Text(Fmt(r.Ticks), new Vector2(0, y + 170), Pal.Mono, 76, r.NewPb ? Pal.Exit : Pal.Text, HorizontalAlignment.Center, 1920);
        string line = r.PrevPb > 0 ? $"previous best {Fmt(r.PrevPb)}   {Delta(r.Ticks - r.PrevPb)}" : "first finish";
        Text(line, new Vector2(0, y + 214), Pal.Mono, 20, r.PrevPb == 0 || r.Ticks < r.PrevPb ? Pal.Ahead : Pal.Behind, HorizontalAlignment.Center, 1920);
        if (r.NewPb) Text("NEW PERSONAL BEST", new Vector2(0, y + 262), Pal.Black, 26, Pal.Exit with { A = 0.7f + 0.3f * Mathf.Sin(_time * 8) }, HorizontalAlignment.Center, 1920);
        if (r.Medal != Medal.None)
        {
            string next = r.NextTarget > 0 ? $"   ·   next medal at {Fmt(r.NextTarget)}" : "   ·   every medal earned";
            string mname = r.Medal == Medal.Dev && Game.IsCustom ? "AUTHOR" : Pal.MedalName(r.Medal);
            Text($"{mname} MEDAL{next}", new Vector2(0, y + 306), Pal.Bold, 22, Pal.MedalColor(r.Medal), HorizontalAlignment.Center, 1920);
        }
        Text(r.Keys, new Vector2(0, y + 380), Pal.Bold, 21, Pal.TextDim, HorizontalAlignment.Center, 1920);
    }
}

/// <summary>Achievement toasts, on their own top layer so they show on every screen.</summary>
public partial class Toasts : Control
{
    readonly Queue<Achievements.Def> _queue = new();
    Achievements.Def? _ach;
    float _time, _at = -10;

    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Size = new Vector2(1920, 1080); }
    public void Achievement(Achievements.Def d) => _queue.Enqueue(d);
    public override void _Process(double delta) { _time += (float)delta; QueueRedraw(); }

    void Text(string s, Vector2 pos, Font f, int size, Color c)
    {
        DrawStringOutline(f, pos, s, HorizontalAlignment.Left, -1, size, 6, new Color(0.03f, 0.04f, 0.06f, 0.85f));
        DrawString(f, pos, s, HorizontalAlignment.Left, -1, size, c);
    }

    public override void _Draw()
    {
        if (_ach == null || _time - _at > 4.2f)
        {
            if (_queue.Count == 0) { _ach = null; return; }
            _ach = _queue.Dequeue(); _at = _time;
            Sfx.I?.Play("pb", 1.3f, -4);
        }
        float t = _time - _at;
        float slide = Math.Min(1, t / 0.25f) * Math.Min(1, (4.2f - t) / 0.25f);
        var r = new Rect2(1920 - 560 * slide, 960, 540, 96);
        DrawRect(r, new Color("12151d"));
        DrawRect(new Rect2(r.Position, new Vector2(6, r.Size.Y)), Pal.MedalColor(Medal.Gold));
        Text("ACHIEVEMENT", r.Position + new Vector2(24, 30), Pal.Bold, 17, Pal.MedalColor(Medal.Gold));
        Text(_ach.Name, r.Position + new Vector2(24, 60), Pal.Black, 26, Pal.Text);
        Text(_ach.Description, r.Position + new Vector2(24, 86), Pal.Regular, 16, Pal.TextDim);
    }
}
