import os, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

import actor as ac
import texture as tx


def res(parts, sprites, tiles):
    return {"tiles": tiles, "parts": parts, "sprites": sprites}


def part(tile, clut, x, y, flags=0):
    return {"tile": tile, "clut_tile": clut, "x": x, "y": y, "flags": flags,
            "bpp8": bool(flags & 0x80)}


class ActorTest(unittest.TestCase):
    def vram4(self):
        v = tx.Vram()
        # palette at (0,10): index1 = red (0x001F), index2 = green (0x03E0)
        v.put_words(0, 10, 16, 1, b"".join(w.to_bytes(2, "little") for w in [0, 0x1F, 0x3E0] + [0] * 13))
        # one word = 4 pixels 1,2,0,1 (low nibble first)
        v.put_words(20, 30, 1, 1, (0x1 << 12 | 0x0 << 8 | 0x2 << 4 | 0x1).to_bytes(2, "little"))
        return v

    def tiles(self):
        return [{"u": 20, "v": 30, "w": 1, "h": 1}, {"u": 0, "v": 10, "w": 16, "h": 1}]

    def test_4bpp_part(self):
        r = res([part(0, 1, 0, 0)], [{"parts": [0]}], self.tiles())
        w, h, px = ac.part_pixels(r, r["parts"][0], self.vram4())
        self.assertEqual((w, h), (4, 1))
        self.assertEqual(px[0][:3], (255, 0, 0))
        self.assertEqual(px[1][:3], (0, 255, 0))
        self.assertEqual(px[2][3], 0)  # index 0 transparent

    def test_flip_x(self):
        r = res([part(0, 1, 0, 0, ac.FLIP_X)], [{"parts": [0]}], self.tiles())
        w, h, px = ac.part_pixels(r, r["parts"][0], self.vram4())
        self.assertEqual(px[0][:3], (255, 0, 0))  # last pixel (index 1) now first
        self.assertEqual(px[3][:3], (255, 0, 0))
        self.assertEqual(px[2][:3], (0, 255, 0))

    def test_8bpp_part(self):
        v = tx.Vram()
        v.put_words(0, 10, 2, 1, b"\x00\x00\x1f\x00")
        v.put_words(50, 60, 1, 1, bytes([1, 0]))
        tiles = [{"u": 50, "v": 60, "w": 1, "h": 1}, {"u": 0, "v": 10, "w": 256, "h": 1}]
        r = res([part(0, 1, 0, 0, 0x80)], [{"parts": [0]}], tiles)
        w, h, px = ac.part_pixels(r, r["parts"][0], v)
        self.assertEqual((w, h), (2, 1))
        self.assertEqual(px[0][:3], (255, 0, 0))
        self.assertEqual(px[1][3], 0)

    def test_render_and_sheet(self):
        r = res([part(0, 1, 5, 7)], [{"parts": [0]}], self.tiles())
        W, H, buf, org = ac.render_sprite(r, 0, self.vram4())
        self.assertEqual((W, H, org), (4, 1, (5, 7)))
        self.assertEqual(len(buf), 16)
        sw, sh, sbuf, cells = ac.sheet(r, self.vram4(), cols=2, pad=1)
        self.assertEqual(len(cells), 1)
        self.assertEqual(len(sbuf), sw * sh * 4)

    def test_empty_sprite(self):
        r = res([], [{"parts": []}], [])
        self.assertEqual(ac.render_sprite(r, 0, tx.Vram())[:2], (0, 0))


if __name__ == "__main__":
    unittest.main()
