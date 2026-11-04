# Gem Drift (gamedev2player)

A 2-player, one-screen 3D low-poly racing game for **CPE102 Game Jam #40**
(theme: *ONE SCREEN, TWO DESTINIES*). Robot pickup trucks race on a line-tracing
track, carry gems in their truck bed, and drift. Crashes and long drifts make gems
fall out. **Winner = highest score: gem value in the bed + 5 for finishing first.**

- Jam page: https://itch.io/jam/cpe102-gamejam-40
- Deadline: **12 Nov 2026 16:59:59** (aim to submit 11 Nov)
- Engine: **Unity 6000.0.85f1**. Both of us must use this exact version.
- Target: WebGL (Gzip + Decompression Fallback are already set)

## Who does what

The full task split is in [`docs/Gem_Drift_Tasks.pdf`](docs/Gem_Drift_Tasks.pdf). Progress is tracked in the GitHub issues (checklists). The menu design spec is [`docs/MAIN_MENU_UI.md`](docs/MAIN_MENU_UI.md).

| Person | Role | Branch | Owns |
|---|---|---|---|
| A (Top) | Track, 3D, UI look, audio, itch page | `design` | `Assets/_Track`, `_Art`, `_UI`, `_Audio`, `Scenes/Track.unity` |
| B | Car, drift, gems, race rules, camera, build | `gameplay` | `Assets/_Scripts`, `_Cars`, `Scenes/CarTest.unity` |

`Scenes/Main.unity` is the scene that gets built. Only one person edits it at a time,
so say so in chat first.

## Naming contract between the track and the code

- Checkpoints: empty objects with a trigger `BoxCollider`, named `CP_00`, `CP_01`, …
  in driving order. `CP_00` is the finish line. Tag: `Checkpoint`.
- Gem spawns: empty objects named `GemSpawn_Red_01`, `GemSpawn_Blue_01`,
  `GemSpawn_Gold_01`, … Tag: `GemSpawn`.
- Start positions: `Start_P1`, `Start_P2`, facing the driving direction. Optional `Start_AI1`, `Start_AI2`, …
  for the AI trucks; without them the AI trucks line up in rows behind P1/P2.
- Layers: `Road` (6), `Wall` (7, must have colliders), `Car` (8), `Gem` (9).

## Play it

Open the project and use the menu **Gem Drift → Play Circuit** (⌘⇧P) or **Gem Drift → Play
Forest** (⌘⇧O). The same shortcut stops the game.

| Scene | Course | Format |
|---|---|---|
| `Main` | **CPE Arena Circuit**: white robot-arena road with a black line-tracing stripe | Loop, 3 laps |
| `Forest` | **Forest Switchback**: dirt road through a forest, 4 U-turn hairpins, S-bend, fog | One way, start → finish (~750 m) |

On the results screen, **N** loads the next course. Everything is built at runtime by `GameBootstrap`: track, trucks, gems, split-screen
cameras and HUD. If the scene contains a hand-built track that follows the naming contract below,
that track is used instead of the generated test circuit.

## Controls

| Action | P1 (left screen) | P2 (right screen) |
|---|---|---|
| Throttle / brake-reverse | W / S | ↑ / ↓ |
| Steer | A / D | ← / → |
| Drift (hold while turning) | Left Shift or Space | Right Shift or / |
| Respawn at last checkpoint | R | Enter |
| Race again / next course (results screen) | R / Enter / Space · N | |

## Rules (in code)

- Checkpoints must be passed in order: 3 laps on the circuit, one run on the forest route. After
  the first truck finishes, the others have 10 s.
- A hand-built one-way track needs an empty GameObject named `PointToPoint`. Its last checkpoint
  is then the finish line.
- `GemDriftConfig.aiCount` (default 2) AI trucks race with the two players and are scored the same way.
- Gems: red 1, blue 2, gold 3 (gold sits on the inside of sharp corners). Gems are physics objects
  (they roll and bounce). Track gems respawn after 10 s.
- **Nitro:** drifting fills the nitro tank (3 s of drifting = full). When it's full, double-tap your
  drift key to fire it: 2.5 s at 1.45× top speed. AI trucks fire theirs on straights.
- The bed holds 12 gems, and a full bed makes the truck up to 15% slower.
- Drift 0.8 s = boost, 1.6 s = super boost. Each 2 s of drifting spills a gem. Ramming a truck fast
  enough (`crashMinSpeed`) makes the truck that was hit drop 1-3 gems (1.5 s cooldown); anyone can pick
  them up, but the owner has to wait 1.2 s. Hitting a wall hard spills 1.
- Score = gem value + 5 for first place (−3 for not finishing). Highest wins, and a tie goes to
  whoever finished first.

## Code map (`Assets/_Scripts`)

| File | What it does |
|---|---|
| `GameBootstrap` | Builds the race when `Main` loads |
| `TrackBuilder` | Generated test circuit, or reads a hand-built track from the scene |
| `CarController` | Truck physics, drift, boost, crashes, respawn |
| `PlayerControls` | Keyboard bindings per player (Input System) |
| `CarAI` | Drives an AI truck by feeding `CarController` steer/throttle/drift (follows checkpoints, grabs gems) |
| `GemBed` / `GemPickup` / `GemMesh` | Gems in the bed, on the track, and the gem mesh |
| `RaceManager` | Countdown, laps, finish, last call, scoring |
| `RaceHUD` | Split-screen HUD and results |
| `Minimap` | North-up course map, top-right of each half: both trucks as arrows (own arrow bigger, white outline) |
| `FollowCamera` | Chase camera per half |
| `Editor/GemDriftSetup` | Menu: Play Race, Rebuild Config (materials + truck measurements) |

## Assets

- Kenney Racing Kit and Car Kit (CC0) are in `Assets/_Art/Kenney/`. The car we use
  is `CarKit/delivery-flat.fbx`.
- Free Unity Asset Store models are fine. **Write each asset's name and author in
  `CREDITS.md`**, because the jam requires credits.

## Packages

Input System 1.11.2, Cinemachine 3.1.2, uGUI/TextMeshPro 2.0.0.
Active Input Handling is set to *Both*.

## Getting started

1. `git clone` this repo, then `git checkout design` (A) or `git checkout gameplay` (B).
2. Unity Hub → **Add → Add project from disk** → pick this folder → open it with 6000.0.85f1.
3. Run `git pull` before you start work, commit often, and merge into `main` when a piece is done.
