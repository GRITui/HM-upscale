#!/usr/bin/env python3
"""Split HM container files into entries (stdlib only). See docs/formats/container.md.

Two layouts:
  hdt : <NAME>.BIN + <NAME>.HDT. HDT is an array of u32 LE ascending byte offsets
        (N+1 values for N segments; the last value is the end of the file).
  toc : DATA000.BIN (table of contents) + DATA001.BIN (payload).
        Header 32 bytes: u32 header_size, u32 version/count-of-files, u32 n_entries,
        u32 reserved, char[16] payload name. Then n_entries 16-byte records
        (u32 a, u32 b, u32 offset, u32 size).
"""
import argparse
import hashlib
import json
import os
import struct
import sys

try:
    from .classify import classify
except ImportError:  # run as a script
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from classify import classify

SECTOR = 0x800


def parse_hdt(hdt, total_size=None):
    """Return a list of (offset, size) segments."""
    if len(hdt) % 4 or len(hdt) < 8:
        raise ValueError("HDT length must be a multiple of 4 and hold >= 2 offsets")
    offs = list(struct.unpack("<%dI" % (len(hdt) // 4), hdt))
    if offs[0] != 0:
        raise ValueError("first HDT offset must be 0")
    for a, b in zip(offs, offs[1:]):
        if b < a:
            raise ValueError("HDT offsets must be ascending")
    if total_size is not None and offs[-1] != total_size:
        raise ValueError("last HDT offset %d != BIN size %d" % (offs[-1], total_size))
    return [(a, b - a) for a, b in zip(offs, offs[1:])]


def parse_toc(toc):
    """Return (payload_name, [(offset, size, a, b), ...])."""
    if len(toc) < 32:
        raise ValueError("TOC too short")
    hsize, _ver, count, _res = struct.unpack_from("<4I", toc, 0)
    name = toc[16:32].split(b"\0")[0].decode("ascii", "replace")
    if hsize != 32 or 32 + 16 * count > len(toc):
        raise ValueError("bad TOC header")
    ents = []
    for i in range(count):
        a, b, off, size = struct.unpack_from("<4I", toc, hsize + 16 * i)
        ents.append((off, size, a, b))
    return name, ents


def _write(outdir, idx, data, manifest, prefix):
    tag, ext = classify(data[:64])
    fn = "%s%03d.%s" % (prefix, idx, ext)
    with open(os.path.join(outdir, fn), "wb") as f:
        f.write(data)
    return {"file": fn, "size": len(data), "sha1": hashlib.sha1(data).hexdigest(), "type": tag}


def unpack_hdt(bin_path, hdt_path, outdir):
    with open(hdt_path, "rb") as f:
        hdt = f.read()
    total = os.path.getsize(bin_path)
    segs = parse_hdt(hdt, total)
    os.makedirs(outdir, exist_ok=True)
    out = []
    with open(bin_path, "rb") as f:
        for i, (off, size) in enumerate(segs):
            f.seek(off)
            e = _write(outdir, i, f.read(size), out, "seg")
            e["offset"] = off
            out.append(e)
    return out


def unpack_toc(toc_path, payload_path, outdir):
    with open(toc_path, "rb") as f:
        _name, ents = parse_toc(f.read())
    total = os.path.getsize(payload_path)
    os.makedirs(outdir, exist_ok=True)
    out = []
    with open(payload_path, "rb") as f:
        for i, (off, size, _a, _b) in enumerate(ents):
            if off + size > total:
                raise ValueError("entry %d exceeds payload" % i)
            f.seek(off)
            e = _write(outdir, i, f.read(size), out, "ent")
            e["offset"] = off
            out.append(e)
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("mode", choices=["hdt", "toc"])
    ap.add_argument("bin", help="payload file (BOY_FILE.BIN / DATA001.BIN)")
    ap.add_argument("index", help="index file (.HDT / DATA000.BIN)")
    ap.add_argument("-o", "--out", required=True)
    a = ap.parse_args(argv)
    if a.mode == "hdt":
        res = unpack_hdt(a.bin, a.index, a.out)
    else:
        res = unpack_toc(a.index, a.bin, a.out)
    with open(os.path.join(a.out, "manifest.json"), "w") as f:
        json.dump(res, f, indent=1)
    print("%d entries -> %s" % (len(res), a.out))


if __name__ == "__main__":
    main()
