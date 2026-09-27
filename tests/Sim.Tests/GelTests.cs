using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class GelTests
{
    [Fact]
    public void SpeedGelRunsFarFasterThenBleedsOffOnNormalFloor()
    {
        var w = new World(L(
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X.....................................................................................X",
            "X.....................................................................................X",
            "X.S...................................................................................X",
            "X=========================================================XXXXXXXXXXXXXXXXXXXXXXXXXXXX"));
        double best = 0;
        for (int i = 0; i < 300 && w.Pos.X < 55; i++) { w.Step(Right); best = Math.Max(best, w.Vel.X); }
        Assert.True(best > Tuning.MaxRun * 2, $"top speed on gel {best}");
        Assert.True(w.OnSpeedGel);
        while (w.Pos.X < 63) w.Step(Right);
        Steps(w, Right, 60);
        Assert.False(w.OnSpeedGel);
        Assert.Equal(Tuning.MaxRun, w.Vel.X, 6);
    }

    [Fact]
    public void BounceGelThrowsYouBackAtImpactSpeed()
    {
        var w = new World(L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X.S........X",
            "XX%%%%%%%%XX"));
        w.Pos = new V2(5.5, 3); w.Grounded = false;          // drop from high up
        double impact = 0, minY = double.MaxValue;
        bool bounced = false;
        for (int i = 0; i < 400; i++)
        {
            double vy = w.Vel.Y;
            w.Step(InputFrame.None);
            if (w.Events.Any(e => e.Kind == SimEventKind.Bounce) && !bounced) { bounced = true; impact = vy; minY = double.MaxValue; }
            if (bounced) minY = Math.Min(minY, w.Pos.Y);
            if (bounced && w.Vel.Y > 0 && minY < 12) break;
        }
        Assert.True(bounced);
        Assert.InRange(minY, 2.6, 3.6);                      // comes back up to about where it fell from
    }

    [Fact]
    public void CrouchLandsNormallyAndJumpBouncesHigher()
    {
        string[] pit = { "XXXXXXXX", "X......X", "X......X", "X......X", "X......X", "X......X", "X......X", "X......X", "X......X", "X.S....X", "XX%%%%XX" };
        var crouch = new World(L(pit));
        Steps(crouch, Down, 60);
        Assert.True(crouch.Grounded);

        var idle = new World(L(pit));
        idle.Step(InputFrame.None);
        Assert.Equal(-Tuning.BounceMin, idle.Vel.Y, 6);    // standing on bounce gel is a small bounce

        // Jump just before landing on bounce gel: a bigger bounce than the plain one.
        var hop = new World(L(pit));
        hop.Step(InputFrame.None);                       // first small bounce
        while (hop.Vel.Y <= 0) hop.Step(InputFrame.None); // up...
        while (hop.Vel.Y > 0 && !hop.Events.Any(e => e.Kind == SimEventKind.Bounce))
            hop.Step(hop.Pos.Y > 8.5 ? Jump : InputFrame.None);   // ...and press jump on the way down
        Assert.True(hop.Vel.Y <= -Tuning.BounceJumpMin + 1e-9, $"vy {hop.Vel.Y}");
    }

    [Fact]
    public void BounceWallsReflectSideways()
    {
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X.............%",
            "X.............%",
            "X.............%",
            "X.S...........%",
            "XXXXXXXXXXXXXXX"));
        bool bounced = false;
        for (int i = 0; i < 200 && !bounced; i++) { w.Step(Right); bounced = w.Events.Any(e => e.Kind == SimEventKind.Bounce); }
        Assert.True(bounced);
        Assert.True(w.Vel.X < 0, "bounced back off the gel wall");
    }
}
