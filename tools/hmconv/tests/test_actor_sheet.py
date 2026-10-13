import json, os, struct, sys, tempfile, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import actor as ac
import actor_sheet as sh
import sprite as sp


def res0e(n_parts=1):
    tiles = struct.pack("<4h", 0, 0, 2, 2)
    sprites = struct.pack("<hH", n_parts, 0)
    parts = struct.pack("<HHhhB3s", 0, 0, 0, 0, 0, b"\0\0\0") * n_parts
    anims = struct.pack("<HH", 1, 0)
    frames = struct.pack("<HhhBBH", 0, 0, 0, 5, 0xFF, 0)
    secs = [tiles, sprites, parts, anims, frames]
    offs, pos = [], sp.HDR
    for b in secs:
        offs.append(pos); pos += len(b)
    return struct.pack("<I5HH5I", 0x0E, 1, 1, n_parts, 1, 1, 0, *offs) + b"".join(secs)


def fake_elf(arr, tbl, row, start, offs, size):
    e = bytearray(0x1000)
    def w(vaddr, v):
        struct.pack_into("<I", e, vaddr + sh.ELF_BIAS, v)
    w(arr, start); w(arr + 4, start + 0x1000)
    w(tbl, row)
    w(row, len(offs))
    for k, o in enumerate(offs):
        w(row + 4 + 4 * k, o)
    w(row + 4 * (len(offs) + 1), size)
    return bytes(e[:0x800]) + bytes(0) if False else bytes(e)


class SheetTest(unittest.TestCase):
    def setUp(self):
        self.saved = (sh.FAMILIES, sh.FIXED)
        sh.FAMILIES = [("main", 0x100, 0x200, 1)]
        sh.FIXED = []

    def tearDown(self):
        sh.FAMILIES, sh.FIXED = self.saved

    def test_layout(self):
        elf = fake_elf(0x100, 0x200, 0x300, 0x4000, [0, 0x80], 0x100)
        (d,) = sh.read_layout(elf)
        self.assertEqual(d["id"], "main_00")
        self.assertEqual((d["start"], d["res_size"]), (0x4000, 0x100))
        self.assertEqual(d["res_offsets"], [0, 0x80])
        self.assertEqual(d["images"], [0x4100])

    def test_unsorted_offsets_use_next_larger_end(self):
        a, b = res0e(1), res0e(3)
        blob = b.ljust(0x80, b"\0") + a  # b first in memory, listed second
        seg = bytearray(0x4000) + blob
        d = {"start": 0x4000, "res_size": len(blob), "res_offsets": [0x80, 0], "images": []}
        vram, ress = sh.decode_sheet(bytes(seg), d)
        self.assertIsNone(vram)
        self.assertEqual([r["counts"]["parts"] for r in ress], [1, 3])

    def test_flip_y(self):
        import texture as tx
        v = tx.Vram()
        v.put_words(0, 10, 16, 1, b"".join(w.to_bytes(2, "little") for w in [0, 0x1F, 0x3E0] + [0] * 13))
        v.put_words(20, 30, 1, 2, (0x1).to_bytes(2, "little") + (0x2).to_bytes(2, "little"))
        r = {"tiles": [{"u": 20, "v": 30, "w": 1, "h": 2}, {"u": 0, "v": 10, "w": 16, "h": 1}], "sprites": [{"parts": [0]}],
             "parts": [{"tile": 0, "clut_tile": 1, "x": 0, "y": 0, "flags": ac.FLIP_Y, "bpp8": False}]}
        w, h, px = ac.part_pixels(r, r["parts"][0], v)
        self.assertEqual((w, h), (4, 2))
        self.assertEqual(px[0][:3], (0, 255, 0))  # row 1 (index 2) now on top
        self.assertEqual(px[4][:3], (255, 0, 0))


if __name__ == "__main__":
    unittest.main()
