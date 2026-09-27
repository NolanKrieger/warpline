namespace Warpline.Sim;

/// <summary>
/// One tick of player input. Aim is a world point (tiles) and only matters on ticks that fire.
/// The game quantizes Aim to 1/64 tile so recorded routes replay bit-for-bit.
/// </summary>
public readonly record struct InputFrame(bool Left, bool Right, bool Jump, bool Down, bool FireA, bool FireB, V2 Aim)
{
    public static readonly InputFrame None = default;

    public bool Any => Left || Right || Jump || Down || FireA || FireB;
    public bool Fires => FireA || FireB;
    public bool SameHeld(InputFrame o) => Left == o.Left && Right == o.Right && Jump == o.Jump && Down == o.Down;

    public static double Quantize(double v) => Math.Round(v * 64.0) / 64.0;
    public static V2 Quantize(V2 p) => new(Quantize(p.X), Quantize(p.Y));
}
