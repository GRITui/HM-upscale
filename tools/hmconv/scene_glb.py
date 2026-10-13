#!/usr/bin/env python3
"""Export a gf.bin scene (models plus instance placement) to a binary glTF (.glb).

Output goes under assets/hm-converted/ (git-ignored); never commit it. Geometry only:
textures are not decoded here, each face keeps its raw tpage/clut in the material name
("tpage_<n>") and in node extras. Units: game units * SCALE (0.01, an assumption, the
real world scale is unconfirmed). Axes: game Y is down, so glTF Y = -Y; the games's
winding is unknown so materials are double sided.
Usage: scene_glb.py <hmpsp.prx.elf> <seg000.bin> <row> <out.glb>
"""
import json, struct, sys

import scene as sc
import scene_mesh as mm

SCALE = 0.01


def _pad(b, fill=b"\0"):
    return b + fill * (-len(b) % 4)


class _Buf:
    def __init__(self):
        self.data = bytearray()
        self.views = []
        self.accessors = []

    def add(self, raw, ctype, count, atype, target=34962, mins=None, maxs=None):
        off = len(self.data)
        self.data += _pad(raw)
        self.views.append({"buffer": 0, "byteOffset": off, "byteLength": len(raw), "target": target})
        a = {"bufferView": len(self.views) - 1, "componentType": ctype, "count": count, "type": atype}
        if mins is not None:
            a["min"], a["max"] = mins, maxs
        self.accessors.append(a)
        return len(self.accessors) - 1


def _mesh(buf, model, materials):
    prims_out = []
    for part in model["parts"]:
        groups = {}
        for pr, t in mm.triangles(part):
            groups.setdefault(pr["tpage"], []).append((pr, t))
        for tpage, tris in sorted(groups.items(), key=lambda kv: -1 if kv[0] is None else kv[0]):
            pos, col, uv = [], [], []
            for pr, t in tris:
                for i in t:
                    x, y, z = part["vertices"][i]
                    pos.append((x * SCALE, -y * SCALE, z * SCALE))
                    k = pr["indices"].index(i)
                    c = pr["colors"][k if len(pr["colors"]) > 1 else 0]
                    col.append(tuple(min(1.0, v / 128.0) for v in c) + (1.0,))
                    if pr["uv"]:
                        uv.append((pr["uv"][k][0] / 256.0, pr["uv"][k][1] / 256.0))
            if not pos:
                continue
            mins = [min(p[a] for p in pos) for a in range(3)]
            maxs = [max(p[a] for p in pos) for a in range(3)]
            attrs = {"POSITION": buf.add(struct.pack("<%df" % (3 * len(pos)), *[v for p in pos for v in p]),
                                         5126, len(pos), "VEC3", mins=mins, maxs=maxs),
                     "COLOR_0": buf.add(struct.pack("<%df" % (4 * len(col)), *[v for c in col for v in c]),
                                        5126, len(col), "VEC4")}
            if len(uv) == len(pos):
                attrs["TEXCOORD_0"] = buf.add(struct.pack("<%df" % (2 * len(uv)), *[v for u in uv for v in u]),
                                              5126, len(uv), "VEC2")
            name = "untextured" if tpage is None else "tpage_%d" % tpage
            if name not in materials:
                materials[name] = len(materials)
            prims_out.append({"attributes": attrs, "material": materials[name], "mode": 4})
    return {"primitives": prims_out}


def build_glb(b, parsed=None):
    """b = scene bytes. Returns the .glb bytes."""
    s = parsed or sc.parse_scene(b)
    buf = _Buf()
    materials = {}
    meshes, nodes, mesh_of = [], [], {}

    def mesh_for(off):
        if off not in mesh_of:
            model = mm.parse_model(b, sc.MODEL_OFF + off)
            meshes.append(_mesh(buf, model, materials))
            mesh_of[off] = len(meshes) - 1
        return mesh_of[off]

    nodes.append({"name": "scene_model", "mesh": mesh_for(0)})
    for inst in s["instances"]:
        if inst["flags"] & 0x8000 or not inst["model_offsets"][0]:
            continue
        x, y, z = inst["shorts"][:3]
        nodes.append({"name": "instance_%d" % inst["index"], "mesh": mesh_for(inst["model_offsets"][0]),
                      "translation": [x * SCALE, -y * SCALE, z * SCALE],
                      "extras": {"flags": inst["flags"], "model_offsets": inst["model_offsets"]}})
    doc = {"asset": {"version": "2.0", "generator": "hmconv scene_glb"},
           "scene": 0, "scenes": [{"nodes": list(range(len(nodes)))}], "nodes": nodes, "meshes": meshes,
           "materials": [{"name": n, "doubleSided": True,
                          "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0}}
                         for n in sorted(materials, key=materials.get)] or [{"name": "untextured"}],
           "accessors": buf.accessors, "bufferViews": buf.views,
           "buffers": [{"byteLength": len(_pad(bytes(buf.data)))}]}
    js = _pad(json.dumps(doc, separators=(",", ":")).encode(), b" ")
    bn = _pad(bytes(buf.data))
    total = 12 + 8 + len(js) + 8 + len(bn)
    return (struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(js), b"JSON") + js
            + struct.pack("<I4s", len(bn), b"BIN\0") + bn)


if __name__ == "__main__":
    from gf import read_table
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    t = read_table(elf)
    r = int(sys.argv[3])
    with open(sys.argv[4], "wb") as f:
        f.write(build_glb(seg[t[r][0]:t[r][1]]))
