# Phase 3 format status (models, maps, text, sprites)

Statuses: SOLVED, PARTIAL, UNSOLVED. Everything not marked SOLVED is a guess or a lead.

## Model and skeleton: SCENES ARE 3D, SKELETONS UNSOLVED

Correction: earlier rounds called the game 2D. gf.bin column 0 is a 3D scene buffer
(vertices, textured triangle and quad primitives, instance placement); see map.md and
tools/hmconv/scene_mesh.py. Scene meshes and collision are SOLVED structurally; textures
(tpage/clut mapping) are PARTIAL. Character models, skeletons and bone animation were not
found: actors may still be sprites (res_0e, FUN_000086ec) and that remains unverified.

## Image data: PARTIAL

See texture.md and compression.md. face.bin portraits fully decodable.

## Sprite/object resource res_0e: PARTIAL

Loaded by FUN_000086ec in hmpsp.prx. Field layout not yet written up; read that
function in the Ghidra project to continue.

## Map / event table: PARTIAL

event_s.bin (seg012) is addressed through an offset table (DAT_00137b74). The map
loader is FUN_00058da0 and uses FUN_00058cf0, an escape-character RLE decoder (distinct
from the 0x100D RLE). Field layouts not documented yet; no parser.

## Text: PARTIAL

seg030, seg031 and messege0..4 (seg002-006) carry text. Encoding and control codes not
documented yet (likely a custom table, to be checked against Font_all glyph order).

## Slp.Bin (seg016): PARTIAL

TIM-like (the extractor names it .tim). Not parsed here.
