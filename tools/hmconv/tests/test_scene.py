import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import scene as sc  # noqa: E402


def fixture():
    b = bytearray(0x2000 + 12 + 0x1C)
    struct.pack_into("<2h", b, 2, 2, 80)
    for i in range(sc.REGION_N):
        struct.pack_into("<22h", b, sc.REGION_OFF + i * sc.REGION_SZ, 0, -1, -1, -1, 999, 99, *[0] * 16)
    s = [16384, 22, 582, -1, 3, 99, 0, -10, 0, -20, 30, 0, 40] + [0] * 9
    struct.pack_into("<22h", b, sc.REGION_OFF + 2 * sc.REGION_SZ, *s)
    struct.pack_into("<I", b, sc.INST_OFF, 0x4000)
    struct.pack_into("<I", b, sc.MODEL_OFF + 8, 1)
    struct.pack_into("<7I", b, sc.MODEL_OFF + 12, 1, 2, 3, 0, 832, 65, 0)
    return bytes(b)


class T(unittest.TestCase):
    def test_parse(self):
        r = sc.parse_scene(fixture())
        self.assertEqual(r["map_id"], 80)
        self.assertEqual(len(r["regions"]), 1)
        g = r["regions"][0]
        self.assertEqual((g["type"], g["event"], g["min"], g["max"]), (22, 582, [-10, -20], [30, 40]))
        self.assertEqual(r["instances"][0]["flags"], 0x4000)
        self.assertEqual(r["models"][0]["primitives"], 65)
        self.assertEqual(sc.to_json(r)["id"], "map_080")

    def test_short(self):
        with self.assertRaises(ValueError):
            sc.parse_scene(b"\0" * 10)


if __name__ == "__main__":
    unittest.main()
