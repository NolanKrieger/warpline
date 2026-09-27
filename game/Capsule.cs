using Godot;

namespace Warpline;

/// <summary>
/// Steam store art rendered from the game's own look: `--capsule=all --out=DIR` writes every capsule size.
/// Composition: dark lab, white portal panels, the magenta→cyan fling with the robot mid-flight, the logo.
/// </summary>
public partial class Capsule : Node2D
{
    public Vector2 Size;
    public bool Logo = true, Scene = true, TransparentBg;
    public bool Portrait => Size.Y > Size.X;

    public override void _Draw()
    {
        float W = Size.X, H = Size.Y, u = Math.Min(W, H) / 430f;   // unit scales with the shorter side
        if (!TransparentBg)
        {
            DrawRect(new Rect2(0, 0, W, H), Pal.Bg);
            var rng = new Random(7);
            var line = new Color(0.75f, 0.82f, 0.95f, 0.05f);
            for (int i = 0; i < 18; i++)
            {
                float rw = (80 + (float)rng.NextDouble() * 300) * u, rh = (60 + (float)rng.NextDouble() * 220) * u;
                DrawRect(new Rect2((float)rng.NextDouble() * W - rw / 2, (float)rng.NextDouble() * H - rh / 2, rw, rh), line, false, 2);
            }
            for (float x = 0; x < W; x += 24 * u) for (float y = 0; y < H; y += 24 * u) DrawRect(new Rect2(x, y, 2, 2), Pal.BgDot);
        }
        if (Scene) DrawScene(W, H, u);
        if (Logo) DrawLogo(W, H, u);
    }

    void Panels(Rect2 r, float tile)
    {
        DrawRect(r, Pal.Panel);
        for (float x = r.Position.X + tile; x < r.End.X - 1; x += tile) DrawLine(new Vector2(x, r.Position.Y), new Vector2(x, r.End.Y), Pal.PanelShade, 2);
        for (float y = r.Position.Y + tile; y < r.End.Y - 1; y += tile) DrawLine(new Vector2(r.Position.X, y), new Vector2(r.End.X, y), Pal.PanelShade, 2);
    }

    void Portal(Vector2 a, Vector2 b, Vector2 n, Color c, float u)
    {
        DrawPolygon(new[] { a, b, b + n * 40 * u, a + n * 40 * u }, new[] { c with { A = 0.6f }, c with { A = 0.6f }, c with { A = 0 }, c with { A = 0 } });
        DrawLine(a + n * 5 * u, b + n * 5 * u, c, 11 * u, true);
        DrawLine(a + n * 2 * u, b + n * 2 * u, Colors.White with { A = 0.9f }, 3 * u, true);
    }

    void Robot(Vector2 c, float s, float lean)
    {
        DrawSetTransform(c, lean, new Vector2(s, s));
        var box = new StyleBoxFlat { BgColor = Pal.Robot }; box.SetCornerRadiusAll(5);
        DrawRect(new Rect2(-8, 14, 6, 9), Pal.RobotShade);
        DrawRect(new Rect2(2, 12, 6, 9), Pal.RobotShade);
        DrawStyleBox(box, new Rect2(-10, -8, 20, 22));
        DrawStyleBox(box, new Rect2(-12, -23, 24, 15));
        var visor = new StyleBoxFlat { BgColor = Pal.RobotDark }; visor.SetCornerRadiusAll(3);
        DrawStyleBox(visor, new Rect2(-8, -20, 18, 8));
        DrawRect(new Rect2(3, -17.5f, 6, 3), new Color("aef4ff"));
        DrawLine(new Vector2(-6, -23), new Vector2(-9, -30), Pal.RobotDark, 2);
        DrawCircle(new Vector2(-9, -31), 2.5f, Pal.Cyan);
        DrawLine(new Vector2(0, -2), new Vector2(14, -6), Pal.RobotDark, 6, true);
        DrawCircle(new Vector2(14, -6), 3, Colors.White);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    void DrawScene(float W, float H, float u)
    {
        float tile = 26 * u;
        // floor of panels with a spike pit, and a wall carrying the magenta exit portal
        float floorY = H - 3.2f * tile;
        Panels(new Rect2(0, floorY, W, 3.2f * tile), tile);
        float pitX0 = W * (Portrait ? 0.15f : 0.46f), pitX1 = W * (Portrait ? 0.62f : 0.8f);
        DrawRect(new Rect2(pitX0, floorY, pitX1 - pitX0, 3.2f * tile), Pal.Bg);
        for (float x = pitX0; x < pitX1 - 1; x += tile / 3)
            DrawColoredPolygon(new[] { new Vector2(x, floorY + 3.2f * tile), new Vector2(x + tile / 3, floorY + 3.2f * tile), new Vector2(x + tile / 6, floorY + 2.3f * tile) }, Pal.Spike);
        // cyan floor portal (entry) near the left and a magenta wall portal (exit) on a pillar
        float px = Portrait ? W * 0.72f : W * 0.9f;
        Panels(new Rect2(px, H * (Portrait ? 0.34f : 0.18f), 2.2f * tile, floorY - H * (Portrait ? 0.34f : 0.18f)), tile);
        Portal(new Vector2(W * (Portrait ? 0.22f : 0.3f), floorY), new Vector2(W * (Portrait ? 0.22f : 0.3f) + 2.4f * tile, floorY), Vector2.Up, Pal.Cyan, u);
        float wy = Portrait ? H * 0.56f : H * 0.44f;
        Portal(new Vector2(px, wy), new Vector2(px, wy + 2.4f * tile), Vector2.Left, Pal.Magenta, u);
        // the fling: a long trail from the magenta portal across the pit, robot at the tip
        var start = new Vector2(px - 1.2f * tile, wy + 1.2f * tile);
        // Landscape: keep the robot clear of the logo block (upper left) — lower and further right.
        var tip = Portrait ? new Vector2(W * 0.36f, wy - 0.02f * H) : new Vector2(W * 0.69f, wy + 0.07f * H);
        for (int i = 0; i < 16; i++)
        {
            float k = i / 16f;
            var p = start.Lerp(tip, k);
            var c = Pal.Magenta.Lerp(Pal.Cyan, k) with { A = 0.08f + 0.22f * k };
            float ts = (Portrait ? 1.6f : 1.3f) * u;
            DrawRect(new Rect2(p - new Vector2(11, 22) * ts, new Vector2(22, 44) * ts), c);
        }
        for (int i = 0; i < 26; i++)
        {
            var p = start.Lerp(tip, i / 26f) + new Vector2(0, ((i * 37) % 17 - 8) * u);
            DrawRect(new Rect2(p, new Vector2(3, 3) * u), (i % 2 == 0 ? Pal.Magenta : Pal.Cyan) with { A = 0.7f });
        }
        Robot(tip, (Portrait ? 2.2f : 1.8f) * u, -0.18f);
    }

    void DrawLogo(float W, float H, float u)
    {
        float size = (Portrait ? 0.17f * W : 0.12f * W) * (TransparentBg ? 1.6f : 1f);
        size = Math.Min(size, H * 0.3f);
        var font = Pal.Black;
        var textSize = font.GetStringSize("WARPLINE", HorizontalAlignment.Left, -1, (int)size);
        float x = Portrait || TransparentBg ? (W - textSize.X) / 2 : W * 0.06f;
        float y = TransparentBg ? H / 2 + size * 0.35f : Portrait ? H * 0.2f : H * 0.36f;
        DrawStringOutline(font, new Vector2(x, y), "WARPLINE", HorizontalAlignment.Left, -1, (int)size, (int)(size / 7), Pal.Bg with { A = TransparentBg ? 0.0f : 0.9f });
        DrawString(font, new Vector2(x, y), "WARPLINE", HorizontalAlignment.Left, -1, (int)size, Pal.Text);
        float barY = y + size * 0.16f, barH = Math.Max(4, size * 0.07f);
        DrawRect(new Rect2(x + 4, barY, textSize.X * 0.48f, barH), Pal.Cyan);
        DrawRect(new Rect2(x + 4 + textSize.X * 0.48f, barY, textSize.X * 0.48f, barH), Pal.Magenta);
        if (!TransparentBg && H > 300 && !Portrait)
            DrawString(Pal.Bold, new Vector2(x + 4, barY + barH + size * 0.42f), "speed in = speed out", HorizontalAlignment.Left, -1, (int)(size * 0.28f), Pal.TextDim);
    }
}

/// <summary>A 256×256 achievement icon: medal ring, portal colours, a short glyph. Locked = greyed.</summary>
public partial class AchievementIcon : Node2D
{
    public string Glyph = "?";
    public bool Locked;

    public override void _Draw()
    {
        var gold = Pal.MedalColor(Warpline.Sim.Medal.Gold);
        var box = new StyleBoxFlat { BgColor = Pal.Bg }; box.SetCornerRadiusAll(36);
        DrawStyleBox(box, new Rect2(0, 0, 256, 256));
        var c = new Vector2(128, 128);
        Color ring = Locked ? Pal.TextDim with { A = 0.5f } : gold;
        DrawArc(c, 100, 0, Mathf.Tau, 64, ring, 12, true);
        DrawArc(c, 84, Mathf.Pi * 0.5f + 0.3f, Mathf.Pi * 1.5f - 0.3f, 32, Locked ? Pal.TextDim with { A = 0.3f } : Pal.Cyan, 8, true);
        DrawArc(c, 84, -Mathf.Pi * 0.5f + 0.3f, Mathf.Pi * 0.5f - 0.3f, 32, Locked ? Pal.TextDim with { A = 0.3f } : Pal.Magenta, 8, true);
        int size = Glyph.Length > 2 ? 64 : 84;
        var ts = Pal.Black.GetStringSize(Glyph, HorizontalAlignment.Left, -1, size);
        DrawString(Pal.Black, new Vector2(128 - ts.X / 2, 128 + size * 0.36f), Glyph, HorizontalAlignment.Left, -1, size, Locked ? Pal.TextDim : Pal.Text);
    }
}
