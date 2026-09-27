# Warpline — Game Design Document

**Version:** 0.1 · **Date:** 2026-09-23 · **Status:** roadmap M0–M8 built as far as it can go without Nolan: 20 levels, machines, gels, moving panels, editor + share codes, music, achievements, Linux/Windows builds, store art and trailer. Waiting on Nolan: feel-tuning notes, price, Steamworks account/App ID, store approval (see `docs/STEAM.md`).
**Engine:** Godot 4.7.2 .NET (C#) · **Target:** paid Steam release, Windows + Linux, keyboard + mouse

How to read this: everything Nolan decided in the brainstorm is stated plainly and listed in §19. Anything marked **(proposal)** was filled in by Bojack and is open to veto. Numbers in tuning tables are starting values, not promises.

---

## 1. Pitch and pillars

**Warpline is a 2D portal speedrun platformer.** A small robot runs, wall-jumps and slides through scrolling test levels with a two-portal gun. Speed in equals speed out, so every drop is stored energy and every portal is a chance to spend it. The clock starts on your first input and every level has a developer ghost showing a faster line than you thought possible.

| Pillar | What it means in practice |
|---|---|
| **Momentum is the currency** | Speed is conserved through portals, there is no air drag, and slides keep speed. Routes are about gaining, storing and redirecting velocity. |
| **Even mix** | Levels alternate puzzle (find the solution), precision (execute it) and flow (keep speed through it). No level is only one of the three. |
| **Readable at any speed** | Clean flat vector art, strict color coding: red always kills, the lightest surface always takes a portal, cyan and magenta are only ever portals. |
| **Exact and fair** | Deterministic 120 Hz sim. Identical inputs give identical runs, so ghosts, replays and dev times are exact. Instant restart, no loading. |
| **Short attempts, long mastery** | 30–90 s per level for a normal clear; a full-game speedrun around 15 minutes. |

## 2. Core loop

1. **Read** the level: the camera follows you, so scout by moving; the timer waits for your first input.
2. **Solve**: find where the portals go and how to get enough speed.
3. **Execute**: run, jump, place portals mid-flight, fling.
4. **Die or finish**: death or R = instant restart, level timer resets. Finish = time, medal, PB comparison.
5. **Optimize**: race your PB ghost and the dev ghost, find skips, earn the next medal.
6. **Run the game**: after the last level, full-game mode chains every level on one clock with splits.

## 3. Player movement

Units: tiles (32 px) and seconds. Sim runs at 120 ticks/s.

### 3.1 Rules

- **Run:** accelerate toward max run speed; strong turn-around acceleration; friction to a stop with no input.
- **Jump:** fixed launch velocity. **Variable height:** releasing jump while still rising *from a jump* halves upward speed once. It never cuts upward speed from a fling, bounce or portal exit.
- **Coyote time:** a jump is still allowed for 10 ticks after walking off a ledge.
- **Jump buffer:** a jump pressed up to 12 ticks before landing fires on landing.
- **Wall slide:** falling while holding into a wall caps fall speed at the wall-slide limit.
- **Wall jump:** jump while touching a wall in the air launches out and up. For a short lock **(proposal: 10 ticks)** input toward the wall is ignored, so you can't instantly climb back.
- **Crouch / slide:** hold crouch. Hitbox drops to half height. On the ground above crouch-walk speed you **slide** with very low friction, keeping flung speed through low tunnels. Below that, you crouch-walk slowly. You can't stand up under a ceiling. In the air, crouch tucks the hitbox **(proposal: shrinks around its center)** to fit gaps.
- **Slide-jump (proposal):** jumping out of a slide keeps horizontal speed. This is the intended way to carry momentum across ground, since bunny hopping was not chosen.
- **Above-max speed on the ground** (after a fling) decays slowly toward max run speed instead of stopping hard; sliding decays it even slower.
- **Air:** no drag. With no input, horizontal speed never changes, so flings are predictable. Input only accelerates you toward max run speed; above it, holding forward neither adds nor removes speed, holding back slows you at air acceleration.
- **Gravity** is constant; downward speed caps at terminal velocity. Upward speed is uncapped. **(proposal)** Safety cap of 80 tiles/s on each axis.

### 3.2 Tuning values

| Parameter | Value | Notes |
|---|---|---|
| Gravity | 70 t/s² | constant up and down |
| Jump velocity | 21 t/s | ≈ 3.15 tile jump, 0.30 s to apex |
| Jump cut | ×0.5 | on release while rising from a jump |
| Max run | 13 t/s | ≈ 4.6 s to cross one screen width |
| Ground accel | 110 t/s² | |
| Ground friction | 130 t/s² | no input, at or below max run |
| Turn accel | 180 t/s² | input opposite to motion on the ground |
| Air accel | 60 t/s² | toward max run only |
| Above-max ground decay | 50 t/s² | not sliding |
| Slide friction | 8 t/s² | crouched on the ground |
| Crouch-walk max | 5 t/s | |
| Terminal fall | 60 t/s | downward only |
| Coyote time | 10 ticks | 83 ms |
| Jump buffer | 12 ticks | 100 ms |
| Hitbox | 0.7 × 1.4 t | crouched 0.7 × 0.7 |
| Wall slide max | 6 t/s | holding into the wall |
| Wall jump | 11 out, 19 up | t/s |
| Wall-jump input lock | 10 ticks | **(proposal)** |

## 4. Portal gun

| Rule | Detail |
|---|---|
| Buttons | **LMB = portal A (cyan)**, **RMB = portal B (magenta)**. |
| Shot | **Instant hitscan** from the robot's center toward the mouse. Max range 64 tiles **(proposal)**. No cooldown **(proposal)**. Works while running, airborne, crouched or sliding. |
| Size | 2 tiles long, lying on one axis-aligned face (floor, ceiling, left or right wall). |
| Valid surface | The hit tile must be a portalable panel. Both tiles of the span must be portalable with the face exposed. Space in front: **1 tile for wall portals** (the robot is under a tile wide), 2 tiles for floor and ceiling portals (its height). |
| Sliding | A shot slides up to **3 tiles** along the surface to find room, nearest span first (ties: the lower span). It never jumps across a gap or a non-portalable tile. If nothing fits, the shot fizzles. |
| Overlap | A portal may not overlap the other portal on the same face; shooting onto it slides the new portal alongside. |
| Where panels are | **Most surfaces take portals** (Nolan, 2026-09-23: "you should be able to put portals in way more places"). Dark metal is the exception, used only to teach or to protect a puzzle. Big panel walls draw a white surface skin over a dark core. |
| Re-firing | Firing A again moves A. There are never more than one A and one B. |
| What stops a shot | Any solid tile (dark metal = fizzle), a fizzler grill (fizzle). Spikes, exits and empty space don't stop it. |
| Fizzle feedback | Small burst in the attempted color at the hit point, a dull click, and the portal stays where it was. |
| **Aim preview** | Always on: a line from the robot to the hit point plus the outline of the exact 2-tile span the portal would take. Outline is white when valid, **red when invalid** **(proposal: white for valid)**. |
| Crosshair | **(proposal)** Split ring: left half cyan, right half magenta; each half is filled when that portal exists. |

## 5. Portal transit

### 5.1 When you go through

You enter a portal when you are moving into its face and your extent along the face fits inside its span, with **0.2 tiles of tolerance**. Transit happens at the moment of contact inside the movement step; the rest of that step continues from the exit with the new velocity. With only one portal placed, it behaves as a normal wall.

### 5.2 The rule: speed kept exactly

Each portal has an outward normal **n** and a fixed tangent **t**:

- **Walls:** t = world up.
- **Floors and ceilings:** t = world right.

Exit velocity from entry portal (n_in, t_in) to exit portal (n_out, t_out):

```
v_out = (v · −n_in) · n_out  +  (v · t_in) · t_out
```

The same mapping applies to position: your offset from the entry portal's center along t_in becomes your offset along t_out, clamped so the whole robot fits inside the exit span. You appear just outside the exit face. Because the into-portal component always becomes an out-of-portal component, you can never exit moving back into the exit.

Speed magnitude is preserved exactly (except for the minimum pop below). There is no hidden per-portal orientation state: where you fire from never changes how a portal behaves.

| Pair | What the player feels |
|---|---|
| Floor → floor | Falling in = rising out; sideways drift kept. |
| Floor → ceiling | Falling in = falling out: the **infinite fall** loop, builds to terminal 60 t/s. |
| Floor → wall | Fall speed becomes horizontal launch speed (the **fling**); rightward drift becomes upward. |
| Wall → floor | Run speed becomes an upward pop; rising drift becomes rightward. |
| Wall ↔ wall, facing each other (doorway) | Straight through; rising stays rising. |
| Wall ↔ wall, same facing | You come back the way you went; vertical speed kept. |

### 5.3 Minimum pop

When the exit is a **floor portal** (faces up):

- Speed out along its normal is at least **10 t/s** (rises ≈ 0.7 tiles).
- If horizontal speed after transit is under **4 t/s**, it is set to **4 t/s** in the robot's facing direction.

Result: walking slowly onto a floor portal pops you out and lands you beside the exit (≈ 1.1 tiles of drift), never bobbing forever. Fast flings are unaffected.

## 6. Surfaces and gimmicks

| Surface | Behavior | Portalable |
|---|---|---|
| **White panel** | Solid. | Yes |
| **Dark metal** | Solid. | No |
| **Speed gel** | Painted on a floor. While grounded on it: max run **26 t/s** and accel **200 t/s²** **(proposal)**. Leaving it keeps the speed, which then decays by normal rules. | Keeps the underlying tile's rule **(proposal)** |
| **Bounce gel** | Any contact reflects the velocity component into the surface, at least **26 t/s** off floors (≈ 5-tile bounce) and **16 t/s** off walls and ceilings **(proposal)**. Works on walls and ceilings too. Holding crouch on landing suppresses the bounce **(proposal)**. | Keeps the underlying tile's rule **(proposal)** |
| **Moving panels** | A block of panel tiles that slides on a rail (ping-pong or loop) or rotates in 90° steps around a pivot. A portal on it rides along. | Per panel |

Gels are **static paint set in the level** **(proposal)**; no flowing or dispensed gel in v1.

**Moving panel transit (proposal):** velocity is converted relative to each panel: `v_out = T(v − v_panel_in) + v_panel_out`. A portal whose panel rotates keeps its place on that panel face and its tangent rule updates with the new face orientation. Panel motion is driven by the level tick, so it is deterministic and identical in every attempt.

## 7. Hazards and obstacles

| Element | Behavior |
|---|---|
| **Spikes** | Touch = death. Hitbox shrunk 0.1 tile inside the tile **(proposal)** so grazes are fair. |
| **Pits** | Falling below the level bounds = death. |
| **Fizzler grill** | Passable field. Crossing it wipes both portals. Portal shots can't pass through. |
| **Laser wall** | An emitter fires a beam along a row or column until it hits a solid tile. Touch = death. Timed on/off cycle (period, duty, phase in level data), locked to the level tick. **(proposal)** Beams pass through portals, so a portal can redirect a laser into or out of your path, or onto a receiver that acts as a button. |
| **Turret** | Fires a straight bolt along its facing on a fixed cadence. Bolt speed 30 t/s **(proposal)**, dies on solids, passes through portals **(proposal)**. Touch = death. Tracking turrets with a 0.5 s telegraphed lock-on are a later **(proposal)**. |
| **Buttons** | Floor buttons pressed by standing on them; wall buttons pressed by touching them (fling into them). |
| **Doors** | Solid, non-portalable blocks linked to buttons. Types: **hold** (open while pressed), **latch** (opens and stays open), **timed** (opens for N seconds after the press; countdown shown on the door). Open/close takes 6 ticks **(proposal)**. A door never closes onto the robot; it waits **(proposal)**. |

## 8. Speedrun systems

### 8.1 Timer

- In-game time counts **sim ticks at 120 Hz**; wall-clock time is never used. Displayed `m:ss.mmm` (ticks × 1000 / 120, rounded) **(proposal)**.
- **Level timer starts on the first input**: any move, jump, crouch or portal shot. Moving the mouse alone doesn't start it **(proposal)**.
- The level timer stops on the tick the robot touches the exit.
- Pause stops the clock **(proposal)**.

### 8.2 Death and restart

- Death (spikes, lasers, bolts, pits) and the **R** key = **instant restart** of the level: robot back at spawn, portals cleared, level state reset, **level timer reset**. No checkpoints.
- No loading screen, no death animation longer than **(proposal)** 0.25 s of real time, which is not counted in any timer.

### 8.3 Modes

| Mode | How it works |
|---|---|
| **Individual level** | Race one level. PB per level. |
| **Full game** | Every level back to back on one clock. Starts on the first input of level 1, stops on the last exit. **(proposal)** The run clock keeps counting through deaths and restarts, since failed attempts cost real time; level transitions take zero ticks. Unlocks after finishing the last level once **(proposal)**. |

Chapter runs were explicitly not chosen.

### 8.4 Splits (full game)

- One split per level (cumulative time at each exit).
- Compared to the PB run: **green** ahead, **red** behind, delta shown on each split.
- **(proposal)** Gold split for a best-ever segment, and a "sum of best" line.

### 8.5 Ghosts

- **PB ghost:** your best run of that level, re-simulated from its stored inputs.
- **Dev ghost:** the developer route for that level.
- Ghosts start on your first input, so they are frame-synced with your run.
- Drawn as a translucent robot outline; their portals are not drawn **(proposal)**.
- **G** cycles PB / Dev / Both / Off **(proposal)**. Full-game mode shows no ghost by default **(proposal)**.

### 8.6 Medals and par times (proposal numbers)

| Medal | Condition |
|---|---|
| Bronze | Finish the level |
| Silver | ≤ dev time × 1.4 |
| Gold | ≤ dev time × 1.15 |
| Dev | ≤ dev time |

The dev time is computed at load by re-simulating the level's dev route, so a tuning change updates it automatically. The HUD shows the next medal's target time.

### 8.7 Routes: one format for replays, ghosts and tests

A **route** is run-length input text, one line per run of identical input:

```
<ticks> [L] [R] [D] [J] [A:x,y] [B:x,y]
30 R          hold right 30 ticks
12 R J        right + jump 12 ticks (jump press = first tick jump is held)
1 A:41.5,12   fire portal A at world point (41.5, 12) with nothing held
40 D R        slide right for 40 ticks
```

- L/R = move, D = crouch, J = jump held. A/B fire on the first tick of that line. Aim points are quantized to 1/64 tile so they print and parse exactly.
- The same format stores PB ghosts, dev ghosts and the per-level solvability tests.

### 8.8 Determinism (hard rule)

- The sim uses no Godot physics, no wall clock, no unseeded randomness, and no `sin/cos/pow/atan2` inside the step (**(proposal)**; `sqrt` is IEEE-exact and allowed).
- Fixed step order, fixed substep size, doubles throughout.
- Same level + same route ⇒ same final state hash. xUnit tests enforce it for every shipped level.
- **Risk:** Windows vs Linux builds must replay each other's routes. Verified at M7 with cross-platform replay tests.

## 9. Levels

| Topic | Decision |
|---|---|
| Count | ~20 levels. Casual clear 1–2 h; full-game speedrun ~15 min. |
| Length | Scrolling levels, 30–90 s for a normal clear. |
| Unlocks | Linear: beat a level to open the next. |
| Camera | Fixed zoom, centered on the robot, clamped to the level bounds. Hard-locked, no smoothing **(proposal)**; it snaps through portals. At 32 px tiles on 1920×1080 you see 60 × 33.75 tiles. |
| Rendering | Render interpolates between the last two sim states; no interpolation across a portal transit. |
| Format | ASCII tile grid with a header, for now (below). Objects with parameters and links (doors, buttons, lasers, turrets, moving panels) go in an object list after the grid **(proposal)**. |
| Themes | No story. Levels are grouped and named by mechanic **(proposal)**. |
| Solvability | Every level ships with a dev route that the test suite runs to completion. |

### 9.1 ASCII format (M0)

```
name: Speedy Thing
---
XXXXXXXXXXXXXXXXXX
X......#.........X
XS.....#......E..X
XXXX^^^XXXXXXXXXXX
```

| Char | Tile |
|---|---|
| `.` | empty |
| `#` | white panel (portalable) |
| `X` | dark metal |
| `^` | spikes |
| `:` | fizzler grill |
| `E` | exit |
| `S` | spawn (robot stands on the first solid tile below) |

Outside the grid: dark metal on the sides and top, open air below (falling out is death). Later characters for gels and object anchors are added with M2–M4 **(proposal)**.

### 9.2 Level plan (proposal)

Grouped by mechanic for the level select only; there are no chapter runs.

| # | Group | Teaches |
|---|---|---|
| 1–4 | Basics | run, jump, wall jump, slide; first portal pair; first fling; fizzler grills |
| 5–8 | Momentum | infinite-fall loops, redirecting mid-air, slide-carry, chained flings |
| 9–12 | Machines | buttons, hold/latch/timed doors, lasers through portals, turrets |
| 13–16 | Gels | speed gel run-ups, bounce gel chains, gel into portals |
| 17–19 | Motion | sliding and rotating panels carrying portals |
| 20 | Finale | everything, with several viable routes |

Each level should have one intended route and at least one skip for runners to find **(proposal)**.

**Built in M0:** 01 Boot (run, jump, slide, wall-jump chimney, first portal pair), 02 Speedy Thing (fall-to-wall fling, slide-carry through a long tunnel, slide-jump gap), 03 Up and Over (floor-to-floor launches, a grill wipe, leading a shot mid-fall), 04 Terminal (floor/ceiling loop to terminal velocity, redirect to a wall, chimney, second launch). Dev times 11.792 / 6.367 / 7.167 / 8.925 s.

## 10. In-game editor and share codes

- **Editor:** paint tiles, place spawn/exit/objects, link buttons to doors, set timers and rails, test-play from spawn. Undo/redo **(proposal)**.
- **Clear check (proposal):** a level can only be exported after the author finishes it. The author's clear becomes that level's dev route and dev time, so every shared level is proven possible.
- **Share code:** level text → deflate → base64url, prefixed with a format version (`WL1-…`) **(proposal)**. Copy/paste into the game to import. No server.
- **Library:** "My levels" and "Imported" lists; PBs stored per level content hash **(proposal)**.
- No Steam Workshop (Nolan picked codes, not Workshop).

## 11. Art direction

- **Clean minimal vector, drawn in code** (Godot `_Draw`): flat shapes, crisp edges, no textures.
- **Palette (proposal hexes):**

| Role | Color |
|---|---|
| Background | `#0f1218`, faint grid |
| White panel (portalable) | `#dde2ea` — always the lightest surface |
| Dark metal | `#2b313b` |
| Hazards (spikes, lasers, bolts) | `#ff3b3b` — red means death and nothing else |
| Fizzler grill | pale blue shimmer `#9fd8ff` at 40% |
| Exit | `#3ddc84` |
| Portal A | cyan `#35d6ff` |
| Portal B | magenta `#ff4fb4` |

- **Cyan / magenta** instead of blue / orange, to avoid Portal's signature look on a paid game.
- **Robot:** small, rounded body, one visor eye, antenna, stubby legs; squash and stretch on jump and land; the eye glances toward the aim point; the gun arm glows the color of the last shot **(proposal details)**.
- **Speed readability:** a fading trail above max run speed, a transit burst at the exit portal **(proposal)**. Screen shake off by default **(proposal)**.
- Background detail never exceeds ~20% luminance so gameplay surfaces always pop **(proposal)**.

## 12. Audio

- **Music:** driving electronic (synth / drum & bass), looping per level group **(proposal)**. **Source is an open question** (§18): commission, license, or make.
- **SFX (proposal):** synthesized in-house. Jump, land, wall jump, slide loop, portal A/B fire (different pitch), fizzle click, transit whoosh pitched by speed, death, exit, split chime, PB sting.
- Volume sliders for master, music and SFX.

## 13. UI, menus, settings

| Screen | Contents |
|---|---|
| Title | Play, Full game (locked until the last level is beaten), Editor, Settings, Quit |
| Level select | Linear list; each level shows PB, medal earned, next medal target |
| HUD (level) | Level timer top center, next medal target, ghost delta at finish |
| HUD (full game) | Run clock plus split list with green/red deltas |
| Results | Time, PB delta, medal; Enter = next, R = retry, Esc = level select |
| Pause | Resume, Restart, Level select, Settings |

**Controls (KB+M only; all rebindable):**

| Action | Default |
|---|---|
| Move | A / D |
| Jump | Space / W |
| Crouch / slide | S / Left Ctrl |
| Portal A / B | Left / right mouse |
| Restart | R |
| Pause | Esc |
| Cycle ghost | G |

**Settings:** key rebinding, volumes, fullscreen/windowed, vsync, speed trail on/off, screen shake on/off **(proposal)**. Aim preview is always on (Nolan picked the always-on option). No assist mode.

## 14. Steam

- **Paid release.** Price is open (§18).
- **Achievements (proposal):** finish each level group, finish the game, full game under target times, all golds, all dev medals, finish a level without firing a portal where possible, import a shared level.
- **Steam Cloud** for saves and PBs **(proposal)**.
- **No online leaderboards** (Nolan did not pick them).
- KB+M only, so no Steam Deck "Verified" target **(proposal)**.
- Store page needs capsule art, screenshots and a trailer (M8).

## 15. Technical architecture

| Area | Choice |
|---|---|
| Engine | Godot 4.7.2 .NET, C# on net8.0 |
| Sim | `src/Sim`: pure C#, **no Godot types**. Owns level, robot, portals, hazards, timer ticks. Deterministic 120 Hz. |
| Tests | `tests/Sim.Tests`: xUnit. Movement, placement, transit math, determinism, route round-trip, every level's dev route completes. |
| Game layer | `game/`: input → sim input frames, drawing, HUD, menus, saves. Reads sim state, never changes it. |
| Content | `levels/*.lvl` (tiles) and `levels/*.route` (dev routes), plain text. Export filter must include them. |
| Tools | `tools/levelgen/build.py` draws the levels as rectangles and writes the `.lvl` files. `tools/Trace` compiles a readable `.plan` ("hold R until x>=20") into the exact `.route` and prints the path over the level. |
| Resolution | 1920×1080 base, stretch mode `canvas_items`, keep aspect; 32 px tiles. |
| Renderer | GL Compatibility **(proposal)**; everything is flat vector. |
| Saves | `user://` JSON: per-level PB ticks + PB route, full-game PB + splits + best segments, settings **(proposal)**. |
| Platforms | Windows + Linux **(proposal)**. |

**Sim step order (proposal):** read input frame → jump buffer / coyote → horizontal movement → jump / wall jump → gravity and caps → substepped move (≤ 0.2 tiles per substep, x then y, portal check on face contact) → hazard / grill / exit overlap → events for the presentation layer.

## 16. Roadmap

| Milestone | Scope | Exit test |
|---|---|---|
| **M0 Playable slice** ✅ | Run, jump, wall jump, crouch/slide; portal gun, transit, minimum pop; spikes, pits, fizzler grills; 3–4 scrolling levels; IGT from first input; instant restart; individual-level + full-game mode over the slice levels with splits; PB + dev ghosts; medals; linear level select; aim preview; robot; cyan/magenta. | xUnit green, including every level's dev route completing and replaying identically; screenshots checked; **Nolan plays and gives feel notes**. |
| **M1 Feel pass** ✅ (UX done; tuning waits on Nolan) | Tune §3 numbers from Nolan's playtest; SFX; trails and particles; settings and key rebinding. | Nolan signs off on the feel. |
| **M2 Machines** ✅ | Buttons, hold/latch/timed doors, lasers (timed, through portals), turrets. | Unit tests per element; one test level each with a dev route. |
| **M3 Gels** ✅ | Speed gel, bounce gel. | Tests; one test level each. |
| **M4 Moving panels** ✅ (sliding + orbiting; true rotation dropped: surfaces must stay axis-aligned, per "no angled panels") | Sliding and rotating panels carrying portals. | Tests including transit on a moving panel; determinism holds. |
| **M5 Content** ✅ (20 levels) | All ~20 levels with dev routes; difficulty curve; level select polish. | Every level green in tests; Nolan's full playthrough notes addressed. |
| **M6 Editor** ✅ | In-game editor, clear check, share codes, level library. | Build a level, clear it, export, import on a fresh save, PB saved. |
| **M7 Presentation** ✅ (Windows build untested on Windows) | Music, menus, final art pass, colorblind check, performance, Windows build, cross-platform replay test. | Stable frame time at 1080p; Linux and Windows replay each other's routes. |
| **M8 Steam** ◐ (everything but the Steamworks account, App ID, SDK hook-up and publishing) | Steamworks (achievements, Cloud), store page, trailer, beta playtest, release. | Nolan approves the store page and the release build. |

## 17. Risks

| Risk | Mitigation |
|---|---|
| Portal transit feels wrong in 2D (the floor ↔ wall cross terms are a design choice, not physics) | Fixed-tangent rule is simple and stateless; playtest in M0 and adjust before content. |
| Determinism breaks across OS builds, desyncing ghosts and dev times | No transcendental math in the sim; cross-platform replay test in M7. |
| Trade dress / legal similarity to Portal | Own title, cyan/magenta, own art; never use Valve names, logos or aperture shapes in the game or store copy. |
| 20 scrolling levels is a lot of design and route work | Route tooling (tracer, level checker) from M0; mechanic-per-group plan in §9.2. |
| Music cost or quality | Decide the source early (§18); temp tracks until then. |
| Shared levels that are impossible | Clear check before export. |
| Speedrun glitches (clips, zips) | Decide per glitch whether it's a feature; any fix must keep existing dev routes green. |
| KB+M only limits Steam Deck and controller players | Accepted by design; revisit only if Nolan asks. |

## 18. Open questions for Nolan

1. **Steam price** point.
2. **Music source:** commission a composer, license a track pack (check Steam commercial terms), or make it (AI-generated music needs a Steam AI-content disclosure and a paid commercial license).
3. **Git:** `git init` + a private GitHub repo? Nothing gets committed or pushed without an explicit yes.
4. **Steamworks account:** which account publishes the game and pays the $100 app fee.
5. **Title check:** a trademark/store search on "Warpline" before the store page.
6. Should the **full-game clock pause** on the pause menu, or run like real time? (Proposal above: pause stops it.)
7. Should the **dev ghost show by default** on a first attempt, or only after the first clear? (Spoils the route vs. teaches it.)

## 19. Decisions log (2026-09-23 brainstorm)

| # | Question | Nolan's answer |
|---|---|---|
| 1 | Moment-to-moment feel | **Even mix** of puzzle, precision and momentum |
| 2 | Level structure | **Scrolling levels**, camera follows, 30–90 s each |
| 3 | Portal rules | **Classic two portals + level gimmicks** |
| 4 | Engine | **Godot 4.7 C#** |
| 5 | Goal | **Paid Steam release** |
| 6 | Art style | **Clean minimal vector** |
| 7 | Speedrun features | **Timer + splits, ghosts (PB + dev), medals / par times** — online leaderboards not chosen |
| 8 | Controls | **Keyboard + mouse only** |
| 9 | Death | **Restart the level**; level timer resets; no checkpoints |
| 10 | Movement tech | **Wall jump, slide / crouch** — bunny hop and dash not chosen |
| 11 | Story / setting | **None, pure gameplay** |
| 12 | Game size | **~20 levels** (casual 1–2 h, full-game run ~15 min) |
| 13 | Surface gimmicks | **Speed gel, bounce gel, moving panels** — angled 45° panels not chosen |
| 14 | Hazards / obstacles | **Spikes and pits, fizzler grills, lasers / turrets, buttons + doors** |
| 15 | Speed through portals | **Kept exactly + minimum pop** from floor portals |
| 16 | Portal shots | **Instant hitscan** |
| 17 | Working title | **Warpline** |
| 18 | Music | **Driving electronic** |
| 19 | Multi-level runs | **Full game only** (all levels back to back) — no chapter runs |
| 20 | Unlocks | **Linear** |
| 21 | Portal colors | **Cyan / magenta** |
| 22 | Aim help | **Preview where it lands** (line + outline, red if invalid), always on |
| 23 | Assist options | **None** |
| 24 | Level editor | **In-game editor + share codes** |
| 25 | Player look | **Small robot** |
| 26 | Camera | **Fixed** zoom, centered on the player |
| 27 | Timer start | **First input** |
| 28 | First build | **Playable slice:** movement, portals, 3–4 levels, timer, splits, PB + dev ghosts, medals |
| 29 | Playtest (2026-09-23) | "i like the game keep making improvements" → roadmap continues (M1 UX, M2 machines, M3 gels) |
| 31 | Scope (2026-09-23) | **"dont stop until you have the whole roadmap done"** → M4–M8 built; music made procedurally (source was open); Steam steps that need the account are listed in docs/STEAM.md |
| 30 | Portal surfaces (2026-09-23) | **"you should be able to put portals in way more places"** → panels are the default surface; wall portals need 1 tile of room; shots slide up to 3 tiles |

Everything else in this document marked **(proposal)** awaits Nolan's yes or veto.
