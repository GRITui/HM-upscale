"""Decoder for the 0x100D image container and its byte-RLE chunk compression.

Verified against real data: seg001 sprite blocks (2970 -> 6400 bytes exactly)
and seg007 chunk 2 (30291 -> 31752 bytes exactly). See docs/formats/compression.md.

RLE token stream (PackBits-like, no end marker; the size is known from the
chunk header):
    c in 0x01..0x7F : repeat the NEXT byte c times
    c in 0x80..0xFF : copy the next (256 - c) bytes literally
    c == 0x00       : never seen; treated as an error
"""
import struct

MAGIC = 0x100D
FLAG_STORED = 0x0400  # guess: chunk data is raw (seen on seg007 chunk 1)


def rle_decode(src, expected=None):
    out = bytearray()
    i, n = 0, len(src)
    while i < n:
        c = src[i]
        if c >= 0x80:
            k = 256 - c
            if i + 1 + k > n:
                raise ValueError("literal run overruns input at %d" % i)
            out += src[i + 1:i + 1 + k]
            i += 1 + k
        elif c == 0 or i + 1 >= n:
            raise ValueError("bad token at %d" % i)
        else:
            out += src[i + 1:i + 2] * c
            i += 2
    if expected is not None and len(out) != expected:
        raise ValueError("decoded %d bytes, expected %d" % (len(out), expected))
    return bytes(out)


def rle_encode(raw):
    """Reference encoder (used for round-trip tests; not the game's encoder)."""
    out = bytearray()
    i, n = 0, len(raw)
    lit = bytearray()

    def flush():
        while lit:
            part = lit[:128]
            del lit[:128]
            out.append(256 - len(part) & 0xFF)
            out.extend(part)

    while i < n:
        j = i
        while j < n and raw[j] == raw[i] and j - i < 127:
            j += 1
        if j - i >= 3:
            flush()
            out += bytes([j - i, raw[i]])
            i = j
        else:
            lit.append(raw[i])
            i += 1
    flush()
    return bytes(out)


def bgr555_to_rgba(v):
    r, g, b = v & 31, (v >> 5) & 31, (v >> 10) & 31
    e = lambda x: (x << 3) | (x >> 2)
    a = 0 if v == 0 else 255  # PS1 convention: 0x0000 is transparent
    return (e(r), e(g), e(b), a)


def parse_image(data, offset=0):
    """Parse a 0x100D image. Returns dict with header, clut (RGBA list), chunks.

    Header (16 bytes, u16 LE): magic, n, clut_bytes, 0x0600(unknown),
    clut_vram_x, clut_vram_y, ncolors, 1(unknown). Then CLUT (RGB555),
    then a stream of items. n counts the items: every chunk and every further
    group header, plus the first group (the header above).

    Item stream (each item starts on an even offset; a u16 0 between items is
    a terminator/padding and is skipped):
      chunk        : u16 size, u16 flag, u16 x, y, w(words), h, payload[size]
                     flag bit 0x400 = payload stored raw (size == w*2*h),
                     otherwise byte-RLE. Other flag bits (0x100) are unknown.
      group header : u16 clut_bytes, u16 0x0600, u16 clut_x, clut_y, ncolors,
                     u16 1, then the CLUT. Starts a new palette group; the
                     chunks that follow belong to it.

    Returns "clut"/"chunks" for the first group (backward compatible) plus
    "groups": list of {"clut_vram", "clut", "chunks"} and "end" (offset one
    past the last item, relative to `offset`).
    """
    magic, n, clut_bytes, unk, cx, cy, ncol, unk2 = struct.unpack_from("<8H", data, offset)
    if magic != MAGIC:
        raise ValueError("not a 0x100D image")
    pos = offset + 16

    def read_clut(pos, ncol):
        vals = struct.unpack_from("<%dH" % ncol, data, pos)
        return [bgr555_to_rgba(v) for v in vals], pos + ncol * 2

    clut, pos = read_clut(pos, ncol)
    groups = [{"clut_vram": (cx, cy), "clut": clut, "chunks": []}]
    items = 1
    while items < n:
        pos = (pos + 1) & ~1
        if pos + 12 > len(data):
            break
        size, flag, x, y, w, h = struct.unpack_from("<6H", data, pos)
        if flag == 0x600:
            gcx, gcy, gnc = x, y, w
            clut, pos = read_clut(pos + 12, gnc)
            groups.append({"clut_vram": (gcx, gcy), "clut": clut, "chunks": []})
            items += 1
            continue
        if size == 0:
            pos += 2
            continue
        pay = data[pos + 12:pos + 12 + size]
        raw_len = w * 2 * h
        if flag & FLAG_STORED and size == raw_len:
            raw = bytes(pay)
        else:
            raw = rle_decode(pay, raw_len)
        groups[-1]["chunks"].append({"flag": flag, "rect": (x, y, w, h), "data": raw})
        pos += 12 + size
        items += 1
    first = groups[0]
    return {"clut_vram": first["clut_vram"], "clut": first["clut"],
            "chunks": first["chunks"], "groups": groups, "end": pos - offset}
