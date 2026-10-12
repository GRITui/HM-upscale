import os, random, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import decompress as d  # noqa: E402


class T(unittest.TestCase):
    def test_roundtrip(self):
        rnd = random.Random(1)
        for _ in range(50):
            raw = b"".join(bytes([rnd.randrange(256)]) * rnd.randrange(1, 300)
                           if rnd.random() < .5 else bytes(rnd.randrange(256) for _ in range(rnd.randrange(1, 200)))
                           for _ in range(20))
            self.assertEqual(d.rle_decode(d.rle_encode(raw), len(raw)), raw)

    def test_known_tokens(self):
        self.assertEqual(d.rle_decode(bytes([3, 0x7a, 0xfe, 1, 2])), b"\x7a\x7a\x7a\x01\x02")

    def test_bad(self):
        with self.assertRaises(ValueError):
            d.rle_decode(bytes([0xfe, 1]))

    def test_image(self):
        raw = bytes(range(8)) * 2  # 2 words x 4 rows? w=2 ->4 bytes, h=4
        comp = d.rle_encode(raw)
        clut = struct.pack("<2H", 0, 0x7fff)
        hdr = struct.pack("<8H", 0x100D, 3, 4, 0x600, 0, 490, 2, 1)
        c1 = struct.pack("<6H", len(raw), 0x400, 0, 0, 2, 4) + raw
        c2 = struct.pack("<6H", len(comp), 0x100, 8, 0, 2, 4) + comp
        im = d.parse_image(hdr + clut + c1 + c2)
        self.assertEqual(im["chunks"][0]["data"], raw)
        self.assertEqual(im["chunks"][1]["data"], raw)
        self.assertEqual(im["clut"][1], (255, 255, 255, 255))

    def test_image_groups(self):
        # n = chunks + groups; groups are separated by a u16 0 and may be
        # preceded by an odd-size payload (pad to even).
        raw = bytes([5, 6, 7, 8])
        odd = d.rle_encode(raw)
        self.assertEqual(len(odd) % 2, 1)
        raw2 = bytes([9]) * 3 + bytes([1])
        comp2 = d.rle_encode(raw2)
        hdr = struct.pack("<8H", 0x100D, 4, 4, 0x600, 0, 490, 2, 1)
        clut_a = struct.pack("<2H", 0, 0x7fff)
        c1 = struct.pack("<6H", len(odd), 0, 0, 0, 1, 2) + odd + b"\0"
        grp = struct.pack("<6H", 4, 0x600, 16, 491, 2, 1) + struct.pack("<2H", 0, 0x001f)
        c2 = struct.pack("<6H", len(comp2), 0x100, 4, 0, 1, 2) + comp2
        blob = hdr + clut_a + c1 + grp + c2
        # a trailing item beyond n must not be read
        im = d.parse_image(b"\xAA" * 6 + blob + b"\xFF" * 40, offset=6)
        self.assertEqual(len(im["groups"]), 2)
        self.assertEqual(im["groups"][0]["chunks"][0]["data"], raw)
        self.assertEqual(im["groups"][1]["clut_vram"], (16, 491))
        self.assertEqual(im["groups"][1]["clut"][1][:3], (255, 0, 0))
        self.assertEqual(im["groups"][1]["chunks"][0]["data"], raw2)
        self.assertEqual(im["end"], len(blob))


if __name__ == "__main__":
    unittest.main()
