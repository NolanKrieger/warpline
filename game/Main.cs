using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>
/// The game: menu ⇄ play. Samples input once per 120 Hz physics tick, steps the session, plays effects,
/// keeps the full-game run clock and splits, and saves PBs. All rules live in Warpline.Sim.
/// </summary>
public partial class Main : Node2D
{
    public sealed record ResultInfo(string Title, int Ticks, int PrevPb, bool NewPb, Medal Medal, int NextTarget, string Keys, bool IsRun);

    public Session? Session { get; private set; }
    public bool Playing { get; private set; }
    public bool Paused { get; private set; }
    public bool RunMode { get; private set; }
    public int RunTicks { get; private set; }
    public int RunLevel { get; private set; }
    public List<int> RunSplits { get; } = new();
    public List<bool> RunGolds { get; } = new();
    public bool Autoplay { get; private set; }
    public ResultInfo? Result { get; private set; }
    /// <summary>Capture mode for store screenshots and the trailer: no dev labels, no result card.</summary>
    public bool Trailer { get; private set; }
    double _finishedFor;
    /// <summary>Playing a custom level (from the editor's Test or the library).</summary>
    public bool IsCustom => _custom != null;
    EditLevel? _custom;
    string _customSlug = "";
    bool _customFromEditor;
    Editor _editor = null!;
    Library _library = null!;
    CanvasLayer _libraryLayer = null!;
    Credits _credits = null!;
    CanvasLayer _creditsLayer = null!;
    AchievementsScreen _achScreen = null!;
    CanvasLayer _achLayer = null!;
    int _runSetbacks;   // deaths + restarts during a full-game run

    WorldView _view = null!;
    BgLayer _bg = null!;
    Camera2D _cam = null!;
    Hud _hud = null!;
    Menu _menu = null!;
    CanvasLayer _menuLayer = null!, _pauseLayer = null!, _settingsLayer = null!;
    PauseMenu _pause = null!;
    SettingsPanel _settings = null!;
    Sfx _sfx = null!;
    Music _music = null!;
    bool _runStarted, _jumpLatch;
    readonly Queue<(int which, V2 aim)> _shots = new();
    InputFrame[] _autoFrames = Array.Empty<InputFrame>();
    int _autoIndex;
    readonly Dictionary<string, string> _args = new();
    int _frames;
    V2? _forcedAim;
    InputFrame? _injected;   // self-test: a frame to use instead of sampling devices

    public override void _Ready()
    {
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            var kv = a.TrimStart('-').Split('=', 2);
            _args[kv[0]] = kv.Length > 1 ? kv[1] : "";
        }
        if (_args.TryGetValue("save", out var savePath)) Store.Path = savePath;
        Store.Load();
        Controls.Ensure();
        Catalog.Load();

        _bg = new BgLayer();
        AddChild(_bg);
        _view = new WorldView { Visible = false };
        AddChild(_view);
        _cam = new Camera2D();
        AddChild(_cam);
        _cam.MakeCurrent();
        _editor = new Editor { Game = this, View = _view, Cam = _cam };
        AddChild(_editor);

        var hudLayer = new CanvasLayer { Layer = 10 };
        AddChild(hudLayer);
        _hud = new Hud { Game = this };
        hudLayer.AddChild(_hud);
        var toastLayer = new CanvasLayer { Layer = 40 };   // above every menu
        AddChild(toastLayer);
        var toasts = new Toasts();
        toastLayer.AddChild(toasts);
        Achievements.Unlocked += d => toasts.Achievement(d);

        _menuLayer = new CanvasLayer { Layer = 20 };
        AddChild(_menuLayer);
        _menu = new Menu { Game = this };
        _menuLayer.AddChild(_menu);

        _libraryLayer = new CanvasLayer { Layer = 21, Visible = false };
        AddChild(_libraryLayer);
        _library = new Library { Game = this };
        _libraryLayer.AddChild(_library);

        _achLayer = new CanvasLayer { Layer = 22, Visible = false };
        AddChild(_achLayer);
        _achScreen = new AchievementsScreen { Game = this };
        _achLayer.AddChild(_achScreen);

        _creditsLayer = new CanvasLayer { Layer = 22, Visible = false };
        AddChild(_creditsLayer);
        _credits = new Credits { Game = this };
        _creditsLayer.AddChild(_credits);

        _pauseLayer = new CanvasLayer { Layer = 25, Visible = false };
        AddChild(_pauseLayer);
        _pause = new PauseMenu { Game = this };
        _pauseLayer.AddChild(_pause);

        _settingsLayer = new CanvasLayer { Layer = 30, Visible = false };
        AddChild(_settingsLayer);
        _settings = new SettingsPanel { Game = this };
        _settingsLayer.AddChild(_settings);

        _sfx = new Sfx();
        AddChild(_sfx);
        _music = new Music();
        AddChild(_music);
        if (_args.TryGetValue("export-music", out var musicDir))
        {
            foreach (var name in new[] { "menu", "act1", "act2", "act3" }) Music.ExportWav(name, System.IO.Path.Combine(musicDir, name + ".wav"));
            GD.Print("music exported to " + musicDir);
            GetTree().Quit();
            return;
        }

        Trailer = _args.ContainsKey("trailer");
        if (_args.ContainsKey("screenshot") || Trailer)
        {
            // A fixed-size window floats instead of being tiled small, so captures come out at full size.
            DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.ResizeDisabled, true);
            DisplayServer.WindowSetSize(new Vector2I(1920, 1080));
        }
        else if (Store.Data.Settings.Fullscreen) SetFullscreen(true);
        if (_args.TryGetValue("aim", out var aim))
        {
            var xy = aim.Split(',');
            _forcedAim = new V2(double.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture));
        }
        Autoplay = _args.ContainsKey("autoplay");

        if (_args.TryGetValue("capsule", out var capsuleOut)) { CallDeferred(MethodName.RenderCapsules, capsuleOut); return; }
        if (_args.ContainsKey("selftest")) { CallDeferred(MethodName.RunSelfTest); return; }
        if (_args.TryGetValue("level", out var lid))
        {
            int idx = Catalog.Levels.FindIndex(l => l.Id == lid || l.Number == lid);
            StartLevel(Math.Max(0, idx), _args.ContainsKey("run"));
            if (_args.ContainsKey("pause")) Pause();
        }
        else if (_args.ContainsKey("settings")) { ShowMenu(); OpenSettings(); }
        else if (_args.TryGetValue("editor", out var edLevel))
        {
            var e = edLevel.Length > 0 ? EditLevel.FromText(Godot.FileAccess.GetFileAsString($"res://levels/{edLevel}.lvl")) : new EditLevel();
            OpenEditor(e, "");
        }
        else if (_args.ContainsKey("library")) OpenLibrary();
        else if (_args.ContainsKey("achievements")) { ShowMenu(); ShowAchievements(); }
        else ShowMenu();
    }

    /// <summary>Render every Steam capsule size into a folder (offscreen, any size).</summary>
    async void RenderCapsules(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        (string name, int w, int h, bool logo, bool scene, bool clear)[] kinds =
        {
            ("header_capsule", 920, 430, true, true, false), ("small_capsule", 462, 174, true, false, false),
            ("main_capsule", 1232, 706, true, true, false), ("vertical_capsule", 748, 896, true, true, false),
            ("library_capsule", 600, 900, true, true, false), ("library_hero", 3840, 1240, false, true, false),
            ("library_logo", 1280, 720, true, false, true), ("page_background", 1438, 810, false, true, false),
        };
        foreach (var k in kinds)
        {
            var vp = new SubViewport { Size = new Vector2I(k.w, k.h), TransparentBg = k.clear, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            AddChild(vp);
            vp.AddChild(new Capsule { Size = new Vector2(k.w, k.h), Logo = k.logo, Scene = k.scene, TransparentBg = k.clear });
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            vp.GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, k.name + ".png"));
            vp.QueueFree();
        }
        // Achievement icons, achieved and locked (Steam wants both).
        var iconDir = System.IO.Path.Combine(dir, "..", "achievements");
        System.IO.Directory.CreateDirectory(iconDir);
        var glyphs = new Dictionary<string, string>
        {
            ["FIRST_STEPS"] = "01", ["SPEEDY_THING"] = "40", ["TERMINAL"] = "60", ["HALFWAY"] = "10", ["ALL_LEVELS"] = "20", ["DEV_ONE"] = "DEV",
            ["GOLD_ALL"] = "AU", ["DEV_ALL"] = "D20", ["FULL_RUN"] = "RUN", ["DEATHLESS"] = "0", ["SUB_DEV_RUN"] = "<", ["RETURN_TO_SENDER"] = "↺",
            ["ARCHITECT"] = "ED", ["SHARED"] = "WL",
        };
        foreach (var a in Achievements.All)
            foreach (bool locked in new[] { false, true })
            {
                var vp = new SubViewport { Size = new Vector2I(256, 256), TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
                AddChild(vp);
                vp.AddChild(new AchievementIcon { Glyph = glyphs.GetValueOrDefault(a.Id, "?"), Locked = locked });
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                vp.GetTexture().GetImage().SavePng(System.IO.Path.Combine(iconDir, $"{a.Id}{(locked ? "_locked" : "")}.png"));
                vp.QueueFree();
            }
        GD.Print("capsules written to " + dir);
        GetTree().Quit();
    }

    // ---------------------------------------------------------------- flow

    public void ShowMenu()
    {
        Playing = false; Paused = false; Result = null; Session = null; RunMode = false;
        _custom = null;
        _editor.Close();
        _libraryLayer.Visible = false;
        _creditsLayer.Visible = false;
        _achLayer.Visible = false;
        _pauseLayer.Visible = false;
        _view.Visible = false;
        _view.Session = null;
        _bg.Level = null;
        _menuLayer.Visible = true;
        _menu.Rebuild();
        _music.Play("menu");
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void StartLevel(int index, bool run)
    {
        RunMode = run;
        if (run) { RunTicks = 0; RunSplits.Clear(); RunGolds.Clear(); _runStarted = false; _runSetbacks = 0; }
        LoadLevel(index);
    }

    void LoadLevel(int index)
    {
        RunLevel = index;
        _custom = null;
        LoadInfo(Catalog.Levels[index]);
    }

    void LoadInfo(LevelInfo info)
    {
        _editor.Close();
        _libraryLayer.Visible = false;
        _cam.Zoom = Vector2.One;
        Session = new Session(info, Store.Data.Ghost);
        _view.Session = Session;
        _view.Tiles.SetLevel(info.Level);
        _view.ClearEffects();
        _view.Visible = true;
        _bg.Level = info.Level;
        _menuLayer.Visible = false;
        _pauseLayer.Visible = false;
        Playing = true; Paused = false; Result = null;
        _shots.Clear(); _jumpLatch = false;
        _autoFrames = Autoplay ? info.DevRoute.ToArray() : Array.Empty<InputFrame>();
        _autoIndex = 0;
        _hud.Intro();
        if (Trailer) { _view.ShowAim = false; _view.ShowCrosshair = false; }
        _music.Play(Music.ForLevel(info.Index));
        Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    void Restart(bool clearEffects = true)
    {
        if (Session == null) return;
        if (RunMode && Session.Started) _runSetbacks++;
        Session.Reset(Store.Data.Ghost);
        Result = null;
        _shots.Clear();
        _autoIndex = 0;
        if (clearEffects) _view.ClearEffects();
    }

    public void Pause()
    {
        if (!Playing || Result != null) return;
        Paused = true;
        _pauseLayer.Visible = true;
        _pause.Open();
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void Resume()
    {
        Paused = false;
        _pauseLayer.Visible = false;
        _shots.Clear();
        Input.MouseMode = Input.MouseModeEnum.Hidden;
    }

    public void RestartFromPause() { Restart(); Resume(); }

    public void OpenSettings()
    {
        _pauseLayer.Visible = false;
        _settingsLayer.Visible = true;
        _settings.Open(() =>
        {
            _settingsLayer.Visible = false;
            if (Playing && Paused) { _pauseLayer.Visible = true; _pause.Open(); }
            else if (!Playing) _menu.Rebuild();
        });
    }

    public bool SettingsOpen => _settingsLayer.Visible;

    public void ShowAchievements()
    {
        _menuLayer.Visible = false;
        _achLayer.Visible = true;
        _achScreen.Open();
    }

    public void ShowCredits()
    {
        _menuLayer.Visible = false;
        _creditsLayer.Visible = true;
        _credits.Open();
    }

    public void SetFullscreen(bool on)
    {
        Store.Data.Settings.Fullscreen = on;
        DisplayServer.WindowSetMode(on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        Store.Save();
    }

    void CycleGhost()
    {
        Store.Data.Ghost = Store.Data.Ghost switch { "pb" => "dev", "dev" => "off", _ => "pb" };
        Store.Save();
        if (Session is { Started: false }) Session.Reset(Store.Data.Ghost);
        _sfx.Play("tick");
    }

    // ---------------------------------------------------------------- custom levels

    public void OpenLibrary(string message = "")
    {
        Playing = false; Paused = false; Result = null; Session = null; RunMode = false; _custom = null;
        _editor.Close();
        _pauseLayer.Visible = false;
        _view.Visible = false;
        _bg.Level = null;
        _menuLayer.Visible = false;
        _libraryLayer.Visible = true;
        _library.Show(message);
        _music.Play("menu");
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public void OpenEditor(EditLevel lvl, string slug)
    {
        Playing = false; Paused = false; Result = null; RunMode = false; _custom = null;
        _pauseLayer.Visible = false;
        _menuLayer.Visible = false;
        _libraryLayer.Visible = false;
        _bg.Level = null;
        _editor.Open(lvl, slug);
    }

    public void PlayCustom(EditLevel lvl, string slug, bool fromEditor)
    {
        RunMode = false;
        _custom = lvl; _customSlug = slug; _customFromEditor = fromEditor;
        _autoFrames = Array.Empty<InputFrame>();
        LoadInfo(CustomLevels.Info(lvl));
        _custom = lvl;
    }

    /// <summary>Leave play: custom levels go back where they came from.</summary>
    public void QuitPlay()
    {
        if (_custom is { } c)
        {
            if (_customFromEditor) OpenEditor(c, _customSlug);
            else OpenLibrary();
        }
        else ShowMenu();
    }

    // ---------------------------------------------------------------- input

    public override void _UnhandledInput(InputEvent ev)
    {
        if (ev.IsEcho()) return;
        if (ev.IsActionPressed(Controls.Fullscreen)) { SetFullscreen(!Store.Data.Settings.Fullscreen); return; }
        if (!Playing || SettingsOpen) return;
        if (Result != null)
        {
            if (ev.IsActionPressed(Controls.Confirm) || ev.IsActionPressed(Controls.Jump)) Advance();
            else if (ev.IsActionPressed(Controls.Restart)) { if (Result.IsRun) StartLevel(0, true); else Restart(); }
            else if (ev.IsActionPressed(Controls.Pause)) QuitPlay();
            else if (ev.IsActionPressed(Controls.Ghost)) CycleGhost();
            return;
        }
        if (Paused)
        {
            if (ev.IsActionPressed(Controls.Pause)) Resume();
            else if (ev.IsActionPressed(Controls.Restart)) RestartFromPause();
            else if (ev is InputEventKey { PhysicalKeycode: Key.Q, Pressed: true }) QuitPlay();
            return;
        }
        if (ev.IsActionPressed(Controls.Pause)) { Pause(); return; }
        if (ev.IsActionPressed(Controls.Restart)) { Restart(); return; }
        if (ev.IsActionPressed(Controls.Ghost)) { CycleGhost(); return; }
        if (Autoplay) return;
        if (ev.IsActionPressed(Controls.Jump)) _jumpLatch = true;
        int which = ev.IsActionPressed(Controls.PortalA) ? 0 : ev.IsActionPressed(Controls.PortalB) ? 1 : -1;
        if (which >= 0)
        {
            var aimPx = ev is InputEventMouse ? ((InputEventMouse)_view.MakeInputLocal(ev)).Position : _view.AimPx;
            _shots.Enqueue((which, InputFrame.Quantize(new V2(aimPx.X / WorldView.T, aimPx.Y / WorldView.T))));
        }
    }

    InputFrame Sample()
    {
        bool fa = false, fb = false; V2 aim = V2.Zero;
        if (_shots.Count > 0)
        {
            var (which, a) = _shots.Dequeue();   // one shot per tick; a second click waits a tick
            fa = which == 0; fb = which == 1; aim = a;
        }
        var f = new InputFrame(
            Input.IsActionPressed(Controls.Left), Input.IsActionPressed(Controls.Right),
            Input.IsActionPressed(Controls.Jump) || _jumpLatch, Input.IsActionPressed(Controls.Crouch),
            fa, fb, aim);
        _jumpLatch = false;
        return f;
    }

    void Advance()
    {
        if (Result == null) return;
        if (IsCustom) { QuitPlay(); return; }
        if (Result.IsRun) { StartLevel(0, true); return; }
        int next = RunLevel + 1;
        if (next < Catalog.Levels.Count && Store.Unlocked(next)) StartLevel(next, false);
        else ShowMenu();
    }

    // ---------------------------------------------------------------- simulation tick

    public override void _PhysicsProcess(double delta) => TickOnce();

    /// <summary>One 120 Hz step of the game. Public so the self-test can drive it faster than real time.</summary>
    public void TickOnce()
    {
        if (!Playing || Paused || SettingsOpen || Result != null || Session == null) return;
        var f = _injected ?? (Autoplay ? (_autoIndex < _autoFrames.Length ? _autoFrames[_autoIndex++] : InputFrame.None) : Sample());
        long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
        bool stepped = Session.Tick(f);
        double stepMs = (System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        if (_frames > 120) { _benchStepMax = Math.Max(_benchStepMax, stepMs); _benchStepSum += stepMs; _benchSteps++; }
        if (RunMode)
        {
            if (!_runStarted && stepped) _runStarted = true;
            if (_runStarted) RunTicks++;
        }
        if (stepped) Effects(Session.World);
        if (stepped && !Autoplay && Session.World.Vel.Y >= Tuning.Terminal - 1e-9) Achievements.Unlock("TERMINAL");
        if (Session.Dead) { Restart(clearEffects: false); return; }   // keep the death burst on screen
        if (Session.Finished) Finish();
    }

    void Effects(World w)
    {
        foreach (var e in w.Events)
        {
            switch (e.Kind)
            {
                case SimEventKind.Jump: _sfx.Play("jump"); _view.Burst(e.At, Pal.PanelShade, 6, 120, 0.25f, 3); _view.Stretch(); break;
                case SimEventKind.WallJump: _sfx.Play("walljump"); _view.Burst(e.At, Pal.PanelShade, 6, 140, 0.25f, 3); _view.Stretch(); break;
                case SimEventKind.Land: _sfx.Play("land", 1, -4); _view.Burst(e.At, Pal.PanelShade, 5, 90, 0.2f, 3); _view.Squash(); break;
                case SimEventKind.PortalPlaced:
                    _sfx.Play(e.Which == 0 ? "shootA" : "shootB");
                    _view.Burst(e.At, Pal.PortalColor(e.Which), 14, 260);
                    _view.RingAt(e.At, Pal.PortalColor(e.Which));
                    break;
                case SimEventKind.PortalFizzled: _sfx.Play("fizzle"); _view.Burst(e.At, Pal.TextDim, 10, 160, 0.3f, 3); break;
                case SimEventKind.PortalsCleared: _sfx.Play("wipe"); _view.Burst(w.Pos, Pal.Grill, 18, 220); _hud.Flash(Pal.Grill with { A = 0.18f }); break;
                case SimEventKind.BulletPressedButton: if (!Autoplay && _injected == null) Achievements.Unlock("RETURN_TO_SENDER"); break;
                case SimEventKind.Teleport:
                    if (!Autoplay && w.Vel.Length >= 40) Achievements.Unlock("SPEEDY_THING");
                    _sfx.Play("teleport", 1f + (float)Math.Min(0.6, w.Vel.Length / 100));
                    _view.RingAt(e.At, Pal.PortalColor(e.Which), 200, 0.3f);
                    _view.RingAt(e.To, Pal.PortalColor(1 - e.Which), 320, 0.35f);
                    break;
                case SimEventKind.Bounce: _sfx.Play("bounce", 1f + (float)Math.Min(0.5, w.Vel.Length / 120)); _view.Burst(e.At, Pal.BounceGel, 10, 200, 0.3f, 4); _view.Stretch(); break;
                case SimEventKind.ButtonPressed: _sfx.Play("button"); break;
                case SimEventKind.DoorOpened: _sfx.Play("door", 1.15f, -2); break;
                case SimEventKind.DoorClosed: _sfx.Play("door", 0.85f, -2); break;
                case SimEventKind.TurretFired: _sfx.Play("pew", 1, -6); break;
                case SimEventKind.BulletHit: _view.Burst(e.At, Pal.Spike, 4, 90, 0.18f, 3); break;
                case SimEventKind.PortalLost: _sfx.Play("fizzle"); _view.Burst(e.At, Pal.PortalColor(e.Which), 12, 180, 0.35f, 3); break;
                case SimEventKind.Death:
                    _sfx.Play("death");
                    _view.Burst(e.At, Pal.Robot, 26, 420, 0.6f, 5);
                    _view.Burst(e.At, Pal.Spike, 12, 300, 0.5f, 4);
                    _hud.Flash(Pal.Spike with { A = 0.22f });
                    break;
            }
        }
    }

    void Finish()
    {
        var s = Session!;
        var info = s.Info;
        int ticks = s.World.Tick;
        var prevRec = Store.Record(info.Id);
        int prev = prevRec?.PbTicks ?? 0;
        bool newPb = !Autoplay && (prev == 0 || ticks < prev);
        if (newPb)
        {
            Store.Data.Levels[info.Id] = new LevelRecord { PbTicks = ticks, PbRoute = s.RecordedRoute() };
            Store.Save();
        }
        _view.Burst(s.World.Pos, Pal.Exit, 30, 380, 0.7f, 5);

        if (_custom is { } cl)
        {
            int prevAuthor = cl.AuthorTicks;
            bool authored = false;
            if (_customFromEditor && !Autoplay && (cl.AuthorTicks == 0 || ticks < cl.AuthorTicks))
            {
                cl.AuthorTicks = ticks;
                cl.AuthorRoute = s.RecordedRoute();
                CustomLevels.Save(_customSlug, cl);
                authored = true;
            }
            _sfx.Play(authored || newPb ? "pb" : "finish");
            if (authored) Achievements.Unlock("ARCHITECT");
            string title = _customFromEditor ? (authored ? (prevAuthor == 0 ? "CLEARED — READY TO SHARE" : "NEW AUTHOR TIME") : "CLEARED") : "LEVEL COMPLETE";
            var m = cl.AuthorTicks > 0 ? Medals.For(ticks, cl.AuthorTicks) : Medal.None;
            Result = new ResultInfo(title, ticks, prev, newPb, m, cl.AuthorTicks > 0 ? Medals.NextTarget(Math.Min(ticks, prev == 0 ? ticks : prev), cl.AuthorTicks) : 0,
                (_customFromEditor ? "Enter  back to editor" : "Enter  back to library") + "      R  retry", false);
            return;
        }

        if (RunMode)
        {
            var run = Store.Data.Run;
            int segment = RunTicks - (RunSplits.Count > 0 ? RunSplits[^1] : 0);
            while (run.BestSegments.Count <= RunLevel) run.BestSegments.Add(0);
            bool gold = run.BestSegments[RunLevel] == 0 || segment < run.BestSegments[RunLevel];
            if (gold && !Autoplay) { run.BestSegments[RunLevel] = segment; Store.Save(); }
            RunSplits.Add(RunTicks);
            RunGolds.Add(gold);
            if (!Autoplay) Achievements.CheckProgress();
            int? delta = RunLevel < run.Splits.Count ? RunTicks - run.Splits[RunLevel] : null;
            _hud.SplitPopup(delta, gold);
            if (RunLevel + 1 < Catalog.Levels.Count)
            {
                _sfx.Play("finish", 1.2f, -3);
                LoadLevel(RunLevel + 1);
                return;
            }
            int prevRun = run.PbTicks;
            bool runPb = !Autoplay && (prevRun == 0 || RunTicks < prevRun);
            if (runPb)
            {
                run.PbTicks = RunTicks;
                run.Splits = RunSplits.ToList();
                Store.Save();
            }
            _sfx.Play(runPb ? "pb" : "finish");
            if (!Autoplay)
            {
                Achievements.Unlock("FULL_RUN");
                if (_runSetbacks == 0) Achievements.Unlock("DEATHLESS");
                if (RunTicks < Catalog.Levels.Sum(l => l.DevTicks)) Achievements.Unlock("SUB_DEV_RUN");
            }
            Result = new ResultInfo("RUN COMPLETE", RunTicks, prevRun, runPb, Medal.None, 0, "Enter  run again      Esc  menu", true);
            return;
        }

        if (!Autoplay) Achievements.CheckProgress();
        int best = prev == 0 || ticks < prev ? ticks : prev;
        var medal = Medals.For(ticks, info.DevTicks);
        _sfx.Play(newPb && prev > 0 ? "pb" : "finish");
        bool hasNext = RunLevel + 1 < Catalog.Levels.Count;
        string keys = (hasNext ? "Enter  next level" : "Enter  menu") + "      R  retry      Esc  menu";
        if (!hasNext && Store.AllFinished) keys = "Enter  menu (full game run unlocked)      R  retry";
        Result = new ResultInfo("LEVEL COMPLETE", ticks, prev, newPb, medal, Medals.NextTarget(best, info.DevTicks), keys, false);
    }

    // ---------------------------------------------------------------- per frame

    readonly List<double> _benchFrames = new();
    double _benchPhysicsMax, _benchStepMax, _benchStepSum;
    int _benchSteps;

    public override void _Process(double delta)
    {
        _frames++;
        if (_args.ContainsKey("bench") && Playing && Session != null)
        {
            if (_frames == 5) DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
            if (_frames > 120 && Session.Started) _benchFrames.Add(delta * 1000);
            if (_frames > 120) _benchPhysicsMax = Math.Max(_benchPhysicsMax, Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000);
            if (Result != null || Session.Finished)
            {
                var sorted = _benchFrames.OrderBy(x => x).ToList();
                if (sorted.Count > 0)
                    GD.Print($"BENCH {Session.Info.Id}: {sorted.Count} frames, avg {sorted.Average():0.00} ms ({1000 / sorted.Average():0} fps), " +
                             $"p99 {sorted[(int)(sorted.Count * 0.99)]:0.00} ms, worst {sorted[^1]:0.00} ms; sim step avg {_benchStepSum / Math.Max(1, _benchSteps):0.000} ms, max {_benchStepMax:0.000} ms");
                GetTree().Quit();
            }
        }
        if (Playing && Session != null)
        {
            _view.AimPx = _forcedAim is { } fa ? WorldView.Px(fa) : _view.GetLocalMousePosition();
            var p = _view.PlayerPx();
            var L = Session.Info.Level;
            float lw = L.W * WorldView.T, lh = L.H * WorldView.T;
            float cx = lw <= 1920 ? lw / 2 : Math.Clamp(p.X, 960, lw - 960);
            float cy = lh <= 1080 ? lh / 2 : Math.Clamp(p.Y, 540, lh - 540);
            _cam.Position = new Vector2(cx, cy);
            _bg.CameraCenter = _cam.Position;
        }
        Screenshot();
        if (_args.TryGetValue("quit-after-finish", out var qa) && Result != null)
        {
            _finishedFor += delta;
            if (_finishedFor >= double.Parse(qa, System.Globalization.CultureInfo.InvariantCulture)) GetTree().Quit();
        }
    }

    void Screenshot()
    {
        if (!_args.TryGetValue("screenshot", out var path)) return;
        bool due = _args.TryGetValue("shot-tick", out var st)
            ? Session != null && (Session.World.Tick >= int.Parse(st) || Session.Finished || Result != null) && _frames > 10
            : _frames >= (_args.TryGetValue("frames", out var fr) ? int.Parse(fr) : 30);
        if (_frames < 20) due = false;   // let the window settle at its capture size
        if (!due || _shotTaken) return;
        _shotTaken = true;
        // Wait a moment so the image includes everything drawn this frame.
        GetTree().CreateTimer(0.08).Timeout += () =>
        {
            GetViewport().GetTexture().GetImage().SavePng(path);
            GD.Print($"screenshot saved {path} (level tick {Session?.World.Tick ?? 0})");
            GetTree().Quit();
        };
    }
    bool _shotTaken;
}
