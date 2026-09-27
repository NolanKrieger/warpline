# Warpline — Steam release checklist

Everything the game can do on its own is built. Steps marked **Nolan** need your Steamworks account, money or approval.

## Ready now
| Item | Where |
|---|---|
| Linux + Windows release builds (zipped, licences included) | `tools/build.sh` → `build/dist/` |
| Store text (short/long description, features, tags, specs) | `docs/STORE.md` |
| Capsule art at every required size | `docs/store/capsules/` (re-render: `godot --path . -- --capsule=docs/store/capsules`) |
| Screenshots (1920×1080) | `docs/store/screenshots/` |
| Trailer | `docs/store/trailer/warpline-trailer.mp4` |
| Achievements (14) with API names | table below; unlocked locally today (`game/Achievements.cs`) |

## Steps
1. **Nolan** — Steamworks partner account, pay the app fee (US$100, recoupable), create the app → note the **App ID**.
2. **Nolan** — store page: paste `docs/STORE.md`, upload capsules + screenshots + trailer, set price, tags, release date ("Coming soon" to start collecting wishlists). Needs your approval before it goes public.
3. **Nolan** (5 min on the site) — create the 14 achievements with the API names below (icons: `docs/store/achievements/`).
4. **Bojack** (≈1 hour once the App ID exists) — Steam hook-up: add the `Steamworks.NET` package, implement `IPlatform` as `SteamPlatform` (init with the App ID, `SteamUserStats.SetAchievement(id)` + `StoreStats()`), set `Achievements.Platform` at startup when Steam is running, ship `steam_api64.dll` / `libsteam_api.so` next to the executable. Deliberately not done yet: it can't be tested without the real App ID, and testing with Valve's sample app would show you as "playing Spacewar" to friends.
5. **Nolan** — Steam Cloud, no code needed (Steamworks → Application → Steam Cloud → Auto-Cloud):
   - Windows: root `WinAppDataRoaming`, subdirectory `Godot/app_userdata/Warpline`, patterns `save.json` and `levels/*`
   - Linux: root `LinuxXdgDataHome`, subdirectory `godot/app_userdata/Warpline`, same patterns (add a root override so both platforms share one cloud).
6. **Bojack/Nolan** — depots + SteamPipe upload (`steamcmd +login <builder account> +run_app_build app_build.vdf`) — one depot per OS from `build/linux` and `build/windows`.
7. **Nolan** — beta branch for playtesters, then release.

## Achievement API names
| API name | Name | How |
|---|---|---|
| FIRST_STEPS | Boot Sequence | Finish the first level |
| SPEEDY_THING | Speedy Thing Goes In | Leave a portal faster than 40 tiles/s |
| TERMINAL | Terminal Velocity | Reach terminal falling speed |
| HALFWAY | Halfway There | Finish 10 levels |
| ALL_LEVELS | Warpline | Finish every level |
| DEV_ONE | Developer | Beat a developer time |
| GOLD_ALL | Gold Rush | Gold or better on every level |
| DEV_ALL | Dev Kit | Dev medal on every level |
| FULL_RUN | Full Send | Finish a full-game run |
| DEATHLESS | Clean Run | Full-game run with no deaths or restarts |
| SUB_DEV_RUN | Faster Than Us | Full-game run under the sum of dev times |
| RETURN_TO_SENDER | Return to Sender | Press a button with a bullet |
| ARCHITECT | Architect | Clear the check on your own level |
| SHARED | Word of Mouth | Share a level code or import one |

## Known gaps
- Windows build is exported but not run on Windows (no Windows/Wine here). Replays are designed to be identical across platforms (no trig in the sim, deterministic sine/cosine for movers) but that is unverified on Windows.
- Windows .exe keeps Godot's default icon (changing it needs `rcedit`).
