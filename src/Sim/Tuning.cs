namespace Warpline.Sim;

/// <summary>
/// Every movement and portal number in one place. Units: tiles and seconds.
/// The sim steps at a fixed 120 ticks per second; that tick is also the speedrun timer's resolution.
/// </summary>
public static class Tuning
{
    public const int TicksPerSecond = 120;
    public const double Dt = 1.0 / TicksPerSecond;

    // Body (half extents of the axis-aligned hitbox)
    public const double HalfW = 0.35;
    public const double HalfH = 0.7;
    public const double CrouchHalfH = 0.35;

    // Ground
    public const double MaxRun = 13;
    public const double GroundAccel = 110;
    public const double GroundFriction = 130;
    public const double TurnAccel = 180;
    public const double OverMaxDecay = 50;     // fling speed above MaxRun bleeds off this slowly on the ground
    public const double CrouchMax = 5;         // crouch-walk speed; crouching faster than this is a slide
    public const double SlideFriction = 8;     // sliding keeps momentum

    // Air
    public const double Gravity = 70;
    public const double Terminal = 60;
    public const double AirAccel = 60;         // only pushes toward MaxRun; never slows a fling you steer with
    public const double JumpVelocity = 21;     // ~3.1 tile jump
    public const double JumpCut = 0.5;         // releasing jump early while rising from a jump
    public const int CoyoteTicks = 10;
    public const int JumpBufferTicks = 12;

    // Walls
    public const double WallSlideMax = 6;
    public const double WallJumpX = 11;
    public const double WallJumpY = 19;
    public const int WallJumpLockTicks = 10;   // input back toward the wall is ignored this long after a wall jump
    public const double WallProbe = 0.05;

    // Portals
    public const int PortalLength = 2;         // tiles along the surface
    public const int PortalClearance = 2;      // empty tiles required in front of a floor or ceiling portal
    public const int PortalClearanceWall = 1;  // ...and in front of a wall portal (the robot is under a tile wide)
    public const int PortalBump = 3;           // a shot slides up to this many tiles along the surface to find room
    public const double PortalFit = 0.2;       // how far the body may hang past a portal edge and still go in
    public const double MaxShotRange = 64;
    public const double FloorExitMinPop = 10;  // a floor portal always throws you up at least this fast
    public const double FloorExitNudge = 4;    // ...and sideways this fast if you had almost no sideways speed

    // Gels
    public const double SpeedGelMax = 30;       // top running speed on speed gel
    public const double SpeedGelAccel = 45;
    public const double SpeedGelFriction = 18;  // slippery: hard to stop
    public const double SpeedGelTurn = 90;
    public const double BounceMin = 17;         // bounce gel always throws you at least this fast (~2 tiles)
    public const double BounceJumpBoost = 6;    // jumping as you land on bounce gel adds this
    public const double BounceJumpMin = 26;     // ...and gives at least this (~4.8 tiles)
    public const double BounceWallMin = 3;      // slower than this into a bounce wall just stops

    // Integration / misc
    public const double MaxStep = 0.2;         // max distance per collision substep (no tunnelling)
    public const double SpikeInset = 0.15;     // spikes use a slightly smaller hitbox (forgiving)
    public const double MaxSpeed = 90;
}
