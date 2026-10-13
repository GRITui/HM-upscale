# Actors: characters, NPCs, animals, objects

Finding: the game has no 3D actor models, skeletons or skinned animations. Every
actor is a 2D sprite (res_0e, see sprite.md) composed from tiles in the 1024x512
PS1-style VRAM (see texture.md). The farm scenery is 3D; people, animals and
objects drawn on top are pre-rendered sprites.

## Where sprites live

- Map blobs in event_s.bin (map.py) hold a 0x100D image block plus a
  char_sprite (0x2000) and object_sprite (0x0800) res_0e block. Sprites must be
  rendered against that map's own image: 1294 sprite sheets decode this way.
- gf.bin rows 255-260 list further res_0e resources at 0x37e2800..0x3913000 of
  seg000.
- seg014 (Status) and seg018/020/022/024/026 (minigames 1-5) are also res_0e.

## Decoding (actor.py)

- Tile (u, v, w, h) is an absolute VRAM rect; u and w are in 16-bit words.
  Pixel width is 4*w (4bpp) or 2*w (8bpp).
- A part's clut_tile locates its palette row (16 or 256 words). Part flag 0x80 is
  8bpp, 0x10 semi-transparent, 0x01 horizontal flip (inferred from mirrored twin
  parts, not confirmed in game). Word 0 is transparent.
- Parts draw in reverse order; sprite bounds are the union of part rects.

CLI: `actor.py <elf> <event_s.bin> <map_id> <out_dir>` writes a PNG and JSON per
sprite block (stem map%03d_b%02d_kind_n).

## Solved vs unknown

Solved: map-embedded char/object sprites; gf-tail resources 0x37f4800 (image of
row 9), 0x3804800 (row 50) and 0x38b5000 (image at 0x38b6000).
Unknown: the VRAM source of the other gf-tail resources (0x37e2800/0x37ec800
partly match row 91; 0x380a000..0x3851800 and 0x38bf800..0x3910000 match no
stored image, so their pixels are probably streamed from another file), the
Status/minigame pixel sources, evch.bin (seg013), the Font_all.tex glyph layout,
and the exact owner of crop/item/tool/animal sprites.
