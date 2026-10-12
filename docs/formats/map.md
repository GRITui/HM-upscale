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
