"""PNG writer (zlib only) and 0x100D image to PNG conversion.

No Pillow: PNG is written by hand (palette PNG with tRNS, or RGBA).
Pixel layout (docs/formats/texture.md): chunk RECT width is in 16-bit VRAM words;
8bpp pixel width = 2*w, 4bpp = 4*w (low nibble first). CLUT entry 0x0000 is
transparent (decompress.bgr555_to_rgba already encodes that as alpha 0).
"""
import struct
import zlib

try:
    from .decompress import parse_image
except ImportError:  # run as a script
    from decompress import parse_image


def _chunk(tag, body):
    return struct.pack(">I", len(body)) + tag + body + struct.pack(">I", zlib.crc32(tag + body) & 0xFFFFFFFF)


def _png(width, height, ctype, raw_rows, extra=()):
    ihdr = struct.pack(">IIBBBBB", width, height, 8, ctype, 0, 0, 0)
    out = [b"\x89PNG\r\n\x1a\n", _chunk(b"IHDR", ihdr)]
    out.extend(extra)
    out.append(_chunk(b"IDAT", zlib.compress(raw_rows, 9)))
    out.append(_chunk(b"IEND", b""))
    return b"".join(out)


def write_png_rgba(width, height, rgba):
    """rgba: bytes of width*height*4. Returns PNG bytes."""
    if len(rgba) != width * height * 4:
        raise ValueError("rgba length mismatch")
    stride = width * 4
    rows = b"".join(b"\x00" + rgba[y * stride:(y + 1) * stride] for y in range(height))
    return _png(width, height, 6, rows)


def write_png_indexed(width, height, indices, palette):
    """indices: bytes width*height (one byte per pixel); palette: list of (r,g,b,a)."""
    if len(indices) != width * height:
        raise ValueError("index length mismatch")
    if not 0 < len(palette) <= 256:
        raise ValueError("palette size")
    if indices and max(indices) >= len(palette):
        raise ValueError("pixel index beyond palette")
    plte = b"".join(bytes(c[:3]) for c in palette)
    extra = [_chunk(b"PLTE", plte)]
    if any(c[3] != 255 for c in palette):
        extra.append(_chunk(b"tRNS", bytes(c[3] for c in palette)))
    rows = b"".join(b"\x00" + indices[y * width:(y + 1) * width] for y in range(height))
    return _png(width, height, 3, rows, extra)


def read_png_rgba(png):
    """Minimal decoder for what this module writes (types 3 and 6, filter 0). Used by tests."""
    assert png[:8] == b"\x89PNG\r\n\x1a\n"
    pos, idat, plte, trns, hdr = 8, b"", b"", b"", None
    while pos < len(png):
        n, tag = struct.unpack_from(">I4s", png, pos)
        body = png[pos + 8:pos + 8 + n]
        if tag == b"IHDR":
            hdr = struct.unpack(">IIBBBBB", body)
        elif tag == b"PLTE":
            plte = body
        elif tag == b"tRNS":
            trns = body
        elif tag == b"IDAT":
            idat += body
        pos += 12 + n
    w, h, _bd, ct = hdr[0], hdr[1], hdr[2], hdr[3]
    raw = zlib.decompress(idat)
    bpp = 1 if ct == 3 else 4
    out = bytearray()
    for y in range(h):
        row = raw[y * (w * bpp + 1) + 1:(y + 1) * (w * bpp + 1)]
        if ct == 3:
            for i in row:
                out += plte[i * 3:i * 3 + 3] + bytes([trns[i] if i < len(trns) else 255])
        else:
            out += row
    return w, h, bytes(out)


def unpack_pixels(data, bpp):
    """Expand packed chunk bytes to one index byte per pixel (4bpp: low nibble first)."""
    if bpp == 8:
        return bytes(data)
    if bpp == 4:
        out = bytearray(len(data) * 2)
        out[0::2] = bytes(b & 15 for b in data)
        out[1::2] = bytes(b >> 4 for b in data)
        return bytes(out)
    raise ValueError("unsupported bpp %d" % bpp)


def image_to_pngs(data):
    """Convert a 0x100D image to a list of (png_bytes, info) per non-empty chunk.

    bpp comes from the CLUT size (16 colours -> 4bpp, otherwise 8bpp).
    """
    im = parse_image(data)
    clut = im["clut"]
    bpp = 4 if len(clut) <= 16 else 8
    pal = list(clut) + [(0, 0, 0, 0)] * (256 - len(clut)) if bpp == 8 else list(clut)
    res = []
    for ci, ch in enumerate(im["chunks"]):
        x, y, w, h = ch["rect"]
        width = w * (4 if bpp == 4 else 2)
        if width == 0 or h == 0:
            continue  # empty terminator chunk
        px = unpack_pixels(ch["data"], bpp)
        if len(px) != width * h:
            raise ValueError("chunk %d: %d pixels, expected %d" % (ci, len(px), width * h))
        res.append((write_png_indexed(width, h, px, pal),
                    {"chunk": ci, "width": width, "height": h, "bpp": bpp,
                     "vram": [x, y], "flag": ch["flag"]}))
    return res


def convert_image(in_path, out_path):
    """CLI helper. One chunk -> out_path; several -> out_path stem + _<chunk>.png. Returns paths."""
    import os
    with open(in_path, "rb") as f:
        pngs = image_to_pngs(f.read())
    if not pngs:
        raise ValueError("image has no non-empty chunks")
    stem, ext = os.path.splitext(out_path)
    d = os.path.dirname(out_path)
    if d:
        os.makedirs(d, exist_ok=True)
    paths = []
    for png, info in pngs:
        p = out_path if len(pngs) == 1 else "%s_%d%s" % (stem, info["chunk"], ext or ".png")
        with open(p, "wb") as f:
            f.write(png)
        paths.append(p)
    return paths
