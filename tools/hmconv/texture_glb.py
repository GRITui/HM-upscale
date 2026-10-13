#!/usr/bin/env python3
"""Export a gf.bin scene to a textured binary glTF (.glb). See docs/formats/texture.md.

Same geometry and conventions as scene_glb.py (scale 0.01, handedness via scene_glb.to_gltf, double
sided), but every (tpage, clut) pair used by a face becomes a material with an embedded
256x256 PNG rendered from the scene's VRAM image (texture.py). Texel 0x0000 is
transparent (alphaMode MASK). Output belongs under assets/hm-converted/ (git-ignored).
Usage: texture_glb.py <hmpsp.prx.elf> <seg000.bin> <row> <out.glb>
"""
import json, struct, sys

import scene as sc
import scene_mesh as mm
import scene_glb as sg
import texture as tx
import texture_png as tp

SCALE = sg.SCALE


def _corner_triangles(part):
    """(prim, (corner idx x3)) where corner idx are positions inside the primitive."""
    for pr in part["primitives"]:
        yield pr, (0, 1, 2)
        if pr["quad"]:
            yield pr, (2, 1, 3)


def _mesh(buf, model, vram, materials, images, textures, stats):
    prims_out = []
    for part in model["parts"]:
        groups = {}
        for pr, corners in _corner_triangles(part):
            key = (pr["tpage"], pr["clut"]) if pr["uv"] else None
            groups.setdefault(key, []).append((pr, corners))
        for key, tris in sorted(groups.items(), key=lambda kv: (-1, -1) if kv[0] is None else kv[0]):
            pos, col, uv = [], [], []
            for pr, corners in tris:
                for k in corners:
                    x, y, z = part["vertices"][pr["indices"][k]]
                    pos.append(sg.to_gltf(x, y, z))
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
            if key is None:
                name = "untextured"
            else:
                name = "tp%d_clut%04x" % key
                if name not in textures:
                    rgba, cov = tx.render_texpage(vram, key[0], key[1])
                    textures[name] = len(textures)
                    images.append(tp.write_png_rgba(256, 256, rgba))
                    stats[name] = cov
                attrs["TEXCOORD_0"] = buf.add(struct.pack("<%df" % (2 * len(uv)), *[v for u in uv for v in u]),
                                              5126, len(uv), "VEC2")
            if name not in materials:
                materials[name] = len(materials)
            prims_out.append({"attributes": attrs, "material": materials[name], "mode": 4})
    return {"primitives": prims_out}


def build_textured_glb(b, image_bytes, parsed=None):
    """b = scene bytes, image_bytes = the row's 0x100D image. Returns (glb bytes, stats)."""
    s = parsed or sc.parse_scene(b)
    vram = tx.vram_from_image_bytes(image_bytes)
    buf = sg._Buf()
    materials, textures, images, stats = {}, {}, [], {}
    meshes, nodes, mesh_of = [], [], {}

    def mesh_for(off):
        if off not in mesh_of:
            model = mm.parse_model(b, sc.MODEL_OFF + off)
            meshes.append(_mesh(buf, model, vram, materials, images, textures, stats))
            mesh_of[off] = len(meshes) - 1
        return mesh_of[off]

    nodes.append({"name": "scene_model", "mesh": mesh_for(0)})
    for inst in s["instances"]:
        if inst["flags"] & 0x8000 or not inst["model_offsets"][0]:
            continue
        x, y, z = inst["shorts"][:3]
        nodes.append({"name": "instance_%d" % inst["index"], "mesh": mesh_for(inst["model_offsets"][0]),
                      "translation": list(sg.to_gltf(x, y, z)),
                      "extras": {"flags": inst["flags"], "model_offsets": inst["model_offsets"]}})
    gl_images, gl_textures = [], []
    for i, png in enumerate(images):
        buf.views.append({"buffer": 0, "byteOffset": len(buf.data), "byteLength": len(png)})
        buf.data += sg._pad(png)
        gl_images.append({"bufferView": len(buf.views) - 1, "mimeType": "image/png"})
        gl_textures.append({"source": i, "sampler": 0})
    mats = []
    for n in sorted(materials, key=materials.get):
        pbr = {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0}
        m = {"name": n, "doubleSided": True, "pbrMetallicRoughness": pbr}
        if n in textures:
            pbr["baseColorTexture"] = {"index": textures[n]}
            m["alphaMode"] = "MASK"
            m["alphaCutoff"] = 0.5
        mats.append(m)
    doc = {"asset": {"version": "2.0", "generator": "hmconv texture_glb"},
           "scene": 0, "scenes": [{"nodes": list(range(len(nodes)))}], "nodes": nodes, "meshes": meshes,
           "materials": mats or [{"name": "untextured"}],
           "samplers": [{"magFilter": 9728, "minFilter": 9728, "wrapS": 33071, "wrapT": 33071}],
           "images": gl_images, "textures": gl_textures,
           "accessors": buf.accessors, "bufferViews": buf.views,
           "buffers": [{"byteLength": len(sg._pad(bytes(buf.data)))}]}
    if not gl_images:
        for k in ("samplers", "images", "textures"):
            del doc[k]
    js = sg._pad(json.dumps(doc, separators=(",", ":")).encode(), b" ")
    bn = sg._pad(bytes(buf.data))
    total = 12 + 8 + len(js) + 8 + len(bn)
    glb = (struct.pack("<4sII", b"glTF", 2, total) + struct.pack("<I4s", len(js), b"JSON") + js
           + struct.pack("<I4s", len(bn), b"BIN\0") + bn)
    return glb, stats


if __name__ == "__main__":
    from gf import read_table
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    t = read_table(elf)
    r = int(sys.argv[3])
    glb, stats = build_textured_glb(seg[t[r][0]:t[r][1]], seg[t[r][1]:t[r][2]])
    with open(sys.argv[4], "wb") as f:
        f.write(glb)
    print("%s: %d textures" % (sys.argv[4], len(stats)))
