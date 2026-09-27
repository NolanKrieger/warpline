using Warpline.Sim;

namespace Warpline;

/// <summary>
/// One attempt at one level as the game sees it: the player's world, the ghost's world, the recording,
/// and the previous positions used to interpolate rendering between 120 Hz ticks.
/// The level timer is World.Tick: the world only steps once the first input arrives.
/// </summary>
public sealed class Session
{
    public LevelInfo Info { get; }
    public World World { get; private set; }
    public World? Ghost { get; private set; }
    public string GhostLabel { get; private set; } = "";
    public bool Started { get; private set; }
    public List<InputFrame> Recorded { get; } = new();
    public V2 PrevPos, PrevGhostPos;
    public bool Dead => World.Dead;
    public bool Finished => World.Finished;
    InputFrame[] _ghostFrames = Array.Empty<InputFrame>();
    int _ghostIndex;

    public Session(LevelInfo info, string ghostMode)
    {
        Info = info;
        World = new World(info.Level);
        Reset(ghostMode);
    }

    public void Reset(string ghostMode)
    {
        World = new World(Info.Level);
        Started = false;
        Recorded.Clear();
        PrevPos = World.Pos;
        Ghost = null; GhostLabel = "";
        _ghostFrames = Array.Empty<InputFrame>();
        _ghostIndex = 0;
        var pb = Store.Record(Info.Id);
        if (ghostMode == "pb" && pb != null && pb.PbRoute.Length > 0)
        {
            _ghostFrames = Route.Parse(pb.PbRoute).ToArray();
            GhostLabel = "PB";
        }
        else if (ghostMode != "off" && Info.DevTicks > 0)
        {
            _ghostFrames = Info.DevRoute.ToArray();
            GhostLabel = "DEV";
        }
        if (_ghostFrames.Length > 0)
        {
            Ghost = new World(Info.Level);
            PrevGhostPos = Ghost.Pos;
        }
    }

    /// <summary>Advance one 120 Hz tick. Returns false while waiting for the first input.</summary>
    public bool Tick(InputFrame f)
    {
        if (World.Dead || World.Finished) return false;
        if (!Started)
        {
            if (!f.Any) return false;
            Started = true;
        }
        PrevPos = World.Pos;
        World.Step(f);
        Recorded.Add(f);
        if (World.TeleportedThisTick) PrevPos = World.Pos;

        if (Ghost != null)
        {
            PrevGhostPos = Ghost.Pos;
            if (_ghostIndex < _ghostFrames.Length) Ghost.Step(_ghostFrames[_ghostIndex++]);
            if (Ghost.TeleportedThisTick) PrevGhostPos = Ghost.Pos;
        }
        return true;
    }

    public string RecordedRoute() => Route.FromFrames(Recorded).Serialize();
}
