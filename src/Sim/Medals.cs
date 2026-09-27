namespace Warpline.Sim;

public enum Medal { None, Bronze, Silver, Gold, Dev }

/// <summary>Medal thresholds relative to the developer route's time (GDD §8.6, proposal numbers).</summary>
public static class Medals
{
    public const double GoldFactor = 1.15;
    public const double SilverFactor = 1.4;

    public static int GoldTicks(int dev) => (int)Math.Floor(dev * GoldFactor);
    public static int SilverTicks(int dev) => (int)Math.Floor(dev * SilverFactor);

    public static Medal For(int ticks, int dev)
    {
        if (ticks <= 0) return Medal.None;
        if (dev <= 0) return Medal.Bronze;
        if (ticks <= dev) return Medal.Dev;
        if (ticks <= GoldTicks(dev)) return Medal.Gold;
        if (ticks <= SilverTicks(dev)) return Medal.Silver;
        return Medal.Bronze;
    }

    /// <summary>The next time to beat for a better medal, or 0 if the dev medal is already held.</summary>
    public static int NextTarget(int ticks, int dev) => For(ticks, dev) switch
    {
        Medal.None or Medal.Bronze => SilverTicks(dev),
        Medal.Silver => GoldTicks(dev),
        Medal.Gold => dev,
        _ => 0,
    };
}
