# 10 High-End C# Terminal Games

Every folder is a standalone C# console game rebuilt as a visually flashy, arcade-grade terminal experience. All 10 share a custom high-performance engine:

- **60 FPS double-buffered renderer** with 24-bit ANSI true color, whole-screen frame swaps, and adaptive sizing (games fit themselves to your terminal)
- **Particle systems** — sparks, trails, debris, shockwave rings
- **Screen effects** — screen shake, color flashes, HUD freeze frames
- **Juice everywhere** — pulsing neon typography with animated gradients, score popups, hit flashes, glow, smooth interpolated motion
- **Persistent high scores** saved to your app data folder

Run any game with:

```powershell
cd NeonSnake
dotnet run
```

Controls are shown inside each game. Most use `WASD` or arrow keys, `Space` for action, and `Q` or `Esc` to quit. A terminal of at least ~60x22 is required.

## The Games

| Game | Genre | What's inside |
|------|-------|---------------|
| `NeonSnake` | Arcade snake | Neon glow, particle trails, speed-up pulse phases, screen shake, combo scoring |
| `FallingBlocks` | Falling-block puzzle | Ghost piece, next-piece preview, smooth fall easing, line-clear explosion effects |
| `Starfighter` | Vertical space shooter | Parallax starfield, enemy waves with aimed shots, explosions, screen flash, wave banners |
| `DungeonDash` | Procedural dungeon crawl | Generated dungeons, torch light radius, fog of war, smooth bump-step movement |
| `BrickBreaker` | Breakout | Ball speed-up, paddle angle control, brick shatter particles, level clear banner |
| `LightCycles` | Tron duel | Fading glow trails, smart AI opponent, near-miss tension, round system |
| `SokobanPro` | Sokoban puzzle | 9 handcrafted levels, smooth push animation, unlimited undo, deadlock detection |
| `CaveRunner` | One-button runner | Parallax cave layers, physics flap, crystals to collect, distance scoring |
| `ArenaSurvivor` | Bullet-hell survivor | Auto-fire hoard combat, XP gems, level-up upgrade choices, vignette |
| `TerminalRacer` | Pseudo-3D racer | OutRun-style projected road with hills and curves, traffic, boost pads, retro sun and skyline |

Each game is a single self-contained `Program.cs` (no dependencies beyond the .NET SDK, `net10.0`). Have fun!
