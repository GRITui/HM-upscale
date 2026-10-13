"""PS1-style VRAM emulation for the game's 0x100D texture banks.

The PSP build runs the original PS1 renderer ("PceEmu"), so there is no PSP swizzle:
textures live in a 1024x512 16-bit VRAM and faces reference them through a tpage word
and a clut word. See docs/formats/texture.md.

  tpage : bits 0-3 x base (64 words), bit 4 y base (256 lines), bits 5-6 blend mode,
          bits 7-8 colour depth (0 = 4bpp, 1 = 8bpp, 2 = 15bpp direct)
  clut  : bits 0-5 x / 16, bits 6-14 y   (palette of 16 or 256 words at that VRAM spot)

A 0x100D image (decompress.parse_image) is a list of CLUT groups and pixel chunks; every
chunk is written to its VRAM rect and every CLUT to its clut_vram position. Word 0x0000
is transparent.
"""
import struct

try:
    from .decompress import parse_image, bgr555_to_rgba
except ImportError:
    from decompress import parse_image, bgr555_to_rgba

VW, VH = 1024, 512


class Vram:
    def __init__(self):
        self.words = [0] * (VW * VH)
        self.written = bytearray(VW * VH)

    def put_words(self, x, y, w, h, data):
        """Write a w x h rect of little-endian 16-bit words (w in words)."""
        if len(data) != w * h * 2:
            raise ValueError("rect data %d bytes, expected %d" % (len(data), w * h * 2))
        if x + w > VW or y + h > VH:
            raise ValueError("rect outside VRAM")
        vals = struct.unpack("<%dH" % (w * h), data)
        for j in range(h):
            base = (y + j) * VW + x
            self.words[base:base + w] = vals[j * w:(j + 1) * w]
            self.written[base:base + w] = b"\x01" * w

    def load_image(self, im):
        """Write a parse_image() result (all CLUT groups and chunks)."""
        for g in im["groups"]:
            cx, cy = g["clut_vram"]
            n = len(g["clut"])
            # the CLUT words were converted to RGBA by parse_image; keep the raw words
            raw = g.get("clut_raw")
            if raw is not None and cx + n <= VW and cy < VH:
                self.words[cy * VW + cx:cy * VW + cx + n] = raw
                self.written[cy * VW + cx:cy * VW + cx + n] = b"\x01" * n
            for ch in g["chunks"]:
                x, y, w, h = ch["rect"]
                if w and h:
                    self.put_words(x, y, w, h, ch["data"])

    def clut(self, clut, n):
        cx, cy = (clut & 63) * 16, (clut >> 6) & 511
        return self.words[cy * VW + cx:cy * VW + cx + n]


def tpage_info(tpage):
    return {"x": (tpage & 15) * 64, "y": ((tpage >> 4) & 1) * 256, "mode": (tpage >> 7) & 3}


def render_texpage(vram, tpage, clut, size=256):
    """Render the size x size texture window of a tpage with a clut as RGBA bytes.

    Returns (rgba, coverage) where coverage is the fraction of texels whose VRAM source
    was written by a loaded image (1.0 = every texel known)."""
    t = tpage_info(tpage)
    out = bytearray(size * size * 4)
    known = 0
    pal = None
    if t["mode"] == 0:
        pal = [bgr555_to_rgba(w) for w in vram.clut(clut, 16)]
    elif t["mode"] == 1:
        pal = [bgr555_to_rgba(w) for w in vram.clut(clut, 256)]
    for v in range(size):
        row = (t["y"] + v) * VW + t["x"]
        for u in range(size):
            if t["mode"] == 0:
                wi = row + (u >> 2)
                idx = (vram.words[wi] >> (4 * (u & 3))) & 15
                px = pal[idx]
            elif t["mode"] == 1:
                wi = row + (u >> 1)
                idx = (vram.words[wi] >> (8 * (u & 1))) & 255
                px = pal[idx]
            else:
                wi = row + u
                px = bgr555_to_rgba(vram.words[wi])
            if vram.written[wi]:
                known += 1
            o = (v * size + u) * 4
            out[o:o + 4] = bytes(px)
    return bytes(out), known / float(size * size)


def used_texel_coverage(vram, prims):
    """Fraction of the texels actually sampled by the given primitives (bounding box of
    each face's uv) that come from written VRAM. prims have 'uv', 'tpage'."""
    tot = ok = 0
    for pr in prims:
        if not pr.get("uv") or pr.get("tpage") is None:
            continue
        t = tpage_info(pr["tpage"])
        us = [p[0] for p in pr["uv"]]
        vs = [p[1] for p in pr["uv"]]
        for v in range(min(vs), max(vs) + 1):
            for u in range(min(us), max(us) + 1):
                div = 4 if t["mode"] == 0 else 2 if t["mode"] == 1 else 1
                wi = (t["y"] + v) * VW + t["x"] + u // div
                tot += 1
                ok += vram.written[wi]
    return ok / tot if tot else 1.0


def vram_from_image_bytes(data):
    v = Vram()
    v.load_image(parse_image(data))
    return v
