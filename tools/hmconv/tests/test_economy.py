import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import economy as e  # noqa: E402


def fixture():
    size = max(e.ITEM_VADDR + e.ITEM_ROWS * e.ITEM_STRIDE, e.CROP_VADDR + 22 * 16) + e.ELF_BIAS + 64
    b = bytearray(size)
    c = e.CROP_VADDR + e.ELF_BIAS
    struct.pack_into("<8H", b, c + 16 * 3, 1, 3, 0, 2, 3, 4, 99, 7)   # seed 3, 3 stages
    it = e.ITEM_VADDR + e.ELF_BIAS
    row = [0] * 20
    row[0], row[1], row[8] = e.KIND_SEED, (3 << 8) | 2, 120
    struct.pack_into("<20H", b, it + 5 * e.ITEM_STRIDE, *row)
    row = [0] * 20
    row[0], row[9] = e.KIND_PRODUCE, 345
    struct.pack_into("<20H", b, it + 7 * e.ITEM_STRIDE, *row)
    return bytes(b)


class T(unittest.TestCase):
    def test_crop_grow_days_uses_stage_count(self):
        c = e.parse_crops(fixture())[3]
        self.assertEqual(c["stage_days"], [2, 3, 4, 99])
        self.assertEqual(c["grow_days"], 9)
        self.assertEqual(c["produce_row"], 7)

    def test_prices(self):
        d = e.crops_with_prices(fixture())[3]
        self.assertEqual((d["seed_price"], d["sell_price"]), (120, 345))

    def test_missing_price_is_none(self):
        d = e.crops_with_prices(fixture())[0]
        self.assertIsNone(d["seed_price"])
        self.assertIsNone(d["sell_price"])

    def test_truncated(self):
        self.assertEqual(e.parse_crops(b"\0" * 8), [])

    def test_passout(self):
        self.assertEqual([e.passout_hours(f) for f in (0, 49, 50, 69, 70, 79, 80, 100)],
                         [0, 0, 2, 2, 4, 4, 6, 6])

    def test_season_rule(self):
        self.assertTrue(e.crop_survives(1, 1))
        self.assertFalse(e.crop_survives(1, 2))

    def test_constants(self):
        k = e.constants()
        self.assertEqual((k["starting_gold"], k["start_max_stamina"], k["stamina_cap"]), (500, 100, 255))


if __name__ == "__main__":
    unittest.main()
