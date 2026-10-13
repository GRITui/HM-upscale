# Original-game baseline (PPSSPP)

Ground truth from the original ULUS10142 running in PPSSPP v1.20.4 (SDL build),
for parity checks of the Unity remake (issue #8, phase 6). Captures live in
`assets/hm-baseline/` (outside the repo tree, never committed). This file holds
only numbers and prose.

## Reproduce

    tools/baseline/capture.py --iso <path/to/hm.iso>     # launch, new game, capture
    tools/baseline/capture.py --attach                   # emulator already in the farmhouse

- `launch.sh` starts PPSSPP with an ini that enables the remote debugger and
  keeps running when unfocused.
- `ppsspp_ws.py` is a stdlib WebSocket client for that debugger. Input is sent
  as virtual pad presses (`input.buttons.press`); memory via `memory.read`.
- `winshot.sh` finds the game window and uses `screencapture -l` for stills and
  `-V` for video.

### New-game key sequence (no save data exists)

1. Wait about 46 s after launch (Natsume logo, then the Boy/Girl screen).
2. `start` (pick Boy), `start`, `start` to reach the Back to Nature title.
3. `up` once (cursor starts on CONTINUE, which reports "There is no data"),
   then `cross` to take NEW GAME.
4. Repeat 100 times: `cross`, `start`, `cross`, one second apart. This
   advances dialogue, types the first letter of each default name, confirms
   names with `start`, answers Yes with `cross`, and sits through the field
   scene, the sepia flashback and the second Mayor scene.
5. Result: the player stands in the farmhouse with control, HUD reading
   "2 Mon AM 6:00".

Extra presses in the farmhouse are harmless. A savestate could skip all this;
none was created.

## What was captured

- Stills of every stage (boot, intro, farmhouse, toolbox menu).
- `baseline60.mov`: 60 s of window video, about 1028x644 retina-scaled, variable
  rate averaging about 53 fps.
- `trace.csv`: once per second for 60 s, CPU ticks, the in-game clock and the
  position candidates.
- `walk.csv`: held-direction tests (20, 40, 60 frames) with positions before and after.
- Interaction check: standing at the toolbox and pressing `cross` opens the
  tool menu (hoe, axe, pickaxe, hammer, watering can in the first slots).

## Measured values

Clock addresses are RAM = 0x08804000 + ELF vaddr.

**Tick / frame rate.** The CPU tick counter advances about 2.22e8 per wall
second (trace over 60 s: 2.2201e8; idle 10 s test: 2.22e8). That matches the
PSP's 222 MHz clock, so emulation ran at 1.0x real time. At the usual 60 fps
that is about 3.7e6 ticks per frame. The tick counter does not directly count
frames, so 60 fps is inferred, not read from the game.

**Walk speed.** Position is three s32 at 0x08950418 (x, constant y = -4500, z).
Horizontal runs in the open part of the farmhouse, from a blocking press, then
a read:

| frames held | x change |
|---|---|
| 20 | 410 |
| 40 | 820 (one run reached 810 with a start offset; a second 40f run 5494 to 6314 = 820) |
| 60 | 1230 |

That is 20.5 units per held frame, linear in hold time, so about 1230 units per
second at 60 fps. Left and right are symmetric. Vertical (z) moves were cut off
by furniture and walls (410, then 451 and 533 for longer holds), so only the
first 20-frame step is a clean 410 (also 20.5 per frame). If 4096 is 1.0 world
unit, this is about 0.30 units per second; the unit scale is unconfirmed.

**Clock.** Stayed at year 0, spring, day 1, Monday, 06:00 for all 60 s traced
and during all walking. The clock does not run inside the house in the intro, so
the minutes-per-frame rate remains untested.

## Camera POV

A fixed, high-angle oblique view, like an angled isometric diorama. The world
is real 3D geometry (wood walls, floor planks, furniture) and characters are
flat sprites. The camera follows the player, who stays near the middle of the
frame. Inside the house, the near walls are cut away so the room reads like a
dollhouse. A small clock/date panel sits at the top right. Outdoors the camera
angle is the same, looking down on the ground. The game renders at 480x272
in a 16:9 frame; the captured window letterboxes it.

## Unanswered

- Minutes of game time per real second (clock frozen indoors in the intro).
- Walk speed outdoors, running speed, and diagonal movement.
- Meaning of the y value (-4500, constant) and the unit scale.
- How d-pad directions map to world axes once the camera turns elsewhere.
- Exact frame rate read from the game (60 fps is inferred from the CPU clock).
- Colliders: the farmhouse furniture stopped vertical movement; the shapes are unmeasured.

## Permissions

- Screen Recording: granted; stills and video work.
- Accessibility: not needed. Input goes through the PPSSPP debugger, not
  synthetic key events.
- Gotchas: `gpu.*` debugger events hang, so they are not used.
  `screencapture` could not write a movie directly onto the external volume, so
  the script records to the temp dir and moves the file.
