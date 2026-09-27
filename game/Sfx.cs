using Godot;

namespace Warpline;

/// <summary>Small synthesized sound set, generated at startup (no audio files needed yet).</summary>
public partial class Sfx : Node
{
    const int Rate = 44100;
    readonly Dictionary<string, AudioStreamWav> _sounds = new();
    readonly List<AudioStreamPlayer> _pool = new();
    int _next;
    public static Sfx? I { get; private set; }

    public override void _Ready()
    {
        I = this;
        var rng = new Random(11);
        double Noise() => rng.NextDouble() * 2 - 1;
        double Env(double t, double d, double k = 6) => Math.Max(0, 1 - t / d) * Math.Exp(-k * t / d);
        double Sq(double ph) => Math.Sin(ph) >= 0 ? 1 : -1;

        _sounds["jump"] = Make(0.09, t => 0.5 * Sq(2 * Math.PI * (320 * t + 2600 * t * t)) * Env(t, 0.09, 3));
        _sounds["walljump"] = Make(0.1, t => 0.5 * Sq(2 * Math.PI * (420 * t + 2600 * t * t)) * Env(t, 0.1, 3));
        double lp = 0;
        _sounds["land"] = Make(0.07, t => { lp += (Noise() - lp) * 0.08; return 2.2 * lp * Env(t, 0.07, 4); });
        _sounds["shootA"] = Make(0.16, t => (0.6 * Math.Sin(2 * Math.PI * (1500 * t - 2600 * t * t)) + 0.1 * Noise()) * Env(t, 0.16, 4));
        _sounds["shootB"] = Make(0.16, t => (0.6 * Math.Sin(2 * Math.PI * (1050 * t - 1800 * t * t)) + 0.1 * Noise()) * Env(t, 0.16, 4));
        double bp = 0;
        _sounds["fizzle"] = Make(0.14, t => { bp += (Noise() - bp) * 0.35; return 0.9 * bp * Env(t, 0.14, 5); });
        _sounds["teleport"] = Make(0.2, t => (0.45 * Math.Sin(2 * Math.PI * (180 * t + 2400 * t * t)) + 0.15 * Noise() * Env(t, 0.05)) * Env(t, 0.2, 3));
        double dl = 0;
        _sounds["death"] = Make(0.35, t => { dl += (Noise() - dl) * 0.12; return (0.8 * dl + 0.35 * Sq(2 * Math.PI * (130 * t - 90 * t * t))) * Env(t, 0.35, 4); });
        _sounds["wipe"] = Make(0.2, t => 0.35 * Sq(2 * Math.PI * 70 * t) * Math.Sin(2 * Math.PI * 25 * t) * Env(t, 0.2, 2));
        _sounds["finish"] = Make(0.55, t =>
        {
            double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
            int n = Math.Min(3, (int)(t / 0.075));
            double tn = t - n * 0.075;
            return 0.45 * Math.Sin(2 * Math.PI * notes[n] * t) * Math.Exp(-tn * 9) * (t < 0.52 ? 1 : 0);
        });
        _sounds["pb"] = Make(0.7, t =>
        {
            double[] notes = { 659.25, 783.99, 987.77, 1318.5, 1567.98 };
            int n = Math.Min(4, (int)(t / 0.07));
            return 0.35 * (Math.Sin(2 * Math.PI * notes[n] * t) + 0.3 * Math.Sin(4 * Math.PI * notes[n] * t)) * Math.Exp(-(t - n * 0.07) * 7);
        });
        _sounds["tick"] = Make(0.03, t => 0.4 * Math.Sin(2 * Math.PI * 1800 * t) * Env(t, 0.03));
        _sounds["button"] = Make(0.08, t => 0.5 * (Sq(2 * Math.PI * 880 * t) * (t < 0.03 ? 1 : 0) + Sq(2 * Math.PI * 1320 * t) * (t >= 0.03 ? 1 : 0)) * Env(t, 0.08, 2));
        double dr = 0;
        _sounds["door"] = Make(0.22, t => { dr += (Noise() - dr) * 0.05; return (1.6 * dr + 0.25 * Math.Sin(2 * Math.PI * (90 + 160 * t) * t)) * Env(t, 0.22, 2.5); });
        _sounds["bounce"] = Make(0.22, t => 0.5 * Math.Sin(2 * Math.PI * (180 * t + 1400 * t * t) + 3 * Math.Sin(2 * Math.PI * 30 * t)) * Env(t, 0.22, 3));
        _sounds["pew"] = Make(0.09, t => 0.45 * Sq(2 * Math.PI * (1200 * t - 5000 * t * t)) * Env(t, 0.09, 4));

        if (AudioServer.GetBusIndex("SFX") < 0)
        {
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, "SFX");
            AudioServer.SetBusSend(idx, "Master");
        }
        ApplyVolumes();
        for (int i = 0; i < 12; i++)
        {
            var p = new AudioStreamPlayer { VolumeDb = -9, Bus = "SFX" };
            AddChild(p);
            _pool.Add(p);
        }
    }

    public static void ApplyVolumes()
    {
        var s = Store.Data.Settings;
        static float Db(int v) => v <= 0 ? -80f : Mathf.LinearToDb(v / 100f);
        AudioServer.SetBusVolumeDb(0, Db(s.MasterVolume));
        AudioServer.SetBusMute(0, s.MasterVolume <= 0);
        int sfx = AudioServer.GetBusIndex("SFX");
        if (sfx >= 0) { AudioServer.SetBusVolumeDb(sfx, Db(s.SfxVolume)); AudioServer.SetBusMute(sfx, s.SfxVolume <= 0); }
    }

    public void Play(string name, float pitch = 1f, float db = 0f)
    {
        if (!_sounds.TryGetValue(name, out var s)) return;
        var p = _pool[_next++ % _pool.Count];
        p.Stream = s;
        p.PitchScale = pitch;
        p.VolumeDb = -9 + db;
        p.Play();
    }

    static AudioStreamWav Make(double dur, Func<double, double> f)
    {
        int n = (int)(dur * Rate);
        var data = new byte[n * 2];
        for (int i = 0; i < n; i++)
        {
            double v = Math.Clamp(f(i / (double)Rate), -1, 1);
            short s = (short)(v * 32000);
            data[2 * i] = (byte)(s & 0xff);
            data[2 * i + 1] = (byte)((s >> 8) & 0xff);
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = false, Data = data };
    }
}
