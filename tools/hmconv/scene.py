#!/usr/bin/env python3
"""Parse gf.bin scene headers (table col0) to JSON. See docs/formats/map.md.

The game is 3D: a scene is a 0xBDC-byte header followed by a model block at +0x2000.
Decoded here (structure only, no mesh): 50 region/trigger entries of 0x2c bytes at
+0x10, 15 model-instance entries of 0x2c bytes at +0x948, and the model directory at
+0x2000 (u32 N at +8, N x 0x1c-byte entries at +0xC). Primitive/vertex data is NOT
decoded, so no terrain heights or walking collision are produced.
Region entry (shorts): [0] flags (0x4000 interactable), [1] type or target map id,
[2] event id (-1 none), [4] required facing, [5] 99 = no condition,
[7],[9] min x,z, [10],[12] max x,z (y shorts are 0 in samples).
Usage: scene.py <hmpsp.prx.elf> <seg000.bin> <row>
"""
import json, struct, sys

HDR_SIZE = 0xBDC
REGION_OFF, REGION_N, REGION_SZ = 0x10, 50, 0x2C
INST_OFF, INST_N, INST_SZ = 0x948, 15, 0x2C
MODEL_OFF = 0x2000


def parse_scene(b):
    if len(b) < HDR_SIZE:
        raise ValueError("scene too short")
    kind, map_id = struct.unpack_from("<2h", b, 2)
    regions = []
    for i in range(REGION_N):
        s = struct.unpack_from("<22h", b, REGION_OFF + i * REGION_SZ)
        if s[1] == -1 and s[0] == 0:
            continue
        regions.append({"index": i, "flags": s[0] & 0xFFFF, "type": s[1], "event": s[2],
                        "facing": s[4], "condition": s[5],
                        "min": [s[7], s[9]], "max": [s[10], s[12]],
                        "arrival": [s[19], s[20], s[21]]})
    insts = []
    for i in range(INST_N):
        o = INST_OFF + i * INST_SZ
        flags = struct.unpack_from("<I", b, o)[0]
        if flags == 0:
            continue
        insts.append({"index": i, "flags": flags,
                      "model_offsets": list(struct.unpack_from("<3I", b, o + 4)),
                      "shorts": list(struct.unpack_from("<12h", b, o + 0x14))})
    models = []
    if len(b) >= MODEL_OFF + 12:
        n = struct.unpack_from("<I", b, MODEL_OFF + 8)[0]
        if n * 0x1C + MODEL_OFF + 12 <= len(b):
            for i in range(n):
                e = struct.unpack_from("<7I", b, MODEL_OFF + 12 + i * 0x1C)
                models.append({"offset": e[4], "primitives": e[5], "bound": list(e[:3])})
    return {"kind": kind, "map_id": map_id, "regions": regions,
            "instances": insts, "models": models}


def to_json(s):
    """ADR 0005 schema 1 plus extensions: regions, instances (raw units)."""
    return {"schema": 1, "id": "map_%03d" % s["map_id"], "name": "map_%03d" % s["map_id"],
            "terrain": None, "objects": [], "spawns": [], "colliders": [],
            "regions": s["regions"], "instances": s["instances"], "models": s["models"],
            "undecoded": ["terrain", "walking collision", "mesh primitives"]}


if __name__ == "__main__":
    from gf import read_table
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    t = read_table(elf)
    r = int(sys.argv[3])
    json.dump(to_json(parse_scene(seg[t[r][0]:t[r][1]])), sys.stdout, indent=1)
