import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import text as t  # noqa: E402


class T(unittest.TestCase):
    def test_decode(self):
        codes = [0x0FEC, 0x07, 0x24 - 0x20 + 0x20, 0x0FFD, 0x00, 0xFFFE, 0x33, 0xFFFF]
        r = t.decode_codes(codes)
        self.assertEqual(len(r), 1)
        self.assertEqual(r[0]["text"], "He A\nt")
        self.assertEqual(r[0]["controls"], [(0, 0x0FEC)])

    def test_unknown_and_sector(self):
        s = struct.pack("<4H", 0x0FF7, 0x62, 0x20, 0xFFFF).ljust(t.SECTOR, b"\0")
        r = t.sector_strings(s)
        self.assertEqual(r[0]["text"], "<0062>a")
        self.assertEqual(t.sector_strings(bytes(t.SECTOR)), [])
        self.assertEqual(len(t.dump_file(s + bytes(t.SECTOR))), 1)

    def test_digits_controls_entries(self):
        c = [0x0F87, 0x0FF7, 0x4010, 0x48, 0x41, 0x69, 0xFFFF]
        r = t.decode_codes(c)[0]
        self.assertEqual(r["text"], "{16}81-")
        self.assertEqual(t.face_of(c), 0x0FEF - 0x0F87)
        self.assertEqual(t.classify_control(0x0E2F), ("colour", 0))
        self.assertEqual(t.classify_control(0x0FFC), ("wait_key", None))
        self.assertEqual(t.decode_codes([0x62, 0xFFFF], guess=True)[0]["text"], "?")
        sec = struct.pack("<3H", 0x0FF7, 0x07, 0xFFFF).ljust(t.SECTOR, b"\0")
        ent = struct.pack("<i", 104) + bytes(t.ENTRY_TEXT - 4) + sec
        ents = t.parse_entries(ent + struct.pack("<i", -1) + bytes(t.ENTRY - 4))
        self.assertEqual(len(ents), 1)
        self.assertEqual((ents[0]["id"], ents[0]["face"]), (0, 104))


if __name__ == "__main__":
    unittest.main()
