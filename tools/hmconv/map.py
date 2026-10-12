#!/usr/bin/env python3
"""Parse event_s.bin (seg012) map blobs to JSON. See docs/formats/map.md.

Index: u32 LE ascending byte offsets into event_s.bin at ELF file offset 0x137b74+0x74
(452 entries; map i spans [t[i], t[i+1]); empty spans are unused ids).
Blob: u16 flags, u16 N, u16 block_flags[24] at +4, u32 offsets[N+1] at +0x34, then
blocks at 0x38+4N+offsets[i]. Block kind by flags: 0x8000 image (0x100D), 0x4000 event
script (16-byte commands), 0x2000 character sprite (res_0e), 0x0800 object sprite (res_0e);
low 10 bits = resource id. Tile layers and collision grids were NOT found in this blob.
Usage: map.py <hmpsp.prx.elf> <event_s.bin> <map_id>
"""
import json, struct, sys

TABLE_OFF = 0x137B74 + 0x74
N_MAPS = 452


def index(elf):
    return list(struct.unpack_from("<%dI" % N_MAPS, elf, TABLE_OFF))


def block_kind(f):
    if f & 0x8000: return "image"
    if f & 0x4000: return "script"
    if f & 0x2000: return "char_sprite"
    if f & 0x0800: return "object_sprite"
    return "unknown"


def parse_map(b):
    if len(b) < 0x38:
        raise ValueError("empty map")
    flags, n = struct.unpack_from("<HH", b)
    bf = struct.unpack_from("<%dH" % n, b, 4)
    offs = struct.unpack_from("<%dI" % (n + 1), b, 0x34)
    base = 0x38 + 4 * n
    blocks = []
    for i, f in enumerate(bf):
        data = b[base + offs[i]: base + offs[i + 1]]
        blocks.append({"index": i, "flags": f, "kind": block_kind(f), "res_id": f & 0x3FF,
                       "size": len(data), "data": data})
    return {"flags": flags, "blocks": blocks}


def parse_script(d):
    """16-byte commands: u8 opcode, u8 a, u8 b, u8 c, 3 x u32. 0xff opcode ends."""
    out = []
    for i in range(0, len(d) - 15, 16):
        r = d[i:i + 16]
        if r[0] == 0xFF:
            break
        out.append({"op": r[0], "b1": r[1], "b2": r[2], "b3": r[3],
                    "args": list(struct.unpack_from("<3i", r, 4))})
    return out


def to_json(m):
    out = {"flags": m["flags"], "blocks": []}
    for b in m["blocks"]:
        e = {k: b[k] for k in ("index", "flags", "kind", "res_id", "size")}
        if b["kind"] == "script":
            e["commands"] = parse_script(b["data"])  # opcode semantics unknown
        out["blocks"].append(e)
    return out


if __name__ == "__main__":
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    t = index(elf)
    i = int(sys.argv[3])
    json.dump(to_json(parse_map(seg[t[i]:t[i + 1]])), sys.stdout, indent=1)
