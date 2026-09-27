using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class MovementTests
{
    static readonly string[] Room =
    {
        "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "X......................................X",
        "X......................................X",
        "X......................................X",
        "X......................................X",
        "X......................................X",
        "X......................................X",
        "X......................................X",
        "X..S...................................X",
        "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
    };

    [Fact]
    public void SpawnStandsStillWhenIdle()
    {
        var w = new World(L(Room));
        var start = w.Pos;
        Steps(w, InputFrame.None, 240);
        Assert.Equal(start, w.Pos);
        Assert.True(w.Grounded);
        Assert.Equal(9 - Tuning.HalfH, w.Pos.Y, 9);
    }

    [Fact]
    public void RunSpeedCapsAtMaxRun()
    {
        var w = new World(L(Room));
        Steps(w, Right, 60);
        Assert.Equal(Tuning.MaxRun, w.Vel.X, 6);
        Assert.True(w.Grounded);
    }

    [Fact]
    public void FullJumpReachesAboutThreeTiles()
    {
        var w = new World(L(Room));
        double feet0 = w.Feet.Y, minFeet = feet0;
        for (int i = 0; i < 120; i++) { w.Step(Jump); minFeet = Math.Min(minFeet, w.Feet.Y); }
        double height = feet0 - minFeet;
        double ideal = Tuning.JumpVelocity * Tuning.JumpVelocity / (2 * Tuning.Gravity);
        Assert.InRange(height, ideal - 0.15, ideal + 0.05);
        Assert.True(w.Grounded);
    }

    [Fact]
    public void ReleasingJumpEarlyJumpsLower()
    {
        var w = new World(L(Room));
        double feet0 = w.Feet.Y, minFeet = feet0;
        Steps(w, Jump, 6);
        for (int i = 0; i < 120; i++) { w.Step(InputFrame.None); minFeet = Math.Min(minFeet, w.Feet.Y); }
        Assert.InRange(feet0 - minFeet, 0.5, 2.0);
    }

    [Fact]
    public void CoyoteTimeAllowsLateJump()
    {
        var lvl = L(
            "XXXXXXXXXXXXXXXXXXXX",
            "X..................X",
            "X..................X",
            "X..................X",
            "X..................X",
            "X.S................X",
            "XXXXX..............X",
            "X..................X",
            "X..................X",
            "XXXXXXXXXXXXXXXXXXXX");
        var w = new World(lvl);
        int t = 0;
        while (w.Pos.X - Tuning.HalfW < 5.0 && t++ < 300) w.Step(Right);  // run until fully off the ledge
        Assert.False(w.Grounded);
        Steps(w, Right, 4);                                                // 4 ticks late, inside the 10-tick window
        var ev = Run(w, new InputFrame(false, true, true, false, false, false, V2.Zero), 1);
        Assert.Contains(ev, e => e.Kind == SimEventKind.Jump);
    }

    [Fact]
    public void WallJumpKicksAwayFromWall()
    {
        var lvl = L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..........X",
            "X........S.X",
            "XXXXXXXXXXXX");
        var w = new World(lvl);
        var jr = new InputFrame(false, true, true, false, false, false, V2.Zero);
        w.Step(jr);                                     // jump...
        int t = 0;
        while (w.WallDir != 1 && t++ < 60) w.Step(jr);  // ...drifting into the right wall
        Assert.Equal(1, w.WallDir);
        Steps(w, Right, 2);                             // release jump, keep holding into the wall
        var ev = Run(w, jr, 1);
        Assert.Contains(ev, e => e.Kind == SimEventKind.WallJump);
        Assert.True(w.Vel.X < -5);
        Assert.True(w.Vel.Y < -10);
    }

    [Fact]
    public void WallSlideCapsFallSpeed()
    {
        var lvl = L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X........S.X",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "X........XXX",
            "XXXXXXXXXXXX");
        var w = new World(lvl);
        // Airborne just left of the pillar, holding right into its face.
        w.Pos = new V2(8.6, 5); w.Vel = V2.Zero; w.Grounded = false;
        Steps(w, Right, 40);
        Assert.Equal(1, w.WallDir);
        Assert.True(w.Vel.Y <= Tuning.WallSlideMax + 1e-9);
    }

    [Fact]
    public void SlideKeepsSpeedLongerAndFitsUnderLowGap()
    {
        var lvl = L(
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X......................................X",
            "X......................................X",
            "X......................................X",
            "X....................XXXXXX............X",
            "X.S....................................X",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
        var w = new World(lvl);
        Steps(w, Right, 60);
        Assert.Equal(Tuning.MaxRun, w.Vel.X, 6);
        // Slide: 30 ticks later a slide still has most of its speed; plain friction would have stopped us.
        Steps(w, Down, 30);
        Assert.True(w.Sliding);
        Assert.True(w.Vel.X > Tuning.MaxRun - 3);
        // Keep holding right + down: slide, then crouch-walk, under the one-tile gap at x 21..26.
        int t = 0;
        var rd = new InputFrame(false, true, false, true, false, false, V2.Zero);
        while (w.Pos.X < 30 && t++ < 600) w.Step(rd);
        Assert.True(w.Pos.X >= 30, $"slid only to {w.Pos.X}");
        Assert.True(w.Crouched);
    }

    [Fact]
    public void CannotStandUpUnderLowCeiling()
    {
        var lvl = L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X...XXXXXXXX",
            "X.S........X",
            "XXXXXXXXXXXX");
        var w = new World(lvl);
        Steps(w, new InputFrame(false, true, false, true, false, false, V2.Zero), 80);
        Assert.True(w.Pos.X > 5);
        Steps(w, InputFrame.None, 5);
        Assert.True(w.Crouched);
    }
}
