using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>
/// The in-game level editor. Paint tiles, place machines, test (which is also the clear check that sets the
/// author time), save to the library and copy a share code. Rendering reuses WorldView on a live preview level.
/// </summary>
public partial class Editor : Node2D
{
    public Main Game = null!;
    public WorldView View = null!;
    public Camera2D Cam = null!;
    public EditLevel Lvl = new();
    public string Slug = "";
    public string Message = "";
    float _messageAt = -10, _time;

    string _tool = "panel";
    int _ch = 1;
    (int dx, int dy) _dir = (1, 0);
    Vector2I _hover;
    Vector2I? _dragStart;
    bool _painting, _erasing, _panning;
    Vector2 _panMouse, _panCam;
    EditObj? _selected;
    readonly List<string> _undo = new();
    float _zoom = 1;

    CanvasLayer _ui = null!;
    Control _root = null!;
    VBoxContainer _props = null!;
    Label _status = null!;
    LineEdit _name = null!, _hint = null!;
    SpinBox _w = null!, _h = null!;
    readonly Dictionary<string, Button> _toolButtons = new();
    readonly List<Button> _chButtons = new();
    bool _building;

    static readonly (string id, string label, char ch)[] TileTools =
    {
        ("erase", "Erase", '.'), ("panel", "Panel  (portals)", '#'), ("metal", "Metal  (no portals)", 'X'), ("spike", "Spikes", '^'),
        ("grill", "Fizzler grill", ':'), ("exit", "Exit", 'E'), ("speed", "Speed gel", '='), ("bounce", "Bounce gel", '%'), ("spawn", "Spawn", 'S'),
    };
    static readonly (string id, string label)[] ObjTools =
    {
        ("door", "Door  (drag)"), ("button", "Button"), ("laser", "Laser"), ("receiver", "Receiver"), ("turret", "Turret"), ("mover", "Moving panel  (drag)"), ("select", "Select / edit"),
    };

    public override void _Ready()
    {
        Visible = false;
        _ui = new CanvasLayer { Layer = 15, Visible = false };
        AddChild(_ui);
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
        _ui.AddChild(_root);
        BuildUi();
    }

    // ---------------------------------------------------------------- UI

    void BuildUi()
    {
        var top = new PanelContainer { Position = new Vector2(0, 0), Size = new Vector2(1920, 64), ClipContents = true };
        top.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.07f, 0.92f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 });
        _root.AddChild(top);
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);
        top.AddChild(bar);
        bar.AddChild(TopLabel("LEVEL EDITOR", Pal.Cyan));
        _name = new LineEdit { PlaceholderText = "level name", CustomMinimumSize = new Vector2(280, 44) };
        _name.TextChanged += t => { Lvl.Name = t; };
        bar.AddChild(_name);
        _hint = new LineEdit { PlaceholderText = "one-line hint (optional)", CustomMinimumSize = new Vector2(420, 44) };
        _hint.TextChanged += t => { Lvl.Hint = t; };
        bar.AddChild(_hint);
        bar.AddChild(TopLabel("W", Pal.TextDim));
        _w = new SpinBox { MinValue = 24, MaxValue = 400, Step = 1, CustomMinimumSize = new Vector2(96, 44) };
        _w.ValueChanged += v => { if (_building) return; Change(() => Lvl.Resize((int)v, Lvl.H)); };
        bar.AddChild(_w);
        bar.AddChild(TopLabel("H", Pal.TextDim));
        _h = new SpinBox { MinValue = 16, MaxValue = 80, Step = 1, CustomMinimumSize = new Vector2(96, 44) };
        _h.ValueChanged += v => { if (_building) return; Change(() => Lvl.Resize(Lvl.W, (int)v)); };
        bar.AddChild(_h);
        bar.AddChild(TopButton("▶ Test  F5", Test));
        bar.AddChild(TopButton("Save  Ctrl+S", Save));
        bar.AddChild(TopButton("Copy code", CopyCode));
        bar.AddChild(TopButton("Back", () => { Save(); Game.OpenLibrary(); }));

        var left = new PanelContainer { Position = new Vector2(0, 64), Size = new Vector2(250, 986) };
        left.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.07f, 0.88f), ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 10 });
        _root.AddChild(left);
        var tools = new VBoxContainer();
        tools.AddThemeConstantOverride("separation", 4);
        left.AddChild(tools);
        tools.AddChild(Small("TILES  ·  LMB paint · Shift+drag fill · RMB erase", Pal.TextDim, 14));
        foreach (var (id, label, _) in TileTools) tools.AddChild(ToolButton(id, label));
        tools.AddChild(Small("MACHINES  ·  RMB deletes", Pal.TextDim, 14));
        foreach (var (id, label) in ObjTools) tools.AddChild(ToolButton(id, label));
        tools.AddChild(Small("CHANNEL  (buttons & receivers open doors on theirs)", Pal.TextDim, 14));
        var chRow = new HBoxContainer();
        for (int c = 1; c <= 4; c++)
        {
            int cc = c;
            var b = new Button { Text = c.ToString(), CustomMinimumSize = new Vector2(52, 40), ToggleMode = true };
            b.AddThemeColorOverride("font_color", WorldView.ChannelColor(c));
            b.Pressed += () => { _ch = cc; if (_selected != null && _selected.Kind is "door" or "button" or "receiver") Change(() => _selected.Ch = cc); SyncButtons(); };
            chRow.AddChild(b);
            _chButtons.Add(b);
        }
        tools.AddChild(chRow);
        tools.AddChild(Small("R rotates lasers & turrets · Ctrl+Z undo\nwheel zoom · middle-drag pan · Del deletes", Pal.TextDim, 14));

        var right = new PanelContainer { Position = new Vector2(1640, 64), Size = new Vector2(280, 986) };
        right.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.04f, 0.05f, 0.07f, 0.88f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 10 });
        _root.AddChild(right);
        _props = new VBoxContainer();
        _props.AddThemeConstantOverride("separation", 6);
        right.AddChild(_props);

        _status = new Label { Position = new Vector2(262, 1048), Size = new Vector2(1370, 30) };
        _status.AddThemeFontOverride("font", Pal.Mono);
        _status.AddThemeFontSizeOverride("font_size", 18);
        _root.AddChild(_status);
    }

    static Label TopLabel(string text, Color c)
    {
        var l = Ui.Label(text, 20, Pal.Bold, c);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        return l;
    }

    static Label Small(string text, Color c, int size = 20)
    {
        var l = Ui.Label(text, size, Pal.Bold, c);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(0, 28);
        return l;
    }

    static Button TopButton(string text, Action a)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 44) };
        b.AddThemeFontOverride("font", Pal.Bold);
        b.AddThemeFontSizeOverride("font_size", 18);
        b.Pressed += a;
        return b;
    }

    Button ToolButton(string id, string label)
    {
        var b = new Button { Text = label, ToggleMode = true, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(228, 34) };
        b.AddThemeFontOverride("font", Pal.Bold);
        b.AddThemeFontSizeOverride("font_size", 17);
        b.Pressed += () => { _tool = id; if (id != "select") _selected = null; SyncButtons(); RebuildProps(); };
        _toolButtons[id] = b;
        return b;
    }

    void SyncButtons()
    {
        foreach (var (id, b) in _toolButtons) b.SetPressedNoSignal(id == _tool);
        for (int i = 0; i < _chButtons.Count; i++) _chButtons[i].SetPressedNoSignal(i + 1 == _ch);
    }

    // ---------------------------------------------------------------- open / close / preview

    public void Open(EditLevel lvl, string slug)
    {
        Lvl = lvl; Slug = slug;
        _undo.Clear();
        _selected = null;
        _building = true;
        _name.Text = Lvl.Name; _hint.Text = Lvl.Hint;
        _w.Value = Lvl.W; _h.Value = Lvl.H;
        _building = false;
        Visible = true; _ui.Visible = true;
        View.Visible = true; View.ShowAim = false; View.ShowCrosshair = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        RebuildPreview();
        SyncButtons();
        RebuildProps();
        var sp = FindSpawn();
        Cam.Position = new Vector2((sp.X + 10) * WorldView.T, Math.Min(Lvl.H * WorldView.T / 2, (sp.Y) * WorldView.T));
        SetZoom(0.8f);
    }

    public void Close()
    {
        Visible = false; _ui.Visible = false;
        View.ShowAim = true; View.ShowCrosshair = true;
        SetZoom(1);
    }

    Vector2I FindSpawn()
    {
        for (int y = 0; y < Lvl.H; y++) for (int x = 0; x < Lvl.W; x++) if (Lvl.Grid[x, y] == 'S') return new Vector2I(x, y);
        return new Vector2I(4, Lvl.H / 2);
    }

    /// <summary>Build a Level straight from the grid (no validation), so even a half-made level draws.</summary>
    Level PreviewLevel()
    {
        var cells = new Tile[Lvl.W * Lvl.H];
        V2 spawn = new(4.5, 4);
        for (int y = 0; y < Lvl.H; y++)
            for (int x = 0; x < Lvl.W; x++)
            {
                char c = Lvl.Grid[x, y];
                if (c == 'S') spawn = new V2(x + 0.5, y + 1 - Tuning.HalfH);
                cells[y * Lvl.W + x] = Tiles.FromChar(c);
            }
        Machines m;
        try { m = Machines.Parse(Lvl.ToText().Split("\n---\n").Skip(2).FirstOrDefault()?.Split('\n') ?? Array.Empty<string>(), "edit"); }
        catch (FormatException) { m = new Machines(); }
        return new Level("edit", Lvl.Name, Lvl.Hint, Lvl.W, Lvl.H, cells, spawn, m);
    }

    void RebuildPreview()
    {
        var level = PreviewLevel();
        var info = new LevelInfo { Index = -1, Id = "edit", Level = level, DevRoute = new Route(), DevTicks = 0, Custom = true };
        View.Session = new Session(info, "off");
        View.Tiles.SetLevel(level);
        QueueRedraw();
    }

    void Change(Action a, bool rebuildProps = true)
    {
        if (_undo.Count == 0 || _undo[^1] != Lvl.ToText()) _undo.Add(Lvl.ToText());
        if (_undo.Count > 80) _undo.RemoveAt(0);
        a();
        Lvl.AuthorTicks = 0; Lvl.AuthorRoute = "";   // any change needs a new clear check
        RebuildPreview();
        if (rebuildProps) RebuildProps();
    }

    void Undo()
    {
        if (_undo.Count == 0) return;
        var text = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        var restored = EditLevel.FromText(text);
        Lvl.Grid = restored.Grid; Lvl.W = restored.W; Lvl.H = restored.H; Lvl.Objects = restored.Objects;
        Lvl.AuthorTicks = 0; Lvl.AuthorRoute = "";
        _selected = null;
        _building = true; _w.Value = Lvl.W; _h.Value = Lvl.H; _building = false;
        RebuildPreview(); RebuildProps();
    }

    public void Toast(string msg) { Message = msg; _messageAt = _time; }

    // ---------------------------------------------------------------- actions

    public void Save()
    {
        if (Slug.Length == 0) Slug = CustomLevels.NewSlug(Lvl.Name);
        CustomLevels.Save(Slug, Lvl);
        Toast($"saved \"{Lvl.Name}\"");
    }

    void CopyCode()
    {
        var p = Lvl.Problem();
        if (p.Length > 0) { Toast("can't share yet: " + p); return; }
        if (Lvl.AuthorTicks <= 0) { Toast("finish it once in Test first (the clear check sets the author time)"); return; }
        Save();
        var code = CustomLevels.Code(Lvl);
        DisplayServer.ClipboardSet(code);
        Toast($"share code copied ({code.Length} characters)");
        Achievements.Unlock("SHARED");
    }

    void Test()
    {
        var p = Lvl.Problem();
        if (p.Length > 0) { Toast("can't test: " + p); return; }
        Save();
        Game.PlayCustom(Lvl, Slug, fromEditor: true);
    }

    // ---------------------------------------------------------------- input

    Vector2I CellAt(Vector2 worldPx) => new((int)Math.Floor(worldPx.X / WorldView.T), (int)Math.Floor(worldPx.Y / WorldView.T));

    EditObj? ObjectAt(Vector2I c) => Lvl.Objects.LastOrDefault(o => o.Covers(c.X, c.Y) ||
        (o.Kind == "mover" && o.Orbit <= 0 && c.X >= o.X + o.Dx && c.X < o.X + o.Dx + o.W && c.Y >= o.Y + o.Dy && c.Y < o.Y + o.Dy + o.H));

    public override void _UnhandledInput(InputEvent ev)
    {
        if (!Visible) return;
        if (ev is InputEventKey { Pressed: true, Echo: false } k)
        {
            if (k.Keycode == Key.F5) { Test(); GetViewport().SetInputAsHandled(); return; }
            if (k.CtrlPressed && k.Keycode == Key.S) { Save(); return; }
            if (k.CtrlPressed && k.Keycode == Key.Z) { Undo(); return; }
            if (k.Keycode == Key.Delete && _selected != null) { var s = _selected; _selected = null; Change(() => Lvl.Objects.Remove(s)); return; }
            if (k.Keycode == Key.R)
            {
                _dir = _dir switch { (1, 0) => (0, 1), (0, 1) => (-1, 0), (-1, 0) => (0, -1), _ => (1, 0) };
                if (_selected is { Kind: "laser" or "turret" } so) Change(() => { so.Dx = _dir.dx; so.Dy = _dir.dy; });
                Toast($"direction: {(_dir.dx > 0 ? "right" : _dir.dx < 0 ? "left" : _dir.dy > 0 ? "down" : "up")}");
                return;
            }
            if (k.Keycode == Key.Escape) { Save(); Game.OpenLibrary(); return; }
        }
        if (ev is InputEventMouseButton mb)
        {
            var world = View.GetGlobalMousePosition();
            var cell = CellAt(world);
            if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed) { ZoomAt(1.12f, world); return; }
            if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed) { ZoomAt(1 / 1.12f, world); return; }
            if (mb.ButtonIndex == MouseButton.Middle) { _panning = mb.Pressed; _panMouse = mb.Position; _panCam = Cam.Position; return; }
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed) LeftDown(cell, mb.ShiftPressed);
                else LeftUp(cell);
                return;
            }
            if (mb.ButtonIndex == MouseButton.Right)
            {
                if (mb.Pressed)
                {
                    if (ObjectAt(cell) is { } o) { if (_selected == o) _selected = null; Change(() => Lvl.Objects.Remove(o)); }
                    else { _erasing = true; Change(() => Lvl.Paint(cell.X, cell.Y, '.')); }
                }
                else _erasing = false;
                return;
            }
        }
        if (ev is InputEventMouseMotion mm)
        {
            if (_panning) { Cam.Position = _panCam - (mm.Position - _panMouse) / _zoom; return; }
            var cell = CellAt(View.GetGlobalMousePosition());
            if (cell != _hover)
            {
                _hover = cell;
                if (_painting && TileChar(_tool) is { } ch) PaintLive(cell, ch);
                if (_erasing) PaintLive(cell, '.');
                QueueRedraw();
            }
        }
    }

    void PaintLive(Vector2I cell, char ch)
    {
        if (Lvl[cell.X, cell.Y] == ch) return;
        Lvl.Paint(cell.X, cell.Y, ch);
        Lvl.AuthorTicks = 0; Lvl.AuthorRoute = "";
        RebuildPreview();
    }

    static char? TileChar(string tool) => TileTools.FirstOrDefault(t => t.id == tool).id != null ? TileTools.First(t => t.id == tool).ch : null;

    /// <summary>Self-test hook: use a tool on a cell (drag = from..to) as if clicked.</summary>
    public void ApplyTool(string tool, Vector2I from, Vector2I to, bool shift = false)
    {
        _tool = tool;
        LeftDown(from, shift);
        _hover = to;
        LeftUp(to);
    }

    void LeftDown(Vector2I cell, bool shift)
    {
        if (TileChar(_tool) is { } ch)
        {
            if (shift && ch != 'S') { _dragStart = cell; return; }
            _painting = ch != 'S';
            Change(() => Lvl.Paint(cell.X, cell.Y, ch));
            return;
        }
        switch (_tool)
        {
            case "door":
            case "mover":
                _dragStart = cell;
                break;
            case "select":
                _selected = ObjectAt(cell);
                RebuildProps();
                break;
            default:
                if (cell.X < 0 || cell.Y < 0 || cell.X >= Lvl.W || cell.Y >= Lvl.H) return;
                var o = new EditObj { Kind = _tool, X = cell.X, Y = cell.Y, Ch = _ch, Dx = _dir.dx, Dy = _dir.dy };
                if (_tool == "laser") o.Period = 0;
                if (_tool == "turret") o.Period = 1.2;
                Change(() =>
                {
                    Lvl.Objects.RemoveAll(x => x.Kind is "button" or "laser" or "receiver" or "turret" && x.X == cell.X && x.Y == cell.Y);
                    if (Lvl[cell.X, cell.Y] is not '.' and not 'S') Lvl.Paint(cell.X, cell.Y, '.');
                    Lvl.Objects.Add(o);
                });
                _selected = o; RebuildProps();
                break;
        }
    }

    void LeftUp(Vector2I cell)
    {
        _painting = false;
        if (_dragStart is not { } a) return;
        _dragStart = null;
        int x0 = Math.Min(a.X, cell.X), x1 = Math.Max(a.X, cell.X), y0 = Math.Min(a.Y, cell.Y), y1 = Math.Max(a.Y, cell.Y);
        x0 = Math.Max(0, x0); y0 = Math.Max(0, y0); x1 = Math.Min(Lvl.W - 1, x1); y1 = Math.Min(Lvl.H - 1, y1);
        if (x1 < x0 || y1 < y0) return;
        if (TileChar(_tool) is { } ch)
        {
            Change(() => { for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Lvl.Paint(x, y, ch); });
            return;
        }
        var o = new EditObj { Kind = _tool, X = x0, Y = y0, W = x1 - x0 + 1, H = y1 - y0 + 1, Ch = _ch };
        if (_tool == "mover") { o.Dx = 8; o.Dy = 0; o.Period = 4; if (o.W * o.H == 1) { o.W = 3; } }
        Change(() =>
        {
            if (_tool == "door") for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) Lvl.Paint(x, y, '.');
            if (_tool == "mover") for (int y = o.Y; y < o.Y + o.H; y++) for (int x = o.X; x < o.X + o.W; x++) Lvl.Paint(x, y, '.');
            Lvl.Objects.Add(o);
        });
        _selected = o;
        _tool = "select"; SyncButtons();
        RebuildProps();
    }

    void ZoomAt(float factor, Vector2 worldAt)
    {
        float z = Math.Clamp(_zoom * factor, 0.25f, 2.5f);
        var before = worldAt;
        SetZoom(z);
        Cam.Position += before - View.GetGlobalMousePosition();
    }

    void SetZoom(float z) { _zoom = z; Cam.Zoom = new Vector2(z, z); }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (!Visible) return;
        // Keyboard panning (only when no text field has focus).
        if (GetViewport().GuiGetFocusOwner() is not LineEdit)
        {
            var pan = new Vector2(
                (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) ? 1 : 0) - (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) ? 1 : 0),
                (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) ? 1 : 0) - (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) ? 1 : 0));
            if (pan != Vector2.Zero && !Input.IsKeyPressed(Key.Ctrl)) Cam.Position += pan * 900 * (float)delta / _zoom;
        }
        var p = Lvl.Problem();
        string author = Lvl.AuthorTicks > 0 ? $"author {Runner.FormatTime(Lvl.AuthorTicks)} ✓ ready to share" : "not cleared yet: finish it in Test to set the author time";
        string msg = _time - _messageAt < 3 ? "   ·   " + Message : "";
        _status.Text = $"{_hover.X},{_hover.Y}   {Lvl.W}×{Lvl.H}   ·   {(p.Length > 0 ? "⚠ " + p : author)}{msg}";
        _status.AddThemeColorOverride("font_color", p.Length > 0 ? Pal.Behind : Pal.TextDim);
        QueueRedraw();
    }

    // ---------------------------------------------------------------- overlay

    public override void _Draw()
    {
        if (!Visible) return;
        float T = WorldView.T;
        var bounds = new Rect2(0, 0, Lvl.W * T, Lvl.H * T);
        DrawRect(bounds, Pal.Cyan with { A = 0.6f }, false, 3);
        // light grid near the cursor only (full grids get noisy when zoomed out)
        for (int y = _hover.Y - 12; y <= _hover.Y + 12; y++)
            for (int x = _hover.X - 18; x <= _hover.X + 18; x++)
            {
                if (x < 0 || y < 0 || x >= Lvl.W || y >= Lvl.H) continue;
                float fade = 0.10f * (1 - Math.Min(1, new Vector2(x - _hover.X, y - _hover.Y).Length() / 16f));
                DrawRect(new Rect2(x * T, y * T, T, T), Colors.White with { A = fade }, false, 1);
            }
        // hovered cell or drag rectangle
        if (_dragStart is { } a)
        {
            int x0 = Math.Min(a.X, _hover.X), x1 = Math.Max(a.X, _hover.X), y0 = Math.Min(a.Y, _hover.Y), y1 = Math.Max(a.Y, _hover.Y);
            DrawRect(new Rect2(x0 * T, y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T), Pal.Magenta with { A = 0.25f });
            DrawRect(new Rect2(x0 * T, y0 * T, (x1 - x0 + 1) * T, (y1 - y0 + 1) * T), Pal.Magenta, false, 2);
        }
        else DrawRect(new Rect2(_hover.X * T, _hover.Y * T, T, T), Pal.Cyan, false, 2);
        // machine markers: channel numbers so links are readable
        foreach (var o in Lvl.Objects)
        {
            var c = new Vector2((o.X + o.W / 2f) * T, (o.Y + o.H / 2f) * T);
            if (o.Kind is "door" or "button" or "receiver")
                DrawString(Pal.Mono, c + new Vector2(-6, 7), o.Ch.ToString(), HorizontalAlignment.Left, -1, 18, WorldView.ChannelColor(o.Ch));
            if (o.Kind == "mover" && o.Orbit <= 0)
                DrawRect(new Rect2((o.X + o.Dx) * T, (o.Y + o.Dy) * T, o.W * T, o.H * T), Colors.White with { A = 0.35f }, false, 2);
        }
        if (_selected is { } s)
        {
            var r = new Rect2(s.X * T - 4, s.Y * T - 4, s.W * T + 8, s.H * T + 8);
            DrawRect(r, Pal.Magenta, false, 3);
        }
    }

    // ---------------------------------------------------------------- properties

    void RebuildProps()
    {
        foreach (var c in _props.GetChildren()) { _props.RemoveChild(c); c.QueueFree(); }
        if (_selected is not { } o)
        {
            _props.AddChild(Small("PROPERTIES", Pal.TextDim, 16));
            _props.AddChild(Small("Pick a machine with Select (or place one) to edit it here.\n\nA level needs a spawn and an exit. Test it: finishing is the clear check that sets the author time, and only cleared levels can be shared.", Pal.TextDim, 16));
            return;
        }
        _props.AddChild(Small(o.Kind.ToUpperInvariant(), Pal.Cyan, 22));
        if (o.Kind is "door" or "button" or "receiver")
            _props.AddChild(Small($"channel {o.Ch} (use the channel buttons)", WorldView.ChannelColor(o.Ch), 16));
        switch (o.Kind)
        {
            case "button":
                Choice("mode", new[] { "latch", "timed", "hold" }, o.Mode, v => o.Mode = v);
                if (o.Mode == "timed") Num("open for (s)", o.Time, 0.2, 30, 0.1, v => o.Time = v);
                break;
            case "laser":
                Num("cycle (s, 0 = always on)", o.Period, 0, 20, 0.1, v => o.Period = v);
                if (o.Period > 0) { Num("on for (s)", o.On, 0.05, 20, 0.05, v => o.On = v); Num("offset (s)", o.Phase, 0, 20, 0.05, v => o.Phase = v); }
                _props.AddChild(Small("R: rotate", Pal.TextDim, 15));
                break;
            case "turret":
                Num("fires every (s)", o.Period, 0.1, 20, 0.05, v => o.Period = v);
                Num("offset (s)", o.Phase, 0, 20, 0.05, v => o.Phase = v);
                Num("bullet speed", o.Speed, 4, 60, 1, v => o.Speed = v);
                _props.AddChild(Small("R: rotate", Pal.TextDim, 15));
                break;
            case "mover":
                Num("orbit radius (0 = slide)", o.Orbit, 0, 30, 0.5, v => o.Orbit = v);
                if (o.Orbit <= 0) { Num("travel x", o.Dx, -200, 200, 1, v => o.Dx = (int)v); Num("travel y", o.Dy, -80, 80, 1, v => o.Dy = (int)v); }
                Num("period (s)", o.Period, 0.5, 30, 0.1, v => o.Period = v);
                Num("offset (s)", o.Phase, 0, 30, 0.1, v => o.Phase = v);
                Choice("surface", new[] { "panel", "metal" }, o.Metal ? "metal" : "panel", v => o.Metal = v == "metal");
                break;
        }
        var del = new Button { Text = "Delete  (Del)", CustomMinimumSize = new Vector2(0, 40) };
        del.Pressed += () => { var s = _selected; _selected = null; if (s != null) Change(() => Lvl.Objects.Remove(s)); };
        _props.AddChild(del);
    }

    void Num(string label, double value, double min, double max, double step, Action<double> set)
    {
        _props.AddChild(Small(label, Pal.Text, 16));
        var sb = new SpinBox { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(240, 38) };
        sb.ValueChanged += v => Change(() => set(v), rebuildProps: false);   // keep the box (and its focus) while typing
        _props.AddChild(sb);
    }

    void Choice(string label, string[] options, string current, Action<string> set)
    {
        _props.AddChild(Small(label, Pal.Text, 16));
        var ob = new OptionButton { CustomMinimumSize = new Vector2(240, 38) };
        for (int i = 0; i < options.Length; i++) { ob.AddItem(options[i], i); if (options[i] == current) ob.Selected = i; }
        ob.ItemSelected += i => Change(() => set(options[(int)i]));
        _props.AddChild(ob);
    }
}
