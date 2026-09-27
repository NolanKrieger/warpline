using Godot;

namespace Warpline;

/// <summary>
/// Original procedural "driving electronic" music, synthesized at startup (off the main thread) into seamless loops:
/// kick, clap, hats, side-chained bass, arpeggio lead, pad, stereo delay. No samples, no licences.
/// Tracks: menu, act1 (levels 1–4), act2 (5–10), act3 (11+).
/// </summary>
public partial class Music : Node
{
    public static Music? I { get; private set; }
    const int Rate = 44100;

    sealed record Track(string Name, double Bpm, int Root, int[] Chords, int Bars, double Drive, bool Kick, int Seed);

    // Chords are scale degrees (0-based) in natural minor.
    static readonly Track[] Tracks =
    {
        new("menu", 104, 57, new[] { 0, 5, 3, 4 }, 16, 0.25, false, 3),
        new("act1", 140, 57, new[] { 0, 5, 2, 6 }, 16, 0.6, true, 11),
        new("act2", 150, 62, new[] { 0, 3, 5, 4 }, 16, 0.8, true, 23),
        new("act3", 160, 64, new[] { 0, 6, 5, 4 }, 16, 1.0, true, 37),
    };

    readonly Dictionary<string, AudioStreamWav> _streams = new();
    readonly Dictionary<string, Task<byte[]>> _rendering = new();
    AudioStreamPlayer _a = null!, _b = null!;
    string _want = "", _playing = "";
    float _fade = 1;

    public override void _Ready()
    {
        I = this;
        if (AudioServer.GetBusIndex("Music") < 0)
        {
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, "Music");
            AudioServer.SetBusSend(idx, "Master");
        }
        ApplyVolume();
        _a = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 };
        _b = new AudioStreamPlayer { Bus = "Music", VolumeDb = -80 };
        AddChild(_a); AddChild(_b);
        foreach (var t in Tracks) { var tt = t; _rendering[t.Name] = Task.Run(() => Render(tt)); }
    }

    public static void ApplyVolume()
    {
        int idx = AudioServer.GetBusIndex("Music");
        if (idx < 0) return;
        int v = Store.Data.Settings.MusicVolume;
        AudioServer.SetBusVolumeDb(idx, v <= 0 ? -80f : Mathf.LinearToDb(v / 100f) - 6f);
        AudioServer.SetBusMute(idx, v <= 0);
    }

    /// <summary>Crossfade to a track (by name). Levels pick their act by index.</summary>
    public void Play(string name) => _want = name;

    public static string ForLevel(int index) => index < 0 ? "act3" : index < 4 ? "act1" : index < 10 ? "act2" : "act3";

    public override void _Process(double delta)
    {
        foreach (var (name, task) in _rendering.ToList())
            if (task.IsCompleted)
            {
                _rendering.Remove(name);
                if (task.IsFaulted) { GD.PushWarning($"music {name} failed: {task.Exception?.GetBaseException().Message}"); continue; }
                var data = task.Result;
                _streams[name] = new AudioStreamWav
                {
                    Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = Rate, Stereo = true, Data = data,
                    LoopMode = AudioStreamWav.LoopModeEnum.Forward, LoopBegin = 0, LoopEnd = data.Length / 4,
                };
            }
        if (_want != _playing && _streams.TryGetValue(_want, out var s))
        {
            (_a, _b) = (_b, _a);            // _a = incoming
            _a.Stream = s;
            _a.VolumeDb = -40;
            _a.Play();
            _playing = _want;
            _fade = 0;
        }
        if (_fade < 1)
        {
            _fade = Math.Min(1, _fade + (float)delta / 1.2f);
            _a.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, _fade));
            _b.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, 1 - _fade));
            if (_fade >= 1) _b.Stop();
        }
    }

    // ---------------------------------------------------------------- synthesis

    static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    static double Hz(int midi) => 440.0 * Math.Pow(2, (midi - 69) / 12.0);
    static int Degree(int root, int deg) => root + Minor[((deg % 7) + 7) % 7] + 12 * (int)Math.Floor(deg / 7.0);

    static byte[] Render(Track t)
    {
        double beat = 60.0 / t.Bpm, bar = beat * 4, len = bar * t.Bars;
        int n = (int)(len * Rate);
        var L = new float[n]; var R = new float[n];
        var rng = new Random(t.Seed);
        double step = beat / 4;                    // 16th note
        int steps = t.Bars * 16;

        // kick envelope for side-chain ducking
        var duck = new float[n];
        for (int i = 0; i < n; i++)
        {
            double tb = (i / (double)Rate) % beat;
            duck[i] = t.Kick ? (float)(0.35 + 0.65 * Math.Min(1, tb / (beat * 0.45))) : 1f;
        }

        void Add(float[] buf, int start, double[] sig, double gain)
        {
            for (int i = 0; i < sig.Length; i++) buf[(start + i) % n] += (float)(sig[i] * gain);
        }

        // --- drums
        for (int s = 0; s < steps; s++)
        {
            int i0 = (int)(s * step * Rate);
            int inBar = s % 16, barNo = s / 16;
            bool fill = barNo % 8 == 7 && inBar >= 12;
            if (t.Kick && (inBar % 4 == 0 || (fill && inBar % 2 == 0)))
            {
                var k = new double[(int)(0.28 * Rate)];
                double ph = 0;
                for (int i = 0; i < k.Length; i++)
                {
                    double tt = i / (double)Rate;
                    ph += 2 * Math.PI * (45 + 110 * Math.Exp(-tt * 38)) / Rate;
                    k[i] = Math.Sin(ph) * Math.Exp(-tt * 11) * (1 + 0.6 * t.Drive) * Math.Min(1, (k.Length - i) / (0.01 * Rate));
                    k[i] = Math.Tanh(k[i] * 1.4);
                }
                Add(L, i0, k, 0.55); Add(R, i0, k, 0.55);
            }
            if (inBar % 8 == 4 && (t.Kick || barNo % 2 == 1))
            {
                var c = new double[(int)(0.22 * Rate)];
                double lp = 0, lp2 = 0;
                for (int i = 0; i < c.Length; i++)
                {
                    double tt = i / (double)Rate, x = rng.NextDouble() * 2 - 1;
                    lp += (x - lp) * 0.5; lp2 += (lp - lp2) * 0.08;
                    c[i] = ((lp - lp2) * 1.4 + 0.25 * Math.Sin(2 * Math.PI * 190 * tt) * Math.Exp(-tt * 30)) * Math.Exp(-tt * 18);
                }
                Add(L, i0, c, 0.34); Add(R, i0 + 90, c, 0.3);
            }
            bool hat = t.Kick ? true : inBar % 2 == 0;
            if (hat)
            {
                bool open = inBar % 4 == 2;
                var h = new double[(int)((open ? 0.16 : 0.04) * Rate)];
                double prev = 0;
                for (int i = 0; i < h.Length; i++)
                {
                    double tt = i / (double)Rate, x = rng.NextDouble() * 2 - 1;
                    h[i] = (x - prev) * Math.Exp(-tt * (open ? 22 : 90));
                    prev = x;
                }
                double g = (inBar % 4 == 0 ? 0.065 : 0.12) * (0.6 + 0.4 * t.Drive);
                Add(L, i0, h, g * 1.2); Add(R, i0, h, g * 0.8);
            }
        }

        // --- bass: 8ths on the chord root, octave pops, side-chained
        {
            double ph = 0, lp = 0;
            for (int i = 0; i < n; i++)
            {
                double tt = i / (double)Rate;
                int barNo = (int)(tt / bar);
                int chord = t.Chords[(barNo / 2) % t.Chords.Length];
                int e8 = (int)(tt / (beat / 2));
                int note = Degree(t.Root - 24, chord) + (e8 % 4 == 3 ? 12 : 0);
                double te = tt % (beat / 2);
                ph += Hz(note) / Rate;
                double saw = 2 * (ph - Math.Floor(ph + 0.5));
                double cutoff = 0.04 + 0.22 * Math.Exp(-te * 9) * (0.5 + t.Drive);
                lp += (saw - lp) * cutoff;
                double env = Math.Min(1, te * 200) * Math.Exp(-te * 2.5) * Math.Min(1, (beat / 2 - te) * 300);   // no clicks between notes
                double v = Math.Tanh(lp * 2.2) * env * 0.2 * duck[i];
                L[i] += (float)v; R[i] += (float)v;
            }
        }

        // --- pad: detuned saws on chord tones, slow swell
        {
            var phs = new double[6]; double lpL = 0, lpR = 0;
            for (int i = 0; i < n; i++)
            {
                double tt = i / (double)Rate;
                int barNo = (int)(tt / bar);
                int chord = t.Chords[(barNo / 2) % t.Chords.Length];
                double tc = tt % (bar * 2);
                double env = Math.Min(1, tc / 0.6) * (1 - 0.25 * Math.Min(1, tc / (bar * 2))) * Math.Min(1, (bar * 2 - tc) * 20);
                double sL = 0, sR = 0;
                for (int v = 0; v < 3; v++)
                {
                    int note = Degree(t.Root - 12, chord + v * 2);
                    phs[v] += Hz(note) * 1.003 / Rate; phs[v + 3] += Hz(note) * 0.997 / Rate;
                    sL += 2 * (phs[v] - Math.Floor(phs[v] + 0.5));
                    sR += 2 * (phs[v + 3] - Math.Floor(phs[v + 3] + 0.5));
                }
                lpL += (sL - lpL) * 0.03; lpR += (sR - lpR) * 0.03;
                float g = (float)(env * 0.05 * (t.Kick ? duck[i] : 1));
                L[i] += (float)lpL * g; R[i] += (float)lpR * g;
            }
        }

        // --- arp lead: 16ths over the chord, second half busier; stereo delay (wraps so the loop is seamless)
        {
            var dryL = new float[n]; var dryR = new float[n];
            int[] pattern = { 0, 2, 4, 7, 4, 2, 0, 4, 2, 4, 7, 9, 7, 4, 2, 0 };
            for (int s = 0; s < steps; s++)
            {
                int barNo = s / 16, inBar = s % 16;
                if (t.Bars >= 8 && barNo < t.Bars / 4 && inBar % 2 == 1) continue;   // sparser intro
                if (!t.Kick && inBar % 4 != 0 && barNo % 2 == 0) continue;
                int chord = t.Chords[(barNo / 2) % t.Chords.Length];
                int note = Degree(t.Root, chord + pattern[inBar] % 7) + (pattern[inBar] >= 7 ? 12 : 0);
                if (barNo >= t.Bars / 2 && inBar % 8 == 6) note += 12;
                int i0 = (int)(s * step * Rate), len16 = (int)(step * 0.9 * Rate);
                double ph = 0, f = Hz(note);
                for (int i = 0; i < len16; i++)
                {
                    double tt = i / (double)Rate;
                    ph += f / Rate;
                    double pulse = (ph - Math.Floor(ph)) < 0.3 ? 1 : -1;
                    double v = pulse * Math.Exp(-tt * 14) * 0.055 * Math.Min(1, i / (0.002 * Rate)) * Math.Min(1, (len16 - i) / (0.004 * Rate));
                    int j = (i0 + i) % n;
                    dryL[j] += (float)v; dryR[j] += (float)v;
                }
            }
            int dl = (int)(beat * 0.75 * Rate), dr = (int)(beat * 0.5 * Rate);
            var wetL = new float[n]; var wetR = new float[n];
            for (int pass = 0; pass < 2; pass++)          // two passes so feedback wraps round the loop
                for (int i = 0; i < n; i++)
                {
                    wetL[i] = dryL[i] + 0.38f * wetR[((i - dl) % n + n) % n];
                    wetR[i] = dryR[i] + 0.38f * wetL[((i - dr) % n + n) % n];
                }
            for (int i = 0; i < n; i++) { L[i] += dryL[i] * 0.8f + wetL[i] * 0.45f; R[i] += dryR[i] * 0.8f + wetR[i] * 0.45f; }
        }

        // --- master: soft clip, 16-bit
        var bytes = new byte[n * 4];
        for (int i = 0; i < n; i++)
        {
            short l = (short)(Math.Tanh(L[i] * 1.1) * 30000), r = (short)(Math.Tanh(R[i] * 1.1) * 30000);
            bytes[4 * i] = (byte)(l & 0xff); bytes[4 * i + 1] = (byte)((l >> 8) & 0xff);
            bytes[4 * i + 2] = (byte)(r & 0xff); bytes[4 * i + 3] = (byte)((r >> 8) & 0xff);
        }
        return bytes;
    }

    /// <summary>Tool hook: write a track to a WAV file (used to listen/check offline).</summary>
    public static void ExportWav(string name, string path)
    {
        var t = Tracks.First(x => x.Name == name);
        var data = Render(t);
        using var f = System.IO.File.Create(path);
        using var w = new System.IO.BinaryWriter(f);
        w.Write("RIFF"u8); w.Write(36 + data.Length); w.Write("WAVE"u8); w.Write("fmt "u8); w.Write(16);
        w.Write((short)1); w.Write((short)2); w.Write(Rate); w.Write(Rate * 4); w.Write((short)4); w.Write((short)16);
        w.Write("data"u8); w.Write(data.Length); w.Write(data);
    }
}
