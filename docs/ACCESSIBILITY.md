# Warpline — accessibility notes

## Colour vision (checked 2026-09-23)

Every gameplay colour was simulated for protanopia, deuteranopia and tritanopia (Machado et al. 2009 matrices, severity 1.0)
and compared in CIE Lab (ΔE76; under ~10 is hard to tell apart, 25+ is clearly different).

| Pair | Normal | Protan | Deutan | Tritan | Notes |
|---|---|---|---|---|---|
| cyan / magenta portal | 112 | 35 | 26 | 126 | Also shape-coded: cyan has round end caps, magenta square; the crosshair halves are left/right. |
| spikes / exit | 133 | 45 | 30 | 143 | Different shapes too (triangles vs door frame + arrow). |
| spikes / speed gel | 54 | 51 | 28 | 41 | Gel is a floor coat, spikes are triangles. |
| exit / cyan | 64 | 59 | 58 | 11 | Tritan-close, but the exit is a door shape with an arrow. |
| channel 1 / channel 3 | — | — | — | — | Was yellow/lime: **2.6 for deuteranopes**. Channel 3 changed to rust `#c46b00` (min ΔE ≥ 21 against every other channel in every mode). Doors and buttons also show their channel as 1–4 pips. |
| grill / cyan | 16 | 5 | 10 | 9 | Grill changed from light blue to pale steel `#c9d1dc` (≥ 18 vs cyan); grills are striped fields, portals are glowing lines. |

Rule going forward: red means it kills, cyan and magenta mean portals, and no information is carried by colour alone.

## Other
- Every action is rebindable (Settings → Controls); mouse buttons and keys both allowed.
- Volume for master, effects and music separately.
- No flashing beyond short (≤ 0.18 s) low-alpha screen tints on death and grill wipes.
- Not yet: controller support (out of scope: KB+M only per Nolan), text scaling, reduced-motion toggle for the parallax and trails.
