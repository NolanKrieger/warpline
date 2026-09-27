using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class MachineTests
{
    static Level LM(string objects, params string[] rows) =>
        Level.Parse("m", "name: m\n---\n" + string.Join("\n", rows) + "\n---\n" + objects);

    static readonly string[] Corridor =
    {
        "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
        "X############################X",
        "X#..........................#X",
        "X#..........................#X",
        "X#..........................#X",
        "X#..........................#X",
        "X#.S........................#X",
        "X############################X",
    };

    [Fact]
    public void ParsesObjects()
    {
        var l = LM("door d1 x=10 y=3 w=1 h=4\nbutton b1 x=6 y=6 opens=d1 mode=timed time=2.5\nlaser l1 x=20 y=2 dir=down period=2 on=1\nreceiver r1 x=25 y=2 opens=d1\nturret t1 x=26 y=6 dir=left period=1.5 speed=20", Corridor);
        var m = l.Machines;
        Assert.Single(m.Doors); Assert.Single(m.Buttons); Assert.Single(m.Lasers); Assert.Single(m.Receivers); Assert.Single(m.Turrets);
        Assert.Equal(300, m.Buttons[0].TimeTicks);
        Assert.Equal(240, m.Lasers[0].Period);
        Assert.Equal(0, l.DoorAt(10, 5));
        Assert.Equal(-1, l.DoorAt(11, 5));
        Assert.Throws<FormatException>(() => LM("button b1 x=6 y=6 opens=nope", Corridor));
    }

    [Fact]
    public void ClosedDoorBlocksAndLatchOpensItForGood()
    {
        var w = new World(LM("door d1 x=12 y=2 w=1 h=5\nbutton b1 x=8 y=6 opens=d1 mode=latch", Corridor));
        Assert.True(w.Solid(12, 4));
        Steps(w, Right, 200);
        Assert.True(w.DoorOpen[0]);
        Assert.True(w.Pos.X > 14, $"walked through the opened door, x={w.Pos.X}");

        var blocked = new World(LM("door d1 x=5 y=2 w=1 h=5\nbutton b1 x=20 y=6 opens=d1", Corridor));
        Steps(blocked, Right, 200);
        Assert.True(blocked.Pos.X < 5, "a closed door is a wall");
    }

    [Fact]
    public void TimedDoorClosesAfterItsTimeButNeverOnTheRobot()
    {
        var w = new World(LM("door d1 x=26 y=2 w=1 h=5\nbutton b1 x=5 y=6 opens=d1 mode=timed time=0.5", Corridor));
        for (int i = 0; i < 60 && !w.ButtonDown[0]; i++) w.Step(Right);   // touch the button on the way past
        Assert.True(w.DoorOpen[0]);
        Steps(w, Right, 50);                 // well past it
        Steps(w, InputFrame.None, 90);       // timer runs out
        Assert.False(w.DoorOpen[0]);

        var inside = new World(LM("door d1 x=5 y=2 w=2 h=5\nbutton b1 x=4 y=6 opens=d1 mode=timed time=0.1", Corridor));
        Steps(inside, Right, 8);             // onto the button and into the doorway
        inside.Pos = new V2(6.0, inside.Pos.Y);
        Steps(inside, InputFrame.None, 60);
        Assert.True(inside.DoorOpen[0], "door waits for the robot to leave");
    }

    [Fact]
    public void HoldButtonOnlyWhilePressed()
    {
        var w = new World(LM("door d1 x=20 y=2 w=1 h=5\nbutton b1 x=3 y=6 opens=d1 mode=hold", Corridor));
        w.Step(InputFrame.None);
        Assert.True(w.DoorOpen[0]);          // standing on it at spawn
        Steps(w, Right, 30);
        Assert.False(w.DoorOpen[0]);
    }

    [Fact]
    public void LaserKillsWhenOnAndIsSafeWhenOff()
    {
        // Laser fires down across the corridor at x=8.
        var on = new World(LM("laser l1 x=8 y=2 dir=down", Corridor));
        Steps(on, Right, 120);
        Assert.True(on.Dead);

        var off = new World(LM("laser l1 x=8 y=2 dir=down period=10 on=0.001 phase=0.5", Corridor));
        Steps(off, Right, 120);
        Assert.False(off.Dead);
        Assert.True(off.Pos.X > 9);
    }

    [Fact]
    public void BeamThroughPortalsLightsTheReceiverAndOpensTheDoor()
    {
        // Emitter at the top fires down onto the floor; a floor portal sends it out of a wall portal toward the receiver.
        var lvl = LM("laser l1 x=6 y=2 dir=down\nreceiver r1 x=25 y=2 opens=d1\ndoor d1 x=27 y=2 w=1 h=5",
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X############################X",
            "X#..........................#X",
            "X#..........................#X",
            "X#..........................#X",
            "X#..........................#X",
            "X#.S........................#X",
            "X############################X");
        var w = new World(lvl);
        w.A = new Portal(0, -1, 7, 5);       // floor under the beam (x 5..7)
        w.B = new Portal(1, 0, 2, 2);        // left wall, facing right, rows 2..4
        w.Pos = new V2(15, 6.3);             // stand clear of the beam
        w.Step(InputFrame.None);
        w.Step(InputFrame.None);
        Assert.True(w.ReceiverLit[0]);
        Assert.True(w.DoorOpen[0]);
        Assert.True(w.Beams[0].Count >= 2);
    }

    [Fact]
    public void ClosedDoorsStopShotsAndClosingEvictsANearbyPortal()
    {
        var w = new World(LM("door d1 x=10 y=2 w=1 h=5\nbutton b1 x=3 y=6 opens=d1 mode=hold", Corridor));
        w.Step(InputFrame.None);                                   // on the button: door open
        Assert.True(w.DoorOpen[0]);
        w.Step(FireA(28.5, 6.5));                                   // shoot through the open doorway to the far wall
        Assert.NotNull(w.A);
        Steps(w, Right, 40);                                        // leave the button: door closes
        Assert.False(w.DoorOpen[0]);
        Assert.Equal(ShotResult.NotPortalable, w.Aim(0, new V2(28.5, 6.5)).Result);

        // A portal whose clearance runs through the door cells is lost when the door closes.
        var v = new World(LM("door d1 x=27 y=2 w=1 h=5\nbutton b1 x=3 y=6 opens=d1 mode=hold", Corridor));
        v.Step(InputFrame.None);
        v.B = new Portal(-1, 0, 28, 5);                             // right wall facing left: its clearance is x 27
        Steps(v, Right, 40);
        Assert.Null(v.B);
    }

    [Fact]
    public void TurretBulletsKillAndTravelThroughPortals()
    {
        var w = new World(LM("turret t1 x=26 y=6 dir=left period=5 speed=20", Corridor));
        Steps(w, InputFrame.None, 240);
        Assert.True(w.Dead);

        var p = new World(LM("turret t1 x=26 y=6 dir=left period=100 speed=20", Corridor));
        p.A = new Portal(1, 0, 2, 5);        // left wall at the bullet's height, facing right
        p.B = new Portal(0, 1, 2, 10);       // ceiling, facing down
        p.Pos = new V2(27.5, 2.7);           // behind the turret, out of the line of fire
        p.Grounded = false;
        int teleported = 0;
        for (int i = 0; i < 400; i++)
        {
            p.Step(InputFrame.None);
            if (p.Bullets.Count > 0 && p.Bullets[0].V.Y > 0) teleported++;
        }
        Assert.True(teleported > 0, "bullet came out of the ceiling portal moving down");
    }

    [Fact]
    public void MachineLevelsReplayDeterministically()
    {
        var lvl = LM("door d1 x=12 y=2 w=1 h=5\nbutton b1 x=8 y=6 opens=d1 mode=timed time=1\nlaser l1 x=18 y=2 dir=down period=1.5 on=0.5\nturret t1 x=26 y=4 dir=left period=0.7", Corridor);
        var rng = new Random(3);
        var frames = Enumerable.Range(0, 1200).Select(_ => new InputFrame(rng.Next(3) == 0, rng.Next(2) == 0, rng.Next(5) == 0, false,
            rng.Next(60) == 0, rng.Next(60) == 0, InputFrame.Quantize(new V2(rng.NextDouble() * 30, rng.NextDouble() * 8)))).ToList();
        frames = frames.Select(f => f.FireA && f.FireB ? f with { FireB = false } : f).ToList();
        var a = new World(lvl); foreach (var f in frames) a.Step(f);
        var b = new World(lvl); foreach (var f in Route.Parse(Route.FromFrames(frames).Serialize()).Frames()) b.Step(f);
        Assert.Equal(a.StateHash(), b.StateHash());
    }
}
