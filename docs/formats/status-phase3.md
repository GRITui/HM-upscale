# Phase 3 format status (models, maps, text, sprites)

Statuses: SOLVED, PARTIAL, UNSOLVED. Everything not marked SOLVED is a guess or a lead.
Refreshed 2026-10-13.

## Scenes and collision: SOLVED structurally

gf.bin column 0 is a 3D scene buffer (vertices, textured triangle and quad primitives,
instance placement); see map.md, ADR 0006 and `tools/hmconv/scene_mesh.py`. Meshes,
collision and GLB export work for the farm yard rows 0..7 and the interior rows 80..95. Texture tpage and CLUT mapping
is PARTIAL.

## Characters: SPRITES, NO SKELETONS FOUND

Actors are sprites in practice. No skeleton or bone data was found; this is a working
conclusion, not a proof. See actor.md and sprite.md.

## Sprite resource res_0e: SOLVED (structure)

Layout read from the code and validated on 21 of 22 blobs. Parser: `tools/hmconv/sprite.py`.
Actor sheets are exported by `hmconv sheets`. Open: part flag bits 0x04, 0x08, 0x20, 0x40;
girl sheet identity; crop, item and tool sprites.

## Image data: SOLVED for portraits, PARTIAL for scenes

See texture.md and compression.md. face.bin portraits decode fully (555 of 555).

## Maps and events: PARTIAL

event_s.bin map blobs convert to JSON (`tools/hmconv/map.py`, 16 farm rows). Event bytecode
decodes to JSON and runs in the deterministic VM (event-vm.md). Open: town map ids, warp
semantics, region semantics, a few op 81 cases, the UI-wait op.

## Text: SOLVED (decoder), PARTIAL (mapping)

Sector decoder and control codes are implemented (text.md, `tools/hmconv/text.py`). Output
stays local. Open: Font_all code-to-cell mapping, face id to character mapping, MesEv
entry selection.

## Slp.Bin (seg016): PARTIAL

TIM-like. Not parsed. Waived for now (see converter-coverage.md).

## Known open coordinate issue

The converter writes (x, -y, z) and glTFast negates X, so imported scenes are x-mirrored
against the game. Tracked in #6; the Unity side keeps `FarmCoords.MirrorX` meanwhile.
