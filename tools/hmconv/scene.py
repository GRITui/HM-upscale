#!/usr/bin/env python3
"""Parse gf.bin scene headers and models (table col0) to JSON. See docs/formats/map.md.

The game is 3D: a scene is a 0xBDC-byte header followed by a model block at +0x2000.
Header: 50 region/trigger entries of 0x2c bytes at +0x10 (empty entries are dropped),
15 model-instance entries of 0x2c bytes at +0x948. Meshes, terrain heights and walking
collision are decoded by scene_mesh.py; scene_glb.py exports geometry.
Region entry (shorts): [0] flags (0x4000 interactable, 0x200 no condition check),
[1] target id (0xfffe = event only, no transition), [2] event id (-1 none),
[4] required facing, [5] condition (99 = none), [7],[9] min x,z, [10],[12] max x,z
(same units as vertices), [19..21] arrival xyz (mostly 0).
Instance: u32 flags (0x8000 skip, 0x4000 animated), u32 x3 model offsets (from
scene+0x2000), shorts[0:3] = position (translation only; the other shorts are 0).
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
        if not any(s) or (s[0] == 0 and s[1] in (0, -1) and s[2] in (0, -1)
                          and s[7] == s[10] and s[9] == s[12]):
            continue  # empty slot
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
                models.append({"vertex_offset": e[0], "vertex_count": e[1], "vertex_end": e[2],
                               "prim_offset": e[4], "prim_count": e[5]})
    return {"kind": kind, "map_id": map_id, "regions": regions,
            "instances": insts, "models": models}


def to_json(s, b=None, entry=None):
    """ADR 0005 schema 1 plus extensions. With the scene bytes b, terrain is the sampled
    walkable height grid of the whole-map model (raw units, null = not walkable)."""
    terrain = None
    if b is not None:
        import scene_mesh
        try:
            terrain = scene_mesh.terrain_grid(scene_mesh.parse_model(b, MODEL_OFF))
        except ValueError:
            terrain = None
    return {"schema": 1, "id": "map_%03d" % s["map_id"], "name": "map_%03d" % s["map_id"],
            "terrain": terrain, "objects": [], "spawns": [], "colliders": [],
            "entry": entry, "regions": s["regions"], "instances": s["instances"], "models": s["models"],
            "undecoded": ["textures", "instance rotation/scale", "scene bit25 and face flags 0x4000/0x2000"]}


if __name__ == "__main__":
    from gf import read_table
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    t = read_table(elf)
    r = int(sys.argv[3])
    sb = seg[t[r][0]:t[r][1]]
    json.dump(to_json(parse_scene(sb), sb), sys.stdout, indent=1)
