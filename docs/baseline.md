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

**Static decompile of hmpsp.prx (Ghidra 12.1.4, ULUS10142; addresses are ELF vaddr, RAM = 0x08804000 + vaddr).**

- Position update: `FUN_000477f4` @0x477f4 adds a per-update table velocity; `FUN_0004c2b8` @0x4c2b8 applies the
  accumulators to the position with a face collision check (`FUN_000202fc`). Confidence high.
- Walk is 41 raw units per update, run is 73 (`FUN_0004786c` @0x4786c, pad bit 0x20 = run, mask 0xf000 = d-pad;
  tables at 0x1374fe..0x13754e, 16-byte blocks of 4 (dx,dz) s16 pairs per facing). Walk confidence high; the run
  button identity (bit 0x20) is unverified, so run is medium-low.
- No diagonals: `FUN_00047754` maps the d-pad to one of 4 facings, so only one axis moves at a time. Confidence high.
- Logic rate is 30 Hz: `FUN_000a9378` @0xa9378 waits vblanks, `FUN_000a9194` @0xa9194 sets the count to 60/n and
  `FUN_000a91c4` @0xa91c4 defaults n to 1, giving 2 vblanks per logic frame. 41 x 30 = 1230 units/s, which equals the
  measured 20.5 per 60 Hz frame. Confidence high.
- Clock: `FUN_000934c8` @0x934c8 counts a frame counter (0x1a9dbc) and adds one game minute when it reaches
  `DAT_0013ed88 * 30 / 60` with `DAT_0013ed88` = 30 (initialised data), i.e. 15 updates = 0.5 s, so 2 game minutes per
  second. Matches the measurement. Confidence high. Minutes roll at 60 into hours (0x1ae1a8), hour 6 starts a new
  day. The forced-sleep site was not located statically (unproven).
- Camera: defaults in `FUN_000709cc` @0x709cc, eye offset `FUN_00070ba4` @0x70ba4, eye and target `FUN_00070cbc`
  @0x70cbc. It is a look-at follow camera, eye = target + R_y(yaw)(0,0,dist) plus a height. Projection distance H is
  1000 with screen centre (0xa0,0x78) on 480x272. A true FOV is not in a table. Confidence medium.
- Correction: the position at RAM 0x08950418 recorded earlier is the camera eye, not the player. The player and the
  map_080..095 scene share one frame (no transform, no scale), so the earlier "different frame" finding was the eye
  offset. The player's own position is near RAM 0x089b2fb0 (unverified by emulator). The yard bounds below are
  therefore eye bounds.

**Yard bounds.** Walkable x runs roughly -637 to 2110..2475 and z roughly
-4479 to -1944 (camera eye). Walking up past z=-1944 near x=2110 changes map (arrives at
about 7495, -11430).

**Unit scale.** The engine has no metre unit; it is an estimate. Anchors: the 48-unit collision probe radius,
100-unit cells, and the sprite height against the 0.1 px per unit projection all point to about 0.005 m per unit
(confidence medium). At 0.005 the walk is 6.15 m/s and run is 10.95 m/s. That is fast for a person but the world is
a chibi diorama where a 2747-unit yard is about 14 m wide. Handedness: the raw frame is right handed (x right, y down,
z into the screen; d-pad up = +z, right = +x). glTF is right handed and glTFast negates X on import, so the converter
writes (-x, -y, z) (a proper rotation, determinant +1, winding preserved; `scene_glb.to_gltf`) and Unity receives
(x, -y, z). `FarmCoords.MirrorX` is therefore false: the one conversion lives in the converter, the mesh, collision
grid, triggers, spawns, sprites and camera yaw all use raw x as world +x, and the on-screen scene is unmirrored
versus the original. Regenerate any GLB made before this change.

**Press-duration caveat.** The debugger press `duration` is not an exact frame
count, and menus or events swallow the first input. Use slopes across several
presses (as the measure scripts do) rather than single readings. Held movement
via `input.buttons.send` only moves if re-sent about every 30 ms.

### Retuned in Unity

- `FarmCoords.DecompiledUnitsToMetres` 0.005 (the estimate), `MirrorX` false (see Handedness).
- DESIGN DECISION (user approved, not a measurement): the farm is built 1.5x larger than the decompiled proportions so it feels like a place rather than a 14 m yard. `FarmCoords.WorldScale = 1.5`, so `UnitsToMetres = 0.0075` (a 2747-unit yard is about 20.6 m). Terrain, collision, regions, spawns, NPC and actor positions, the GLB (`GlbScale` 0.75), sprites (pixels per unit x1.5, so characters stay in proportion to doors and furniture) and the camera follow distance (20 to 30 m, same framing) all derive from it.
- Speeds stay in the decompiled metres: `PlayerMover` walk 6.15 m/s, run 10.95 m/s (73/41 of walk), `FarmEventHost.WalkMetresPerSecond` = 1230 * 0.005 = 6.15 m/s. Crossing the larger world therefore takes 1.5x longer. Game clock, event timing and rules are unchanged.
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

- The metre scale (0.005 is an estimate; the shipped world is 1.5x it by design) (the import is now unmirrored).
- Run button identity, and the forced-sleep location.
- Camera yaw, pitch and FOV for the farm map (only the look-at structure and H = 1000 are known).
- Player position address confirmation in the emulator (one spot-check pending).
- Unattended end-to-end `capture.py` run.
- Collider shapes.

## Permissions

- Screen Recording: granted; stills and video work.
- Accessibility: not needed. Input goes through the PPSSPP debugger, not
  synthetic key events.
- Gotchas: `gpu.*` debugger events hang, so they are not used.
  `screencapture` could not write a movie directly onto the external volume, so
  the script records to the temp dir and moves the file.
