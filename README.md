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

The full task split is in [`docs/Gem_Drift_Tasks.pdf`](docs/Gem_Drift_Tasks.pdf).

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
- Start positions: `Start_P1`, `Start_P2`, facing the driving direction.
- Layers: `Road` (6), `Wall` (7, must have colliders), `Car` (8), `Gem` (9).

## Controls (planned)

| Action | P1 | P2 |
|---|---|---|
| Throttle / reverse | W / S | ↑ / ↓ |
| Steer | A / D | ← / → |
| Drift (hold) | Left Shift | Right Shift |

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
