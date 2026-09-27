using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>
/// `godot --path . -- --selftest --save=/tmp/x.json` : drives the real game through its input and tick paths.
/// Prints PASS/FAIL per check and exits 0 only if everything passed.
/// </summary>
public partial class Main
{
    readonly List<string> _fails = new();
    int _checks;

    void Check(bool ok, string what)
    {
        _checks++;
        GD.Print((ok ? "PASS " : "FAIL ") + what);
        if (!ok) _fails.Add(what);
    }

    void Press(string action, bool down)
    {
        var ev = new InputEventAction { Action = action, Pressed = down };
        Input.ParseInputEvent(ev);
        Input.FlushBufferedEvents();
    }

    async void RunSelfTest()
    {
        if (!_args.ContainsKey("save")) { GD.PrintErr("selftest needs --save=<temp path> so it never touches the real save"); GetTree().Quit(2); return; }
        if (File.Exists(Store.Path)) File.Delete(Store.Path);
        Store.Load();
        if (System.IO.Directory.Exists(CustomLevels.Dir)) System.IO.Directory.Delete(CustomLevels.Dir, true);

        Check(Catalog.Levels.Count >= 4, $"catalog loaded {Catalog.Levels.Count} levels");
        foreach (var li in Catalog.Levels) Check(li.DevTicks > 0, $"dev route finishes {li.Id} in {Runner.FormatTime(li.DevTicks)}");
        Check(Store.Unlocked(0) && !Store.Unlocked(1), "only level 1 is open on a fresh save");

        // Real input path: start level 1, hold right via the Input singleton.
        StartLevel(0, false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int i = 0; i < 10; i++) TickOnce();
        Check(!Session!.Started && Session.World.Tick == 0, "timer waits for the first input");
        double x0 = Session.World.Pos.X;
        Press(Controls.Right, true);
        for (int i = 0; i < 60; i++) TickOnce();
        Press(Controls.Right, false);
        Check(Session.Started && Session.World.Pos.X > x0 + 3, $"move_right moved the robot ({x0:0.0} -> {Session.World.Pos.X:0.0})");
        Check(Session.World.Tick == 60, $"level timer counted 60 ticks ({Session.World.Tick})");

        // Mouse click through _UnhandledInput -> portal shot at the clicked world point.
        var target = WorldView.Px(new V2(124.0 - 0.05, 17.2));
        var click = new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = GetViewport().GetCanvasTransform() * target };
        _UnhandledInput(click);
        TickOnce();
        Check(Session.World.A != null || Session.World.Events.Any(e => e.Kind == SimEventKind.PortalFizzled), "left click fired a cyan shot");

        // R restarts: timer resets, portals cleared.
        _UnhandledInput(new InputEventAction { Action = Controls.Restart, Pressed = true });
        Check(Session.World.Tick == 0 && Session.World.A == null && !Session.Started, "R restarts the level and resets the timer");

        // Autoplay the dev route through the game loop: finishing saves a PB and unlocks level 2.
        Autoplay = true;
        StartLevel(0, false);
        for (int i = 0; i < 5000 && Result == null; i++) TickOnce();
        Autoplay = false;
        Check(Result != null && Result.Ticks == Catalog.Levels[0].DevTicks, "dev playback finishes level 1 in the game loop with the dev time");
        Check(Store.Record(Catalog.Levels[0].Id) == null, "dev playback does not count as a PB");

        // Player-driven finish (replay the dev frames as if typed) -> PB saved, next level unlocked, ghost available.
        StartLevel(0, false);
        foreach (var f in Catalog.Levels[0].DevRoute.Frames()) { FeedFrame(f); if (Result != null) break; }
        var rec = Store.Record(Catalog.Levels[0].Id);
        Check(rec != null && rec.PbTicks == Catalog.Levels[0].DevTicks, "finishing saves the PB");
        Check(Result?.Medal == Medal.Dev, $"dev-time finish earns the DEV medal ({Result?.Medal})");
        Check(Store.Unlocked(1), "finishing level 1 unlocks level 2");
        StartLevel(0, false);
        Check(Session!.GhostLabel == "PB" && Session.Ghost != null, "PB ghost loads on the next attempt");

        // Death restarts instantly and keeps the attempt counter honest.
        StartLevel(0, false);
        FeedFrame(new InputFrame(false, true, false, false, false, false, V2.Zero));
        Session!.World.Pos = new V2(32.5, 28.3);   // stand the robot on the spikes
        FeedFrame(InputFrame.None);
        Check(Session.World.Tick == 0 && !Session.Started, "dying restarts the level with a fresh timer");

        // Full-game run: all levels back to back, one clock, one split per level.
        foreach (var li in Catalog.Levels) Store.Data.Levels[li.Id] = new LevelRecord { PbTicks = li.DevTicks, PbRoute = li.DevRoute.Serialize() };
        StartLevel(0, true);
        int expected = 0;
        for (int li = 0; li < Catalog.Levels.Count; li++)
        {
            foreach (var f in Catalog.Levels[li].DevRoute.Frames()) { FeedFrame(f); if (RunLevel != li || Result != null) break; }
            expected += Catalog.Levels[li].DevTicks;
        }
        Check(Result is { IsRun: true }, "full-game run completes");
        Check(RunSplits.Count == Catalog.Levels.Count, $"one split per level ({RunSplits.Count})");
        Check(RunTicks == expected, $"run clock = sum of level times ({RunTicks} vs {expected})");
        Check(Store.Data.Run.PbTicks == RunTicks, "run PB saved with splits");

        Check(Store.Data.Run.SumOfBest(Catalog.Levels.Count) == RunTicks, "first run's segments become the golds (sum of best = run time)");
        Check(RunGolds.Count == Catalog.Levels.Count && RunGolds.All(g => g), "every split of a first run is gold");

        // Pause stops everything; Esc resumes.
        StartLevel(0, false);
        FeedFrame(new InputFrame(false, true, false, false, false, false, V2.Zero));
        int before = Session!.World.Tick;
        _UnhandledInput(new InputEventAction { Action = Controls.Pause, Pressed = true });
        FeedFrame(new InputFrame(false, true, false, false, false, false, V2.Zero));
        Check(Paused && Session.World.Tick == before, "Esc pauses and the level clock stops");
        _UnhandledInput(new InputEventAction { Action = Controls.Pause, Pressed = true });
        FeedFrame(new InputFrame(false, true, false, false, false, false, V2.Zero));
        Check(!Paused && Session.World.Tick == before + 1, "Esc again resumes");

        // Rebinding: move jump to K; the InputMap follows and Space no longer jumps.
        Store.Data.Settings.Bindings[Controls.Jump] = new List<string> { Controls.Encode(new InputEventKey { PhysicalKeycode = Key.K, Pressed = true })! };
        Controls.Apply(Store.Data.Settings);
        Check(InputMap.ActionHasEvent(Controls.Jump, new InputEventKey { PhysicalKeycode = Key.K }), "rebinding jump to K reaches the InputMap");
        Check(!InputMap.ActionHasEvent(Controls.Jump, new InputEventKey { PhysicalKeycode = Key.Space }), "the old jump key is released");
        Store.Data.Settings.Bindings.Clear();
        Controls.Apply(Store.Data.Settings);
        Check(InputMap.ActionHasEvent(Controls.Jump, new InputEventKey { PhysicalKeycode = Key.Space }), "reset restores default keys");

        // ---- Level editor, clear check, share codes, library
        var ed = new EditLevel(60, 24);
        OpenEditor(ed, "");
        Check(_editor.Visible && ed.Problem() == "", "editor opens a new level that already has a spawn and an exit");
        _editor.ApplyTool("spike", new Vector2I(20, 19), new Vector2I(22, 19), shift: true);   // a row of spikes on the floor
        Check(ed[20, 19] == '^' && ed[22, 19] == '^', "shift-drag fills a rectangle of tiles");
        _editor.ApplyTool("panel", new Vector2I(19, 17), new Vector2I(19, 17));
        _editor.ApplyTool("door", new Vector2I(40, 16), new Vector2I(40, 19));
        _editor.ApplyTool("button", new Vector2I(30, 19), new Vector2I(30, 19));
        Check(ed.Objects.Count == 2 && ed.Objects[0].Kind == "door" && ed.Objects[0].H == 4, "drag places a door, click places a button");
        Check(Level.Parse("t", ed.ToText()).Machines.Buttons[0].Opens.Length == 1, "the button is linked to the door on its channel");
        _editor.Save();
        string slug = _editor.Slug;
        Check(System.IO.File.Exists(System.IO.Path.Combine(CustomLevels.Dir, slug + ".lvl")), $"saved to the library as {slug}.lvl");

        // Test play = clear check: run right, hop the spikes, press the button, go through the door.
        PlayCustom(ed, slug, fromEditor: true);
        var runRight = new InputFrame(false, true, false, false, false, false, V2.Zero);
        var hop = new InputFrame(false, true, true, false, false, false, V2.Zero);
        for (int i = 0; i < 2000 && Result == null; i++)
        {
            double x = Session!.World.Pos.X;
            FeedFrame(x > 16.5 && x < 19.5 ? hop : runRight);
        }
        Check(Result != null && ed.AuthorTicks > 0 && ed.AuthorRoute.Length > 0, $"finishing in Test sets the author time ({Runner.FormatTime(ed.AuthorTicks)})");
        Check(Route.Parse(ed.AuthorRoute).TotalTicks == ed.AuthorTicks, "the author route is exactly the clearing run");
        var code = CustomLevels.Code(ed);
        Check(code.StartsWith("WL1-"), $"share code made ({code.Length} chars)");
        string imported = CustomLevels.Import(code);
        var entries = CustomLevels.List();
        var copy = entries.First(e => e.Slug == imported).Level;
        Check(entries.Count == 2 && copy.AuthorTicks == ed.AuthorTicks, "importing the code adds a copy with the same author time");
        Check(CustomLevels.Info(copy).Id == CustomLevels.Info(ed).Id, "the copy shares PBs with the original (same content hash)");
        PlayCustom(copy, imported, fromEditor: false);
        Check(Session!.GhostLabel == "PB" || Session.GhostLabel == "DEV", "the author ghost (or your PB) rides along on shared levels");
        foreach (var f in Route.Parse(copy.AuthorRoute).Frames()) { FeedFrame(f); if (Result != null) break; }
        Check(Result?.Ticks == copy.AuthorTicks && Result.Medal == Medal.Dev, "the author route replays to the author time on the imported copy");
        Advance();
        Check(_libraryLayer.Visible, "finishing a library level returns to the library");

        Check(Achievements.Has("FIRST_STEPS") && Achievements.Has("ALL_LEVELS") && Achievements.Has("DEV_ALL"), "level achievements unlock from saved PBs");
        Check(Achievements.Has("FULL_RUN") && Achievements.Has("DEATHLESS") && !Achievements.Has("SUB_DEV_RUN"), "run achievements: full + clean unlock, 'faster than us' needs < dev sum");
        Check(Achievements.Has("ARCHITECT") && Achievements.Has("TERMINAL") && Achievements.Has("SPEEDY_THING"), "editor clear and speed achievements unlock");
        Check(!Achievements.Has("SHARED"), "nothing is marked shared without copying or importing a code through the UI");

        GD.Print($"selftest: {_checks - _fails.Count}/{_checks} passed");
        GetTree().Quit(_fails.Count == 0 ? 0 : 1);
    }

    void FeedFrame(InputFrame f)
    {
        // Same path as a physics tick, with the frame supplied instead of sampled from devices.
        _injected = f;
        TickOnce();
        _injected = null;
    }
}
