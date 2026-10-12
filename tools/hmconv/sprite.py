#!/usr/bin/env python3
"""Parse res_0e sprite resources (FUN_000086ec) to JSON metadata. See docs/formats/sprite.md.

Layout (all LE): u32 magic 0x0e; u16 counts[5] (tiles, sprites, parts, anims, frames);
u16 pad; u32 offsets[5] at byte 0x10; header ends at 0x24.
  tiles   8 B  : s16 u, v, w, h
  sprites 4 B  : s16 part_count, u16 part_offset (bytes into parts)
  parts   12 B : u16 tile, u16 clut_tile, s16 x, s16 y, u8 flags(+8), 3 unknown bytes
  anims   4 B  : u16 frame_count, u16 frame_offset (bytes into frames)
  frames  10 B : u16 sprite, s16 xoff, s16 yoff, u8 duration(+6), u8 control(+7), u16 unknown(+8)
Texpage/CLUT bases are runtime state, so tile coords are relative.
"""
import json, struct, sys

HDR = 0x24
SECTIONS = [("tiles", 8), ("sprites", 4), ("parts", 12), ("anims", 4), ("frames", 10)]


def parse_res0e(data):
    if len(data) < HDR or struct.unpack_from("<I", data, 0)[0] != 0x0E:
        raise ValueError("not a res_0e resource")
    counts = struct.unpack_from("<5H", data, 4)
    offs = struct.unpack_from("<5I", data, 0x10)
    out = {}
    for (name, sz), n, off in zip(SECTIONS, counts, offs):
        if off + n * sz > len(data):
            raise ValueError("%s section out of range" % name)
        out[name] = [data[off + i * sz: off + (i + 1) * sz] for i in range(n)]
    tiles = [dict(zip("uvwh", struct.unpack("<4h", b))) for b in out["tiles"]]
    parts = []
    for b in out["parts"]:
        t, c, x, y = struct.unpack_from("<HHhh", b)
        fl = b[8]
        parts.append({"tile": t, "clut_tile": c, "x": x, "y": y, "flags": fl,
                      "bpp8": bool(fl & 0x80), "semi_transparent": bool(fl & 0x10),
                      "unknown": b[9:].hex()})
    sprites = []
    for b in out["sprites"]:
        n, o = struct.unpack("<hH", b)
        idx = o // 12
        sprites.append({"part_count": n, "parts": list(range(idx, idx + n))})
    frames = []
    for b in out["frames"]:
        s, xo, yo, unk = struct.unpack_from("<Hhh2xH", b)
        ctl = b[7]
        frames.append({"sprite": s, "xoff": xo, "yoff": yo, "unknown": unk,
                       "duration": b[6], "control": ctl,
                       "hold": ctl == 0xFF, "jump": (ctl & 0x7F) if ctl & 0x80 and ctl != 0xFF else None})
    anims = []
    for b in out["anims"]:
        n, o = struct.unpack("<HH", b)
        anims.append({"frame_count": n, "frames": list(range(o // 10, o // 10 + n))})
    return {"counts": dict(zip([s[0] for s in SECTIONS], counts)), "tiles": tiles,
            "sprites": sprites, "parts": parts, "anims": anims, "frames": frames}


def render_sprite(res, sprite_idx, vram_rgba):
    """Compose a sprite to RGBA. vram_rgba(u, v, part) -> (r,g,b,a); returns (w,h,pixels,origin)."""
    sp = res["sprites"][sprite_idx]
    ps = [res["parts"][i] for i in reversed(sp["parts"])]
    if not ps:
        return 0, 0, [], (0, 0)
    x0 = min(p["x"] for p in ps)
    y0 = min(p["y"] for p in ps)
    x1 = max(p["x"] + res["tiles"][p["tile"]]["w"] for p in ps)
    y1 = max(p["y"] + res["tiles"][p["tile"]]["h"] for p in ps)
    W, H = x1 - x0, y1 - y0
    buf = [(0, 0, 0, 0)] * (W * H)
    for p in ps:
        t = res["tiles"][p["tile"]]
        for j in range(t["h"]):
            for i in range(t["w"]):
                px = vram_rgba(t["u"] + i, t["v"] + j, p)
                if px[3]:
                    buf[(p["y"] - y0 + j) * W + p["x"] - x0 + i] = px
    return W, H, buf, (x0, y0)


if __name__ == "__main__":
    json.dump(parse_res0e(open(sys.argv[1], "rb").read()), sys.stdout, indent=1)
