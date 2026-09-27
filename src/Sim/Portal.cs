namespace Warpline.Sim;

/// <summary>
/// A portal lies on one face of the tile grid, or on a face of a moving panel.
/// (Nx, Ny) is the face normal pointing out into open space. Line is the face's grid line
/// (x for vertical faces, y for horizontal ones). The portal covers tangent cells [Start, Start + Length).
/// Mover = -1 for the static grid; otherwise the index of the moving panel, and Line/Start are measured
/// at that panel's rest position — its world position is that plus the panel's current offset
/// (use World.PortalCenter / World.PortalEnds, not Center / EndA / EndB, for world coordinates).
/// </summary>
public readonly record struct Portal(int Nx, int Ny, int Line, int Start, int Mover = -1)
{
    public bool OnMover => Mover >= 0;
    public bool Vertical => Nx != 0;
    public V2 N => new(Nx, Ny);

    /// <summary>
    /// The fixed tangent used for transit: world up on walls, world right on floors and ceilings.
    /// Exit velocity = (v·−n_in)·n_out + (v·t_in)·t_out, so rising stays rising between wall portals,
    /// sideways drift is kept between floor/ceiling portals, and doorway pairs are a pure translation.
    /// </summary>
    public V2 T => Vertical ? new V2(0, -1) : new V2(1, 0);

    public V2 Center => Vertical
        ? new V2(Line, Start + Tuning.PortalLength / 2.0)
        : new V2(Start + Tuning.PortalLength / 2.0, Line);

    public V2 EndA => Vertical ? new V2(Line, Start) : new V2(Start, Line);
    public V2 EndB => Vertical ? new V2(Line, Start + Tuning.PortalLength) : new V2(Start + Tuning.PortalLength, Line);

    public bool SameFace(Portal o) => Nx == o.Nx && Ny == o.Ny && Line == o.Line && Mover == o.Mover;
    public bool Overlaps(Portal o) => SameFace(o) && Math.Abs(Start - o.Start) < Tuning.PortalLength;

    /// <summary>Does a body spanning [lo, hi] along this portal's tangent axis fit through it?</summary>
    public bool Fits(double lo, double hi) =>
        lo >= Start - Tuning.PortalFit - 1e-9 && hi <= Start + Tuning.PortalLength + Tuning.PortalFit + 1e-9;
}
