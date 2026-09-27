using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class MoverTests
{
    static Level LM(string objects, params string[] rows) =>
        Level.Parse("m", "name: m\n---\n" + string.Join("\n", rows) + "\n---\n" + objects);

    static readonly string[] Hall =
    {
        "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "X######################################X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#.S..................................#X",
        "X######################################X",
    };

    [Fact]
    public void OffsetIsAnEasedPingPong()
    {
        var m = LM("mover m1 x=10 y=8 w=4 h=1 to=20,8 period=2", Hall).Machines.Movers[0];
        Assert.Equal(V2.Zero, m.OffsetAt(0));
        Assert.Equal(10, m.OffsetAt(120).X, 9);        // halfway through the period: at the far end
        Assert.Equal(5, m.OffsetAt(60).X, 9);          // eased, symmetric
        Assert.Equal(0, m.OffsetAt(240).X, 9);
        Assert.True(m.OffsetAt(10).X < 10 * 10 / 120.0, "starts slow");
    }

    [Fact]
    public void RidesAHorizontalPlatform()
    {
        // Platform under the spawn slides right 10 tiles; the robot standing on it goes along.
        var w = new World(LM("mover m1 x=2 y=10 w=4 h=1 to=12,10 period=4",
            "XXXXXXXXXXXXXXXXXXXXXXXXX",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X.......................X",
            "X..S....................X",
            "X.......................X",
            "X^^^^^^^^^^^^^^^^^^^^^^^X",
            "XXXXXXXXXXXXXXXXXXXXXXXXX"));
        // Spawn search stops at the spikes' floor; stand the robot on the platform instead.
        w.Pos = new V2(3.5, 10 - Tuning.HalfH); w.Grounded = true;
        Steps(w, InputFrame.None, 240);               // half a period
        Assert.False(w.Dead);
        Assert.InRange(w.Pos.X, 13.3, 13.7);
        Assert.Equal(0, w.GroundMover);
    }

    [Fact]
    public void ElevatorLiftsAndJumpingOffKeepsItsSpeed()
    {
        var w = new World(LM("mover lift x=10 y=10 w=3 h=1 to=10,3 period=3", Hall));
        w.Pos = new V2(11.5, 10 - Tuning.HalfH); w.Grounded = true;
        Steps(w, InputFrame.None, 90);                // rising fast mid-way
        Assert.True(w.Pos.Y < 8.5, $"lifted to {w.Pos.Y}");
        var up = w.MoverVelocity(0).Y;
        Assert.True(up < -2);
        w.Step(Jump);
        Assert.True(w.Vel.Y < -Tuning.JumpVelocity - 1, $"jump got the lift's speed too: {w.Vel.Y}");
    }

    [Fact]
    public void PushedAlongAndCrushedAgainstAWall()
    {
        var push = new World(LM("mover ram x=2 y=8 w=2 h=3 to=10,8 period=4", Hall));
        push.Pos = new V2(5.5, 10.3); push.Grounded = true;
        Steps(push, InputFrame.None, 240);
        Assert.False(push.Dead);
        Assert.True(push.Pos.X > 10, $"pushed to {push.Pos.X}");

        var crush = new World(LM("mover ram x=2 y=8 w=2 h=3 to=36,8 period=4", Hall));
        crush.Pos = new V2(30.5, 10.3); crush.Grounded = true;
        Steps(crush, InputFrame.None, 240);
        Assert.True(crush.Dead);
    }

    [Fact]
    public void PortalsRideThePanelAndStillWork()
    {
        // A tall panel slab slides up and down; put a portal on its left face and one on the floor.
        var w = new World(LM("mover slab x=30 y=4 w=2 h=6 to=30,2 period=4", Hall));
        w.Step(FireA(29.9, 9.2));
        var a = Assert.NotNull(w.A);
        Assert.Equal(0, a.Mover);
        var c0 = w.PortalCenter(a);
        Steps(w, InputFrame.None, 120);
        var c1 = w.PortalCenter(a);
        Assert.True(c1.Y < c0.Y - 1, "portal moved up with the slab");

        // Walk into it from the left once it is back at floor level... use a floor portal as the exit.
        w.Step(FireB(6.5, 11.2));
        Assert.NotNull(w.B);
        var t = 0; bool tele = false;
        while (t++ < 900 && !tele)
        {
            w.Step(Right);
            tele = w.TeleportedThisTick;
        }
        Assert.True(tele, "went through the portal on the moving slab");
    }

    [Fact]
    public void LasersAndBulletsStopAtPanels()
    {
        var w = new World(LM("laser l1 x=36 y=5 dir=left\nturret t1 x=36 y=9 dir=left period=0.2 speed=20\nmover wall x=20 y=4 w=2 h=6 to=20,4 period=1", Hall));
        Steps(w, InputFrame.None, 200);
        Assert.False(w.Dead);
        Assert.All(w.Beams[0], seg => Assert.True(Math.Min(seg.A.X, seg.B.X) >= 21.9));
        Assert.All(w.Bullets, b => Assert.True(b.P.X > 21.9));
    }

    [Fact]
    public void MoverLevelsReplayExactly()
    {
        var lvl = LM("mover m1 x=10 y=8 w=4 h=1 to=20,4 period=3\nmover m2 x=25 y=3 w=2 h=5 to=25,5 period=2.5 phase=1", Hall);
        var rng = new Random(9);
        var frames = Enumerable.Range(0, 1500).Select(_ => new InputFrame(rng.Next(3) == 0, rng.Next(2) == 0, rng.Next(5) == 0, rng.Next(9) == 0,
            rng.Next(50) == 0, false, InputFrame.Quantize(new V2(rng.NextDouble() * 40, rng.NextDouble() * 12)))).ToList();
        var a = new World(lvl); foreach (var f in frames) a.Step(f);
        var b = new World(lvl); foreach (var f in Route.Parse(Route.FromFrames(frames).Serialize()).Frames()) b.Step(f);
        Assert.Equal(a.StateHash(), b.StateHash());
    }
}

public class DetMathTests
{
    [Theory]
    [InlineData(0.0)] [InlineData(0.1)] [InlineData(0.25)] [InlineData(0.37)] [InlineData(0.5)] [InlineData(0.77)] [InlineData(0.999)] [InlineData(-1.3)] [InlineData(12.6)]
    public void MatchesTheLibraryToTwelveDigits(double turns)
    {
        Assert.Equal(Math.Sin(turns * Math.Tau), DetMath.SinTurns(turns), 12);
        Assert.Equal(Math.Cos(turns * Math.Tau), DetMath.CosTurns(turns), 12);
    }

    [Fact]
    public void OrbitingPanelCirclesItsRestPoint()
    {
        var lvl = Level.Parse("o", "name: o\n---\nXXXXXXXXXXXXXXXXXXXXXXXXXXXX\nX..........................X\nX.S........................X\nXXXXXXXXXXXXXXXXXXXXXXXXXXXX\n---\nmover w1 x=12 y=1 w=2 h=1 orbit=3 period=4");
        var m = lvl.Machines.Movers[0];
        Assert.Equal(0, m.OffsetAt(0).X, 12);
        Assert.Equal(0, m.OffsetAt(0).Y, 12);
        var half = m.OffsetAt(240);
        Assert.Equal(-6, half.X, 9);
        Assert.Equal(0, half.Y, 9);
        var quarter = m.OffsetAt(120);
        Assert.Equal(-3, quarter.X, 9);
        Assert.Equal(3, quarter.Y, 9);
    }
}
