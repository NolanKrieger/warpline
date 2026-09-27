using System.Globalization;
using System.Text;

namespace Warpline.Sim;

/// <summary>
/// A run-length list of inputs: the format for developer routes, PB ghosts and replays.
/// One line per segment: <c>ticks [L] [R] [J] [D] [A:x,y] [B:x,y]</c>.
/// Held keys apply to every tick of the segment; a shot (A = cyan, B = magenta) fires on its first tick only.
/// '#' starts a comment. Example: <c>40 R</c> runs right for 40 ticks; <c>1 A:12.5,20</c> fires cyan at (12.5, 20).
/// </summary>
public sealed class Route
{
    public readonly record struct Segment(int Ticks, InputFrame Frame);

    public List<Segment> Segments { get; } = new();
    public int TotalTicks => Segments.Sum(s => s.Ticks);

    public IEnumerable<InputFrame> Frames()
    {
        foreach (var s in Segments)
            for (int i = 0; i < s.Ticks; i++)
                yield return i == 0 ? s.Frame : s.Frame with { FireA = false, FireB = false };
    }

    public InputFrame[] ToArray() => Frames().ToArray();

    public static Route Parse(string text)
    {
        var r = new Route();
        int lineNo = 0;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            lineNo++;
            var line = raw;
            int hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            var tok = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tok.Length == 0) continue;
            if (!int.TryParse(tok[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int ticks) || ticks <= 0)
                throw new FormatException($"route line {lineNo}: '{raw}' must start with a positive tick count");
            bool l = false, rr = false, j = false, d = false, fa = false, fb = false;
            V2 aim = V2.Zero;
            foreach (var t in tok.Skip(1))
            {
                switch (t)
                {
                    case "L": l = true; break;
                    case "R": rr = true; break;
                    case "J": j = true; break;
                    case "D": d = true; break;
                    default:
                        if ((t.StartsWith("A:") || t.StartsWith("B:")) && t.Length > 2)
                        {
                            var xy = t[2..].Split(',');
                            if (xy.Length != 2) throw new FormatException($"route line {lineNo}: bad shot '{t}'");
                            aim = new V2(double.Parse(xy[0], CultureInfo.InvariantCulture), double.Parse(xy[1], CultureInfo.InvariantCulture));
                            if (t[0] == 'A') fa = true; else fb = true;
                        }
                        else throw new FormatException($"route line {lineNo}: unknown token '{t}'");
                        break;
                }
            }
            if (fa && fb) throw new FormatException($"route line {lineNo}: one shot per segment");
            r.Segments.Add(new Segment(ticks, new InputFrame(l, rr, j, d, fa, fb, aim)));
        }
        return r;
    }

    public static Route FromFrames(IEnumerable<InputFrame> frames)
    {
        var r = new Route();
        foreach (var f in frames)
        {
            if (!f.Fires && r.Segments.Count > 0 && r.Segments[^1].Frame.SameHeld(f))
            {
                var last = r.Segments[^1];
                r.Segments[^1] = last with { Ticks = last.Ticks + 1 };
            }
            else
            {
                var clean = f.Fires ? f with { Aim = InputFrame.Quantize(f.Aim) } : f with { Aim = V2.Zero };
                r.Segments.Add(new Segment(1, clean));
            }
        }
        return r;
    }

    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var s in Segments)
        {
            sb.Append(s.Ticks.ToString(CultureInfo.InvariantCulture));
            var f = s.Frame;
            if (f.Left) sb.Append(" L");
            if (f.Right) sb.Append(" R");
            if (f.Jump) sb.Append(" J");
            if (f.Down) sb.Append(" D");
            if (f.FireA || f.FireB)
                sb.Append(' ').Append(f.FireA ? 'A' : 'B').Append(':')
                  .Append(f.Aim.X.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                  .Append(f.Aim.Y.ToString("R", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
