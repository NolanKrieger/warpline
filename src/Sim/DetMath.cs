namespace Warpline.Sim;

/// <summary>
/// Sine and cosine from + − × ÷ only (range reduction + Taylor series in a fixed order),
/// so every platform computes bit-identical results and replays never drift.
/// </summary>
public static class DetMath
{
    public const double Pi = 3.141592653589793;
    public const double Tau = 6.283185307179586;

    /// <summary>sin(2π·turns).</summary>
    public static double SinTurns(double turns) => SinReduced(Reduce(turns));
    /// <summary>cos(2π·turns).</summary>
    public static double CosTurns(double turns) => SinReduced(Reduce(turns + 0.25));

    // turns → angle in [-π, π]
    static double Reduce(double turns)
    {
        double f = turns - Math.Floor(turns);   // [0,1)
        if (f > 0.5) f -= 1.0;                  // (-0.5, 0.5]
        return f * Tau;
    }

    static double SinReduced(double x)
    {
        // Fold into [-π/2, π/2] using sin(π − x) = sin(x).
        if (x > Pi / 2) x = Pi - x;
        else if (x < -Pi / 2) x = -Pi - x;
        double x2 = x * x;
        // Taylor to x^21: error < 1e-17 on [-π/2, π/2].
        double t = 1.0 / 51090942171709440000.0;         // 1/21!
        t = t * x2 - 1.0 / 121645100408832000.0;         // 1/19!
        t = t * x2 + 1.0 / 355687428096000.0;            // 1/17!
        t = t * x2 - 1.0 / 1307674368000.0;              // 1/15!
        t = t * x2 + 1.0 / 6227020800.0;                 // 1/13!
        t = t * x2 - 1.0 / 39916800.0;                   // 1/11!
        t = t * x2 + 1.0 / 362880.0;                     // 1/9!
        t = t * x2 - 1.0 / 5040.0;                       // 1/7!
        t = t * x2 + 1.0 / 120.0;                        // 1/5!
        t = t * x2 - 1.0 / 6.0;                          // 1/3!
        t = t * x2 + 1.0;
        return t * x;
    }
}
