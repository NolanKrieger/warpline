using Warpline.Sim;

namespace Warpline.Tests;

public class LevelTextTests
{
    [Theory]
    [MemberData(nameof(LevelTests.Levels), MemberType = typeof(LevelTests))]
    public void EveryShippedLevelRoundTripsAndStillSolves(string id)
    {
        var text = File.ReadAllText(Path.Combine(H.LevelsDir, id + ".lvl"));
        var a = Level.Parse(id, text);
        var again = Level.Parse(id, LevelText.ToText(a));
        Assert.Equal(a.GridText(), again.GridText());
        Assert.Equal(a.Spawn, again.Spawn);
        Assert.Equal(LevelText.ToText(a), LevelText.ToText(again));
        var route = Route.Parse(File.ReadAllText(Path.Combine(H.LevelsDir, id + ".route")));
        Assert.True(Runner.Run(again, route.Frames()).Finished, "the re-serialized level still plays the same");
    }

    [Fact]
    public void ShareCodesRoundTripWithTheAuthorRoute()
    {
        var text = File.ReadAllText(Path.Combine(H.LevelsDir, "07-crossfire.lvl"));
        var route = File.ReadAllText(Path.Combine(H.LevelsDir, "07-crossfire.route"));
        var code = LevelText.ToCode(text, route);
        Assert.StartsWith("WL1-", code);
        Assert.DoesNotContain('+', code);
        Assert.True(code.Length < text.Length, $"code {code.Length} chars vs text {text.Length}");
        var (t2, r2) = LevelText.FromCode("  " + code[..20] + "\n" + code[20..] + " ");
        Assert.Equal(text, t2);
        Assert.Equal(route, r2);
        Assert.Throws<FormatException>(() => LevelText.FromCode("hello"));
        Assert.Throws<FormatException>(() => LevelText.FromCode("WL1-!!!!"));
    }

    [Fact]
    public void AuthorTimeLivesInTheHeader()
    {
        var l = Level.Parse("x", "name: x\n---\nXXXXX\nX.S.X\nXXXXX\n");
        var text = LevelText.ToText(l, 1234);
        Assert.Equal(1234, LevelText.AuthorTicks(text));
        Assert.Equal(0, LevelText.AuthorTicks(LevelText.ToText(l)));
    }
}
