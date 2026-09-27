# Warpline — project rules

A 2D portal speedrun platformer. Godot 4.7.2 .NET, C#. Goal: a paid Steam release.

## Read first
1. `docs/GDD.md` — the design; §19 is Nolan's decisions log. Items marked **(proposal)** are Bojack's and can be vetoed.
2. `docs/PROGRESS.md` — where the build stands and what's next. Update it at every milestone and before stopping.
3. `README.md` — run, test, make levels.
4. `~/.claude/workspace/tools/languages.md` → "Godot 4.7.2 .NET" for toolchain gotchas.

## Architecture (keep it)
- All rules live in `src/Sim`: plain C#, **no Godot types**, deterministic 120 Hz, doubles, no trig in the step. The Godot layer (`game/`) feeds `InputFrame`s and reads state; it never changes sim state.
- One input format everywhere: routes (`Route`) are dev ghosts, PB ghosts, replays and level tests.
- Every sim feature gets xUnit tests. Every level ships a `.plan` + compiled `.route`; `dotnet test` proves it finishes.
- Every game-layer flow gets a `--selftest` check (`godot --headless --path . -- --selftest --save=/tmp/x.json`). Run it in the exported build too (`build/linux/Warpline.x86_64 --headless -- --selftest --save=...`).
- Levels: `tools/levelgen/build.py` (+ `pack_a.py`, `pack_b.py`) → `.lvl`; routes are `.plan` files compiled by `tools/Trace --compile`. Rebuild `tools/Trace` after any `src/Sim` change or it runs stale rules. Every route must finish (LevelTests) and survive a text round trip (LevelTextTests).
- Moving panels: portals on them store panel-local coordinates (`Portal.Mover`); always use `World.PortalCenter/PortalEnds` for world positions.
- Anything random or trig-like inside the sim must be deterministic across platforms (`DetMath`), never `Math.Sin`.
- Fonts SIL OFL only, licence beside each font. Log any future asset sources in `docs/LICENCES.md`; AI-generated assets in `docs/AI-ASSETS.md`.

## How to work
- Roadmap M0–M8 is built (see `docs/PROGRESS.md`). What's left needs Nolan: feel-tuning notes, price, Steamworks App ID (then do the SDK hook-up in `docs/STEAM.md` step 4), store approval.
- Verify before claiming: `dotnet test tests/Sim.Tests` green; selftest PASS; a screenshot via `--screenshot=... --shot-tick=N` looked at with the Read tool.
- Run `godot` and `dotnet` **non-sandboxed**. Screenshot runs force a fixed-size 1920×1080 window so the tiler leaves it alone.

## Needs Nolan (one line each)
- Any change to how it looks or feels that he hasn't seen. Tuning numbers after his playtest.
- Anything that costs money (Steamworks fee, music, paid assets). Price point.
- Git: not under version control yet. Ask about `git init` + private GitHub repo. **Never commit, push or publish without his explicit yes.**

## Nolan's rules
- Replies: no fluff. Answer or action first, 1–5 short lines, exact paths/numbers. No preamble or recaps.
- He has no Godot experience: give exact clicks when he must do something in the editor.
- Never `pkill -f` / `pgrep -f` a pattern in your own command line. Collect PIDs, then `kill`.
