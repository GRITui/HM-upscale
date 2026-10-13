import os, struct, sys, unittest
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
import script


def cmd(op, p1=0, p2=0, p3=0, p4=0, done=0xFF, wait=0xFF, y=0):
    return struct.pack("<4B2h2i", op, done, wait, y, p1, p2, p3, p4)


class ScriptTest(unittest.TestCase):
    def test_parse_layout_and_terminator(self):
        d = cmd(2, 1, 77, 5, 0, done=3, wait=4, y=1) + cmd(0xFF) + cmd(2)
        c = script.parse(d)
        self.assertEqual(len(c), 1)
        self.assertEqual(c[0]["p"], [1, 77, 5, 0])
        self.assertEqual((c[0]["done"], c[0]["wait"], c[0]["yield"]), (3, 4, 1))

    def test_dialog_and_field(self):
        d = script.describe(script.parse(cmd(2, 9, 40, -1, 1))[0])
        self.assertEqual(d["dialog"], {"channel": 0, "entry": 40, "entry_is_var": True,
                                       "result_var": -1})
        f = script.describe(script.parse(cmd(13, 0x35, 0, 4, 1))[0])
        self.assertEqual(f["field"], "flag_5")
        self.assertEqual(script.describe(script.parse(cmd(13, 10))[0])["field"], "hour")

    def test_time_gates(self):
        c = script.parse(cmd(13, 10, 0, 0, 6) + cmd(36, 7, 30) + cmd(13, 0, 0, 0, 1))
        g = script.time_gates(c)
        self.assertEqual([x["field"] for x in g], ["hour", "time"])

    def test_convert_map(self):
        m = {"blocks": [{"kind": "script", "index": 3, "data": cmd(2, 1, 8, -1, 0) + cmd(37)},
                        {"kind": "other", "index": 4, "data": b""}]}
        j = script.convert_map(m)
        self.assertEqual(j["schema"], 1)
        self.assertEqual(j["scripts"][0]["dialogs"], [[1, 8, 0]])
        self.assertEqual(j["scripts"][0]["commands"][1]["name"], "script_end")


if __name__ == "__main__":
    unittest.main()
