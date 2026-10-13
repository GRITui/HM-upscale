"""Font decoding: the 8x12 text page and the Font_all 12x12 glyph sheets.

See docs/formats/font.md. Everything here is pure functions over bytes so tests can
use synthetic fixtures; export() writes PNG + JSON to a git-ignored output directory.
"""
import json
import os

try:
    from .decompress import parse_image
    from .texture import Vram
    from . import texture_png as tp
except ImportError:
    from decompress import parse_image
    from texture import Vram
    import texture_png as tp

GLYPH_W, GLYPH_H = 8, 12
PAGE_COLS = 16
PAGE_RECT = (896, 0, 32, 120)        # words: 128 x 120 px at 4bpp
CLUT_ROWS = (250, 251, 252, 253, 254)  # VRAM rows holding the colour variants (x = 896)
ICON_X = (928, 934, 940, 946)          # 24x24 icon chunks (6 words wide)
CELL = 12


def page_pixels(vram, rect=PAGE_RECT):
    """Return (width, height, indices) of the 4bpp page at rect (words) in VRAM."""
    x, y, w, h = rect
    width = w * 4
    out = bytearray(width * h)
    for j in range(h):
        row = vram.words[(y + j) * 1024 + x:(y + j) * 1024 + x + w]
        for i, v in enumerate(row):
            o = j * width + i * 4
            out[o] = v & 15
            out[o + 1] = (v >> 4) & 15
            out[o + 2] = (v >> 8) & 15
            out[o + 3] = (v >> 12) & 15
    return width, h, bytes(out)


def glyph_rect(code, cols=PAGE_COLS, gw=GLYPH_W, gh=GLYPH_H):
    """Pixel rect (x, y, w, h) of a text code in the 8x12 page: col = code & 15, row = code >> 4."""
    return ((code & (cols - 1)) * gw, (code >> 4) * gh, gw, gh)


def cell_grid(width, height, cell=CELL):
    """Cells (col, row) of a width x height sheet; partial edge cells are dropped."""
    return [(c, r) for r in range(height // cell) for c in range(width // cell)]


def crop(width, indices, x, y, w, h):
    return b"".join(indices[(y + j) * width + x:(y + j) * width + x + w] for j in range(h))


def _pal(vram, row, x=896, n=16):
    try:
        from .decompress import bgr555_to_rgba
    except ImportError:
        from decompress import bgr555_to_rgba
    return [bgr555_to_rgba(v) for v in vram.words[row * 1024 + x:row * 1024 + x + n]]


def export(seg000, font_all, out_dir):
    """seg000: bytes of boy/seg000.bin (8x12 page). font_all: bytes of seg007.bin (Font_all.tex).

    Writes font8_<clutrow>.png, icons, font_all_<n>.png and font_index.json. Returns the index.
    """
    os.makedirs(out_dir, exist_ok=True)
    index = {"font8": {"glyph": [GLYPH_W, GLYPH_H], "cols": PAGE_COLS, "pages": [], "icons": []},
             "font_all": {"cell": [CELL, CELL], "sheets": []}}
    vram = Vram()
    vram.load_image(parse_image(seg000, 0))
    w, h, px = page_pixels(vram)
    for row in CLUT_ROWS:
        name = "font8_clut%d.png" % row
        with open(os.path.join(out_dir, name), "wb") as f:
            f.write(tp.write_png_indexed(w, h, px, _pal(vram, row)))
        index["font8"]["pages"].append({"file": name, "clut_row": row})
    for n, x in enumerate(ICON_X):
        iw, ih, ipx = page_pixels(vram, (x, 0, 6, 24))
        name = "icon_%d.png" % n
        with open(os.path.join(out_dir, name), "wb") as f:
            f.write(tp.write_png_indexed(iw, ih, ipx, _pal(vram, CLUT_ROWS[0])))
        index["font8"]["icons"].append({"file": name, "size": [iw, ih]})
    for png, info in tp.image_to_pngs(font_all):
        name = "font_all_%d.png" % info["chunk"]
        with open(os.path.join(out_dir, name), "wb") as f:
            f.write(png)
        cols, rows = info["width"] // CELL, info["height"] // CELL
        index["font_all"]["sheets"].append({"file": name, "cols": cols, "rows": rows,
                                            "cells": cols * rows, "mapping": "unknown"})
    with open(os.path.join(out_dir, "font_index.json"), "w") as f:
        json.dump(index, f, indent=1)
    return index
