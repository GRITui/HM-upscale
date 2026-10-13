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

Extra presses in the farmhouse are harmless. The sequence also meets a Mayor
scene, and the first exit from the house triggers a police cutscene that needs
more `cross` presses; the 5 PM shipper scene follows on the first day.

### Savestates (skip the sequence)

PPSSPP has no debugger event for savestates, so they use hotkeys: F2 saves and
F4 loads, posted with `key.swift` (compile: `swiftc key.swift -o /tmp/hmkey`;
the game window must be frontmost; keycodes F2=120, F4=118). A farm-outdoors
state lives at `assets/hm-baseline/farm.ppst` (git-ignored). Load it at launch:

    tools/baseline/launch.sh <iso> <outdir> <state.ppst>
    tools/baseline/capture.py --iso <iso> --state assets/hm-baseline/farm.ppst

Not verified: a fully unattended `capture.py` run end to end. The `--state`
path is new and untested as a whole; the old farmhouse walk test still runs
afterwards and is not outdoor-aware.

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

Clock addresses are RAM = 0x08804000 + ELF vaddr. Clock: six u16 at +0x1AE1A0
(year, season, day, weekday, hour, minute). Position: three s32 at 0x08950418.

**Tick / frame rate.** The CPU tick counter advances about 2.22e8 per wall
second, the PSP's 222 MHz, so emulation ran at 1.0x. Sixteen blocking frames
took 0.267 s, which supports 60 fps (still inferred, not read from the game).

**Clock rate (outdoors).** 2.00 game minutes per emulated second (about 0.033
per frame). The clock freezes during dialogue and events. Measured with
`measure_clock.py` as the slope across several blocking presses. Confidence: high.

**Walk speed (outdoors).** 20.5 raw units per frame, about 1229 per second at
60 fps; the same value as indoors. Confidence: high for the raw number.

**D-pad axes.** Down is -z, up is +z, left is -x, right is +x.

**Run and diagonal.** No run button was found (triangle opens the rucksack,
select holds the map). Diagonal trials moved only one axis; that is
inconclusive, since walls or axis priority could explain it. Unmeasured.

**Yard bounds.** Walkable x runs roughly -637 to 2110..2475 and z roughly
-4479 to -1944. Walking up past z=-1944 near x=2110 changes map (arrives at
about 7495, -11430).

**Unit scale.** Not proven. Player coordinates sit in a different frame from
the map_080 scene (terrain x -1800..1800, z -1768..1800), and no landmark
matched. The farm is about 36 m wide at 0.01, but 1229 units/s would be
12.3 m/s walking, which is implausible as metres. So FarmCoords scale 0.01 and
MirrorX are unconfirmed assumptions, not contradicted by position, and probably
wrong for speed. Unity keeps them unchanged; walking pace was tuned to the
measured raw speed times 0.01.

**Press-duration caveat.** The debugger press `duration` is not an exact frame
count, and menus or events swallow the first input. Use slopes across several
presses (as the measure scripts do) rather than single readings. Held movement
via `input.buttons.send` only moves if re-sent about every 30 ms.

### Retuned in Unity

- `PlayerMover` walk 12.3 (was 3.5), run 20.5 (was 6); run is walk times 20/12
  (low confidence).
- `FarmEventHost.WalkMetresPerSecond` = 1230 * 0.01.
- Game clock stays at 2 minutes per real second (matches the measurement).
- Camera: not measured, unchanged.

## Camera POV

A fixed, high-angle oblique view, like an angled isometric diorama. The world
is real 3D geometry (wood walls, floor planks, furniture) and characters are
flat sprites. The camera follows the player, who stays near the middle of the
frame. Inside the house, the near walls are cut away so the room reads like a
dollhouse. A small clock/date panel sits at the top right. Outdoors the camera
angle is the same, looking down on the ground. The game renders at 480x272
in a 16:9 frame; the captured window letterboxes it.

## Unanswered

- True unit scale and mirroring (raw units to metres), and the walking speed in metres.
- Run speed and diagonal movement.
- Camera pitch, yaw and distance outdoors.
- Exact frame rate read from the game.
- Unattended end-to-end `capture.py` run.
- Collider shapes.

## Permissions

- Screen Recording: granted; stills and video work.
- Accessibility: not needed. Input goes through the PPSSPP debugger, not
  synthetic key events.
- Gotchas: `gpu.*` debugger events hang, so they are not used.
  `screencapture` could not write a movie directly onto the external volume, so
  the script records to the temp dir and moves the file.
