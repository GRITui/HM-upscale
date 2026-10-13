import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import scene as sc  # noqa: E402
import scene_mesh as mm  # noqa: E402
import scene_glb  # noqa: E402

KINDS = [0x21, 0x31, 0x25, 0x35, 0x29, 0x39, 0x2D, 0x3D]


def prim(kind, idx, flags=0):
    tex, quad, gou = kind & 4, kind & 8, kind & 0x10
    nv = 4 if quad else 3
    out = struct.pack("<I", (kind << 24) | 0x010000 | flags)
    if tex:
        out += b"".join(struct.pack("<BBH", 1 + k, 2 + k, [7, 9, 0, 0][k]) for k in range(nv))
    out += b"\x80\x80\x80\0" * (nv if gou else 1)
    return out + struct.pack("<4H", *(list(idx[:nv]) + [0] * (4 - nv)))


def model(verts, prims, center=(0, 0, 0)):
    """One-part blob at offset 0."""
    vb = b"".join(struct.pack("<4h", *v, 0) for v in verts)
    pb = b"".join(prims)
    d = 0x1C + 8 + 4  # directory + cell + one centre, relative to blob+0xC
    vo = d
    po = vo + len(vb)
    head = struct.pack("<3I", 0, 0, 1)
    ent = struct.pack("<7I", vo, len(verts), vo + len(vb), 0, po, len(prims), 0)
    return head + ent + struct.pack("<I", 0) + struct.pack("<4h", *center, 0) + vb + pb


class T(unittest.TestCase):
    def test_all_kinds_and_strip(self):
        verts = [(0, 0, 0), (100, 0, 0), (0, 0, 100), (100, 0, 100)]
        m = mm.parse_model(model(verts, [prim(k, [0, 1, 2, 3]) for k in KINDS]))
        part = m["parts"][0]
        self.assertEqual(len(part["primitives"]), 8)
        tris = list(mm.triangles(part))
        self.assertEqual(len(tris), 12)  # 4 triangles + 4 quads x 2
        q = [t for t in tris if t[0]["quad"]][:2]
        self.assertEqual([t[1] for t in q], [(0, 1, 2), (2, 1, 3)])
        tex = [p for p in part["primitives"] if p["uv"]][0]
        self.assertEqual((tex["clut"], tex["tpage"]), (7, 9))

    def test_malformed(self):
        with self.assertRaises(ValueError):
            mm.parse_model(b"\0" * 4)
        bad = bytearray(model([(0, 0, 0)] * 3, [prim(0x21, [0, 1, 2])]))
        bad[-8 + 3] = 0x7F
        with self.assertRaises(ValueError):
            mm.parse_model(bytes(bad))

    def test_height_and_grid(self):
        verts = [(0, -50, 0), (200, -50, 0), (0, -50, 200), (200, -50, 200)]
        m = mm.parse_model(model(verts, [prim(0x29, [0, 1, 2, 3]), prim(0x21, [0, 1, 2], 0x8000)]))
        self.assertAlmostEqual(mm.walk_height(m, 50, 50), -50)
        self.assertIsNone(mm.walk_height(m, 500, 500))
        self.assertIsNone(mm.walk_height(m, 50, 50, y=300))  # beyond tolerance
        g = mm.terrain_grid(m, cell=100)
        self.assertEqual((g["width"], g["depth"], len(g["heights"])), (3, 3, 9))
        self.assertEqual(g["heights"][0], -50)

    def test_scenery_flag_not_walkable(self):
        verts = [(0, 0, 0), (100, 0, 0), (0, 0, 100)]
        m = mm.parse_model(model(verts, [prim(0x21, [0, 1, 2], 0x8000)]))
        self.assertIsNone(mm.walk_height(m, 10, 10))

    def test_glb(self):
        verts = [(0, 0, 0), (100, 0, 0), (0, 0, 100), (100, 0, 100)]
        blob = model(verts, [prim(0x3D, [0, 1, 2, 3]), prim(0x21, [0, 1, 2])])
        b = bytearray(0x2000) + blob
        struct.pack_into("<2h", b, 2, 2, 80)
        g = scene_glb.build_glb(bytes(b))
        self.assertEqual(g[:4], b"glTF")
        self.assertEqual(struct.unpack_from("<I", g, 8)[0], len(g))
        self.assertEqual(len(g) % 4, 0)


if __name__ == "__main__":
    unittest.main()
