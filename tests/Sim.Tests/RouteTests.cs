using Warpline.Sim;
using static Warpline.Tests.H;

namespace Warpline.Tests;

public class RouteTests
{
    [Fact]
    public void RoundTripsThroughText()
    {
        var text = "40 R\n1 A:12.5,20.015625\n12 R J\n3 L D\n1 B:3,4\n";
        var r = Route.Parse(text);
        Assert.Equal(57, r.TotalTicks);
        Assert.Equal(text, r.Serialize());
        var frames = r.ToArray();
        Assert.True(frames[40].FireA);
        Assert.False(frames[41].FireA);
    }

    [Fact]
    public void RecordedFramesReplayToTheSameState()
    {
        // A messy, input-heavy session: running, jumping, crouching, shooting.
        var lvl = L(
            "XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX",
            "X######################################X",
            "X#....................................#X",
            "X#....................................#X",
            "X#....................................#X",
            "X#..........XXXX......................#X",
            "X#....................................#X",
            "X#.S..................................#X",
            "X######################################X");
        var rng = new Random(7);
        var frames = new List<InputFrame>();
        for (int i = 0; i < 1500; i++)
        {
            bool fire = rng.Next(40) == 0;
            frames.Add(new InputFrame(rng.Next(3) == 0, rng.Next(2) == 0, rng.Next(4) == 0, rng.Next(6) == 0,
                fire && rng.Next(2) == 0, false,
                fire ? InputFrame.Quantize(new V2(rng.NextDouble() * 40, rng.NextDouble() * 9)) : V2.Zero));
        }
        var w1 = new World(lvl);
        foreach (var f in frames) w1.Step(f);

        var replay = Route.Parse(Route.FromFrames(frames).Serialize()).ToArray();
        Assert.Equal(frames.Count, replay.Length);
        var w2 = new World(lvl);
        foreach (var f in replay) w2.Step(f);
        Assert.Equal(w1.StateHash(), w2.StateHash());
    }
}
