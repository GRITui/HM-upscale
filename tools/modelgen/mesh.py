"""Pure-geometry mesh builder and GLB writer (stdlib only).

Conventions: glTF 2.0, right-handed, +Y up, metres, 1 tile = 1 m.
All shading is flat (per-face normals) and colour comes from COLOR_0
(normalised unsigned byte, linear) on one shared material.
"""
import json
import math
import random
import struct


def srgb_to_linear(c):
    c = c / 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def hexcolor(h):
    """'#rrggbb' sRGB -> (r, g, b, a) linear bytes."""
    h = h.lstrip("#")
    rgb = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    return tuple(int(round(srgb_to_linear(v) * 255)) for v in rgb) + (255,)


def _sub(a, b):
    return (a[0] - b[0], a[1] - b[1], a[2] - b[2])


def _cross(a, b):
    return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])


def _dot(a, b):
    return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def _rot(p, rot):
    rx, ry, rz = (math.radians(v) for v in rot)
    x, y, z = p
    if rx:
        c, s = math.cos(rx), math.sin(rx)
        y, z = y * c - z * s, y * s + z * c
    if ry:
        c, s = math.cos(ry), math.sin(ry)
        x, z = x * c + z * s, -x * s + z * c
    if rz:
        c, s = math.cos(rz), math.sin(rz)
        x, y = x * c - y * s, x * s + y * c
    return (x, y, z)


class Part:
    def __init__(self, name, pivot):
        self.name = name
        self.pivot = pivot
        self.tris = []  # (p0, p1, p2, rgba) in model space

    def _emit(self, local_tris, center, rot, color):
        """Rotate about prim centre, translate, orient outward (convex/star shapes)."""
        col = hexcolor(color) if isinstance(color, str) else color
        for tri in local_tris:
            pts = [_rot(p, rot) for p in tri]
            pts = [(p[0] + center[0], p[1] + center[1], p[2] + center[2]) for p in pts]
            n = _cross(_sub(pts[1], pts[0]), _sub(pts[2], pts[0]))
            if _dot(n, n) < 1e-14:
                continue  # degenerate
            cen = tuple(sum(p[i] for p in pts) / 3.0 for i in range(3))
            if _dot(n, _sub(cen, center)) < 0:
                pts = [pts[0], pts[2], pts[1]]
            self.tris.append((pts[0], pts[1], pts[2], col))

    def box(self, center, size, color, rot=(0, 0, 0)):
        hx, hy, hz = (s / 2.0 for s in size)
        v = [(sx * hx, sy * hy, sz * hz) for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)]
        quads = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        tris = []
        for a, b, c, d in quads:
            tris += [(v[a], v[b], v[c]), (v[a], v[c], v[d])]
        self._emit(tris, center, rot, color)

    def cyl(self, center, r, h, seg, color, r_top=None, rot=(0, 0, 0), phase=0.0):
        """Y-axis frustum centred on `center`. r_top=0 gives a cone."""
        rt = r if r_top is None else r_top
        ring = lambda rad, y: [(rad * math.cos(phase + 2 * math.pi * i / seg), y,
                                rad * math.sin(phase + 2 * math.pi * i / seg)) for i in range(seg)]
        bot, top = ring(r, -h / 2.0), ring(rt, h / 2.0)
        tris = []
        for i in range(seg):
            j = (i + 1) % seg
            if rt > 1e-9:
                tris += [(bot[i], bot[j], top[j]), (bot[i], top[j], top[i])]
            else:
                tris.append((bot[i], bot[j], (0, h / 2.0, 0)))
            if r > 1e-9:
                tris.append(((0, -h / 2.0, 0), bot[j], bot[i]))
            if rt > 1e-9:
                tris.append(((0, h / 2.0, 0), top[i], top[j]))
        self._emit(tris, center, rot, color)

    def sphere(self, center, radii, seg, rings, color, rot=(0, 0, 0), jitter=0.0, seed=0):
        if not isinstance(radii, (tuple, list)):
            radii = (radii,) * 3
        rng = random.Random(seed)
        grid = [[(0, radii[1], 0)] * 1]
        for r in range(1, rings):
            th = math.pi * r / rings
            row = []
            for s in range(seg):
                ph = 2 * math.pi * s / seg
                k = 1.0 + (rng.uniform(-jitter, jitter) if jitter else 0.0)
                row.append((radii[0] * math.sin(th) * math.cos(ph) * k, radii[1] * math.cos(th) * k,
                            radii[2] * math.sin(th) * math.sin(ph) * k))
            grid.append(row)
        grid.append([(0, -radii[1], 0)])
        tris = []
        for s in range(seg):
            t = (s + 1) % seg
            tris.append((grid[0][0], grid[1][s], grid[1][t]))
            tris.append((grid[-1][0], grid[-2][t], grid[-2][s]))
            for r in range(1, rings - 1):
                a, b, c, d = grid[r][s], grid[r][t], grid[r + 1][t], grid[r + 1][s]
                tris += [(a, d, c), (a, c, b)]
        self._emit(tris, center, rot, color)

    def roof(self, center, size, color, rot=(0, 0, 0)):
        """Triangular prism: ridge along Z, size=(width x, height y, length z)."""
        w, h, l = (s / 2.0 for s in size)
        a0, a1 = (-w, -h, -l), (w, -h, -l)
        b0, b1 = (-w, -h, l), (w, -h, l)
        r0, r1 = (0, h, -l), (0, h, l)
        tris = [(a0, a1, r0), (b0, r1, b1), (a0, r0, r1), (a0, r1, b0),
                (a1, b1, r1), (a1, r1, r0), (a0, b0, b1), (a0, b1, a1)]
        self._emit(tris, center, rot, color)


class Model:
    def __init__(self, name):
        self.name = name
        self.parts = []

    def part(self, name="body", pivot=(0.0, 0.0, 0.0)):
        p = Part(name, pivot)
        self.parts.append(p)
        return p

    def tri_count(self):
        return sum(len(p.tris) for p in self.parts)

    def to_glb(self):
        buf = bytearray()
        views, accessors, meshes, nodes = [], [], [], []

        def add_view(data, target):
            while len(buf) % 4:
                buf.append(0)
            views.append({"buffer": 0, "byteOffset": len(buf), "byteLength": len(data), "target": target})
            buf.extend(data)
            return len(views) - 1

        for part in self.parts:
            px, py, pz = part.pivot
            pos, nor, col, idx = [], [], [], []
            for p0, p1, p2, c in part.tris:
                n = _cross(_sub(p1, p0), _sub(p2, p0))
                ln = math.sqrt(_dot(n, n))
                n = (n[0] / ln, n[1] / ln, n[2] / ln)
                for p in (p0, p1, p2):
                    idx.append(len(pos))
                    pos.append((p[0] - px, p[1] - py, p[2] - pz))
                    nor.append(n)
                    col.append(c)
            n_v = len(pos)
            assert n_v < 65536
            pos_f = [round(v, 6) for p in pos for v in p]
            mins = [min(p[i] for p in pos) for i in range(3)]
            maxs = [max(p[i] for p in pos) for i in range(3)]
            vp = add_view(struct.pack("<%df" % len(pos_f), *pos_f), 34962)
            vn = add_view(struct.pack("<%df" % (n_v * 3), *[round(v, 5) for n in nor for v in n]), 34962)
            vc = add_view(bytes(b for c in col for b in c), 34962)
            vi = add_view(struct.pack("<%dH" % len(idx), *idx), 34963)
            base = len(accessors)
            accessors += [
                {"bufferView": vp, "componentType": 5126, "count": n_v, "type": "VEC3",
                 "min": [round(m, 6) for m in mins], "max": [round(m, 6) for m in maxs]},
                {"bufferView": vn, "componentType": 5126, "count": n_v, "type": "VEC3"},
                {"bufferView": vc, "componentType": 5121, "normalized": True, "count": n_v, "type": "VEC4"},
                {"bufferView": vi, "componentType": 5123, "count": len(idx), "type": "SCALAR"},
            ]
            meshes.append({"name": part.name, "primitives": [{
                "attributes": {"POSITION": base, "NORMAL": base + 1, "COLOR_0": base + 2},
                "indices": base + 3, "material": 0, "mode": 4}]})
            node = {"name": part.name, "mesh": len(meshes) - 1}
            if part.pivot != (0.0, 0.0, 0.0):
                node["translation"] = [px, py, pz]
            nodes.append(node)
        while len(buf) % 4:
            buf.append(0)
        doc = {
            "asset": {"version": "2.0", "generator": "HM-upscale tools/modelgen"},
            "scene": 0,
            "scenes": [{"name": self.name, "nodes": [0]}],
            "nodes": [{"name": self.name, "children": list(range(1, len(nodes) + 1))}] + nodes,
            "meshes": meshes, "accessors": accessors, "bufferViews": views,
            "buffers": [{"byteLength": len(buf)}],
            "materials": [{"name": "hm_vertex_color", "pbrMetallicRoughness": {
                "baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0, "roughnessFactor": 1.0}}],
        }
        js = json.dumps(doc, sort_keys=True, separators=(",", ":")).encode()
        js += b" " * (-len(js) % 4)
        total = 12 + 8 + len(js) + 8 + len(buf)
        return (struct.pack("<III", 0x46546C67, 2, total) + struct.pack("<II", len(js), 0x4E4F534A) + js
                + struct.pack("<II", len(buf), 0x004E4942) + bytes(buf))
