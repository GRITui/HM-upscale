# ADR 0007: Real farm as a local, git-ignored overlay

Status: accepted

## Context

Commit 5c0f94b decodes the real farm (map rows 80..95). The converted JSON
(`assets/hm-converted/maps/map_0NN.json`) and GLBs (`assets/hm-converted/scenes/farm_NN.glb`)
derive from the user's own disc image and are git-ignored. They must never be committed.

## Decision

- `HM.Gameplay.FarmOverlayBootstrap` hooks `SceneManager.sceneLoaded` for the scene
  `SliceFarm`. No scene file changes and no ProjectSettings changes are needed.
- Converted root: env `HM_CONVERTED`, then `<repo>/assets/hm-converted`. Env
  `HM_FARM_OVERLAY=0` disables the overlay. The default row is 80. The game derives the farm row as 0x50 + season + 4*variant (high confidence), so rows 80..95 are the 4 seasons x 4 variants.
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

All behind `FarmCoords`: `UnitsToMetres = 0.01`, `MirrorX = true` (glTFast negates X on
import and the converter writes y = -y), Y down in raw data.

Check performed: PlayMode `FarmOverlayTests.RealFarmWalkGridContainsThePlayer` renders the
GLB, then the collision floor raised 5 cm over it (set `HM_SHOT_DIR` to save
`farm_overlay_default.png` and `farm_overlay_collision_floor.png`). The floor follows the
plateau outline of the GLB with the expected orientation, and the player is stopped at the
walls when pushed in all four directions. This confirms handedness and scale to visual
precision only; a game-faithful check (walking distances, warp arrival) is still open.

Changing scale or handedness means editing the two constants in `FarmCoords`.
