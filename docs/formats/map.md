# Map blobs (event_s.bin, seg012)

Status: container PARTIAL-SOLVED (high confidence: offset table, block table, image and
sprite blocks parse to exact block ends over all 439 non-empty maps sampled). Event script
opcodes UNSOLVED. Tile layers, collision grid, farm map id: NOT FOUND. Parser:
tools/hmconv/map.py. Loader: FUN_00058da0, block walkers FUN_00058b9c / FUN_000588bc.

Index: 452 u32 LE offsets at ELF file offset 0x137b74+0x74 (vaddr 0x137b74). Map id i
spans [t[i], t[i+1]) in event_s.bin, loaded from segment 0xc via FUN_00008504. Equal
neighbours mean an unused id (14 of 451 spans are empty).

Blob layout:

| Off | Type | Meaning |
|-----|------|---------|
| 0 | u16 | flags (seen 0x180, 0x8080; bit 0x20 of the first word changes scroll origin) |
| 2 | u16 | N block count |
| 4 | u16[24] | block flags |
| 0x34 | u32[N+1] | block offsets, relative to 0x38+4N |

Block kind from flags (low 10 bits = resource id):

| Flag | Kind | Content |
|------|------|---------|
| 0x8000 | image | 0x100D image with palette groups; background art, parsed by decompress.parse_image (ends exactly at block end) |
| 0x4000 | script | 16-byte commands, last block; first byte 0xff terminates |
| 0x2000 | char_sprite | res_0e (sprite.md) for an NPC slot |
| 0x0800 | object_sprite | res_0e for a map object (pool of 10) |
| 0x401/0x404-6 | special | fixed sprite slots |

Script command (16 bytes): u8 opcode, u8 b1, u8 b2, u8 b3, i32 a, i32 b, i32 c.
Opcode 0x23 first command (header, a = command count 0x2a); 0x04 appears with a=id and
b,c = x,y (position, units unknown); 0x02, 0x33, 0x3d/0x3e, 0x0f, 0x1b common. All
semantics unknown. The identical 0x12800 gf.bin column-3 blob uses the same shape.

Backgrounds are pre-rendered VRAM art, not a tile grid in this blob. Walkability and
the farm layout are probably in gf.bin column 0 or the GF_FARM overlay (seg011); not
yet decoded. The special maps 0x15f-0x190 use an escape-RLE path (FUN_00058cf0: first
byte is the escape; esc,val,count emits val count times) with sizes from a table at
0x138284.

Unknown: farm map id, collision, opcode table, flags word meaning.

## Correction: scenes are 3D (gf.bin), not pre-rendered tiles

The earlier statement that backgrounds are pre-rendered and the game has no 3D is wrong.
Map id N is row N of the gf.bin index table (see `tools/hmconv/gf.py`); column 0 is the
scene buffer. `tools/hmconv/scene.py` parses its structure:

- Header 0xBDC bytes: shorts [1]=scene kind, [2]=map id.
- +0x10: 50 region entries of 0x2c bytes. Shorts: [0] flags (0x4000 interactable),
  [1] type or target map id, [2] event id (-1 none), [4] required facing, [5] 99 = no
  condition, [7],[9] min x,z, [10],[12] max x,z, [19..21] possible arrival xyz.
- +0x948: 15 model-instance entries of 0x2c bytes (flags, 3 offsets into the model block,
  placement shorts).
- +0x2000: model directory (u32 N at +8, N x 0x1c bytes from +0xC: bound xyz, offset,
  primitive count).

Farm ids are rows 80..95: base 0x50 + season + 4*variant (from the farm-id code at
FUN_00042bac). Rows 80-83 have 23 models, 84-87 have 34, 88-91 have 48, 92-95 have 8.
Output goes to the git-ignored `assets/hm-converted/maps/map_NNN.json` (schema 1 with
extra `regions`, `instances`, `models`; `terrain` is null).

Still unknown: the mesh primitive format (so no terrain heights and no walking
collision), whether region field [1] is a warp destination (supported by
FUN_00041d3c, unconfirmed per entry), the arrival-position fields, the 100-entry object
list in column 2, town map ids, and the role of the shared column 3 blob.
