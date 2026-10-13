# ADR 0007: Real farm as a local, git-ignored overlay

Status: accepted

## Context

Commit 5c0f94b decodes the real farm (yard rows 0..7; rows 80..95 are the farmhouse interiors). The converted JSON
(`assets/hm-converted/maps/map_0NN.json`) and GLBs (`assets/hm-converted/scenes/farm_NN.glb`)
derive from the user's own disc image and are git-ignored. They must never be committed.

## Decision

- `HM.Gameplay.FarmOverlayBootstrap` hooks `SceneManager.sceneLoaded` for the scene
  `SliceFarm`. No scene file changes and no ProjectSettings changes are needed.
- Converted root: env `HM_CONVERTED`, then `<repo>/assets/hm-converted`. Env
  `HM_FARM_OVERLAY=0` disables the overlay. The default row is 0. The game derives the yard row as season + 4*building flag (FUN_0006872c), so rows 0..7 are the yard; rows 80..95 are interiors (0x50 + season + 4*house level). Textured GLBs under scenes_textured are preferred.
- Missing JSON, a parse error, or missing terrain each log one `[HM-FARM]` line and leave
  the placeholder slice untouched. A missing GLB keeps the collision floor visible.
- Raw terrain (`cell`, `origin`, null = blocked) goes through `WalkGrid`: a MeshCollider floor
  over cells whose four corners are all walkable, plus merged box walls on the blocked side of
  every walkable/non-walkable edge. The player's CharacterController is therefore contained by
  the decoded grid, not by the hand-placed layout.
- Regions become trigger boxes carrying `MapRegionTrigger` (raw index, flags, type, event id,
  facing, condition, arrival). Event and warp ids stay data; nothing interprets them here.
- `JsonUtility` cannot hold null in a float[]; the parser rewrites nulls in `heights` to the
  sentinel `1e30` (`MapTerrain.Blocked`). `"terrain": null` is normalised to no terrain.

## Assumptions and checks

All behind `FarmCoords`: `DecompiledUnitsToMetres = 0.005` (estimate, docs/baseline.md) times `WorldScale = 1.5` (a user design decision, not a measurement) gives `UnitsToMetres = 0.0075`, `MirrorX = false` (the single documented handedness point: glTFast negates X on import, so the converter writes (-x, -y, z), a proper rotation; no runtime mirror remains; `XSign`/`ToRaw` derive from it), Y down in raw data.

Check performed: PlayMode `FarmOverlayTests.RealFarmWalkGridContainsThePlayer` renders the
GLB, then the collision floor raised 5 cm over it (set `HM_SHOT_DIR` to save
`farm_overlay_default.png` and `farm_overlay_collision_floor.png`). The floor follows the
plateau outline of the GLB with the expected orientation, and the player is stopped at the
walls when pushed in all four directions. This confirms handedness and scale to visual
precision only; a game-faithful check (walking distances, warp arrival) is still open.

Changing scale or handedness means editing the constants in `FarmCoords`.

## Decision: the farm is 1.5x larger than the decompiled proportions

The user chose to make the farm feel like a place instead of a 14 m yard. This is a deliberate
departure from raw proportions, applied from the single source `FarmCoords.WorldScale` (1.5).
Everything derived from raw units scales with it: walk grid, regions, spawns, actors (all through
`UnitsToMetres` 0.0075), the GLB (converter writes 0.01 m per unit, so `GlbScale` 0.75), sprite
billboards (`LoadedSheet.MetresPerPixel` = 10 raw units per pixel x 0.005 x 1.5, so characters stay proportionate to doors and
furniture rather than shrinking against them), and the camera follow offset (the measured raw offset converted
through `FarmCoords`, so it carries the 1.5x). Player speed stays 6.15 walk / 10.95 run m/s
(`FarmCoords.DecompiledMetresPerSecond`), so crossing takes 1.5x longer. The game clock and event
rules are untouched. Invisible collision proxies (wall height, region height) and the player
capsule are unchanged.

## Update: measured camera and sprite scale

The guessed camera (pitch, yaw, distance, FOV, x1.5 rescale in the bootstrap) is replaced by the measured eye
offset (6363, -4500, -6364) raw through `FarmCoords.CameraOffset`: a fixed follow with zero damping, rotation
looking along the negated offset (yaw 315, pitch 26.57), and `FarmCamera.DerivedVerticalFov` 15.49 degrees
(derived from H = 1000 and a 272 px screen, not measured). Sprites are 1:1 with the screen in the original, so the
sprite scale is 10 raw units per pixel. Run is Circle (73 vs 41). The earlier map_080 default was a mistake (it is the farmhouse interior); map_000 is the outdoor yard and shares the player frame, see docs/baseline.md, "Unity vs emulator comparison".
