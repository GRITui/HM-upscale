import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import sprite as s  # noqa: E402


def fixture():
    tiles = struct.pack("<4h", 0, 0, 2, 2) + struct.pack("<4h", 2, 0, 2, 2)
    sprites = struct.pack("<hH", 2, 0)
    parts = (struct.pack("<HHhhB3s", 0, 0, 0, 0, 0x80, b"\0\0\0")
             + struct.pack("<HHhhB3s", 1, 0, 2, 0, 0x10, b"\0\0\0"))
    anims = struct.pack("<HH", 2, 0)
    frames = (struct.pack("<HhhBBH", 0, 0, 0, 5, 0, 0) + struct.pack("<HhhBBH", 0, 1, 1, 3, 0xFF, 0))
    secs = [tiles, sprites, parts, anims, frames]
    offs, pos = [], s.HDR
    for b in secs:
        offs.append(pos); pos += len(b)
    hdr = struct.pack("<I5HH5I", 0x0E, 2, 1, 2, 1, 2, 0, *offs)
    return hdr + b"".join(secs)


class T(unittest.TestCase):
    def test_parse(self):
        r = s.parse_res0e(fixture())
        self.assertEqual(r["counts"]["parts"], 2)
        self.assertEqual(r["tiles"][1]["u"], 2)
        self.assertTrue(r["parts"][0]["bpp8"])
        self.assertTrue(r["parts"][1]["semi_transparent"])
        self.assertEqual(r["sprites"][0]["parts"], [0, 1])
        self.assertTrue(r["frames"][1]["hold"])
        self.assertEqual(r["frames"][0]["duration"], 5)

    def test_render(self):
        r = s.parse_res0e(fixture())
        w, h, buf, o = s.render_sprite(r, 0, lambda u, v, p: (u, v, 0, 255))
        self.assertEqual((w, h), (4, 2))
        self.assertEqual(buf[0], (0, 0, 0, 255))
        self.assertEqual(buf[3], (3, 0, 0, 255))

    def test_bad(self):
        with self.assertRaises(ValueError):
            s.parse_res0e(b"\1\0\0\0" + bytes(40))


if __name__ == "__main__":
    unittest.main()
