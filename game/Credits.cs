using Godot;

namespace Warpline;

/// <summary>Credits and licences (Godot MIT notice, Noto fonts OFL).</summary>
public partial class Credits : Control
{
    public Main Game = null!;

    public override void _Ready()
    {
        Size = new Vector2(1920, 1080);
        AddChild(new ColorRect { Color = Pal.Bg, Size = new Vector2(1920, 1080) });
        var title = Ui.Label("CREDITS", 64, Pal.Black, Pal.Text);
        title.Position = new Vector2(150, 80);
        AddChild(title);
        string[] lines =
        {
            "WARPLINE",
            "Design and direction — Nolan Krieger",
            "Built with Bojack (Claude) — code, levels, art, sound and music",
            "",
            "Made with the Godot Engine (godotengine.org), MIT licence:",
            "© 2014-present Godot Engine contributors; © 2007-2014 Juan Linietsky, Ariel Manzur.",
            "Godot's own third-party notices ship with the game in COPYRIGHT.txt.",
            "",
            "Noto Sans and Noto Sans Mono — © The Noto Project Authors, SIL Open Font License 1.1.",
            "All sound effects and music are synthesized by the game's own code.",
        };
        for (int i = 0; i < lines.Length; i++)
        {
            var l = Ui.Label(lines[i], i == 0 ? 34 : 24, i == 0 ? Pal.Black : Pal.Regular, i == 0 ? Pal.Cyan : Pal.Text);
            l.Position = new Vector2(150, 200 + i * 44);
            AddChild(l);
        }
        var back = Ui.Button("Back", 300);
        back.Position = new Vector2(150, 700);
        back.Pressed += () => Game.ShowMenu();
        AddChild(back);
    }

    public void Open()
    {
        Visible = true;
        GetChild<Button>(GetChildCount() - 1).CallDeferred(Control.MethodName.GrabFocus);
    }
}

/// <summary>The achievements list: unlocked ones lit, the rest dim with their goal.</summary>
public partial class AchievementsScreen : Control
{
    public Main Game = null!;
    VBoxContainer _list = null!;
    Button _back = null!;

    public override void _Ready()
    {
        Size = new Vector2(1920, 1080);
        AddChild(new ColorRect { Color = Pal.Bg, Size = new Vector2(1920, 1080) });
        var title = Ui.Label("ACHIEVEMENTS", 64, Pal.Black, Pal.Text);
        title.Position = new Vector2(150, 70);
        AddChild(title);
        _list = new VBoxContainer { Position = new Vector2(150, 190) };
        _list.AddThemeConstantOverride("separation", 4);
        AddChild(_list);
        _back = Ui.Button("Back", 300);
        _back.Position = new Vector2(150, 980);
        _back.Pressed += () => Game.ShowMenu();
        AddChild(_back);
    }

    public void Open()
    {
        Visible = true;
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        foreach (var d in Achievements.All)
        {
            bool has = Achievements.Has(d.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 20);
            var name = Ui.Label((has ? "★ " : "☆ ") + d.Name, 26, Pal.Black, has ? Pal.MedalColor(Warpline.Sim.Medal.Gold) : Pal.TextDim);
            name.CustomMinimumSize = new Vector2(520, 50);
            row.AddChild(name);
            row.AddChild(Ui.Label(d.Description, 22, Pal.Regular, has ? Pal.Text : Pal.TextDim));
            _list.AddChild(row);
        }
        _back.CallDeferred(Control.MethodName.GrabFocus);
    }
}
