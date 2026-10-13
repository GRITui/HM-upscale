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

`capture.py --state` ran unattended end to end from a cold start (PPSSPP not
running) four times on 2026-10-13, each in 92 to 98 s wall time (launch, state
load, control check, 8 outdoor holds, 60 s video, PPSSPP killed at the end).
Without `--state` it takes CONTINUE if a save exists (ULUS10142 under
`~/.config/ppsspp/PSP/SAVEDATA`), else NEW GAME; `ensure_control()` is a bounded
`cross` loop that clears cutscenes and dialogue until a right/left hold moves the
player. The no-state path (about 46 s of boot wait extra) was not re-run.
`--keep` leaves PPSSPP running. The outdoor walk test holds each direction, walk
and run (circle), and writes `walk.csv`; walls and obstacles stop some holds, so
read the rows that moved (left and right are free in the yard; up and down hit
fences and rocks and can report 0). `hold_probe.py <seconds> <buttons>...` is the
single-hold version used for the checks below.

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
(year, season, day, weekday, hour, minute). Camera eye: three s32 at 0x08950418; player: three s32 at 0x08950424.

**Tick / frame rate.** The CPU tick counter advances about 2.22e8 per wall
second, the PSP's 222 MHz, so emulation ran at 1.0x. Sixteen blocking frames
took 0.267 s, which supports 60 fps (still inferred, not read from the game).

**Clock rate (outdoors).** 2.00 game minutes per emulated second (about 0.033
per frame). The clock freezes during dialogue and events. Measured with
`measure_clock.py` as the slope across several blocking presses. Confidence: high.

**Walk speed (outdoors), verified in the emulator.** 41 raw units per 30 Hz
update (1229 per second), same as indoors: left -41.4, right +41.2 and +40.7,
down -40.7. Run is 73 per update (2190 per second): left+circle -72.8,
right+circle +73.7. Confidence: high.

**Run button.** CIRCLE (the decompiled pad bit 0x20). Square with a direction
does not run (still 41). Triangle opens the rucksack menu, cross interacts, L and
R do nothing outdoors. Confidence: high.

**Player position (verified).** Three s32 (x, y=0, z) at RAM 0x08950424, which sits
16 bytes after the camera eye. It has mirror copies at 0x089b2fb0 (and 16 copies) and
0x089e1ac4 (x at +0, z at +4), which are not the player's own transform. Writing a
held direction moves 0x08950424 by exactly the walk and run numbers above.

**Camera (farm outdoors, measured).** Eye is 0x08950418. The eye minus player offset
is constant at (+6363, -4500, -6364) (y negative is up), so the camera does not
lag. Horizontal distance 9000 (6363 x sqrt 2 = 8999.5), height 4500, total
distance about 10062. Yaw 45 degrees (eye at +x, -z of the player; a matrix constant
2896 = 4096 x cos 45 agrees). Pitch atan(4500/9000) = 26.57 degrees down. Projection
H = 1000, centre (160,120). A vertical FOV is not in any table; if the 480x272 screen
applies it would be about 15.5 degrees vertical and 27 degrees horizontal, derived
and not verified. Confidence: high for eye offset, yaw, pitch and distance; low for FOV.

**D-pad axes.** Down is -z, up is +z, left is -x, right is +x.

**Static decompile of hmpsp.prx (Ghidra 12.1.4, ULUS10142; addresses are ELF vaddr, RAM = 0x08804000 + vaddr).**

- Position update: `FUN_000477f4` @0x477f4 adds a per-update table velocity; `FUN_0004c2b8` @0x4c2b8 applies the
  accumulators to the position with a face collision check (`FUN_000202fc`). Confidence high.
- Walk is 41 raw units per update, run is 73 (`FUN_0004786c` @0x4786c, pad bit 0x20 = run, mask 0xf000 = d-pad;
  tables at 0x1374fe..0x13754e, 16-byte blocks of 4 (dx,dz) s16 pairs per facing). Walk confidence high; the run
  button identity (bit 0x20) is circle, verified in the emulator (see above).
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
  map_000..007 yard scene (and the 080..095 interiors) share one frame (no transform, no scale), so the earlier "different frame" finding was the eye
  offset. The player's own position is at RAM 0x08950424 (verified; 0x089b2fb0 is a mirror). The yard bounds below are
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
- DESIGN DECISION (user approved, not a measurement): the farm is built 1.5x larger than the decompiled proportions so it feels like a place rather than a 14 m yard. `FarmCoords.WorldScale = 1.5`, so `UnitsToMetres = 0.0075` (a 2747-unit yard is about 20.6 m). Terrain, collision, regions, spawns, NPC and actor positions, the GLB (`GlbScale` 0.75), sprites (pixels per unit x1.5, so characters stay in proportion to doors and furniture) and the camera offset (measured raw offset through FarmCoords) all derive from it.
- Speeds stay in the decompiled metres: `PlayerMover` walk 6.15 m/s, run 10.95 m/s (73/41 of walk), `FarmEventHost.WalkMetresPerSecond` = 1230 * 0.005 = 6.15 m/s. Crossing the larger world therefore takes 1.5x longer. Game clock, event timing and rules are unchanged.
- Game clock stays at 2 minutes per real second (matches the measurement).
- Camera: the measured offset is now what Unity uses. `FarmCoords.RawCameraOffset` (6363, -4500, -6364) converts through `FarmCoords.ToWorld` to (47.72, 33.75, -47.72) m (distance 75.5 m, yaw 315 = -45, pitch 26.57). `FarmCamera.Configure` applies it as a fixed Cinemachine follow with zero position and rotation damping (the original has no lag) and no LookAt. The vertical FOV is `FarmCamera.DerivedVerticalFov` = 2 atan(136/1000) = 15.49 degrees, one constant marked DERIVED (not measured). Tests: `FarmCameraTests` (offset equals the converted raw offset, rotation, FOV reproduces H = 1000, d-pad directions on screen).
- Sprites are drawn 1:1 with the screen in the original (0.0994 px per raw unit, about 10 units per pixel), so `LoadedSheet.BaseMetresPerPixel` is now 10 x 0.005 = 0.05 m (was a guessed 0.03). DERIVED from the projection; the player went from about half to about the original share of the screen height.
- Run is Circle: `PlayerMover` binds gamepad buttonEast and keyboard left shift, 73 vs 41 raw units (the 73/41 ratio is tested).

### Unity vs emulator comparison (frame capture)

`unity/HMUpscale` dev player, `-hm-shot farm.png -hm-shot-frames 6 -hm-shot-interval 1 -hm-shot-size 960x544`
(`HM_CONVERTED` pointing at assets/hm-converted). Frames and a still side-by-side with `now.png` are in the
git-ignored assets/hm-baseline/unity/. No Unity video exists, so the ffmpeg command above was only run on stills
(`-i now.png -i unity/farm_003.png ... hstack`).

- Map id: the outdoor yard is gf.bin row 0 (spring, base buildings), not rows 80..95. Rows 0..3 are the seasons and
  4..7 the building variant; rows 80..95 are farmhouse interiors. The earlier "offset (-5750, +3150)" was the
  comparison of the yard against an interior; against row 0 there is no offset. Unity now loads map_000 and the
  textured scenes_textured/farm_00.glb.
- Matches: the scene is a textured outdoor yard (grass with dirt patches, barn, fence, mailbox, doghouse), same
  camera angle and framing direction, sprite scale, and unmirrored orientation. Spawn is the map `entry`
  (-33.48, 0, 30.24 m), which sits beside the barn door.
- Still deviates: the emulator screenshot (now.png) shows the player at another spot (fence and a hatch), so the two
  frames are not the same viewpoint and no pixel diff is claimed; the HUD text panel is a Unity placeholder; the
  original uses absolute d-pad axes while `PlayerMover` is camera-relative; the walk grid and door/exit placement were
  checked by coordinate (exit raw (-4253, 0, 4420), arrival (-4464, 0, 4032) lie inside the row 0 yard) but not
  walked in an automated test against the emulator.

## Side-by-side with Unity

Reference footage stays git-ignored (`assets/hm-baseline/baseline60.mov`, the
farm outdoors, 60 s). To compare with a Unity capture of the same walk:

    ffmpeg -i assets/hm-baseline/baseline60.mov -i unity_capture.mov \
      -filter_complex "[0:v]scale=-2:720[a];[1:v]scale=-2:720[b];[a][b]hstack=inputs=2" \
      -c:v libx264 -crf 20 side_by_side.mp4

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
- The forced-sleep location.
- Camera FOV (yaw, pitch and distance are measured; FOV is only derived from H = 1000).
- The no-state (CONTINUE or NEW GAME) path of `capture.py` after the latest edits.
- Collider shapes.

## Permissions

- Screen Recording: granted; stills and video work.
- Accessibility: not needed. Input goes through the PPSSPP debugger, not
  synthetic key events.
- Gotchas: `gpu.*` debugger events hang, so they are not used.
  `screencapture` could not write a movie directly onto the external volume, so
  the script records to the temp dir and moves the file.

## Viewpoint match and walk (map 0, Unity vs PPSSPP)

Method. PPSSPP loaded `assets/hm-baseline/farm.ppst`; the player read (3 x s32 at RAM 0x08950424) as raw
(-4464, 0, 4032) and the camera eye (3 x s32 at 0x08950418) as (1899, -4500, -2332), so eye minus player is the
measured (6363, -4500, -6364). Unity: a development player (`BuildScript.BuildMacDevelopment`, git-ignored
`unity/Build`) run with `-batchmode -hm-shot out.png -hm-shot-delay 15 -hm-shot-size 960x544 -hm-shot-pos -4464,4032`
and `HM_CONVERTED` pointing at `assets/hm-converted`. The captures stay git-ignored (game imagery); the side by side
was made with ffmpeg (crop the 480x272 viewport out of the window, scale to 960x544, `hstack`).

Before the fix the H = 1000 lens (15.49 deg) drew the barn and the mailbox 1.42 to 1.52 times narrower than the
original at the same position (tile, barn and mailbox placement relative to the player were already right, the
player stood at the barn door in both). Fixes in scope, both MEASURED from this one position and rounded:
- `FarmCamera.FramingZoom` 1.5: the vertical FOV is `FarmVerticalFov` (about 10.3 deg), not the derived 15.49.
- `LoadedSheet.SpriteViewScale` 0.75: sprites were about 1.5 times too small once the lens was narrowed.

After the fix, door width / viewport width is 0.10 (original) vs 0.104 (Unity), the player feet sit at 50% / 50%
of the viewport height in both, the mailbox position relative to the door matches, and the player is 98 px tall on
the 544 px crop in the original vs about 95 in Unity. Remaining differences (visual notes, no pixel diff claimed):
- Grass is darker and less saturated in Unity (lighting and colour space, not tuned); dirt patches line up.
- The original shows a wooden fence and a sign next to the HUD at the top right; Unity draws the fence posts black
  there (missing alpha or material on the fence) and the HUD is a Unity placeholder.
- Unity adds the placeholder box, cow and a demo NPC that are not in the original frame.
- The original player is a blue-capped boy; the Unity sprite is a different, orange-haired placeholder sheet.
- Still one position only; the FOV is an empirical fit, not a decoded value.

Scripted walk (PlayMode `FarmWalkWarpTests.HeldRightMovesPlusXAtTheDecompiledPace`): holding D for 1.0 s of
simulation time moves the player +820.0 raw units along +x with 0.0 z drift (ratio 1.000 to the documented pace
6.15 m/s). The original walks 41 raw units per 30 Hz update, 1230 raw/s, so the Unity pace is 0.667 of the original.
This is the ADR 0007 consequence of building the farm 1.5x larger while keeping the metre pace; the pace is not
raised to 1230 raw/s on purpose (design decision), so this deviation is expected and recorded here.

Seasons. All eight yard rows load and render textured in the player (`-hm-farm-season 0..3` with and without
`-hm-farm-building 1`; rows 0..7 = map_000..map_007, about 31.0k to 31.3k walkable cells, 24 regions each):
spring green, summer olive, autumn, winter snow cover; rows 4..7 (building flag) show the extended building. The
barn doorway renders as a black opening in rows 3..7 (closed door model in rows 0..2); the interior behind it is not
modelled, so this is an unlit hole rather than a texture failure.
