#!/usr/bin/env python3
"""Enumerate gf.bin (seg000) sub-assets from the index table in hmpsp.prx.

The table (ELF file offset 0x138908 + 0x74, 261 rows of 4 u32 byte offsets, all
0x800-aligned) gives per-scene asset starts. Column roles (by content signature):
  0  3D scene buffer: 0xBDC header (regions, instances) + model block at +0x2000
     (scene.py, scene_mesh.py), ~0x6800-0x37800 bytes; 6 rows are res_0e
  1  0x100D CLUT image (255 rows)            -> decompress.parse_image
  2  palette-like u16 table (RGB555) or res_0e, 0x800-0x1000 bytes
  3  shared 0x12800 blob, byte-identical across rows (script/tile table; unknown)
Usage: gf.py <hmpsp.prx.elf> <seg000.bin> [--json]
"""
import json, struct, sys

TABLE_OFF = 0x138908 + 0x74
ROWS = 261


def read_table(elf):
    return [struct.unpack_from("<4I", elf, TABLE_OFF + 16 * i) for i in range(ROWS)]


def classify(head):
    if head[:2] == b"\x0d\x10":
        return "image_100d"
    if head[:4] == b"\x0e\x00\x00\x00":
        return "res_0e_sprite"
    return "unknown"


def enumerate_assets(elf, seg):
    rows = read_table(elf)
    out = []
    for i, r in enumerate(rows):
        for c, off in enumerate(r):
            end = r[c + 1] if c < 3 else (rows[i + 1][0] if i + 1 < ROWS else off)
            out.append({"row": i, "col": c, "offset": off, "size": end - off,
                        "type": classify(seg[off:off + 4])})
    return out


if __name__ == "__main__":
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    a = enumerate_assets(elf, seg)
    if "--json" in sys.argv:
        json.dump(a, sys.stdout)
    else:
        from collections import Counter
        print(Counter((x["col"], x["type"]) for x in a))
