using Warpline.Sim;

// Usage: Trace <level.lvl> [route.route] [--every=N] [--from=x0 --to=x1] [--events]
// Prints result/time, the event log and the level with the path drawn on it:
//   'o' body centre every N ticks, '*' teleports, 'c'/'m' cyan/magenta portal tiles, '@' death, '!' finish.
if (args.Length < 1) { Console.WriteLine("usage: Trace <level> [route] [--every=N] [--from=X --to=X]"); return 1; }
string lvlPath = args[0];
string? routePath = args.Length > 1 && !args[1].StartsWith("--") ? args[1] : null;
int every = 6, from = 0, to = int.MaxValue, dumpA = -1, dumpB = -1;
foreach (var a in args)
{
    if (a.StartsWith("--every=")) every = int.Parse(a[8..]);
    if (a.StartsWith("--from=")) from = int.Parse(a[7..]);
    if (a.StartsWith("--to=")) to = int.Parse(a[5..]);
    if (a.StartsWith("--dump=")) { var ab = a[7..].Split('-'); dumpA = int.Parse(ab[0]); dumpB = int.Parse(ab[1]); }
}
var level = Level.Parse(Path.GetFileNameWithoutExtension(lvlPath), File.ReadAllText(lvlPath));
routePath ??= Path.ChangeExtension(lvlPath, ".route");
InputFrame[] frames;
var planPath = Path.ChangeExtension(lvlPath, ".plan");
if (args.Contains("--compile") && File.Exists(planPath))
{
    var (pf, _) = Plan.Compile(level, File.ReadAllText(planPath), Console.Out);
    File.WriteAllText(routePath, $"# {level.Name} - developer route, compiled from {Path.GetFileName(planPath)}\n" + Route.FromFrames(pf).Serialize());
    frames = pf.ToArray();
}
else frames = File.Exists(routePath) ? Route.Parse(File.ReadAllText(routePath)).ToArray() : Array.Empty<InputFrame>();

var w = new World(level);
var grid = new char[level.H + 3, level.W];
for (int y = 0; y < level.H + 3; y++)
    for (int x = 0; x < level.W; x++)
        grid[y, x] = y < level.H ? Tiles.ToChar(level[x, y]) : ' ';
void Put(V2 p, char c)
{
    int x = (int)Math.Floor(p.X), y = (int)Math.Floor(p.Y);
    if (x >= 0 && x < level.W && y >= 0 && y < level.H + 3) grid[y, x] = c;
}
Put(level.Spawn, 'S');
var mach = level.Machines;
foreach (var d in mach.Doors) for (int y = d.Y; y < d.Y + d.H; y++) for (int x = d.X; x < d.X + d.W; x++) grid[y, x] = 'D';
foreach (var b in mach.Buttons) grid[b.Y, b.X] = 'b';
foreach (var l in mach.Lasers) grid[l.Y, l.X] = 'L';
foreach (var r in mach.Receivers) grid[r.Y, r.X] = 'O';
foreach (var t in mach.Turrets) grid[t.Y, t.X] = 'T';
foreach (var mv in mach.Movers) for (int y = mv.Y; y < mv.Y + mv.H; y++) for (int x = mv.X; x < mv.X + mv.W; x++) grid[y, x] = 'M';
int i = 0;
foreach (var f in frames.Concat(Enumerable.Repeat(InputFrame.None, 240)))
{
    w.Step(f);
    i++;
    if (w.Tick >= dumpA && w.Tick <= dumpB)
        Console.WriteLine($"  #{w.Tick} in[{(f.Left ? "L" : "")}{(f.Right ? "R" : "")}{(f.Jump ? "J" : "")}{(f.Down ? "D" : "")}] pos {w.Pos} vel {w.Vel} {(w.Grounded ? "G" : "air")} wall{w.WallDir}");
    foreach (var e in w.Events)
        if (e.Kind is not (SimEventKind.TurretFired or SimEventKind.BulletHit))
            Console.WriteLine($"t{w.Tick,5} {Runner.FormatTime(w.Tick)} {e.Kind,-15} at {e.At} {(e.Kind == SimEventKind.Teleport ? "-> " + e.To : "")} vel {w.Vel} {(e.Which >= 0 ? "which " + e.Which : "")}");
    if (w.Tick % every == 0) Put(w.Pos, 'o');
    foreach (var e in w.Events) if (e.Kind == SimEventKind.Teleport) { Put(e.At, '*'); Put(e.To, '*'); }
    if (w.Dead) { Put(w.Pos, '@'); break; }
    if (w.Finished) { Put(w.Pos, '!'); break; }
    if (i > frames.Length + 240) break;
}
foreach (var (p, c) in new[] { (w.A, 'c'), (w.B, 'm') })
{
    if (p is not { } po) continue;
    for (int k = 0; k < Tuning.PortalLength; k++)
    {
        int x = po.Vertical ? (po.Nx < 0 ? po.Line : po.Line - 1) : po.Start + k;
        int y = po.Vertical ? po.Start + k : (po.Ny < 0 ? po.Line : po.Line - 1);
        if (x >= 0 && x < level.W && y >= 0 && y < level.H) grid[y, x] = c;
    }
}
Console.WriteLine($"{level.Name}: {(w.Finished ? "FINISHED" : w.Dead ? "DIED" : "not finished")} at tick {w.Tick} = {Runner.FormatTime(w.Tick)}  route {frames.Length} ticks  pos {w.Pos} vel {w.Vel}");
int x0 = Math.Max(0, from), x1 = Math.Min(level.W, to);
Console.Write("     ");
for (int x = x0; x < x1; x++) Console.Write(x % 10 == 0 ? (char)('0' + x / 10 % 10) : ' ');
Console.WriteLine();
Console.Write("     ");
for (int x = x0; x < x1; x++) Console.Write((char)('0' + x % 10));
Console.WriteLine();
for (int y = 0; y < level.H + 3; y++)
{
    Console.Write($"{y,3}  ");
    for (int x = x0; x < x1; x++) Console.Write(grid[y, x]);
    Console.WriteLine();
}
return w.Finished ? 0 : 2;
