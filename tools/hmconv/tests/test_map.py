import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import map as m  # noqa: E402


def fixture():
    script = struct.pack("<4B3i", 4, 255, 255, 0, 6, 100, 200) + b"\xff" + bytes(15)
    sprite = b"\x0e\0\0\0" + bytes(8)
    blocks = [sprite, script]
    offs, p = [], 0
    for b in blocks:
        offs.append(p); p += len(b)
    offs.append(p)
    flags = [0x2006, 0x4000] + [0] * 22
    hdr = struct.pack("<HH24H", 0x180, 2, *flags) + struct.pack("<3I", *offs)
    return hdr + b"".join(blocks)


class T(unittest.TestCase):
    def test_parse(self):
        r = m.parse_map(fixture())
        k = [b["kind"] for b in r["blocks"]]
        self.assertEqual(k, ["char_sprite", "script"])
        self.assertEqual(r["blocks"][0]["res_id"], 6)
        j = m.to_json(r)
        self.assertEqual(j["blocks"][1]["commands"][0]["args"], [6, 100, 200])
        self.assertEqual(len(j["blocks"][1]["commands"]), 1)

    def test_empty(self):
        with self.assertRaises(ValueError):
            m.parse_map(b"")


if __name__ == "__main__":
    unittest.main()
