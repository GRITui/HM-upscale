import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import texture as tx
import texture_glb as tg
import scene_glb as sg


def words(*w):
    return struct.pack("<%dH" % len(w), *w)


class VramTest(unittest.TestCase):
    def test_put_and_bounds(self):
        v = tx.Vram()
        v.put_words(10, 20, 2, 2, words(1, 2, 3, 4))
        self.assertEqual(v.words[20 * 1024 + 10], 1)
        self.assertEqual(v.words[21 * 1024 + 11], 4)
        self.assertEqual(v.written[21 * 1024 + 10], 1)
        with self.assertRaises(ValueError):
            v.put_words(1023, 0, 2, 1, words(1, 2))
        with self.assertRaises(ValueError):
            v.put_words(0, 0, 2, 1, words(1))

    def test_load_image_writes_clut_and_chunks(self):
        im = {"groups": [{"clut_vram": (32, 5), "clut": [0] * 4, "clut_raw": [7, 8, 9, 10],
                          "chunks": [{"rect": (0, 0, 1, 1), "data": words(0x1234)}]}]}
        v = tx.Vram()
        v.load_image(im)
        self.assertEqual(v.words[5 * 1024 + 32: 5 * 1024 + 36], [7, 8, 9, 10])
        self.assertEqual(v.words[0], 0x1234)

    def test_tpage_and_clut(self):
        self.assertEqual(tx.tpage_info(0x89), {"x": 576, "y": 0, "mode": 1})
        self.assertEqual(tx.tpage_info(0x1A)["y"], 256)
        v = tx.Vram()
        v.put_words(16, 3, 2, 1, words(5, 6))
        self.assertEqual(v.clut(1 | (3 << 6), 2), [5, 6])


class RenderTest(unittest.TestCase):
    def test_4bpp_low_nibble_first_and_transparent(self):
        v = tx.Vram()
        # clut at (0,10): index 0 transparent, index 1 = red (R in low 5 bits)
        v.put_words(0, 10, 2, 1, words(0x0000, 0x001F))
        # texel word 0x0010 -> pixel0 = idx0, pixel1 = idx1
        v.put_words(0, 0, 1, 1, words(0x0010))
        rgba, cov = tx.render_texpage(v, 0, 10 << 6, size=4)
        self.assertEqual(rgba[3], 0)
        self.assertEqual(rgba[4:7], bytes([255, 0, 0]))
        self.assertEqual(rgba[7], 255)
        self.assertEqual(cov, 4 / 16.0)

    def test_8bpp_low_byte_first(self):
        v = tx.Vram()
        pal = [0] * 256
        pal[2] = 0x03E0  # green
        v.put_words(0, 10, 256, 1, words(*pal))
        v.put_words(0, 0, 1, 1, words(0x0002))
        rgba, _ = tx.render_texpage(v, 0x80, 10 << 6, size=2)
        self.assertEqual(rgba[0:3], bytes([0, 255, 0]))

    def test_15bpp_direct(self):
        v = tx.Vram()
        v.put_words(0, 0, 1, 1, words(0x7C00))  # blue
        rgba, cov = tx.render_texpage(v, 0x100, 0, size=1)
        self.assertEqual(rgba[0:3], bytes([0, 0, 255]))
        self.assertEqual(cov, 1.0)

    def test_used_texel_coverage(self):
        v = tx.Vram()
        v.put_words(0, 0, 1, 1, words(1))  # covers 8bpp u=0..1, v=0
        full = {"tpage": 0x80, "uv": [(0, 0), (1, 0), (0, 0), (1, 0)]}
        part = {"tpage": 0x80, "uv": [(0, 0), (3, 0), (0, 0), (3, 0)]}
        self.assertEqual(tx.used_texel_coverage(v, [full]), 1.0)
        self.assertEqual(tx.used_texel_coverage(v, [part]), 0.5)


class GlbMeshTest(unittest.TestCase):
    def test_mesh_groups_by_material(self):
        v = tx.Vram()
        v.put_words(0, 0, 1, 1, words(0x7FFF))
        part = {"vertices": [(0, 0, 0), (100, 0, 0), (0, 100, 0), (100, 100, 0)],
                "primitives": [
                    {"quad": True, "indices": [0, 1, 2, 3], "colors": [(128, 128, 128)],
                     "uv": [(0, 0), (1, 0), (0, 1), (1, 1)], "tpage": 0x80, "clut": 0},
                    {"quad": False, "indices": [0, 1, 2], "colors": [(128, 0, 0)],
                     "uv": [], "tpage": None, "clut": None}]}
        buf = sg._Buf()
        materials, textures, images, stats = {}, {}, [], {}
        mesh = tg._mesh(buf, {"parts": [part]}, v, materials, images, textures, stats)
        self.assertEqual(len(mesh["primitives"]), 2)
        self.assertEqual(set(materials), {"untextured", "tp128_clut0000"})
        self.assertEqual(len(images), 1)
        self.assertTrue(images[0].startswith(b"\x89PNG"))


if __name__ == "__main__":
    unittest.main()
