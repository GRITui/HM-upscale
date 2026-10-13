# Sprite resource (res_0e)

Status: layout SOLVED from code (FUN_000086ec, 000088f0, 00008a5c, 00008ae8, 00009408),
validated by parsing 21 of 22 res_0e blobs in gf.bin without range errors. Confidence:
high for structure, medium for field semantics. Parser: tools/hmconv/sprite.py.

All little-endian. Header 0x24 bytes:

| Off | Type | Meaning |
|-----|------|---------|
| 0 | u32 | magic 0x0e |
| 4 | u16[5] | counts: tiles, sprites, parts, anims, frames |
| 0xe | u16 | pad |
| 0x10 | u32[5] | section offsets (same order) |

Sections:

| Section | Entry | Fields |
|---------|-------|--------|
| tiles | 8 | s16 u, v, w, h (pixels inside the texpage; a second tile reference gives a CLUT x,y) |
| sprites | 4 | s16 part_count, u16 byte offset into parts |
| parts | 12 | u16 tile, u16 clut_tile, s16 x, s16 y, u8 flags (+8), 3 bytes (+9, usually 80 80 80, likely RGB modulation) |
| anims | 4 | u16 frame_count, u16 byte offset into frames |
| frames | 10 | u16 sprite, s16 xoff, s16 yoff, u8 duration (+6), u8 control (+7), u16 unknown (+8) |

Part flags: 0x01 = flip-x, 0x02 = flip-y, 0x10 = semi-transparent, 0x80 = 8bpp (else 4bpp).
Bits 0x04, 0x08, 0x20, 0x40 unknown.
Parts of a sprite are drawn in reverse order (last listed drawn first).
Frame control: 0xFF hold, 0x80|n jump to frame n, 0 next. Duration is scaled 30/60.
Texpage and CLUT base coordinates are runtime state, not stored: tiles are relative.
Tile (u,v) is composited from the sibling 0x100D image in VRAM (1024x512 16-bit);
decompress.parse_image gives chunks and CLUT groups for that.

Unknown: part bytes +9..11 meaning, frame +8 u16, flags 0x04/0x08/0x20/0x40. The
0x3858000 slot is a fixed sheet (res 0x800, image at +0x800), see actor.md.
Tests: tools/hmconv/tests/test_sprite.py (synthetic fixture).
