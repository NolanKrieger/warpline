using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>Custom levels: play, edit, copy a share code, delete; make a new one or import a code.</summary>
public partial class Library : Control
{
    public Main Game = null!;
    VBoxContainer _list = null!;
    Label _msg = null!;
    string _confirmDelete = "";
    float _time, _msgAt = -10;

    public override void _Ready()
    {
        Size = new Vector2(1920, 1080);
        var bg = new ColorRect { Color = Pal.Bg, Size = new Vector2(1920, 1080), MouseFilter = MouseFilterEnum.Ignore };
        AddChild(bg);
        var title = Ui.Label("CUSTOM LEVELS", 64, Pal.Black, Pal.Text);
        title.Position = new Vector2(150, 70);
        AddChild(title);
        var sub = Ui.Label("Build your own, clear it once to set the author time, share it as a code.", 24, Pal.Bold, Pal.TextDim);
        sub.Position = new Vector2(150, 160);
        AddChild(sub);

        var actions = new HBoxContainer { Position = new Vector2(150, 220) };
        actions.AddThemeConstantOverride("separation", 12);
        AddChild(actions);
        actions.AddChild(Action("New level", () => Game.OpenEditor(new EditLevel(), "")));
        actions.AddChild(Action("Import code from clipboard", Import));
        actions.AddChild(Action("Back", () => Game.ShowMenu()));

        _msg = Ui.Label("", 22, Pal.Bold, Pal.Exit);
        _msg.Position = new Vector2(150, 290);
        AddChild(_msg);

        var scroll = new ScrollContainer { Position = new Vector2(150, 340), Size = new Vector2(1620, 700), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
    }

    Button Action(string text, System.Action a)
    {
        var b = Ui.Button(text, 0, 24, Pal.Bold);
        b.CustomMinimumSize = new Vector2(0, 52);
        b.Pressed += a;
        return b;
    }

    public void Show(string message = "")
    {
        Visible = true;
        _confirmDelete = "";
        if (message.Length > 0) Toast(message);
        Rebuild();
    }

    void Toast(string m) { _msg.Text = m; _msgAt = _time; }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        _msg.Modulate = Colors.White with { A = Math.Clamp(4 - (_time - _msgAt), 0, 1) };
    }

    void Import()
    {
        var code = DisplayServer.ClipboardGet();
        try
        {
            var slug = CustomLevels.Import(code);
            Achievements.Unlock("SHARED");
            Toast($"imported \"{CustomLevels.List().First(e => e.Slug == slug).Level.Name}\"");
            Rebuild();
        }
        catch (FormatException ex) { Toast("couldn't import: " + ex.Message); }
    }

    void Rebuild()
    {
        foreach (var c in _list.GetChildren()) { _list.RemoveChild(c); c.QueueFree(); }
        var levels = CustomLevels.List();
        if (levels.Count == 0)
        {
            _list.AddChild(Ui.Label("No custom levels yet. Make one with New level, or paste a code someone sent you.", 24, Pal.Regular, Pal.TextDim));
            return;
        }
        Button? first = null;
        foreach (var e in levels)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var info = CustomLevels.Info(e.Level);
            var rec = Store.Record(info.Id);
            string right = e.Level.AuthorTicks > 0 ? $"author {Runner.FormatTime(e.Level.AuthorTicks)}" : "not cleared";
            if (rec != null) right = $"PB {Runner.FormatTime(rec.PbTicks)}   ·   " + right;
            var play = Ui.Button(e.Level.Name, 1100, 26);
            Ui.RightLabel(play, right, rec != null ? Pal.MedalColor(Medals.For(rec.PbTicks, e.Level.AuthorTicks)) : Pal.TextDim, 20);
            var lvl = e.Level; var slug = e.Slug;
            play.Pressed += () =>
            {
                var p = lvl.Problem();
                if (p.Length > 0) { Toast($"\"{lvl.Name}\" can't be played yet: {p}"); return; }
                Game.PlayCustom(lvl, slug, fromEditor: false);
            };
            row.AddChild(play);
            first ??= play;
            row.AddChild(Small("Edit", () => Game.OpenEditor(lvl, slug)));
            row.AddChild(Small("Copy code", () =>
            {
                if (lvl.AuthorTicks <= 0) { Toast("clear it in the editor's Test first — only cleared levels can be shared"); return; }
                DisplayServer.ClipboardSet(CustomLevels.Code(lvl));
                Achievements.Unlock("SHARED");
                Toast($"code for \"{lvl.Name}\" copied");
            }));
            row.AddChild(Small(_confirmDelete == slug ? "Really delete?" : "Delete", () =>
            {
                if (_confirmDelete != slug) { _confirmDelete = slug; Rebuild(); return; }
                CustomLevels.Delete(slug);
                _confirmDelete = "";
                Toast($"deleted \"{lvl.Name}\"");
                Rebuild();
            }));
            _list.AddChild(row);
        }
        first?.CallDeferred(Control.MethodName.GrabFocus);
    }

    static Button Small(string text, System.Action a)
    {
        var b = Ui.Button(text, 150, 20, Pal.Bold);
        b.Pressed += a;
        return b;
    }
}
