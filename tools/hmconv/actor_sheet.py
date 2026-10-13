#!/usr/bin/env python3
"""Decode the standalone actor sheets of gf.bin (seg000): res_0e + inline 0x100D image.

The loaders in hmpsp.prx read ranges of gf.bin by byte offset (FUN_000083d0: file 0,
sector = offset >> 11). Besides the 261 scene rows (gf.py) the offset table continues
as flat u32 lists, viewed by several loaders at different bases. Each entry [A[i], A[i+1])
is one sheet laid out as

    [blob: size bytes]  [0x100D image]  ([optional 2nd 0x100D image at table[n+2]])

where the blob packs n res_0e resources at offsets table[1..n] (table = a data row
{n, off_0..off_{n-1}, size, ...} reached through a pointer array). The image's chunks and
CLUTs land at the absolute VRAM positions the tiles reference, so a fresh Vram per sheet
renders it. Families (loader function, offset list, pointer array, ELF vaddrs):
  main  FUN_00065ed8  0x139910 / 0x1354f8   6 sheets: 0,1 player sets, 2..5 animals
  chr   FUN_00066154  0x13992c / 0x135a54  10 sheets: small field objects
  obj   FUN_0006624c  0x139970 / 0x1362ec  50 sheets: human characters, spouse variants
Fixed sheets (loaded by address): see FIXED. See docs/formats/actor.md.

Usage: actor_sheet.py <hmpsp.prx.elf> <seg000.bin> <out_dir>
"""
import json, os, struct, sys

try:
    from . import actor, sprite as sp, texture as tx, texture_png as tp
    from .decompress import parse_image
except ImportError:
    import actor, sprite as sp, texture as tx, texture_png as tp
    from decompress import parse_image

ELF_BIAS = 0x74
FAMILIES = [("main", 0x139910, 0x1354f8, 6), ("chr", 0x13992c, 0x135a54, 10),
            ("obj", 0x139970, 0x1362ec, 50)]
# (id, gf.bin offset, res_0e bytes, image offset from sheet start or None)
FIXED = [("fx_37e2800", 0x37e2800, 0xA20, 0xA20), ("fx_37ec800", 0x37ec800, 0x838, 0x838),
         ("fx_380a000", 0x380a000, 0x540, 0x1500), ("fx_3858000", 0x3858000, 0x800, 0x800),
         ("fx_37f4800", 0x37f4800, None, None), ("fx_3804800", 0x3804800, None, None)]


def _u(elf, vaddr):
    return struct.unpack_from("<I", elf, vaddr + ELF_BIAS)[0]


def read_layout(elf):
    """Sheet descriptors from the ELF tables: id, family, index, start, res offsets, sizes."""
    out = []
    for fam, arr, tbl, count in FAMILIES:
        for i in range(count):
            row = _u(elf, tbl + 4 * i)
            n = _u(elf, row)
            offs = [_u(elf, row + 4 + 4 * k) for k in range(n)]
            size = _u(elf, row + 4 * (n + 1))
            start = _u(elf, arr + 4 * i)
            images = [start + size]
            if fam == "obj" and n == 2 and i >= 35:  # second image offset in the table
                images.append(start + _u(elf, row + 4 * (n + 2)))
            out.append({"id": "%s_%02d" % (fam, i), "family": fam, "index": i,
                        "start": start, "res_size": size, "res_offsets": offs,
                        "images": images})
    for name, start, size, img in FIXED:
        out.append({"id": name, "family": "fixed", "index": None, "start": start,
                    "res_size": size, "res_offsets": [0] if size else [],
                    "images": [start + img] if img is not None else []})
    return out


def decode_sheet(seg, desc):
    """(vram, [res dict per sub-resource]) of one descriptor; vram is None when no image."""
    s, size = desc["start"], desc["res_size"]
    vram = None
    if desc["images"]:
        vram = tx.Vram()
        for off in desc["images"]:
            vram.load_image(parse_image(seg, off))
    offs = desc["res_offsets"]
    if size is None:  # res-only fixed sheet: one resource, image lives in a scene row
        return None, [sp.parse_res0e(seg[s:s + 0x10000])]
    bounds = sorted(set(offs) | {size})  # table order is not address order
    ends = {o: bounds[bounds.index(o) + 1] for o in offs}
    return vram, [sp.parse_res0e(seg[s + o:s + ends[o]]) for o in offs]


def export(elf, seg, out_dir):
    """Write <id>_r<k>.png/.json per resource and sprite_index.json. Returns the index."""
    os.makedirs(out_dir, exist_ok=True)
    index = []
    for d in read_layout(elf):
        res_only = d["res_size"] is None or not d["images"]
        entry = {"id": d["id"], "family": d["family"], "index": d["index"],
                 "gf_offset": d["start"], "res_size": d["res_size"],
                 "image_offsets": d["images"], "resources": []}
        vram, ress = decode_sheet(seg, d)
        for k, res in enumerate(ress):
            stem = "%s_r%02d" % (d["id"], k)
            meta = {"counts": res["counts"], "anims": res["anims"], "frames": res["frames"]}
            rec = {"res": k, "json": stem + ".json", "png": None, "counts": res["counts"]}
            if vram is not None:
                W, H, rgba, cells = actor.sheet(res, vram, cols=10)
                with open(os.path.join(out_dir, stem + ".png"), "wb") as f:
                    f.write(tp.write_png_rgba(W, H, rgba))
                meta["cells"] = cells
                rec["png"] = stem + ".png"
            with open(os.path.join(out_dir, stem + ".json"), "w") as f:
                json.dump(meta, f)
            entry["resources"].append(rec)
        index.append(entry)
    with open(os.path.join(out_dir, "sprite_index.json"), "w") as f:
        json.dump({"sheets": index}, f, indent=1)
    return index


if __name__ == "__main__":
    elf = open(sys.argv[1], "rb").read()
    seg = open(sys.argv[2], "rb").read()
    idx = export(elf, seg, sys.argv[3])
    print(len(idx), "sheets,", sum(len(e["resources"]) for e in idx), "resources")
