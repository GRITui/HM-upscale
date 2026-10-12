#!/usr/bin/env python3
"""Decode HM dialogue text sectors (messege0..4) to JSON. See docs/formats/text.md.

Text is a stream of little-endian u16 codes. 0x00-0x19 = A-Z, 0x20-0x39 = a-z (glyph
indices into Font_all), 0x0ffd = space, 0xfffe = line break, 0xffff = end of string,
0x0e00-0x0fff (other than 0x0ffd) = control codes (semantics unknown, kept raw). Any
other code is emitted as <xxxx> (punctuation/digits/kana not yet mapped).
Output must never be committed (game text); this tool only writes where asked.
Usage: text.py <messegeN.bin> [out.json]
"""
import json, struct, sys

SECTOR = 0x800
SPACE, NEWLINE, END = 0x0FFD, 0xFFFE, 0xFFFF


def glyph(code):
    if 0x00 <= code <= 0x19: return chr(0x41 + code)
    if 0x20 <= code <= 0x39: return chr(0x61 + code - 0x20)
    return None


def decode_codes(codes):
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
        elif glyph(c): text.append(glyph(c))
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
