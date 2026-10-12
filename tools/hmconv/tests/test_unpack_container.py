"""Tests for unpack_container using synthetic fixtures built in-test."""
import json
import os
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
import unpack_container as uc  # noqa: E402
from classify import classify  # noqa: E402


def make_hdt_container(payloads):
    blob, offs = b"", [0]
    for p in payloads:
        blob += p + b"\0" * (-len(p) % uc.SECTOR)
        offs.append(len(blob))
    return blob, struct.pack("<%dI" % len(offs), *offs)


def make_toc_container(payloads, name=b"PAYLOAD.BIN"):
    blob, recs = b"", []
    for p in payloads:
        recs.append((len(blob), len(p)))
        blob += p + b"\0" * (-len(p) % uc.SECTOR)
    toc = struct.pack("<4I16s", 32, 1, len(recs), 0, name)
    for off, size in recs:
        toc += struct.pack("<4I", 0, 0, off, size)
    return blob, toc


class T(unittest.TestCase):
    def setUp(self):
        self.d = tempfile.TemporaryDirectory()
        self.addCleanup(self.d.cleanup)

    def w(self, name, data):
        p = os.path.join(self.d.name, name)
        with open(p, "wb") as f:
            f.write(data)
        return p

    def test_hdt_roundtrip(self):
        pl = [b"VAGp" + b"\1" * 100, b"\xff\xff\xff\xff" + b"\0" * 5000, b"abc"]
        blob, hdt = make_hdt_container(pl)
        out = os.path.join(self.d.name, "o")
        res = uc.unpack_hdt(self.w("X.BIN", blob), self.w("X.HDT", hdt), out)
        self.assertEqual(len(res), 3)
        self.assertEqual(res[0]["type"], "audio_vag")
        self.assertEqual(res[1]["type"], "sparse_ff_table")
        with open(os.path.join(out, res[0]["file"]), "rb") as f:
            self.assertTrue(f.read().startswith(pl[0]))
        self.assertEqual([r["offset"] for r in res], [0, 2048, 2048 + 6144])

    def test_hdt_rejects_bad(self):
        with self.assertRaises(ValueError):
            uc.parse_hdt(struct.pack("<3I", 0, 100, 50))
        with self.assertRaises(ValueError):
            uc.parse_hdt(struct.pack("<2I", 0, 100), total_size=99)
        with self.assertRaises(ValueError):
            uc.parse_hdt(struct.pack("<2I", 4, 100))

    def test_toc_roundtrip(self):
        riff = b"RIFF\x10\0\0\0WAVEfmt " + struct.pack("<IH", 16, 0xFFFE) + b"\0" * 20
        pl = [b"VAGp" + b"\2" * 60, riff]
        blob, toc = make_toc_container(pl)
        out = os.path.join(self.d.name, "t")
        res = uc.unpack_toc(self.w("D0.BIN", toc), self.w("D1.BIN", blob), out)
        self.assertEqual([r["size"] for r in res], [len(p) for p in pl])
        self.assertEqual(res[1]["type"], "audio_riff_extensible_at3p")
        name, ents = uc.parse_toc(toc)
        self.assertEqual(name, "PAYLOAD.BIN")
        self.assertEqual(len(ents), 2)

    def test_toc_out_of_range(self):
        blob, toc = make_toc_container([b"x" * 10])
        with self.assertRaises(ValueError):
            uc.unpack_toc(self.w("a", toc), self.w("b", b"x" * 4), os.path.join(self.d.name, "z"))

    def test_cli_manifest(self):
        blob, hdt = make_hdt_container([b"hello world, text here....", b"\x04\0\0\0\x08\x83\x0b\x80" + b"\0" * 8])
        out = os.path.join(self.d.name, "c")
        uc.main(["hdt", self.w("B.BIN", blob), self.w("B.HDT", hdt), "-o", out])
        with open(os.path.join(out, "manifest.json")) as f:
            m = json.load(f)
        self.assertEqual(m[1]["type"], "ptr_table")

    def test_classify_misc(self):
        self.assertEqual(classify(b"\x0d\x10\x0a\0" + b"\0" * 20)[0], "stream_100d")
        self.assertEqual(classify(b"\xe0\xff\xbd\x27" + b"\0" * 20)[0], "code_mips_overlay")


if __name__ == "__main__":
    unittest.main()
