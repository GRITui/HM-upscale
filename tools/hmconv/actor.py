#!/usr/bin/env python3
"""Compose actor sprites (res_0e) from a map's VRAM image and export PNG sheets.

Actors (player, NPCs, animals, objects) are 2D sprites, not 3D meshes. Each map blob
(event_s.bin, see map.py) carries a 0x100D image plus char_sprite / object_sprite res_0e
resources. A res_0e tile (u, v, w, h) is an ABSOLUTE VRAM rect: u, w in 16-bit words,
v, h in lines; pixel width is 4*w (4bpp) or 2*w (8bpp, part flag 0x80). A part's
clut_tile (u, v, w) is the palette row in VRAM. See docs/formats/actor.md.

Usage: actor.py <hmpsp.prx.elf> <event_s.bin> <map_id> <out_dir>
"""
import json, os, sys

try:
    from . import map as mp, sprite as sp, texture as tx, texture_png as tp
    from .decompress import bgr555_to_rgba, parse_image
except ImportError:
    import map as mp, sprite as sp, texture as tx, texture_png as tp
    from decompress import bgr555_to_rgba, parse_image

FLIP_X = 0x01  # observed: mirrored twin parts differ only by this bit (see actor.md)


def map_vram(m):
    """Vram holding every image block of a parsed map (map.parse_map)."""
    v = tx.Vram()
    for bl in m["blocks"]:
        if bl["kind"] == "image":
            v.load_image(parse_image(bl["data"]))
    return v


def part_pixels(res, part, vram):
    """(w, h, [rgba]) of one part, honouring bpp and the per-part CLUT."""
    t = res["tiles"][part["tile"]]
    c = res["tiles"][part["clut_tile"]]
    bpp8 = part["bpp8"]
    ncol = 256 if bpp8 else 16
    base = c["v"] * tx.VW + c["u"]
    pal = [bgr555_to_rgba(w) for w in vram.words[base:base + ncol]]
    div = 2 if bpp8 else 4
    w, h = t["w"] * div, t["h"]
    px = []
    for j in range(h):
        row = (t["v"] + j) * tx.VW + t["u"]
        for i in range(w):
            word = vram.words[row + i // div]
            if bpp8:
                idx = (word >> (8 * (i & 1))) & 255
            else:
                idx = (word >> (4 * (i & 3))) & 15
            px.append(pal[idx])
    if part["flags"] & FLIP_X:
        px = [px[j * w + (w - 1 - i)] for j in range(h) for i in range(w)]
    return w, h, px


def render_sprite(res, idx, vram):
    """Compose sprite idx; returns (W, H, rgba bytes, (x0, y0)). Last part drawn first."""
    sprite = res["sprites"][idx]
    parts = [res["parts"][i] for i in reversed(sprite["parts"])]
    if not parts:
        return 0, 0, b"", (0, 0)
    pix = [(p, part_pixels(res, p, vram)) for p in parts]
    x0 = min(p["x"] for p, _ in pix)
    y0 = min(p["y"] for p, _ in pix)
    x1 = max(p["x"] + g[0] for p, g in pix)
    y1 = max(p["y"] + g[1] for p, g in pix)
    W, H = x1 - x0, y1 - y0
    buf = bytearray(W * H * 4)
    for p, (w, h, px) in pix:
        for j in range(h):
            for i in range(w):
                c = px[j * w + i]
                if c[3]:
                    o = ((p["y"] - y0 + j) * W + p["x"] - x0 + i) * 4
                    buf[o:o + 4] = bytes(c)
    return W, H, bytes(buf), (x0, y0)


def sheet(res, vram, cols=8, pad=2):
    """Grid sheet of every sprite. Returns (w, h, rgba, cells) with cell rects."""
    rend = [render_sprite(res, i, vram) for i in range(len(res["sprites"]))]
    cw = max([r[0] for r in rend] + [1]) + pad
    ch = max([r[1] for r in rend] + [1]) + pad
    rows = (len(rend) + cols - 1) // cols
    W, H = cw * cols, ch * max(rows, 1)
    buf = bytearray(W * H * 4)
    cells = []
    for n, (w, h, rgba, org) in enumerate(rend):
        cx, cy = (n % cols) * cw, (n // cols) * ch
        for j in range(h):
            o = ((cy + j) * W + cx) * 4
            buf[o:o + w * 4] = rgba[j * w * 4:(j + 1) * w * 4]
        cells.append({"sprite": n, "x": cx, "y": cy, "w": w, "h": h, "origin": list(org)})
    return W, H, bytes(buf), cells


def export_map(m, out_dir, map_id):
    """Write sheet PNG + JSON per sprite resource of one parsed map. Returns file names."""
    os.makedirs(out_dir, exist_ok=True)
    vram = map_vram(m)
    names = []
    for bl in m["blocks"]:
        if bl["kind"] not in ("char_sprite", "object_sprite"):
            continue
        res = sp.parse_res0e(bl["data"])
        W, H, rgba, cells = sheet(res, vram)
        stem = "map%03d_b%02d_%s_%d" % (map_id, bl["index"], bl["kind"], bl["res_id"])
        with open(os.path.join(out_dir, stem + ".png"), "wb") as f:
            f.write(tp.write_png_rgba(W, H, rgba))
        meta = {"map": map_id, "kind": bl["kind"], "res_id": bl["res_id"], "counts": res["counts"],
                "cells": cells, "anims": res["anims"], "frames": res["frames"]}
        with open(os.path.join(out_dir, stem + ".json"), "w") as f:
            json.dump(meta, f)
        names.append(stem)
    return names


if __name__ == "__main__":
    elf = open(sys.argv[1], "rb").read()
    ev = open(sys.argv[2], "rb").read()
    i = int(sys.argv[3])
    idx = mp.index(elf)
    print(export_map(mp.parse_map(ev[idx[i]:idx[i + 1]]), sys.argv[4], i))
