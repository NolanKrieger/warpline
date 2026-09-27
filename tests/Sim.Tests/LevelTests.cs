using Warpline.Sim;

namespace Warpline.Tests;

public class LevelTests
{
    public static IEnumerable<object[]> Levels() =>
        Directory.GetFiles(H.LevelsDir, "*.lvl").OrderBy(f => f).Select(f => new object[] { Path.GetFileNameWithoutExtension(f) });

    [Fact]
    public void ThereAreLevels() => Assert.True(Levels().Count() >= 4);

    [Theory]
    [MemberData(nameof(Levels))]
    public void DevRouteFinishesTheLevel(string id)
    {
        var level = Level.Parse(id, File.ReadAllText(Path.Combine(H.LevelsDir, id + ".lvl")));
        var route = Route.Parse(File.ReadAllText(Path.Combine(H.LevelsDir, id + ".route")));
        var r = Runner.Run(level, route.Frames());
        Assert.True(r.Finished, $"{id}: route ended {(r.Dead ? "dead" : "unfinished")} at tick {r.Ticks}, pos {r.World.Pos}");
        Assert.Equal(route.TotalTicks, r.Ticks);   // the route ends exactly on the finishing tick
    }

    [Theory]
    [MemberData(nameof(Levels))]
    public void StandingStillNeverFinishes(string id)
    {
        var level = Level.Parse(id, File.ReadAllText(Path.Combine(H.LevelsDir, id + ".lvl")));
        var r = Runner.Run(level, Enumerable.Repeat(InputFrame.None, 600));
        Assert.False(r.Finished);
        // Spawn is never an instant death (turret levels may shoot you if you stand around long enough).
        var first = Runner.Run(level, Enumerable.Repeat(InputFrame.None, Tuning.TicksPerSecond));
        Assert.False(first.Dead);
    }
}
