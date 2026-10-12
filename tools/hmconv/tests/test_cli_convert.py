"""Synthetic tests for the hmconv CLI, PNG writer, VAG decoder and batch (no game data)."""
import contextlib
import io
import json
import os
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))))

from hmconv import audio, cli, manifest, texture_png  # noqa: E402
from hmconv.decompress import rle_encode  # noqa: E402


def make_image(w_words=2, h=3, ncolors=256, flag=0x0100, fill=None):
    """Build a 0x100D image: header, CLUT, one RLE chunk, empty terminator chunk."""
    clut = [0x0000, 0x001F, 0x03E0, 0x7C00] + [0x1111] * (ncolors - 4)
    bpp8 = ncolors > 16
    px_w = w_words * (2 if bpp8 else 4)
    if bpp8:
        raw = bytes((i * 7) % 4 for i in range(px_w * h))
    else:
        raw = bytes(((i * 2) % 4) | ((((i * 2) + 1) % 4) << 4) for i in range(px_w * h // 2))
    pay = rle_encode(raw)
    out = struct.pack("<8H", 0x100D, 2, ncolors * 2, 0x0600, 0, 490, ncolors, 1)
    out += struct.pack("<%dH" % ncolors, *clut)
    out += struct.pack("<6H", len(pay), flag, 960, 175, w_words, h) + pay
    out += struct.pack("<6H", 0, 0, 0, 0, 0, 0)
    return out, raw, px_w


class PngTests(unittest.TestCase):
    def test_indexed_roundtrip_with_transparency(self):
        pal = [(0, 0, 0, 0), (255, 0, 0, 255), (0, 255, 0, 255)]
        png = texture_png.write_png_indexed(3, 2, bytes([0, 1, 2, 2, 1, 0]), pal)
        w, h, rgba = texture_png.read_png_rgba(png)
        self.assertEqual((w, h), (3, 2))
        self.assertEqual(rgba[:4], bytes([0, 0, 0, 0]))
        self.assertEqual(rgba[4:8], bytes([255, 0, 0, 255]))

    def test_rgba_roundtrip(self):
        data = bytes(range(16))
        w, h, rgba = texture_png.read_png_rgba(texture_png.write_png_rgba(2, 2, data))
        self.assertEqual(rgba, data)

    def test_bad_index_rejected(self):
        with self.assertRaises(ValueError):
            texture_png.write_png_indexed(1, 1, b"\x05", [(0, 0, 0, 255)])

    def test_nibble_order(self):
        self.assertEqual(texture_png.unpack_pixels(b"\x21", 4), bytes([1, 2]))


class ImageTests(unittest.TestCase):
    def test_8bpp(self):
        data, raw, px_w = make_image()
        (png, info), = texture_png.image_to_pngs(data)
        self.assertEqual((info["width"], info["height"], info["bpp"]), (px_w, 3, 8))
        w, h, rgba = texture_png.read_png_rgba(png)
        self.assertEqual((w, h), (px_w, 3))
        self.assertEqual(rgba[3], 0)  # index 0 -> CLUT 0x0000 -> transparent
        self.assertEqual(rgba[7], 255)

    def test_4bpp_width_is_4x_words(self):
        data, raw, px_w = make_image(w_words=2, h=2, ncolors=16)
        (png, info), = texture_png.image_to_pngs(data)
        self.assertEqual((info["width"], info["bpp"]), (8, 4))
        self.assertEqual(texture_png.read_png_rgba(png)[:2], (8, 2))

    def test_cli_image(self):
        with tempfile.TemporaryDirectory() as d:
            src = os.path.join(d, "a.bin")
            open(src, "wb").write(make_image()[0])
            out = os.path.join(d, "o", "a.png")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(cli.main(["image", src, out]), 0)
            self.assertTrue(open(out, "rb").read().startswith(b"\x89PNG"))

    def test_cli_image_garbage_fails_cleanly(self):
        with tempfile.TemporaryDirectory() as d:
            src = os.path.join(d, "a.bin")
            open(src, "wb").write(b"\x00" * 64)
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(cli.main(["image", src, os.path.join(d, "x.png")]), 1)


def vag_frame(shift, pred, flag, nibbles):
    b = bytes([(pred << 4) | shift, flag])
    body = bytes((nibbles[i] & 15) | ((nibbles[i + 1] & 15) << 4) for i in range(0, 28, 2))
    return b + body


def make_vag(frames, rate=16000, name="T.wav"):
    payload = b"".join(frames)
    h = b"VAGp" + struct.pack(">IIII", 4, 0, len(payload), rate) + b"\0" * 12 + name.encode().ljust(16, b"\0")
    return h + payload


class VagTests(unittest.TestCase):
    def test_decode_predict0(self):
        # nibble 1 with shift 12 -> (1<<12)>>12 = 1; nibble 0xF (-1) -> -1
        fr = [vag_frame(12, 0, 0, [1, 15] * 14), vag_frame(12, 0, 1, [0] * 28)]
        rate, pcm = audio.vag_decode(make_vag(fr))
        self.assertEqual(rate, 16000)
        self.assertEqual(len(pcm), 28)  # end frame (flag 1) is not decoded
        self.assertEqual(list(pcm[:4]), [1, -1, 1, -1])

    def test_decode_filter1_accumulates(self):
        fr = [vag_frame(12, 1, 0, [1] * 28), vag_frame(12, 0, 7, [0] * 28)]
        _, pcm = audio.vag_decode(make_vag(fr))
        self.assertEqual(pcm[0], 1)
        self.assertGreaterEqual(pcm[27], pcm[0])  # 60/64 feedback keeps it non-decreasing

    def test_clamp(self):
        fr = [vag_frame(0, 0, 0, [7] * 28), vag_frame(12, 0, 7, [0] * 28)]
        _, pcm = audio.vag_decode(make_vag(fr))
        self.assertEqual(pcm[0], 7 << 12)
        fr = [vag_frame(0, 4, 0, [7] * 28), vag_frame(12, 0, 7, [0] * 28)]
        _, pcm = audio.vag_decode(make_vag(fr))
        self.assertLessEqual(max(pcm), 32767)

    def test_bad_input(self):
        with self.assertRaises(audio.AudioError):
            audio.vag_decode(b"RIFFxxxxxxxxxxxxxxxxxxxxxxxxxxxx")

    def test_split_bank(self):
        a = make_vag([vag_frame(12, 0, 0, [1] * 28), vag_frame(12, 0, 7, [0] * 28)], name="A.wav")
        b = make_vag([vag_frame(12, 0, 7, [2] * 28)], name="B.wav")
        bank = a + b"\0" * 16 + b
        parts = audio.vag_split(bank)
        self.assertEqual([p[1] for p in parts], ["A.wav", "B.wav"])
        self.assertEqual(parts[1][0], len(a) + 16)

    def test_wav_written_and_cli(self):
        with tempfile.TemporaryDirectory() as d:
            src = os.path.join(d, "s.vag")
            open(src, "wb").write(make_vag([vag_frame(12, 0, 0, [3] * 28), vag_frame(12, 0, 7, [0] * 28)]))
            dst = os.path.join(d, "s.wav")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(cli.main(["audio", src, dst]), 0)
            w = open(dst, "rb").read()
            self.assertEqual(w[:4], b"RIFF")
            self.assertEqual(struct.unpack_from("<I", w, 24)[0], 16000)
            self.assertEqual(struct.unpack_from("<I", w, 40)[0], 56)  # 28 samples * 2 bytes

    def test_unrecognised_audio(self):
        with tempfile.TemporaryDirectory() as d:
            src = os.path.join(d, "x")
            open(src, "wb").write(b"junkjunkjunkjunk")
            with contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(cli.main(["audio", src, os.path.join(d, "x.wav")]), 1)


class ManifestTests(unittest.TestCase):
    def test_validate(self):
        good = {"schema": 1, "assets": [manifest.entry("a_1", "texture", "t/a.png", ["x"])]}
        self.assertEqual(manifest.validate(good), [])
        bad = {"schema": 1, "assets": [
            manifest.entry("A", "texture", "t/a.png"),
            manifest.entry("b", "audio", "../b.wav"),
            manifest.entry("b", "data", "d.json", license=" "),
        ]}
        self.assertGreaterEqual(len(manifest.validate(bad)), 4)

    def test_write_requires_files(self):
        with tempfile.TemporaryDirectory() as d:
            with self.assertRaises(ValueError):
                manifest.write_manifest(d, [manifest.entry("a", "texture", "nope.png")])


class CliTests(unittest.TestCase):
    def test_placeholders_not_implemented(self):
        for name in ("sprite", "map", "text", "tables"):
            with contextlib.redirect_stderr(io.StringIO()) as err:
                rc = cli.main([name, "in", "out"])
            if "not implemented" in err.getvalue():
                self.assertEqual(rc, cli.NOT_IMPLEMENTED)

    def test_unpack_and_batch(self):
        with tempfile.TemporaryDirectory() as raw:
            usr = os.path.join(raw, "extracted", "PSP_GAME", "USRDIR")
            os.makedirs(usr)
            blk = lambda: (make_image(w_words=40, h=60)[0] + b"\0" * 0x1800)[:0x1800]
            seg0 = b"\0" * 0x800
            seg1 = blk() + blk()
            seg1 += b"\0" * (-len(seg1) % 0x800)
            open(os.path.join(usr, "BOY_FILE.BIN"), "wb").write(seg0 + seg1)
            open(os.path.join(usr, "BOY_FILE.HDT"), "wb").write(
                struct.pack("<3I", 0, len(seg0), len(seg0) + len(seg1)))
            # unpack command on its own
            ent = os.path.join(raw, "ent")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(cli.main(["unpack", os.path.join(usr, "BOY_FILE.BIN"), ent]), 0)
            self.assertTrue(os.path.isfile(os.path.join(ent, "seg001.bin")))
            # batch
            with contextlib.redirect_stdout(io.StringIO()):
                cli.main(["batch", "--raw", raw])  # girl/data001 absent: nonzero exit is expected
            conv = os.path.join(raw, "converted")
            m = json.load(open(os.path.join(conv, "manifest.json")))
            self.assertEqual(manifest.validate(m), [])
            faces = [e for e in m["assets"] if "portrait" in e["tags"]]
            self.assertEqual(len(faces), 2)
            w, h, _ = texture_png.read_png_rgba(open(os.path.join(conv, faces[0]["path"]), "rb").read())
            self.assertEqual((w, h), (80, 60))
            self.assertTrue(any(e["id"] == "asset_index" for e in m["assets"]))


if __name__ == "__main__":
    unittest.main()
