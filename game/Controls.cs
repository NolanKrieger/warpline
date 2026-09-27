using Godot;

namespace Warpline;

/// <summary>Key bindings: defaults in code, player overrides from the save, applied to Godot's InputMap.</summary>
public static class Controls
{
    public const string Left = "move_left", Right = "move_right", Jump = "jump", Crouch = "crouch",
        PortalA = "portal_a", PortalB = "portal_b", Restart = "restart", Pause = "pause", Ghost = "ghost", Confirm = "confirm",
        Fullscreen = "fullscreen";

    /// <summary>Actions the player can rebind, in the order the settings screen lists them.</summary>
    public static readonly (string action, string label)[] Rebindable =
    {
        (Left, "Run left"), (Right, "Run right"), (Jump, "Jump"), (Crouch, "Crouch / slide"),
        (PortalA, "Cyan portal"), (PortalB, "Magenta portal"), (Restart, "Restart"), (Ghost, "Cycle ghost"),
    };

    static readonly Dictionary<string, string[]> Defaults = new()
    {
        [Left] = new[] { K(Key.A), K(Key.Left) },
        [Right] = new[] { K(Key.D), K(Key.Right) },
        [Jump] = new[] { K(Key.Space), K(Key.W), K(Key.Up) },
        [Crouch] = new[] { K(Key.S), K(Key.Down), K(Key.Ctrl) },
        [PortalA] = new[] { M(MouseButton.Left) },
        [PortalB] = new[] { M(MouseButton.Right) },
        [Restart] = new[] { K(Key.R) },
        [Ghost] = new[] { K(Key.G) },
        [Pause] = new[] { K(Key.Escape) },
        [Confirm] = new[] { K(Key.Enter), K(Key.KpEnter) },
        [Fullscreen] = new[] { K(Key.F11) },
    };

    static string K(Key k) => "key:" + (long)k;
    static string M(MouseButton b) => "mouse:" + (long)b;

    public static void Ensure() => Apply(Store.Data.Settings);

    public static void Apply(Settings s)
    {
        foreach (var (action, defaults) in Defaults)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            InputMap.ActionEraseEvents(action);
            var list = s.Bindings.TryGetValue(action, out var custom) && custom.Count > 0 ? custom.ToArray() : defaults;
            foreach (var code in list)
                if (Decode(code) is { } ev) InputMap.ActionAddEvent(action, ev);
        }
    }

    public static InputEvent? Decode(string code)
    {
        var kv = code.Split(':');
        if (kv.Length != 2 || !long.TryParse(kv[1], out var n)) return null;
        return kv[0] switch
        {
            "key" => new InputEventKey { PhysicalKeycode = (Key)n },
            "mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)n },
            _ => null,
        };
    }

    /// <summary>The code for a pressed key or mouse button, or null for anything else.</summary>
    public static string? Encode(InputEvent ev) => ev switch
    {
        InputEventKey { Pressed: true } k => K(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton { Pressed: true } m => M(m.ButtonIndex),
        _ => null,
    };

    public static string Describe(string code) => Decode(code) switch
    {
        InputEventKey k => OS.GetKeycodeString(DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode)) is { Length: > 0 } s ? s : k.PhysicalKeycode.ToString(),
        InputEventMouseButton m => m.ButtonIndex switch
        {
            MouseButton.Left => "Left click", MouseButton.Right => "Right click", MouseButton.Middle => "Middle click",
            MouseButton.Xbutton1 => "Mouse 4", MouseButton.Xbutton2 => "Mouse 5", _ => "Mouse " + (int)m.ButtonIndex,
        },
        _ => "?",
    };

    public static string DescribeAction(string action)
    {
        var codes = Store.Data.Settings.Bindings.TryGetValue(action, out var c) && c.Count > 0 ? c.ToArray() : Defaults[action];
        return string.Join(" / ", codes.Select(Describe));
    }

    public static bool IsDefault(string action) => !Store.Data.Settings.Bindings.ContainsKey(action);

    public static IEnumerable<string> CodesFor(string action) =>
        Store.Data.Settings.Bindings.TryGetValue(action, out var c) && c.Count > 0 ? c : Defaults[action];
}
