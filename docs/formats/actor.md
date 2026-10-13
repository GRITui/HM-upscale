# Actors: characters, NPCs, animals, objects

Finding: the game has no 3D actor models, skeletons or skinned animations. Every
actor is a 2D sprite (res_0e, see sprite.md) composed from tiles in the 1024x512
PS1-style VRAM (see texture.md). The farm scenery is 3D; people, animals and
objects drawn on top are pre-rendered sprites.

## Where sprites live

- Map blobs in event_s.bin (map.py) hold a 0x100D image block plus a
  char_sprite (0x2000) and object_sprite (0x0800) res_0e block. Sprites must be
  rendered against that map's own image: 1294 sprite sheets decode this way.
- gf.bin rows 255-260 are not scene rows: the offset list continues as flat u32
  arrays that several loaders in hmpsp.prx view from different bases. Each sheet is
  [res blob][0x100D image] inside seg000 (see "Standalone sheets").
- seg014 (Status) and seg018/020/022/024/026 (minigames 1-5) are also res_0e.

## Decoding (actor.py)

- Tile (u, v, w, h) is an absolute VRAM rect; u and w are in 16-bit words.
  Pixel width is 4*w (4bpp) or 2*w (8bpp).
- A part's clut_tile locates its palette row (16 or 256 words). Part flag 0x80 is
  8bpp, 0x10 semi-transparent, 0x01 horizontal flip, 0x02 vertical flip (both
  confirmed in the game's packet builder). Word 0 is transparent.
- Parts draw in reverse order; sprite bounds are the union of part rects.

CLI: `actor.py <elf> <event_s.bin> <map_id> <out_dir>` writes a PNG and JSON per
sprite block (stem map%03d_b%02d_kind_n).

## Standalone sheets (actor_sheet.py)

Loaders read seg000 (gf.bin) by byte offset (FUN_000083d0, sector = offset >> 11).
A sheet is [blob of `res_size` bytes][0x100D image]; the blob packs n res_0e
resources at the offsets listed in a per-sheet ELF row {n, off_0..off_n-1, res_size,
[second image offset]}. The offsets in a row are not in address order, so a resource
ends at the next larger offset. The image goes into a fresh VRAM, then actor.sheet
renders it. File offsets in the ELF are vaddr + 0x74.

| id | loader | list / pointer rows (vaddr) | sheets | content |
|----|--------|------------------------------|--------|---------|
| main_00..01 | FUN_00065ed8 | 0x139910 / 0x1354f8 | 2 | animals: main_00 is a cow, main_01 is a sheep (10 resources each); NOT the player sets |
| main_02..05 | FUN_00065ed8 | same | 4 | more animals (5,5,5,3 resources) |
| chr_00..09 | FUN_00066154 | 0x13992c / 0x135a54 | 10 | small field objects, 16-45 resources each |
| obj_00..49 | FUN_0006624c | 0x139970 / 0x1362ec | 50 | human characters: rivals, villagers, spouse and baby variants; 35..49 carry a second image at row[n+2] |
| fx_37e2800, fx_37ec800 | FUN_00065d5c | fixed | 2 | inline image after the res (0xa20 / 0x838 bytes) |
| fx_380a000 | FUN_00066034 | fixed | 1 | res 0x540, image at +0x1500 |
| fx_3858000 | FUN_00066098 | fixed | 1 | res 0x800, image at +0x800 (the old "failed magic" slot) |
| fx_37f4800, fx_3804800 | FUN_00065ce0 | fixed | 2 | res-only, pixels from scene rows 9 and 50 |

FUN_00065f90 (0x1398fc / 0x135500) is an alias of main_02..05.

`hmconv sheets <elf> <seg000.bin> <out_dir>` (or `actor_sheet.py`) writes
`<id>_rNN.png/.json` per resource (cells, anims, frames) and `sprite_index.json`:
{sheets: [{id, family, index, gf_offset, res_size, image_offsets, resources:
[{res, json, png, counts}]}]}. Current game data: 72 sheets, 785 resources.

## Solved vs unknown

Solved: map-embedded char/object sprites; every gf-tail sheet (location, loader,
VRAM image, all sub-resources parse and render); flip-x (0x01) and flip-y (0x02).
Runtime: object-struct bits 0x08/0x10 XOR a whole-sprite flip-x/flip-y, bit 0x20 forces
semi-transparent.
Unknown: part flag bits 0x04, 0x08, 0x20, 0x40; which sheet is the girl (the boy is
obj_14_r00; pink-haired obj_01 is only a candidate for the girl); the in-game names of the chr/obj sheets; the animation-id to behaviour mapping
(game logic); rendering of the second image of obj_35..49 (located, not composited);
the Status/minigame pixel sources, evch.bin (seg013), and the Font_all.tex glyph layout.
