import json, os, struct, sys, tempfile, unittest
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import font
import face
import texture as tx
import decompress as dc


def image(chunks, clut_vram=(896, 250), ncol=16):
    """Build a stored (raw) 0x100D image with one CLUT group; chunks = [(x, y, w, h, bytes)]."""
    clut = b"".join(struct.pack("<H", (i * 0x0421) & 0x7FFF) for i in range(ncol))
    out = struct.pack("<8H", 0x100D, 1 + len(chunks), len(clut), 0x600, clut_vram[0], clut_vram[1], ncol, 1) + clut
    for x, y, w, h, data in chunks:
        out += struct.pack("<6H", len(data), 0x400, x, y, w, h) + data
    return out


class FontTest(unittest.TestCase):
    def test_glyph_rect(self):
        self.assertEqual(font.glyph_rect(0x00), (0, 0, 8, 12))
        self.assertEqual(font.glyph_rect(0x2A), (80, 24, 8, 12))

    def test_cell_grid_drops_partial(self):
        self.assertEqual(len(font.cell_grid(252, 252)), 441)
        self.assertEqual(len(font.cell_grid(25, 13)), 2)

    def test_page_pixels_nibble_order(self):
        v = tx.Vram()
        v.put_words(896, 0, 1, 1, struct.pack("<H", 0x4321))
        w, h, px = font.page_pixels(v, (896, 0, 1, 1))
        self.assertEqual((w, h, list(px)), (4, 1, [1, 2, 3, 4]))

    def test_export(self):
        seg0 = image([(896, 0, 32, 120, bytes(32 * 2 * 120))])
        # Font_all stand-in: one 24x24 4bpp chunk (6 words wide)
        fa = image([(0, 0, 6, 24, bytes(6 * 2 * 24))], clut_vram=(0, 0))
        with tempfile.TemporaryDirectory() as d:
            idx = font.export(seg0, fa, d)
            self.assertEqual(len(idx["font8"]["pages"]), 5)
            self.assertEqual(idx["font_all"]["sheets"][0]["cells"], 4)
            self.assertTrue(os.path.isfile(os.path.join(d, "font_index.json")))
            self.assertTrue(os.path.isfile(os.path.join(d, "font8_clut254.png")))


class FaceTest(unittest.TestCase):
    def test_split_and_export(self):
        blk = image([(0, 0, 4, 8, bytes(4 * 2 * 8))], clut_vram=(0, 0), ncol=256)
        blk = blk.ljust(face.BLOCK, b"\0")
        data = blk * 2 + b"\x00" * 10
        self.assertEqual(len(face.split_blocks(data)), 2)
        with tempfile.TemporaryDirectory() as d:
            idx = face.export(data, d)
            self.assertEqual(len(idx["faces"]), 2)
            self.assertEqual(idx["faces"][0]["width"], 8)
            self.assertEqual(idx["faces"][0]["height"], 8)


if __name__ == "__main__":
    unittest.main()
