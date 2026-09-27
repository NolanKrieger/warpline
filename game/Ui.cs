using Godot;

namespace Warpline;

/// <summary>Shared widget styling so every screen looks like the same game.</summary>
public static class Ui
{
    public static StyleBoxFlat Box(Color bg, Color border, int left = 0, int all = 0, int radius = 6) => new()
    {
        BgColor = bg, BorderColor = border, ContentMarginLeft = 24, ContentMarginRight = 24, ContentMarginTop = 6, ContentMarginBottom = 6,
        BorderWidthLeft = Math.Max(left, all), BorderWidthRight = all, BorderWidthTop = all, BorderWidthBottom = all,
        CornerRadiusTopRight = radius, CornerRadiusBottomRight = radius, CornerRadiusTopLeft = all > 0 ? radius : 0, CornerRadiusBottomLeft = all > 0 ? radius : 0,
    };

    public static Button Button(string text, float width, int size = 28, Font? font = null)
    {
        var b = new Button { Text = text, Alignment = HorizontalAlignment.Left, CustomMinimumSize = new Vector2(width, 56), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontOverride("font", font ?? Pal.Black);
        b.AddThemeFontSizeOverride("font_size", size);
        b.AddThemeColorOverride("font_color", Pal.Text);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_focus_color", Colors.White);
        b.AddThemeColorOverride("font_pressed_color", Pal.Cyan);
        b.AddThemeColorOverride("font_disabled_color", Pal.TextDim with { A = 0.5f });
        b.AddThemeStyleboxOverride("normal", Box(new Color(1, 1, 1, 0.04f), Colors.Transparent, 5));
        b.AddThemeStyleboxOverride("hover", Box(new Color(1, 1, 1, 0.09f), Pal.Cyan, 5));
        b.AddThemeStyleboxOverride("focus", Box(new Color(1, 1, 1, 0.09f), Pal.Magenta, 5));
        b.AddThemeStyleboxOverride("pressed", Box(new Color(1, 1, 1, 0.14f), Pal.Cyan, 5));
        b.AddThemeStyleboxOverride("disabled", Box(new Color(1, 1, 1, 0.02f), Colors.Transparent, 5));
        return b;
    }

    /// <summary>A right-aligned value label inside a button (times, medals, current key).</summary>
    public static Label RightLabel(Control parent, string text, Color color, int size = 22)
    {
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        l.OffsetRight = -24;
        l.AddThemeFontOverride("font", Pal.Mono);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        parent.AddChild(l);
        return l;
    }

    public static Label Label(string text, int size, Font font, Color color)
    {
        var l = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontOverride("font", font);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    /// <summary>A centred card panel with a cyan/magenta top rule, like the finish card.</summary>
    public static PanelContainer Card(Control parent, float width)
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop, Size = new Vector2(1920, 1080) };
        parent.AddChild(dim);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(1920, 1080) };
        parent.AddChild(center);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        var sb = new StyleBoxFlat
        {
            BgColor = new Color("12151d"), BorderColor = Pal.Cyan with { A = 0.9f },
            ContentMarginLeft = 40, ContentMarginRight = 40, ContentMarginTop = 30, ContentMarginBottom = 30,
        };
        sb.SetBorderWidthAll(3);
        sb.BorderWidthTop = 6;
        card.AddThemeStyleboxOverride("panel", sb);
        center.AddChild(card);
        return card;
    }
}
