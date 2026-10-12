# Phase 3 format status (models, maps, text, sprites)

Statuses: SOLVED, PARTIAL, UNSOLVED. Everything not marked SOLVED is a guess or a lead.

## Model and skeleton: DO NOT EXIST (high confidence)

The game is a native port of a PS1 title with PS1 library emulation and is a 2D sprite
game. No segment carries a 3D mesh signature, and the PRX contains no skeletal
animation or vertex-skinning code; scene state is built from sprite resources
(res_0e, FUN_000086ec) and tile maps. There is no model, skeleton or bone animation to
convert. Sprites animate by frame sequences.

Round-3 re-check of gf.bin (confirmed, no 3D): all 255 table rows have a 0x100D 4/8bpp
CLUT image in column 1; the engine emits only GP0 0x64 textured rectangles and
ordering-table links (FUN_00009408), no GTE/polygon packets; the module imports no
GU/GUM/VFPU 3D API; res_0e entries are 2D part tables (tile u,v,w,h + x/y offsets +
frame sequences). The remaining gf.bin columns (0, 2, 3) are not yet decoded, but
col 2 reads as RGB555 palettes and col 3 is a byte-identical shared 0x12800 blob, so
nothing there is a per-scene mesh.

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
