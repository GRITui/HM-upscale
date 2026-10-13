import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import rules  # noqa: E402


def fixture():
    b = bytearray(rules.FESTIVAL_VADDR + rules.ELF_BIAS + 6 * rules.FESTIVAL_N)
    for i in range(rules.FESTIVAL_N):
        struct.pack_into("<3h", b, rules.FESTIVAL_VADDR + rules.ELF_BIAS + i * 6, 0, 0, -1)
    struct.pack_into("<3h", b, rules.FESTIVAL_VADDR + rules.ELF_BIAS + 6 * 2, 1, 4, 77)
    return bytes(b)


class T(unittest.TestCase):
    def test_festivals(self):
        f = rules.parse_festivals(fixture())
        self.assertEqual(len(f), 18)
        self.assertEqual((f[2]["season"], f[2]["day"], f[2]["event"]), (1, 5, 77))
        self.assertTrue(f[13]["scanned"])
        self.assertFalse(f[14]["scanned"])

    def test_truncated(self):
        self.assertEqual(rules.parse_festivals(b"\0" * 10), [])

    def test_clock(self):
        c = rules.clock()
        self.assertEqual(c["seasons"] * c["days_per_season"], 120)
        self.assertEqual(c["minute_period_ticks"], 15)


if __name__ == "__main__":
    unittest.main()
