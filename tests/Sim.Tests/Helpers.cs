using Warpline.Sim;

namespace Warpline.Tests;

static class H
{
    public static Level L(params string[] rows) => Level.Parse("test", "name: test\n---\n" + string.Join("\n", rows));

    public static InputFrame Right => new(false, true, false, false, false, false, V2.Zero);
    public static InputFrame Left => new(true, false, false, false, false, false, V2.Zero);
    public static InputFrame Jump => new(false, false, true, false, false, false, V2.Zero);
    public static InputFrame Down => new(false, false, false, true, false, false, V2.Zero);
    public static InputFrame FireA(double x, double y) => new(false, false, false, false, true, false, new V2(x, y));
    public static InputFrame FireB(double x, double y) => new(false, false, false, false, false, true, new V2(x, y));

    public static void Steps(World w, InputFrame f, int n) { for (int i = 0; i < n; i++) w.Step(f); }

    public static IEnumerable<SimEvent> Run(World w, InputFrame f, int n)
    {
        var all = new List<SimEvent>();
        for (int i = 0; i < n; i++) { w.Step(f); all.AddRange(w.Events); }
        return all;
    }

    public static string LevelsDir
    {
        get
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "project.godot"))) d = d.Parent;
            return Path.Combine(d!.FullName, "levels");
        }
    }
}
