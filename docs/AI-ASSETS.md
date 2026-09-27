# Warpline — AI use (for Steam's content survey)

**Steam's disclosure applies.** Steam asks about content "pre-generated" with AI during development, and that covers code and design, not just art:
- All game code, the level layouts (all 20; levels 11–20 by Claude agents), the procedural art, sound and music code, store text and store art were produced with Claude (Bojack), directed and reviewed by Nolan.
- No image, audio or video generator was used: art is drawn by the game's own code, store art is rendered by `game/Capsule.cs`, sound and music are synthesized by `game/Sfx.cs` and `game/Music.cs`.
- No AI runs inside the shipped game ("live-generated" content: none).

Suggested disclosure text: "Warpline's code, level design, and procedural art and audio were developed with the help of AI coding tools, directed and edited by the developer. No AI-generated images, audio or voices are used, and no AI runs during play."
