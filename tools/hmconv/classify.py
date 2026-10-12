"""Heuristic type classification of container entries by their first bytes.

Returns (tag, extension). Tags are first guesses; see docs/formats/classification.md.
"""
import struct

RUNTIME_PTR_HI = (0x80,)  # KSEG0 pointers, e.g. 0x800b8308


def _printable_ratio(b):
    if not b:
        return 0.0
    return sum(1 for c in b if 32 <= c < 127 or c in (9, 10, 13)) / len(b)


def classify(data):
    """Classify the leading bytes (>= 64 recommended) of an entry."""
    h = data[:64]
    if len(h) < 4:
        return "tiny", "bin"
    if h[:4] == b"VAGp":
        return "audio_vag", "vag"
    if h[:4] == b"pBAV":
        return "audio_vag_le", "vag"
    if h[:4] == b"RIFF" and h[8:12] == b"WAVE":
        tag = struct.unpack_from("<H", h, 20)[0] if len(h) >= 22 else 0
        if tag == 0xFFFE:
            return "audio_riff_extensible_at3p", "at3"
        if tag == 0x270:
            return "audio_riff_at3", "at3"
        return "audio_riff_wave", "wav"
    if h[:4] == b"\x7fELF":
        return "code_elf", "elf"
    if h[:4] == b"\x89PNG":
        return "image_png", "png"
    if h[:4] == b"MIG.":
        return "texture_gim", "gim"
    if h[:4] in (b"\x10\x00\x00\x00",) and len(h) >= 8 and struct.unpack_from("<I", h, 4)[0] in (8, 9, 2, 3):
        return "texture_tim", "tim"
    if h[:4] == b"\xff\xff\xff\xff":
        return "sparse_ff_table", "bin"
    if h[:2] == b"\x0d\x10":
        return "stream_100d", "bin"
    if h[:4] == b"\xe0\xff\xbd\x27":
        return "code_mips_overlay", "mips"
    w = struct.unpack_from("<I", h, 0)[0]
    if len(h) >= 8 and w < 0x100:
        w1 = struct.unpack_from("<I", h, 4)[0]
        if w1 >> 24 == 0x80:
            return "ptr_table", "bin"
        if _printable_ratio(h[4:]) > 0.7:
            return "text_table", "bin"
        if w == 0x0e:
            return "res_0e", "bin"
        return "counted_table", "bin"
    if _printable_ratio(h) > 0.9:
        return "text", "txt"
    return "unknown", "bin"
