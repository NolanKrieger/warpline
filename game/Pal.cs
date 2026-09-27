using Godot;
using Warpline.Sim;

namespace Warpline;

/// <summary>The palette and fonts. Red only ever means danger; cyan and magenta only ever mean portals.</summary>
public static class Pal
{
    public static readonly Color Bg = new("0d0f15");
    public static readonly Color BgDot = new(1, 1, 1, 0.05f);
    public static readonly Color Panel = new("e8ecf1");
    public static readonly Color PanelShade = new("c9d0da");
    public static readonly Color PanelEdge = new("9aa5b5");
    public static readonly Color PanelCore = new("3a414e");
    public static readonly Color PanelCoreLine = new("444c5a");
    public static readonly Color Metal = new("262b35");
    public static readonly Color MetalLight = new("39414e");
    public static readonly Color MetalDark = new("171a21");
    public static readonly Color Spike = new("ff3d4f");
    public static readonly Color Grill = new("c9d1dc");   // pale steel: must never read as a cyan portal
    public static readonly Color Exit = new("5cf2a4");
    public static readonly Color SpeedGel = new("ff9a2e");
    public static readonly Color BounceGel = new("a36bff");
    public static readonly Color Cyan = new("1fd8ff");
    public static readonly Color Magenta = new("ff3db8");
    public static readonly Color Robot = new("f3f5f8");
    public static readonly Color RobotShade = new("c3cad4");
    public static readonly Color RobotDark = new("181c24");
    public static readonly Color Text = new("eef1f5");
    public static readonly Color TextDim = new("8b94a4");
    public static readonly Color Ahead = new("54e38c");
    public static readonly Color Behind = new("ff8a5c");

    public static Color PortalColor(int which) => which == 0 ? Cyan : Magenta;

    public static Color MedalColor(Medal m) => m switch
    {
        Medal.Dev => new Color("b98cff"),
        Medal.Gold => new Color("ffd34d"),
        Medal.Silver => new Color("cfd6df"),
        Medal.Bronze => new Color("d98a4e"),
        _ => TextDim,
    };

    public static string MedalName(Medal m) => m switch
    {
        Medal.Dev => "DEV",
        Medal.Gold => "GOLD",
        Medal.Silver => "SILVER",
        Medal.Bronze => "BRONZE",
        _ => "—",
    };

    static FontFile? _black, _bold, _regular, _mono;
    public static FontFile Black => _black ??= GD.Load<FontFile>("res://assets/fonts/NotoSans-Black.ttf");
    public static FontFile Bold => _bold ??= GD.Load<FontFile>("res://assets/fonts/NotoSans-Bold.ttf");
    public static FontFile Regular => _regular ??= GD.Load<FontFile>("res://assets/fonts/NotoSans-Regular.ttf");
    public static FontFile Mono => _mono ??= GD.Load<FontFile>("res://assets/fonts/NotoSansMono-Bold.ttf");
}
