# Warpline — progress

## 2026-09-23 — the whole roadmap (Nolan: "then continue" · "dont stop until you have the whole roadmap done")

**Built and verified** (`dotnet test` 127/127 · `--selftest` 58/58 in the editor build *and* in the exported Linux build):
- **M4 moving panels** — sliding (eased ping-pong) and orbiting (Ferris-wheel) panels; ride, get pushed, get crushed; jumping off keeps their speed; portals ride along; beams and bullets hit them. True rotation dropped: portal surfaces stay axis-aligned (Nolan ruled out angled panels).
- **M5 content** — 20 levels, each with a compiled dev route: 11 Relay, 12 Slipstream, 13 Lockstep, 14 Ricochet, 15 Overdrive, 16 Trampoline, 17 Ferris, 18 Piston, 19 Undertow, 20 Warpline (the finale). Title screen is now a level-card grid.
- **M6 editor** — Main menu → EDITOR: paint tiles, place doors/buttons/lasers/receivers/turrets/moving panels on channels 1–4, edit their properties, undo, test. Finishing in Test is the clear check: it sets the author time and the author ghost. Library: play, edit, copy code, delete, import a code from the clipboard. Share codes start with `WL1-` and carry the author's route.
- **M7 presentation** — procedural "driving electronic" music (menu + three acts; measured clean, **not listened to — please check**), music volume, reduce-motion option, credits + licences, colour-blind pass (`docs/ACCESSIBILITY.md`), 350–480 fps with a 0.02 ms sim step, app icon, Linux + Windows release builds (`tools/build.sh`).
- **M8 Steam (prep)** — 14 achievements with toasts and a screen, store text (`docs/STORE.md`), capsule art at every size, achievement icons, screenshots, trailer, and the account-side checklist (`docs/STEAM.md`).

**Needs Nolan:** feel-tuning notes · the price · Steamworks account + App ID (then the SDK hook-up is ~1 hour) · approving the store page · a listen to the music · git/GitHub.
**Not verifiable here:** the Windows build on real Windows.

## 2026-09-23 — improvements pass (after Nolan: "i like the game keep making improvements")

**Built and verified:**
- **Portals go almost anywhere** (Nolan: "you should be able to put portals in way more places"): panels are the default surface in every level, metal only where it teaches (Boot's step and chimney pillar). Wall portals need 1 tile of room (floors/ceilings 2); a shot slides up to 3 tiles along the surface to find a fit. Panel walls draw a white skin over a dark core.
- **M1 UX:** settings (master/effects volume, fullscreen/F11, full key rebinding, saved), pause menu with buttons, level intro card, death/grill flash, squash & stretch and lean, portal opening animation, parallax background, medal points on the title screen, run golds + sum of best + split popups.
- **M2 machines:** doors with hold/latch/timed buttons, lasers (timed, through portals, dashed warning line before they fire), receivers, turrets (bullets go through portals and press buttons). Doors never close on the robot; closing one evicts a portal it blocks.
- **M3 gels:** speed gel (30 t/s, slippery), bounce gel (keeps impact speed, jump on landing = higher bounce, hold S to stick, bounce walls reflect).
- **5 new levels:** 05 Gatekeeper 0:08.000 · 06 Beamline 0:09.767 · 07 Crossfire 0:10.225 · 08 Afterburner 0:05.267 · 09 Rebound 0:13.200 (dev times).
- Tests: `dotnet test` 66/66 (every level's dev route finishes); `--selftest` 34/34; screenshots of settings, pause and levels 5–9 checked.

**For Nolan's feel pass:** walking onto a floor portal makes you pop *up* out of a wall portal (the fixed-tangent transit rule maps sideways speed on a floor to upward speed on a wall). Say if that feels wrong.

**Next:** M4 moving panels, M6 in-game editor + share codes, more levels toward ~20, music source decision.

## 2026-09-23 — M0 playable slice built

**Works, verified:**
- Sim (`src/Sim`): run, variable jump, coyote + buffer, wall slide + wall jump, crouch/slide, portals (hitscan, 2-tile, placement bump, clearance, no overlap), transit with the fixed-tangent rule, floor-exit minimum pop, spikes, pits, fizzler grills, exit.
- 4 levels, each solved by a dev route: Boot 0:11.792, Speedy Thing 0:06.367, Up and Over 0:07.167, Terminal 0:08.925.
- Game: menu with linear unlocks, IL mode, full-game run with one clock and splits (green/red vs PB), PB + dev ghosts (G cycles), medals (dev / gold ≤ ×1.15 / silver ≤ ×1.4 / bronze), instant restart, pause, aim preview + crosshair, speed trail, particles, synthesized SFX, JSON save.
- Tests: `dotnet test` 41/41; in-engine `--selftest` 22/22; screenshots of menu, levels 1/2/4 mid-route and the finish card checked.

**Not verified:** how it feels in human hands. Nobody has played it with a keyboard and mouse yet.

**Next (M1 feel pass):** Nolan plays the four levels and gives notes on run speed, jump height, gravity, fling speeds, camera zoom (32 px tiles), robot size and readability. Then: settings and key rebinding.

**Open for Nolan:** Steam price, music source (commission / license / make), git + private GitHub repo.
