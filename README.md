# Warpline

A 2D portal speedrun platformer. Godot 4.7 (.NET / C#). Design: [`docs/GDD.md`](docs/GDD.md). Status: [`docs/PROGRESS.md`](docs/PROGRESS.md).

## Play

- **App menu → Warpline**, or `godot --path ~/warpline`
- **Open in the Godot editor:** `godot -e --path ~/warpline`, then press F5 (or the ▶ button top-right) to run.
- After changing C# code outside the editor, rebuild before playing: `dotnet build Warpline.csproj`.

| Key | Does |
|---|---|
| A / D (or arrows) | run |
| Space / W | jump; press against a wall in the air to wall-jump |
| S / Ctrl | crouch; at speed it is a slide that keeps your momentum |
| Left click / right click | cyan / magenta portal at the mouse |
| R | instant restart |
| G | ghost: your PB → dev route → off |
| Esc | pause (Q from pause = menu) |

The level timer starts on your first input. Beating a level opens the next; beating all twenty opens the full-game run.
**EDITOR** (title screen) builds, tests and shares levels; **ACHIEVEMENTS** lists the 14 goals.
Settings (volume, fullscreen, key rebinding) are on the title screen and in the pause menu. F11 toggles fullscreen.
Saves: `~/.local/share/godot/app_userdata/Warpline/save.json`.

## Test

```bash
dotnet test tests/Sim.Tests                                   # rules: movement, portals, routes, every level solvable
godot --headless --path . -- --selftest --save=/tmp/w.json    # the real game loop: input, timer, PBs, unlocks, run splits
godot --path . -- --level=02-speedy --autoplay --shot-tick=290 --screenshot=/tmp/s.png   # picture of the dev route mid-level
```

Other flags (after `--`): `--level=<id>` start in a level, `--run` full-game mode, `--autoplay` play the dev route,
`--aim=x,y` fix the aim point (tiles) for screenshots, `--frames=N` screenshot after N frames.

## Make or change a level

1. Edit `tools/levelgen/build.py` (levels are drawn as rectangles of tiles) and run it: it writes `levels/<id>.lvl`.
2. Write the developer route as a plan in `levels/<id>.plan`, e.g. `R until x>=20.5` / `RJ until wallR` / `R A:31,29 for 1`.
3. Compile and look at it: `dotnet run --project tools/Trace -- levels/<id>.lvl levels/<id>.route --compile --every=3`.
   It writes `levels/<id>.route` and draws the path over the level (`o` path, `*` teleports, `c`/`m` portals, `@` death, `!` finish).
4. Add the id to `levels/index.txt`. `dotnet test` then fails if the route stops finishing the level.

Tile characters: `#` portal panel (the default surface), `X` metal (no portals), `^` spikes, `:` fizzler grill, `=` speed gel,
`%` bounce gel, `E` exit, `S` spawn, `.` empty.

Machines go after a second `---` in the `.lvl` file (in `build.py`: `L.obj("...")`), one per line:

```
door d1 x=40 y=20 w=2 h=6
button b1 x=38 y=28 opens=d1 mode=latch|hold|timed time=3.5
laser l1 x=50 y=3 dir=down period=1.6 on=0.8 phase=0.2     # seconds; no period = always on
receiver r1 x=70 y=4 opens=d1                              # lit by a laser beam
turret t1 x=80 y=10 dir=left period=1.3 speed=16            # bullets also press buttons
```

Plan conditions: `x>=`, `y<=`, `vx`, `vy`, `tick>=`, `grounded`, `air`, `wallL`, `wallR`, `tele`, `open=d1`, `closed=d1`, `lit=r1`,
`laseron=l1`, `laseroff=l1`, `finished`. Rebuild the trace tool after changing `src/Sim` (`dotnet build tools/Trace`), or it runs the old rules.

## Release

```bash
tools/build.sh                                        # Linux + Windows zips in build/dist/ (licences included)
godot --path . -- --capsule=docs/store/capsules       # Steam capsule art + achievement icons
godot --path . --resolution 1920x1080 --write-movie out.avi --fixed-fps 60 -- --level=02-speedy --autoplay --trailer --quit-after-finish=0.8
godot --path . --resolution 1920x1080 -- --level=07-crossfire --autoplay --bench   # frame/sim timings
```
Steam steps and what's left: `docs/STEAM.md`. Store text: `docs/STORE.md`.

## Layout

| Path | What |
|---|---|
| `src/Sim/` | All game rules in plain C# with no Godot types: deterministic 120 Hz, so replays and ghosts are exact. |
| `tests/Sim.Tests/` | xUnit tests for the sim and every shipped level. |
| `game/` | Godot layer: `Main` (input → sim, run clock, saves), `Session` (one attempt + ghost), `WorldView`/`MachineView` (drawing), `Hud`, `Menu`, `Overlays` (pause, settings), `Editor`/`Library`/`EditLevel`/`CustomLevels` (level editor + share codes), `Music`/`Sfx` (synthesized audio), `Achievements`, `Capsule` (store art), `Store` (save file), `Catalog` (levels). |
| `levels/` | `.lvl` tile grids, `.plan` route plans, `.route` compiled dev routes, `index.txt` order. |
| `tools/` | `levelgen` (level drawing script + `pack_a/pack_b` for levels 11–20), `Trace` (plan compiler and path printer), `build.sh` (release zips). |
| `assets/fonts/` | Noto Sans / Noto Sans Mono, SIL OFL (licence beside them). |

## Toolchain

Godot 4.7.2 .NET at `~/.local/opt/godot-4.7.2-mono` (launcher `~/.local/bin/godot` sets `DOTNET_ROOT`), .NET 8 SDK at `~/.dotnet`.
