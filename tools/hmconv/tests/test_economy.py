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


def real_elf():
    path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", "..", "..",
                        "assets", "hm-raw", "eboot", "hmpsp.prx.elf")
    path = os.environ.get("HM_ELF", path)
    return open(path, "rb").read() if os.path.exists(path) else None


class T(unittest.TestCase):
    def test_tool_cost_table_synthetic(self):
        b = bytearray(e.TOOL_COST_VADDR + e.ELF_BIAS + 64)
        for r in range(5):
            struct.pack_into("<5h", b, e.TOOL_COST_VADDR + e.ELF_BIAS + r * 10, -1, -2, -3, -4, -5)
        b = bytes(b)
        self.assertEqual(e.tool_stamina_cost(b, 1), 1)
        self.assertEqual(e.tool_stamina_cost(b, 6, held_frames=45), 3)
        self.assertEqual(e.tool_stamina_cost(b, 21, held_frames=9999), 5)

    def test_regrow_days(self):
        c = {"regrow_stage": 1, "stages": 3, "stage_days": [3, 3, 2, 0]}
        self.assertEqual(e.regrow_days(c), 6)
        self.assertIsNone(e.regrow_days({"regrow_stage": 65535, "stages": 2, "stage_days": [1, 1, 0, 0]}))

    def test_sleep_recovery(self):
        self.assertEqual(e.sleep_recovery(8), (106, -21))
        self.assertEqual(e.sleep_recovery(20), (127, -45))
        self.assertEqual(e.sleep_recovery(100), (127, -128))

    def test_action_fatigue(self):
        self.assertEqual(e.action_fatigue(1, 12, 50), 1)
        self.assertEqual(e.action_fatigue(1, 23, 50), 2)
        self.assertEqual(e.action_fatigue(1, 3, 0), 5)
        self.assertEqual(e.action_fatigue(1, 3, 0, half=True), 2)
        self.assertEqual(e.action_fatigue(1, 3, 0, immune=True), 0)

    @unittest.skipIf(real_elf() is None, "hmpsp.prx.elf not available")
    def test_real_elf_decodes_match_wiki(self):
        d = real_elf()
        self.assertEqual(e.tool_costs(d), [[1, 2, 3, 4, 5]] * 5)
        crops = e.parse_crops(d)
        regrow = sorted({e.regrow_days(c) for c in crops if e.regrow_days(c)})
        self.assertEqual(regrow, [3, 4, 6, 9])
        by = {e.SEED_NAMES[c["seed_index"]]: c for c in e.crops_with_prices(d)}
        self.assertEqual((by["Turnip"]["grow_days"], by["Turnip"]["seed_price"], by["Turnip"]["sell_price"]), (4, 120, 60))
        self.assertEqual((by["Corn"]["grow_days"], by["Corn"]["seed_price"], by["Corn"]["sell_price"]), (14, 300, 100))
        self.assertEqual((by["Pineapple"]["grow_days"], e.regrow_days(by["Pineapple"])), (20, 6))

    @unittest.skipIf(real_elf() is None, "hmpsp.prx.elf not available")
    def test_real_elf_festivals_match_wiki(self):
        wiki = {0: [1, 8, 14, 18, 22], 1: [1, 7, 12, 20, 24], 2: [3, 9, 13, 21], 3: [10, 14, 24, 30]}
        got = {}
        for s, d, _ in e.festivals(real_elf()):
            got.setdefault(s, []).append(d + 1)
        self.assertEqual({k: sorted(v) for k, v in got.items()}, wiki)

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
