# 10 Gen Z Terminal Games

Every folder is a standalone C# console game rebuilt for the terminally online: doomscrolls, aura farms, delulu quizzes, brat summer, and the backrooms. All ten share one small engine:

- **60 FPS double-buffered renderer** with 24-bit ANSI true color and whole-screen frame swaps
- **A real UI kit** — title menus, how-to-play cards, pause menus, result cards with gen z ranks, toasts, badges and scrolling tickers
- **CRT post-processing** — scanlines and a vignette, both toggleable per player
- **Particles, screen shake, flashes, confetti, popups, gradient type, and phone/chat chrome**
- **Adaptive sizing** so games fit the terminal you actually have
- **Persistent high scores and settings** in your app data folder (`term-games`)
- **One shared engine file** at `Shared/Engine.cs`, linked into each project

Run any game with:

```powershell
cd Doomscroll
dotnet run
```

A terminal of at least **58x22** is required (some games look best around 70x26+).

## Controls

Every game shares the same shell:

| Key | Does |
|-----|------|
| `↑` `↓` / `W` `S` | move in menus |
| `Enter` / `Space` | confirm a menu choice |
| `1`–`9` | jump straight to a menu item |
| `P` / `Esc` | pause, or resume from pause |
| `R` | restart from a result screen |
| `Q` | quit |

The pause menu also holds the display settings — scanlines, vignette and reduce motion — which are saved to `settings.cfg` next to your high scores and apply to every game.

## The Games

| Game | Genre | The vibe | Controls |
|------|-------|----------|----------|
| `Doomscroll` | Reaction / feed survival | A phone-shaped feed of slay posts, cringe, and sponsored scams. Like, skip, or block before your battery dies. A 5-streak charges a battery back. | `←` skip · `→` like · `↓` block |
| `TouchGrass` | Cozy catch | Catch petals, sunshine, and self-care. Dodge doom clouds and notification anxiety. Stand still to breathe and recover wellness. | `A/D` or `←/→` |
| `Delulu` | Quiz / spot-the-cope | Four statements, one delusion. Pick the cope before the timer runs out and protect your last three brain cells. `H` crosses out one option for 100 points. | `1-4` or `A-D` · `H` hint |
| `AuraFarm` | Top-down survival | Collect aura, dodge NPCs, dash through them for style. Hyper aura is worth 1500 and a moment of invincibility; self care hearts give a life back. | `WASD` / arrows · `Space` dash |
| `BrainrotBlaster` | Vertical shooter | Hold fire, clear waves of brainrot, collect brain cells, upgrade from single to triple blaster. Every 25 kills earns a screen-clearing clout bomb. | `A/D` move · hold `Space` fire · `B` bomb |
| `Situationship` | Branching chat story | Eleven texts. Three replies each. Peace, heart, and rizz decide which of five endings you deserve. | `1-3` choose · `Space` skip typing |
| `VibeCheck` | Four-lane rhythm | A procedural 118 BPM chart. Hit notes on the line, build combo, and hold a 30 note streak to trigger fever mode for double points. | `D F J K` |
| `Liminal` | Backrooms maze | Fog-of-war level 0, three VHS tapes, almond water, sanity drain, and something that starts following you. `F` fires a camera flash to see further, for a price. | `WASD` / arrows · `F` flash |
| `BratSummer` | Lime-green snake | Eat the lime, dodge responsibilities, hit party mode for rainbow trail, double points and open walls. 365 party girl. | `WASD` / arrows |
| `Y2KStack` | Falling blocks | Chrome tetrominoes, ghost piece, next preview, hold slot, level speed-ups, and CD-burning line clears. | arrows · `Space` drop · `C` hold · `P` pause |

Each game is a thin `Program.cs` on top of the shared engine, target `net10.0`, with no dependencies beyond the .NET SDK. Have fun, log off eventually.
