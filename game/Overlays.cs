using Godot;

namespace Warpline;

/// <summary>Pause menu: real buttons (mouse or keyboard). Esc also resumes.</summary>
public partial class PauseMenu : Control
{
    public Main Game = null!;
    VBoxContainer _list = null!;

    public override void _Ready()
    {
        Size = new Vector2(1920, 1080);   // parent is a CanvasLayer: size explicitly, anchors alone leave it 0×0 while hidden
        var card = Ui.Card(this, 620);
        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        card.AddChild(_list);
    }

    public void Open()
    {
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        var title = Ui.Label("PAUSED", 54, Pal.Black, Pal.Text);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _list.AddChild(title);
        if (Game.RunMode)
        {
            var note = Ui.Label("the run clock is stopped while paused", 19, Pal.Regular, Pal.TextDim);
            note.HorizontalAlignment = HorizontalAlignment.Center;
            _list.AddChild(note);
        }
        _list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        Add("Resume", () => Game.Resume(), true);
        Add("Restart level", () => Game.RestartFromPause());
        if (Game.RunMode) Add("Restart run", () => Game.StartLevel(0, true));
        Add("Settings", () => Game.OpenSettings());
        Add(Game.IsCustom ? "Back" : "Quit to menu", () => Game.QuitPlay());
        Visible = true;
    }

    void Add(string text, Action onPress, bool focus = false)
    {
        var b = Ui.Button(text, 540);
        b.Pressed += onPress;
        _list.AddChild(b);
        if (focus) b.CallDeferred(Control.MethodName.GrabFocus);
    }
}

/// <summary>Settings: volumes, fullscreen and key rebinding. Rebinding captures the next key or mouse button.</summary>
public partial class SettingsPanel : Control
{
    public Main Game = null!;
    VBoxContainer _list = null!;
    string? _capturing;
    Button? _captureButton;
    Action? _onClose;

    public override void _Ready()
    {
        Size = new Vector2(1920, 1080);
        var card = Ui.Card(this, 900);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(820, 900), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        card.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
    }

    public void Open(Action onClose)
    {
        _onClose = onClose;
        _capturing = null;
        Rebuild();
        Visible = true;
    }

    void Rebuild()
    {
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        var s = Store.Data.Settings;
        var title = Ui.Label("SETTINGS", 44, Pal.Black, Pal.Text);
        _list.AddChild(title);

        Slider("Master volume", s.MasterVolume, v => { s.MasterVolume = v; Sfx.ApplyVolumes(); });
        Slider("Effects volume", s.SfxVolume, v => { s.SfxVolume = v; Sfx.ApplyVolumes(); Sfx.I?.Play("tick"); });
        Slider("Music volume", s.MusicVolume, v => { s.MusicVolume = v; Music.ApplyVolume(); });

        var fs = Ui.Button("Fullscreen", 820, 24, Pal.Bold);
        Ui.RightLabel(fs, s.Fullscreen ? "ON" : "OFF", s.Fullscreen ? Pal.Exit : Pal.TextDim);
        fs.Pressed += () => { Game.SetFullscreen(!s.Fullscreen); Rebuild(); };
        _list.AddChild(fs);
        var rm = Ui.Button("Reduce motion  (no parallax, trails or flashes)", 820, 24, Pal.Bold);
        Ui.RightLabel(rm, s.ReduceMotion ? "ON" : "OFF", s.ReduceMotion ? Pal.Exit : Pal.TextDim);
        rm.Pressed += () => { s.ReduceMotion = !s.ReduceMotion; Store.Save(); Rebuild(); };
        _list.AddChild(rm);

        _list.AddChild(Ui.Label("CONTROLS  ·  click a row, then press the new key or mouse button (Esc cancels)", 19, Pal.Bold, Pal.TextDim));
        foreach (var (action, label) in Controls.Rebindable)
        {
            var b = Ui.Button(label, 820, 22, Pal.Bold);
            b.CustomMinimumSize = new Vector2(800, 46);
            var val = Ui.RightLabel(b, _capturing == action ? "press a key…" : Controls.DescribeAction(action),
                _capturing == action ? Pal.Magenta : Controls.IsDefault(action) ? Pal.TextDim : Pal.Cyan, 20);
            string a = action;
            b.Pressed += () => { _capturing = a; _captureButton = b; Rebuild(); };
            _list.AddChild(b);
        }
        var reset = Ui.Button("Reset controls to defaults", 820, 22, Pal.Bold);
        reset.Pressed += () => { s.Bindings.Clear(); Controls.Apply(s); Store.Save(); Rebuild(); };
        _list.AddChild(reset);

        _list.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        var back = Ui.Button("Back", 820);
        back.Pressed += Close;
        _list.AddChild(back);
        if (_capturing == null) back.CallDeferred(Control.MethodName.GrabFocus);
    }

    void Slider(string label, int value, Action<int> set)
    {
        var row = new HBoxContainer();
        var l = Ui.Label(label, 24, Pal.Bold, Pal.Text);
        l.CustomMinimumSize = new Vector2(300, 48);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        var slider = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = value, CustomMinimumSize = new Vector2(420, 48), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var num = Ui.Label(value.ToString(), 22, Pal.Mono, Pal.TextDim);
        num.CustomMinimumSize = new Vector2(80, 48);
        num.HorizontalAlignment = HorizontalAlignment.Right;
        num.VerticalAlignment = VerticalAlignment.Center;
        slider.ValueChanged += v => { set((int)v); num.Text = ((int)v).ToString(); };
        slider.DragEnded += _ => Store.Save();
        row.AddChild(slider);
        row.AddChild(num);
        _list.AddChild(row);
    }

    void Close()
    {
        Store.Save();
        Visible = false;
        _onClose?.Invoke();
    }

    public override void _Input(InputEvent ev)
    {
        if (!Visible) return;
        if (_capturing == null)
        {
            if (ev.IsActionPressed("ui_cancel")) { GetViewport().SetInputAsHandled(); Close(); }
            return;
        }
        if (ev is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            _capturing = null;
            GetViewport().SetInputAsHandled();
            Rebuild();
            return;
        }
        if (ev is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown }) return;
        var code = Controls.Encode(ev);
        if (code == null || ev.IsEcho()) return;
        GetViewport().SetInputAsHandled();
        var s = Store.Data.Settings;
        // One input per action: take it away from any other action that had it.
        foreach (var (other, _) in Controls.Rebindable)
        {
            if (other == _capturing) continue;
            var codes = Controls.CodesFor(other).ToList();
            if (codes.Remove(code)) s.Bindings[other] = codes;
        }
        s.Bindings[_capturing] = new List<string> { code };
        _capturing = null;
        Controls.Apply(s);
        Store.Save();
        // The press that set the binding must not also click the focused button.
        CallDeferred(MethodName.Rebuild);
    }
}
