# ADR 0006: Original scenes are 3D; rebuild real geometry and layout

Status: accepted. Supersedes the "2D sprite game" premise in earlier notes
(docs/formats/status-phase3.md) and corrects the framing of ADR 0002.

## Context

Earlier analysis concluded the game had no 3D data. Decoding gf.bin column 0 shows each
scene is a 3D buffer: a header with regions and model instances, and a model block of
vertices and textured triangle/quad primitives. Farm rows 80..95 contain terrain, buildings
and objects. Walking collision uses the same triangles (flat ground at y = 0 on the farm).

## Decision

- The remake rebuilds scenes as real geometry and layout, converted by `tools/hmconv`
  (`scene_mesh.py`, `scene_glb.py`) into the git-ignored `assets/hm-converted/`.
- ADR 0002 still holds for logic: rules are reimplemented, not ported. Its premise that
  maps are abstract data only no longer applies; geometry and placement are extracted data.
- ADR 0005 map JSON stays the runtime format. `terrain` carries the walkable height grid
  sampled from the scene (raw units), `regions` and `instances` carry triggers and
  placement. Raw units and scale are unconfirmed (assumed 0.01 m per unit).

## Consequences

- No converted bytes enter the repository; only the converters and synthetic tests do.
- Texture decoding, instance rotation, and region semantics remain open (see map.md).
