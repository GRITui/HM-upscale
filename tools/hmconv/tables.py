"""Item / shop / seed price table found in the hmpsp.prx ELF image.

Layout (see docs/formats/tables.md): rows of 0x28 bytes, 20 little-endian u16 fields,
starting at ELF file offset TABLE_OFF. Field meanings carry a confidence level in FIELDS.
Hooks call convert(in_path, out_path) with the ELF path; output is JSON.
"""
import json
import struct

TABLE_OFF = 0x12763C
ROW = 0x28
NROWS = 331

# name -> (u16 index, confidence)
FIELDS = {
    "delta": (0, "low"),         # signed small value, maybe stamina/fatigue
    "kind": (4, "medium"),       # type/flag word: 0x4000 item, 0x4040 seed, 0x8000.. other
    "seed_class": (5, "high"),   # low byte 2 = seed; high byte = seed index
    "item_id": (11, "medium"),
    "price": (12, "high"),       # buy price
    "sell": (13, "medium"),      # sell/value
}


def s16(v):
    return v - 0x10000 if v >= 0x8000 else v


def parse_rows(data, off=TABLE_OFF, n=NROWS):
    rows = []
    for i in range(n):
        p = off + i * ROW
        if p + ROW > len(data):
            break
        f = struct.unpack_from("<20H", data, p)
        rows.append({
            "row": i,
            "delta": s16(f[0]),
            "kind": f[4],
            "class": f[5] & 0xFF,
            "seed_index": (f[5] >> 8) if (f[5] & 0xFF) == 2 else None,
            "item_id": s16(f[11]),
            "price": f[12],
            "sell": f[13],
        })
    return rows


def seed_prices(rows):
    """Seeds: kind 0x4040 and class 2. Returns {seed_index: buy_price}."""
    return {r["seed_index"]: r["price"] for r in rows
            if r["kind"] == 0x4040 and r["class"] == 2}


def convert(in_path, out_path):
    with open(in_path, "rb") as fh:
        rows = parse_rows(fh.read())
    out = {"rows": rows, "seed_prices": seed_prices(rows),
           "confidence": {k: v[1] for k, v in FIELDS.items()}}
    with open(out_path, "w") as fh:
        json.dump(out, fh, indent=1)
