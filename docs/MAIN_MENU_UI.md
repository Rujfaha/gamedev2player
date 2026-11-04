# Main Menu UI: Gem Drift

Design spec for task **A7** (Top). This file says what the menu screens show and how they behave.
B wires up the code once the layout is in place.

Status: the race and its HUD work now (Gem Drift → Play Race). **There is no menu yet.** The game
opens straight into the countdown.

---

## 1. Screen flow

```
 ┌───────────┐   Enter / Space   ┌──────────────┐   Enter / Space   ┌─────────────┐
 │ MAIN MENU │ ────────────────▶ │ HOW TO PLAY  │ ────────────────▶ │ RACE (Main) │
 └───────────┘                   └──────────────┘                   └─────────────┘
       ▲  ▲                            │ Esc                              │ race ends
       │  └────────────────────────────┘                                  ▼
       │                                                          ┌─────────────┐
       └────────────────────────── Esc ───────────────────────────│   RESULTS   │
                                                                  └─────────────┘
                                                        R / Enter / Space = race again
```

- New scene **`Assets/Scenes/Menu.unity`**. It must be **first** in Build Settings and `Main` second.
- The menu is driven by the keyboard only. Both players share one keyboard, and WebGL players often
  don't touch the mouse. Mouse clicks should also work.
- **Browser audio rule:** a WebGL page can't play sound until the player presses a key or clicks. The
  menu's "PRESS ANY KEY" step unlocks audio, so start the BGM there.

---

## 2. Main Menu screen

```
┌──────────────────────────────────────────────────────────────────────┐
│                                                                      │
│                     💎  G E M   D R I F T  💎                        │  ← logo / title
│              ONE SCREEN, TWO DESTINIES · CPE102 Game Jam             │  ← subtitle (small)
│                                                                      │
│        [ red truck turning ]                [ blue truck turning ]   │  ← 3D trucks rotating on a
│                P1                                    P2              │    turntable (or a screenshot)
│                                                                      │
│                        ▶  START RACE                                 │  ← selected = highlighted
│                           HOW TO PLAY                                │
│                           CREDITS                                    │
│                                                                      │
│                    W/S or ↑/↓ to choose · Enter to select            │  ← hint bar (bottom)
└──────────────────────────────────────────────────────────────────────┘
```

| Element | Content | Notes |
|---|---|---|
| Title | `GEM DRIFT` | Big and bold, with a slight gem shine or gradient. Logo image or text. |
| Subtitle | `ONE SCREEN, TWO DESTINIES · CPE102 Game Jam #40` | Small, 60% opacity. |
| Trucks | P1 red tint (left), P2 blue tint (right) | Slow spin (~20°/s). Shows the players their colours. |
| Buttons | START RACE · HOW TO PLAY · CREDITS | Selected button: bigger, brighter, ▶ marker, sound on move. |
| Hint bar | `W/S or ↑/↓ to choose · Enter to select` | Bottom centre. |
| Background | Slow camera fly-over of the track, or a blurred screenshot | Optional. A plain gradient is fine. |

**Keys:** W/S/↑/↓ move the selection, Enter/Space confirms, and either player can drive the menu.

---

## 3. How to Play screen

Split it into two halves so each player reads their own side, matching the in-game split screen.

```
┌───────────────────────────────┬───────────────────────────────┐
│  P1  (red)                    │  P2  (blue)                   │
│                               │                               │
│   W        accelerate         │   ↑        accelerate         │
│   S        brake / reverse    │   ↓        brake / reverse    │
│   A / D    steer              │   ← / →    steer              │
│   L-Shift  drift (hold)       │   R-Shift  drift (hold)       │
│   R        respawn            │   Enter    respawn            │
├───────────────────────────────┴───────────────────────────────┤
│  💎 Collect gems into your truck bed: red 1 · blue 2 · gold 3   │
│  🏁 3 laps. First across the line gets +5                       │
│  ⚡ Drift 0.8 s = BOOST · 1.6 s = SUPER BOOST                   │
│  ⚠ Crashes and drifts longer than 2 s spill your gems           │
│  🏆 Highest total (gems + bonus) wins                           │
│                                                               │
│                Press Enter to race · Esc back                 │
└───────────────────────────────────────────────────────────────┘
```

Emoji above are placeholders. The built-in font can't draw them, so use small icons (Kenney UI
Pack or game-icons.net) instead.

---

## 4. Credits screen

A scrolling or static list. It must match `CREDITS.md`, because the jam requires credits.

```
GEM DRIFT
Made by  Top (design / 3D)  &  <friend> (programming)
CPE102 Engineering Exploration · KMUTT · 2026

Assets
  Kenney Racing Kit · Kenney Car Kit (CC0) — kenney.nl
  <each Asset Store model: name — author>
  <music / sfx: name — author — licence>

Esc  back
```

---

## 5. Visual style

| Token | Value | Used for |
|---|---|---|
| Background | `#1B1F24` → `#2C3440` gradient | Menu background |
| Text | `#FFFFFF` | Body text |
| Muted text | `#FFFFFF` at 60% | Subtitle, hints |
| P1 colour | `#FF7366` (red) | P1 labels, left side |
| P2 colour | `#73B3FF` (blue) | P2 labels, right side |
| Gem red / blue / gold | `#FF2633` / `#2673FF` / `#FFC726` | Gem icons |
| Boost blue / orange | `#4DCCFF` / `#FF8C1A` | Drift bar levels (already used in HUD) |
| Font | A bold rounded display font for titles, e.g. *Lilita One* or *Baloo 2* (OFL, Google Fonts) | Titles and buttons |
| Text outline | black, 80% | Everything over 3D, so it stays readable |

- Reference resolution **1920×1080**, Canvas Scaler *Scale With Screen Size*, match 0.5. The HUD
  already uses this, so menu and HUD stay consistent.
- Keep text away from the outer 5% of the screen (browser fullscreen can crop it).
- Low-poly and bright, matching the Kenney models.

---

## 6. Sounds for the menu (task A8)

| Event | Sound |
|---|---|
| Menu open (after first key) | BGM loop starts |
| Selection move | short tick |
| Confirm | positive blip |
| Back | soft click |
| Countdown 3-2-1 / GO | beep ×3 + higher beep |

---

## 7. Build checklist

- [ ] Create `Assets/Scenes/Menu.unity`, put it first in Build Settings, keep `Main` second
- [ ] Main menu layout: title, trucks, 3 buttons, hint bar
- [ ] How to Play screen: two halves plus the rules box
- [ ] Credits screen, matching `CREDITS.md`
- [ ] Fonts and colours from section 5
- [ ] "Press any key" start step (unlocks browser audio) + BGM
- [ ] Hand over to B: wire up keyboard navigation and `SceneManager.LoadScene("Main")`
- [ ] Results screen "Esc = back to menu" (B)
- [ ] Screenshot of the menu for the itch.io page
