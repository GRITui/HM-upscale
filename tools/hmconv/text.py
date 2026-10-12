#!/usr/bin/env python3
"""Decode HM dialogue text sectors (messege0..4) to JSON. See docs/formats/text.md.

Text is a stream of little-endian u16 codes. 0x00-0x19 = A-Z, 0x20-0x39 = a-z (glyph
indices into Font_all), 0x0ffd = space, 0xfffe = line break, 0xffff = end of string,
0x0e00-0x0fff (other than 0x0ffd) = control codes (semantics unknown, kept raw). Any
other code is emitted as <xxxx> (punctuation/digits/kana not yet mapped).
Control semantics (confirmed from the renderer, FUN_00004dfc in hmpsp.prx):
  0x0E25-0x0E2F colour/palette select = 0x0E2F - code; 0x0E38-0x0FEF face id = 0x0FEF - code
  (applied by 0x0FF7); 0x0FF6/0x0FF4 flag off/on; 0x0FFC wait for key; 0x4000|n = inline
  substitution slot n (player name, numbers; see docs/formats/text.md).
Output must never be committed (game text); this tool only writes where asked.
Usage: text.py <messegeN.bin> [out.json]
"""
import json, struct, sys

SECTOR = 0x800
SPACE, NEWLINE, END = 0x0FFD, 0xFFFE, 0xFFFF


ENTRY = 0x2000          # messege0..4 fixed entry: i32 face id, face block, text sector at +0x1800
ENTRY_TEXT = 0x1800
# Glyph codes inferred from context, not from the font image (confidence: medium). Only
# used when decode_codes(..., guess=True).
GUESS = {0x62: "?", 0x63: ",", 0x66: ":", 0x6C: ".", 0x8C: "!", 0x61: "*"}


def glyph(code, guess=False):
    if 0x00 <= code <= 0x19: return chr(0x41 + code)
    if 0x20 <= code <= 0x39: return chr(0x61 + code - 0x20)
    if 0x40 <= code <= 0x49: return chr(0x30 + code - 0x40)   # digits: confirmed (number printer)
    if code == 0x69: return "-"                               # minus sign: confirmed
    if guess: return GUESS.get(code)
    return None


def classify_control(c):
    """-> (kind, param) for a control/substitution code, or None for plain glyphs."""
    if 0x0E25 <= c <= 0x0E2F: return ("colour", 0x0E2F - c)
    if 0x0E38 <= c <= 0x0FEF: return ("face", 0x0FEF - c)
    names = {0x0FF7: "face_show", 0x0FF6: "flag58_off", 0x0FF5: "flag4c_on", 0x0FF4: "flag58_on",
             0x0FF3: "flag68_on", 0x0FF2: "nop", 0x0FF1: "flag7c_on", 0x0FFC: "wait_key",
             0x0FFB: "pause", 0x0FFA: "pause", 0x0FF9: "pause", 0x0FF8: "pause"}
    if c in names: return (names[c], None)
    if c & 0x4000: return ("subst", c & 0xFFF)
    return None


def decode_codes(codes, guess=False):
    """-> list of strings: {'text': str, 'controls': [(pos, code)], 'raw': [codes]}."""
    out, cur, text, ctl = [], [], [], []
    for c in codes:
        if c == END:
            out.append({"text": "".join(text), "controls": ctl, "raw": cur})
            cur, text, ctl = [], [], []
            continue
        cur.append(c)
        if c == NEWLINE: text.append("\n")
        elif c == SPACE: text.append(" ")
        elif glyph(c, guess): text.append(glyph(c, guess))
        elif c & 0x4000: ctl.append((len(text), c)); text.append("{%d}" % (c & 0xFFF))
        elif 0x0E00 <= c <= 0x0FFF:
            ctl.append((len(text), c))
        else: text.append("<%04x>" % c)
    return out


def sector_strings(sector):
    u = struct.unpack("<%dH" % (len(sector) // 2), sector)
    if not u or (u[0] >> 8) != 0x0F or END not in u:
        return []
    last = len(u) - 1 - u[::-1].index(END)
    return decode_codes(u[:last + 1])


def face_of(codes):
    """Face id selected by the leading controls of a string, or None."""
    for c in codes:
        k = classify_control(c)
        if k and k[0] == "face": return k[1]
        if not (0x0E00 <= c <= 0x0FFF): break
    return None


def parse_entries(data):
    """Parse a messege segment of 0x2000-byte entries -> [{'id','face','strings'}].
    id is the entry index (string id = (segment, index)); face is the i32 header (-1 none)."""
    res = []
    for i in range(len(data) // ENTRY):
        e = data[i * ENTRY:(i + 1) * ENTRY]
        face = struct.unpack_from("<i", e, 0)[0]
        st = sector_strings(e[ENTRY_TEXT:ENTRY_TEXT + SECTOR])
        if st: res.append({"id": i, "face": face, "strings": st})
    return res


def dump_file(data):
    res = []
    for off in range(0, len(data) - SECTOR + 1, SECTOR):
        s = sector_strings(data[off:off + SECTOR])
        if s:
            res.append({"offset": off, "strings": s})
    return res


if __name__ == "__main__":
    r = dump_file(open(sys.argv[1], "rb").read())
    if len(sys.argv) > 2:
        json.dump(r, open(sys.argv[2], "w"))
    print(len(r), "text sectors")
