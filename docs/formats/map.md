# Map blobs (event_s.bin, seg012)

Status: container PARTIAL-SOLVED (high confidence: offset table, block table, image and
sprite blocks parse to exact block ends over all 439 non-empty maps sampled). Event script
opcodes UNSOLVED. Tile layers, collision grid: NOT FOUND. Farm map id: row 0x50 + season + 4*variant (high). Parser:
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

Unknown: collision, opcode table, flags word meaning.

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
- +0x2000: model blob, decoded below (scene_mesh.py).

Farm ids are rows 80..95: base 0x50 + season + 4*variant (from the farm-id code at
FUN_00042bac). Rows 80-83 have 23 models, 84-87 have 34, 88-91 have 48, 92-95 have 8.
Output goes to the git-ignored `assets/hm-converted/maps/map_NNN.json` (schema 1 with
extra `regions`, `instances`, `models`; `terrain` is the sampled walkable height grid).
Geometry exports to `assets/hm-converted/scenes/farm_NN.glb` (`tools/hmconv/scene_glb.py`).

### Model blob (decoded, high confidence for structure)

Offsets are relative to the blob start. Instances reference blobs by offset from scene+0x2000;
the blob at offset 0 is the whole-map terrain and background.

- +8 u32 N (part count); +0xC N directory entries of 7 u32, offsets relative to blob+0xC:
  [0] vertex array, [1] vertex count, [2] vertex array end, [3] 0, [4] primitive list,
  [5] primitive count, [6] 0 in the file (runtime visibility pointer).
- After the directory: u32 cell size (1600 in row 80), then N x (i16 x, y, z, w) part
  centres. A part is considered for picking when the actor is within cell/2 + 600 on x and z.
- Vertex: 8 bytes, i16 x, y, z, pad. Y is DOWN: ground is 0, tall things negative, pits
  positive. The row 80 farm spans x -1800..1800, z -1768..1800.
- Primitive: u32 header (byte 3 & 0xFD gives the kind: 0x04 textured, 0x08 quad, 0x10
  per-vertex colour, 0x20 always set; byte 3 bit 1 "bit25" meaning unknown), then
  [uv words][colour words][u16 indices padded to 8 bytes]. A uv word is u8 u, u8 v, u16
  extra; word 0 extra = clut, word 1 extra = tpage. Colour is r,g,b,0 with 0x80 neutral.
  Quads are strips: (i0,i1,i2), (i2,i1,i3). All primitive lists tile exactly.
- Face flags (header low 16 bits): 0x0000 ground, 0x8000 scenery seen in the farm. Walking
  collision ignores faces with any of 0xC000 set (actor masks seen: 0xC000, 0xC080, 0xE080).

### Ground height and walking collision

Reimplemented from FUN_000158a4 and helpers (FUN_00015174 point-in-triangle on XZ,
FUN_00015004 plane height, FUN_00015318 quad split). An actor at (x, y, z) stands on a
face when the point is inside its XZ projection, flags & mask == 0, and |y - face height|
is within the tolerance (default 200, FUN_00014fb0). `scene_mesh.walk_height` and
`terrain_grid` implement this. Result: every farm row (80..95) is flat at y = 0. Row 80
has 532 walkable 100-unit cells (building footprints are holes), row 84 1193, row 88
1920, row 92 200. Seasons of one variant share identical geometry.

### Instances

Translation only: shorts[0:3] are x,y,z; the other nine shorts are 0 in every farm row, so
rotation and scale are unobserved. Flag 0x8000 = skip, 0x4000 = animated object with 3
model slots (idle/state variants).

### Still unknown

World scale (0.005 m per unit is the decompiled estimate and the shipped world is 1.5x it by design, 0.0075, see docs/baseline.md and ADR 0007; player coordinates are the scene frame; the farm overlay of ADR 0007 matches the GLB
visually under it, with scale and handedness isolated in `FarmCoords`; handedness is resolved: raw is right handed and the converter writes (-x, -y, z) so glTFast's X negation yields an unmirrored scene), texture decode (tpage and clut
to bank mapping), the meaning of bit25 and face flag bits 0x4000/0x2000/0x0080, whether
0x8000 faces act as obstacles beyond leaving no ground, instance rotation, region
semantics per entry (field [1] is passed to FUN_00068804 after the interact state
machine in FUN_00041d3c; 0xfffe means event only; arrival and facing are not yet confirmed
against a warp), town map ids, the 100-entry object list in column 2, and the shared
column 3 blob.
