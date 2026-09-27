namespace Warpline.Sim;

/// <summary>Double-precision 2D vector. World units are tiles; +y points down (screen space).</summary>
public readonly record struct V2(double X, double Y)
{
    public static readonly V2 Zero = new(0, 0);
    public static V2 operator +(V2 a, V2 b) => new(a.X + b.X, a.Y + b.Y);
    public static V2 operator -(V2 a, V2 b) => new(a.X - b.X, a.Y - b.Y);
    public static V2 operator -(V2 a) => new(-a.X, -a.Y);
    public static V2 operator *(V2 a, double k) => new(a.X * k, a.Y * k);
    public static V2 operator *(double k, V2 a) => new(a.X * k, a.Y * k);
    public double Dot(V2 o) => X * o.X + Y * o.Y;
    public double Length => Math.Sqrt(X * X + Y * Y);
    public V2 Normalized() { var l = Length; return l < 1e-12 ? Zero : new V2(X / l, Y / l); }
    public override string ToString() => FormattableString.Invariant($"({X:0.###}, {Y:0.###})");
}
