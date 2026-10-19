# 10 Gen Z Terminal Games

Every folder is a standalone C# console game rebuilt for the terminally online: doomscrolls, aura farms, delulu quizzes, brat summer, and the backrooms. All ten share one small engine:

- **60 FPS double-buffered renderer** with 24-bit ANSI true color and whole-screen frame swaps
- **Particles, screen shake, flashes, popups, gradient type, and phone/chat chrome**
- **Adaptive sizing** so games fit the terminal you actually have
- **Persistent high scores** in your app data folder (`term-games`)
- **One shared engine file** at `Shared/Engine.cs`, linked into each project

Run any game with:

```powershell
cd Doomscroll
dotnet run
```

Controls are shown inside each game. `Q` quits. A terminal of at least **58x22** is required (some games look best around 70x26+).

## The Games

| Game | Genre | The vibe | Controls |
|------|-------|----------|----------|
| `Doomscroll` | Reaction / feed survival | A phone-shaped feed of slay posts, cringe, and sponsored scams. Like, skip, or block before your battery dies. | `←` skip · `→` like · `↓` block |
| `TouchGrass` | Cozy catch | Catch petals, sunshine, and self-care. Dodge doom clouds and notification anxiety. Keep your wellness up. | `A/D` or `←/→` |
| `Delulu` | Quiz / spot-the-cope | Four statements, one delusion. Pick the cope before the timer runs out and protect your last three brain cells. | `1-4` or `A-D` |
| `AuraFarm` | Top-down survival | Collect aura, dodge NPCs, dash through them for style. Every hit drains your aura and your patience. | `WASD` / arrows · `Space` dash |
| `BrainrotBlaster` | Vertical shooter | Hold fire, clear waves of brainrot, collect brain cells, upgrade from single to triple blaster. | `A/D` move · hold `Space` fire |
| `Situationship` | Branching chat story | Eight texts. Three replies each. Peace, heart, and rizz decide which ending you deserve. | `1-3` choose |
| `VibeCheck` | Four-lane rhythm | A procedural 118 BPM chart. Hit notes on the line, build combo, get graded on accuracy. | `D F J K` |
| `Liminal` | Backrooms maze | Fog-of-war level 0, three VHS tapes, almond water, sanity drain, and something that starts following you. | `WASD` / arrows |
| `BratSummer` | Lime-green snake | Eat the lime, dodge responsibilities, hit party mode for rainbow trail and double points. 365 party girl. | `WASD` / arrows |
| `Y2KStack` | Falling blocks | Chrome tetrominoes, ghost piece, next preview, level speed-ups, and CD-burning line clears. | arrows · `Space` drop · `P` pause |

Each game is a thin `Program.cs` on top of the shared engine, target `net10.0`, with no dependencies beyond the .NET SDK. Have fun, log off eventually.
