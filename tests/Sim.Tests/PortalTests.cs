using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class PortalTests
{
    // Panel walls left (x=1) and right (x=38), panel floor, metal ceiling, a grill column at x=20.
    static readonly string[] Chamber =
    {
        "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "X######################################X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#....................................#X",
        "X#.........S..........................#X",
        "X######################################X",
    };

    [Fact]
    public void ShotOnPanelPlacesTwoTilePortal()
    {
        var w = new World(L(Chamber));
        w.Step(FireA(37.5, 7.5));   // right wall, face at x = 38 facing left
        var a = Assert.NotNull(w.A);
        Assert.Equal((-1, 0, 38), (a.Nx, a.Ny, a.Line));
        Assert.Contains(w.Events, e => e.Kind == SimEventKind.PortalPlaced && e.Which == 0);
    }

    [Fact]
    public void MetalRefusesPortals()
    {
        var w = new World(L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X..........X",
            "X..S.......X",
            "XXXXXXXXXXXX"));
        w.Step(FireA(10.5, 3));
        Assert.Null(w.A);
        Assert.Contains(w.Events, e => e.Kind == SimEventKind.PortalFizzled);
        Assert.Equal(ShotResult.NotPortalable, w.Aim(0, new V2(10.5, 3)).Result);
    }

    [Fact]
    public void GrillBlocksShots()
    {
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X#############X",
            "X#.......:...#X",
            "X#.......:...#X",
            "X#.......:...#X",
            "X#..S....:...#X",
            "X#############X"));
        Assert.Equal(ShotResult.Blocked, w.Aim(0, new V2(13, 4)).Result);
    }

    [Fact]
    public void NeedsTwoPanelTilesAndClearance()
    {
        // Only one panel tile on the right wall; the rest is metal.
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X.............X",
            "X.............X",
            "X.............X",
            "X.............#",
            "X....S........X",
            "XXXXXXXXXXXXXXX"));
        Assert.Equal(ShotResult.NoRoom, w.Aim(0, new V2(14.5, 4.5)).Result);
    }

    [Fact]
    public void WallPortalsFitInOneTileWideGaps()
    {
        // A 1-wide vertical shaft: the robot fits sideways out of a wall portal, so wall portals are allowed.
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X#####.#######X",
            "X#####.#######X",
            "X#####.#######X",
            "X#####.#######X",
            "X#............X",
            "X#.S..........X",
            "XXXXXXXXXXXXXXX"));
        w.Pos = new V2(6.5, 3); w.Grounded = false;
        Assert.Equal(ShotResult.Placed, w.Aim(0, new V2(5.9, 3.0)).Result);
    }

    [Fact]
    public void SlidesUpToThreeTilesButNotAcrossAGap()
    {
        // Panels at x 9..10 only; a hit at x=7 (metal) must not place, a hit on panel x=9 near a metal edge slides onto 9..10.
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X.............X",
            "X.............X",
            "X.............X",
            "X.............X",
            "X.S...........X",
            "XXXXXXXXX##XXXX"));
        Assert.Equal(ShotResult.NotPortalable, w.Aim(0, new V2(7.5, 6)).Result);
        var shot = w.Aim(0, new V2(9.1, 6));
        Assert.Equal(ShotResult.Placed, shot.Result);
        Assert.Equal(9, shot.Portal!.Value.Start);
    }

    [Fact]
    public void BumpsToFitNearAnEdge()
    {
        // Panel floor tiles at x=5,6 only; aiming at the far half of x=6 must still use tiles 5..6.
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X.............X",
            "X.............X",
            "X.............X",
            "X.............X",
            "X.S...........X",
            "XXXXX##XXXXXXXX"));
        var shot = w.Aim(0, new V2(6.9, 6));
        Assert.Equal(ShotResult.Placed, shot.Result);
        Assert.Equal(5, shot.Portal!.Value.Start);
    }

    [Fact]
    public void PortalsCannotOverlap()
    {
        var w = new World(L(Chamber));
        w.Step(FireA(37.5, 5));
        var a = w.A;
        w.Step(FireB(37.5, 5));        // same spot: slides along the wall to the nearest free span
        Assert.Equal(a, w.A);
        Assert.NotNull(w.B);
        Assert.False(w.A!.Value.Overlaps(w.B!.Value));
        Assert.True(Math.Abs(w.B!.Value.Start - w.A!.Value.Start) <= 3);
    }

    [Fact]
    public void DoorwayPairIsAPureTranslation()
    {
        // Walk right into a left-facing portal on the right wall; come out of a right-facing one on the left wall.
        var w = new World(L(Chamber));
        w.Step(FireA(37.5, 8.5));      // aim low so the 2-tile portal sits on the floor
        w.Step(FireB(1.5, 8.5));
        var ev = Run(w, Right, 300).ToList();
        var tp = Assert.Single(ev.Where(e => e.Kind == SimEventKind.Teleport).Take(1));
        Assert.True(tp.To.X < 4);
        // Still moving right at run speed after the transit.
        Assert.True(w.Vel.X > 10);
    }

    [Fact]
    public void SameFacingWallsKeepVerticalMotion()
    {
        var w = new World(L(Chamber));
        var a = new Portal(-1, 0, 38, 6);   // right wall, facing left
        var b = new Portal(-1, 0, 38, 3);   // same wall, higher, also facing left
        w.A = a; w.B = b;
        w.Pos = new V2(37.6, 7.3); w.Vel = new V2(8, -3);  // moving right and rising, about to hit A
        w.Grounded = false;
        w.Step(InputFrame.None);
        Assert.True(w.TeleportedThisTick);
        Assert.True(w.Vel.X < 0, "comes back out moving left");
        Assert.True(w.Vel.Y < 0, "still rising");
    }

    [Fact]
    public void FallingIntoFloorPortalFlingsOutOfWallPortal()
    {
        // Tall shaft with a panel floor; exit portal on a far wall facing right.
        var lvl = L(
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X.....................................................X",
            "X.S...................................................X",
            "XXXX..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "X##X..................................................X",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
        // Hand-place: floor portal at the bottom of a pit we fall down into, exit on the right face of x=2..3 column.
        var w = new World(lvl);
        w.A = new Portal(0, -1, 16, 4);   // floor at y=16, tiles x 4..5, facing up
        w.B = new Portal(1, 0, 4, 5);     // right face of column x=3 (line x=4), tiles y 5..6, facing right
        w.Pos = new V2(5.0, 3.0); w.Vel = V2.Zero; w.Grounded = false;
        SimEvent? tp = null;
        for (int i = 0; i < 200 && tp == null; i++)
        {
            w.Step(InputFrame.None);
            foreach (var e in w.Events) if (e.Kind == SimEventKind.Teleport) tp = e;
        }
        Assert.NotNull(tp);
        double fall = 16 - (3.0 + Tuning.HalfH);
        double expected = Math.Sqrt(2 * Tuning.Gravity * fall);
        Assert.InRange(w.Vel.X, expected - 1.0, expected + 0.5);
        Assert.True(w.Pos.X > 4);
    }

    [Fact]
    public void FloorCeilingLoopReachesTerminalVelocity()
    {
        var lvl = L(
            "XXXXXXXXXXXXXXXX",
            "X######XXXXXXXXX",
            "X..............X",
            "X..............X",
            "X..............X",
            "X..............X",
            "X.S............X",
            "X######XXXXXXXXX",
            "XXXXXXXXXXXXXXXX");
        var w = new World(lvl);
        w.A = new Portal(0, -1, 7, 2);   // floor under spawn
        w.B = new Portal(0, 1, 2, 2);    // ceiling above
        Steps(w, InputFrame.None, 600);
        Assert.Equal(Tuning.Terminal, w.Vel.Y, 6);
        Assert.False(w.Dead);
    }

    [Fact]
    public void FloorExitAlwaysPopsYouOut()
    {
        var lvl = L(
            "XXXXXXXXXXXXXXXXXXXX",
            "X..................X",
            "X..................X",
            "X..................X",
            "X..................X",
            "X.S................X",
            "X##XXXXXXXXXX##XXXXX",
            "XXXXXXXXXXXXXXXXXXXX");
        var w = new World(lvl);
        w.A = new Portal(0, -1, 6, 1);
        w.B = new Portal(0, -1, 6, 13);
        // Standing on A's span already: the next tick's gravity pushes into it.
        w.Step(InputFrame.None);
        Assert.True(w.TeleportedThisTick);
        Assert.True(w.Vel.Y <= -Tuning.FloorExitMinPop + Tuning.Gravity * Tuning.Dt + 1e-9);
        Assert.Equal(Tuning.FloorExitNudge, Math.Abs(w.Vel.X), 6);
        // It must land beside the exit, not bob forever.
        Steps(w, InputFrame.None, 240);
        Assert.True(w.Grounded);
    }

    [Fact]
    public void GrillWipesPortals()
    {
        var w = new World(L(
            "XXXXXXXXXXXXXXX",
            "X#############X",
            "X#.......:...#X",
            "X#.......:...#X",
            "X#.......:...#X",
            "X#..S....:...#X",
            "X#############X"));
        w.Step(FireA(1.5, 3));
        w.Step(FireB(4.5, 6.5));
        Assert.NotNull(w.A);
        var ev = Run(w, Right, 120).ToList();
        Assert.Contains(ev, e => e.Kind == SimEventKind.PortalsCleared);
        Assert.Null(w.A);
        Assert.Null(w.B);
    }

    [Fact]
    public void SpikesAndPitsKill()
    {
        var w = new World(L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X.S....^^..X",
            "XXXXXXXXXXXX"));
        Steps(w, Right, 120);
        Assert.True(w.Dead);

        var p = new World(L(
            "XXXXXXXXXXXX",
            "X..........X",
            "X..........X",
            "X.S........X",
            "XXXXX..XXXXX"));
        Steps(p, Right, 200);
        Assert.True(p.Dead);
    }
}
