#!/usr/bin/env python3
"""Decode scene model blobs (gf.bin table col0, model block at +0x2000) and derive
terrain heights / walking collision. See docs/formats/map.md.

Model blob (offsets relative to the blob start; a scene may hold several, instances
reference them by offset from +0x2000):
  +8        u32 N, part count
  +0xC      N x 0x1C directory entries (7 u32), all offsets relative to blob+0xC:
              [0] vertex array offset   [1] vertex count   [2] end of vertex array
              [3] 0                     [4] primitive list offset
              [5] primitive count       [6] 0 in the file (runtime visibility pointer)
  +0xC+N*0x1C  u32 cell size, then N records of 8 bytes at +4+8*i: i16 x, y, z, w
              (part centre, used to pick the part under the player; radius is
              cell/2 + 600).
Vertex: 8 bytes, i16 x, y, z, pad. Y is DOWN (ground 0, tall things negative).
Primitive: u32 header, then payload, all kinds tile the list exactly.
  header byte 3 & 0xFD: bit 2 (0x04) textured, bit 3 (0x08) quad, bit 4 (0x10)
  per-vertex colours, bit 5 (0x20) always set; bit 1 of that byte (0x02) is kept as
  "bit25". Header low 16 bits are face flags: walking collision skips faces with any
  of 0xC000 set (the game's actor-specific masks are 0xC000, 0xC080, 0xE080).
  payload = [uv words] [colour words] [indices], where
    uv word  = u8 u, u8 v, u16 extra (word 0 = clut, word 1 = tpage, others 0)
    colour   = r, g, b, 0 (0x80 is neutral); 1 word, or one per vertex when 0x10
    indices  = 3 or 4 u16 into the part's vertex array, padded to 8 bytes
  Quads are triangle strips: (i0,i1,i2) and (i1,i2,i3).
"""
import struct

DIR_OFF = 0xC
DIR_SZ = 0x1C
KIND_SIZE = {0x21: 16, 0x31: 24, 0x25: 28, 0x35: 36, 0x29: 16, 0x39: 28, 0x2D: 32, 0x3D: 44}
WALK_MASK = 0xC000
STEP_TOL = 200  # the game's default vertical tolerance (FUN_00014fb0(200))


def parse_model(b, base=0):
    """Parse the model blob at b[base:]. Raises ValueError on malformed data."""
    if base + DIR_OFF > len(b):
        raise ValueError("model header out of range")
    n = struct.unpack_from("<I", b, base + 8)[0]
    if n == 0 or n > 4096 or base + DIR_OFF + n * DIR_SZ > len(b):
        raise ValueError("bad part count %d" % n)
    parts = []
    info = base + DIR_OFF + n * DIR_SZ
    cell = struct.unpack_from("<I", b, info)[0] if n > 1 and info + 4 <= len(b) else 0
    for i in range(n):
        e = struct.unpack_from("<7I", b, base + DIR_OFF + i * DIR_SZ)
        vo, vc, ve, _, po, pc, _ = e
        vb = base + DIR_OFF + vo
        if vc > 0xFFFF or vb + vc * 8 > len(b):
            raise ValueError("vertex array out of range")
        verts = [struct.unpack_from("<3h", b, vb + k * 8) for k in range(vc)]
        prims = []
        o = base + DIR_OFF + po
        for _ in range(pc):
            if o + 4 > len(b):
                raise ValueError("primitive out of range")
            h = struct.unpack_from("<I", b, o)[0]
            kind = (h >> 24) & 0xFD
            size = KIND_SIZE.get(kind)
            if size is None or h & 0xFF0000 != 0x010000 or o + size > len(b):
                raise ValueError("bad primitive header %08x" % h)
            prims.append(_prim(b, o, h, kind, vc))
            o += size
        center = None
        if n > 1 and info + 4 + 8 * i + 8 <= len(b):
            center = list(struct.unpack_from("<3h", b, info + 4 + 8 * i))
        parts.append({"vertices": verts, "primitives": prims, "center": center,
                      "vertex_end": ve, "end": o - base})
    return {"parts": parts, "cell": cell}


def _prim(b, o, h, kind, vcount):
    quad = bool(kind & 0x08)
    tex = bool(kind & 0x04)
    gou = bool(kind & 0x10)
    nv = 4 if quad else 3
    p = o + 4
    uv = []
    if tex:
        for k in range(nv):
            u, v, x = struct.unpack_from("<BBH", b, p + k * 4)
            uv.append((u, v, x))
        p += nv * 4
    ncol = nv if gou else 1
    cols = [tuple(b[p + k * 4:p + k * 4 + 3]) for k in range(ncol)]
    p += ncol * 4
    idx = list(struct.unpack_from("<%dH" % nv, b, p))
    if any(i >= vcount for i in idx):
        raise ValueError("vertex index out of range")
    return {"flags": h & 0xFFFF, "bit25": bool(h & 0x02000000), "quad": quad,
            "indices": idx, "uv": uv, "colors": cols,
            "clut": uv[0][2] if uv else None, "tpage": uv[1][2] if uv else None}


def triangles(part):
    """Yield (prim, (i0,i1,i2)) for every triangle; quad strips keep winding."""
    for pr in part["primitives"]:
        i = pr["indices"]
        yield pr, (i[0], i[1], i[2])
        if pr["quad"]:
            yield pr, (i[2], i[1], i[3])


def bounds(model):
    vs = [v for p in model["parts"] for v in p["vertices"]]
    if not vs:
        return None
    return [[min(v[a] for v in vs) for a in range(3)], [max(v[a] for v in vs) for a in range(3)]]


def heights_at(model, x, z, mask=WALK_MASK):
    """All walkable face heights (game Y, down positive) under world x,z, sorted
    top-most first. Mirrors FUN_000153d0/FUN_00015174/FUN_00015004: a face counts when
    its flags & mask == 0 and the point lies inside the triangle in the XZ plane; the
    height is the face plane evaluated at that point."""
    out = []
    for part in model["parts"]:
        c = part["center"]
        if c is not None and model["cell"]:
            r = model["cell"] // 2 + 600
            if abs(c[0] - x) >= r or abs(c[2] - z) >= r:
                continue
        vs = part["vertices"]
        for pr, t in triangles(part):
            if pr["flags"] & mask:
                continue
            a, b_, c_ = vs[t[0]], vs[t[1]], vs[t[2]]
            if not _inside(a, b_, c_, x, z):
                continue
            y = _plane_y(a, b_, c_, x, z)
            if y is not None:
                out.append(y)
    return sorted(out)


def walk_height(model, x, z, y=None, tol=STEP_TOL, mask=WALK_MASK):
    """Height the game would put an actor at (x, y, z) on, or None when not walkable.
    With y=None the top-most face is used."""
    hs = heights_at(model, x, z, mask)
    if y is not None:
        hs = [h for h in hs if abs(y - h) <= tol]
        return min(hs, key=lambda h: abs(y - h)) if hs else None
    return hs[0] if hs else None


def _inside(a, b, c, x, z):
    d1 = (b[0] - a[0]) * (z - a[2]) - (b[2] - a[2]) * (x - a[0])
    d2 = (c[0] - b[0]) * (z - b[2]) - (c[2] - b[2]) * (x - b[0])
    d3 = (a[0] - c[0]) * (z - c[2]) - (a[2] - c[2]) * (x - c[0])
    return not ((d1 > 0 or d2 > 0 or d3 > 0) and (d1 < 0 or d2 < 0 or d3 < 0))


def _plane_y(a, b, c, x, z):
    u = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    v = (c[0] - b[0], c[1] - b[1], c[2] - b[2])
    nx = u[1] * v[2] - u[2] * v[1]
    ny = u[2] * v[0] - u[0] * v[2]
    nz = u[0] * v[1] - u[1] * v[0]
    if ny == 0:
        return None  # vertical face, no ground height
    return b[1] - (nx * (x - b[0]) + nz * (z - b[2])) / ny


def terrain_grid(model, cell=100, mask=WALK_MASK):
    """Sample walkable top-most heights on a regular XZ grid. Returns the schema used by
    the map JSON: origin [x,z] of cell (0,0), cell size, width, depth and a row-major
    list of heights (null when not walkable)."""
    bb = bounds(model)
    if bb is None:
        return None
    x0 = (bb[0][0] // cell) * cell
    z0 = (bb[0][2] // cell) * cell
    w = (bb[1][0] - x0) // cell + 1
    d = (bb[1][2] - z0) // cell + 1
    heights = []
    for j in range(d):
        for i in range(w):
            h = walk_height(model, x0 + i * cell + cell // 2, z0 + j * cell + cell // 2, mask=mask)
            heights.append(None if h is None else round(h, 1))
    return {"origin": [x0, z0], "cell": cell, "width": w, "depth": d, "heights": heights}
