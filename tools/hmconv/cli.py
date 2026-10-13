"""Single converter CLI:  python -m hmconv <command> <in> <out>   (run from tools/).

Commands: unpack, image, audio, batch, and sprite/map/text/tables (delegated to
hmconv.<name> when another module provides it, otherwise 'not implemented yet').
Extension hook for those modules: `convert(in_path, out_path)` for single files and
optionally `batch_convert(entries_root, out_dir) -> [manifest entry dicts]` for batch.
"""
import argparse
import hashlib
import importlib
import json
import os
import sys

from . import audio, manifest, texture_png
from . import unpack_container as uc

PLACEHOLDERS = ("sprite", "map", "text", "tables")
FACE_BLOCK = 0x1800
NOT_IMPLEMENTED = 2


# --- subcommands -----------------------------------------------------------

def cmd_unpack(a):
    base = os.path.basename(a.input).upper()
    if a.index:
        idx = a.index
    elif base.startswith("DATA001"):
        idx = os.path.join(os.path.dirname(a.input), "DATA000.BIN")
    else:
        idx = os.path.splitext(a.input)[0] + (".HDT" if os.path.exists(os.path.splitext(a.input)[0] + ".HDT") else ".hdt")
    mode = a.mode or ("toc" if base.startswith("DATA001") else "hdt")
    if mode == "hdt":
        res = uc.unpack_hdt(a.input, idx, a.output, uc.BOY_SEGMENT_NAMES if base.startswith("BOY") else None)
    else:
        res = uc.unpack_toc(idx, a.input, a.output)
    with open(os.path.join(a.output, "manifest.json"), "w") as f:
        json.dump(res, f, indent=1)
    print("%d entries -> %s" % (len(res), a.output))
    return 0


def cmd_image(a):
    for p in texture_png.convert_image(a.input, a.output):
        print(p)
    return 0


def cmd_audio(a):
    kind = audio.convert_audio(a.input, a.output)
    print("%s (%s) -> %s" % (a.input, kind, a.output))
    return 0


def _placeholder(name):
    def run(a):
        try:
            mod = importlib.import_module("hmconv." + name)
        except ImportError:
            mod = None
        fn = getattr(mod, "convert", None)
        if fn is None:
            print("hmconv %s: not implemented yet (blocked on format work, see docs/converter-coverage.md)" % name,
                  file=sys.stderr)
            return NOT_IMPLEMENTED
        return fn(a.input, a.output) or 0
    return run


# --- batch -----------------------------------------------------------------

def _sha1(b):
    return hashlib.sha1(b).hexdigest()


def _ensure_unpacked(usr, ent_root, force, report):
    plan = [("boy", "BOY_FILE.BIN", "BOY_FILE.HDT", "hdt"), ("girl", "GIRL_FILE.BIN", "GIRL_FILE.HDT", "hdt"),
            ("data001", "DATA001.BIN", "DATA000.BIN", "toc")]
    for name, binf, idxf, mode in plan:
        out = os.path.join(ent_root, name)
        if os.path.isfile(os.path.join(out, "manifest.json")) and not force:
            continue
        b, i = os.path.join(usr, binf), os.path.join(usr, idxf)
        if not (os.path.isfile(b) and os.path.isfile(i)):
            report["missing"].append(binf)
            continue
        res = uc.unpack_hdt(b, i, out, uc.BOY_SEGMENT_NAMES if name == "boy" else None) if mode == "hdt" \
            else uc.unpack_toc(i, b, out)
        with open(os.path.join(out, "manifest.json"), "w") as f:
            json.dump(res, f, indent=1)


def _save(out_dir, rel, data):
    p = os.path.join(out_dir, rel)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    with open(p, "wb") as f:
        f.write(data)


def _faces(ent_root, out_dir, side, entries, index, report):
    src = os.path.join(ent_root, side, "seg001.bin")
    if not os.path.isfile(src):
        return
    data = open(src, "rb").read()
    total = len(data) // FACE_BLOCK
    ok = 0
    for i in range(total):
        try:
            pngs = texture_png.image_to_pngs(data[i * FACE_BLOCK:(i + 1) * FACE_BLOCK])
            png, info = pngs[0]
        except Exception as ex:  # keep going; count failures
            report["errors"].append("%s face %d: %s" % (side, i, ex))
            continue
        rel = "textures/%s/face/face_%03d.png" % (side, i)
        _save(out_dir, rel, png)
        entries.append(manifest.entry("%s_face_%03d" % (side, i), "texture", rel, ["portrait", side, "face"],
                                      "%s seg001 block %d" % (side, i)))
        index["portraits"].append({"id": "%s_face_%03d" % (side, i), "width": info["width"], "height": info["height"]})
        ok += 1
    report["stats"]["face_%s" % side] = {"converted": ok, "total": total}


def _fonts(ent_root, out_dir, side, entries, report):
    src = os.path.join(ent_root, side, "seg007.bin")
    if not os.path.isfile(src):
        return
    ok = 0
    try:
        pngs = texture_png.image_to_pngs(open(src, "rb").read())
    except Exception as ex:
        report["errors"].append("%s Font_all: %s" % (side, ex))
        pngs = []
    for png, info in pngs:
        rel = "textures/%s/font/font_all_%d.png" % (side, info["chunk"])
        _save(out_dir, rel, png)
        entries.append(manifest.entry("%s_font_all_%d" % (side, info["chunk"]), "texture", rel,
                                      ["font", "glyph_sheet", side], "%s seg007 chunk %d" % (side, info["chunk"])))
        ok += 1
    report["stats"]["font_%s" % side] = {"converted": ok, "total": 2}


def _audio(ent_root, out_dir, entries, index, report):
    d = os.path.join(ent_root, "data001")
    mf = os.path.join(d, "manifest.json")
    if not os.path.isfile(mf):
        return
    ents = json.load(open(mf))
    tracks_ok = se_ok = se_total = 0
    for e in ents:
        src = os.path.join(d, e["file"])
        data = open(src, "rb").read()
        if e["type"] == "audio_vag":
            for k, (off, name, vag) in enumerate(audio.vag_split(data)):
                se_total += 1
                rel = "audio/se/se_%03d.ogg" % k
                tmp = os.path.join(out_dir, rel + ".vag")
                try:
                    os.makedirs(os.path.dirname(tmp), exist_ok=True)
                    open(tmp, "wb").write(vag)
                    audio.vag_to_audio(tmp, os.path.join(out_dir, rel))
                except Exception as ex:
                    report["errors"].append("se %d: %s" % (k, str(ex)[:160]))
                    continue
                finally:
                    if os.path.exists(tmp):
                        os.unlink(tmp)
                rate, pcm = audio.vag_decode(vag)
                entries.append(manifest.entry("se_%03d" % k, "audio", rel, ["se", "vag"], "DATA001 ent000 offset %d %s" % (off, name)))
                index["audio"].append({"id": "se_%03d" % k, "name": name, "rate": rate, "seconds": round(len(pcm) / rate, 3)})
                se_ok += 1
        elif e["type"].startswith("audio_riff"):
            n = int(e["file"][3:6])
            rel = "audio/bgm/track_%03d.ogg" % n
            try:
                audio.convert_audio(src, os.path.join(out_dir, rel))
            except Exception as ex:
                report["errors"].append("track %d: %s" % (n, ex))
                continue
            entries.append(manifest.entry("track_%03d" % n, "audio", rel, ["track", "atrac3plus"], "DATA001 ent%03d" % n))
            index["audio"].append({"id": "track_%03d" % n, "source": e["file"], "sha1": e["sha1"]})
            tracks_ok += 1
    report["stats"]["audio_tracks"] = {"converted": tracks_ok + (1 if se_ok else 0), "total": len(ents)}
    report["stats"]["audio_se_bank"] = {"converted": se_ok, "total": se_total}


def _hooks(ent_root, out_dir, entries, report):
    for name in PLACEHOLDERS:
        try:
            mod = importlib.import_module("hmconv." + name)
        except ImportError:
            report["stats"][name] = "not implemented"
            continue
        fn = getattr(mod, "batch_convert", None)
        if fn is None:
            report["stats"][name] = "no batch_convert hook"
            continue
        try:
            got = fn(ent_root, out_dir) or []
            entries.extend(got)
            report["stats"][name] = {"converted": len(got)}
        except Exception as ex:
            report["errors"].append("%s: %s" % (name, ex))
            report["stats"][name] = "failed"


def cmd_sheets(a):
    from . import actor_sheet
    elf = open(a.elf, "rb").read()
    seg = open(a.gf, "rb").read()
    idx = actor_sheet.export(elf, seg, a.output)
    print("%d sheets, %d resources -> %s" % (len(idx), sum(len(e["resources"]) for e in idx), a.output))
    return 0


def cmd_batch(a):
    raw = a.raw
    usr = os.path.join(raw, "extracted", "PSP_GAME", "USRDIR")
    ent_root = os.path.join(raw, "extracted_entries")
    out = a.output or os.path.join(raw, "converted")
    report = {"stats": {}, "errors": [], "missing": []}
    entries, index = [], {"portraits": [], "audio": []}
    _ensure_unpacked(usr, ent_root, a.force, report)
    for side in ("boy", "girl"):
        _faces(ent_root, out, side, entries, index, report)
        _fonts(ent_root, out, side, entries, report)
    _audio(ent_root, out, entries, index, report)
    _hooks(ent_root, out, entries, report)
    _save(out, "data/asset_index.json", json.dumps(index, indent=1).encode())
    entries.append(manifest.entry("asset_index", "data", "data/asset_index.json", ["index"], "converter output"))
    manifest.write_manifest(out, entries)
    with open(os.path.join(out, "batch_report.json"), "w") as f:
        json.dump(report, f, indent=1)
    print("manifest: %d assets -> %s" % (len(entries), out))
    for k, v in sorted(report["stats"].items()):
        print("  %-16s %s" % (k, v))
    for m in report["missing"]:
        print("  missing input:", m)
    print("  errors: %d" % len(report["errors"]))
    for e in report["errors"][:10]:
        print("   ", e)
    return 1 if report["errors"] and not a.keep_going else 0


# --- entry point -----------------------------------------------------------

def build_parser():
    p = argparse.ArgumentParser(prog="hmconv", description="HM-upscale asset converter")
    sub = p.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("unpack", help="container (BOY/GIRL_FILE.BIN, DATA001.BIN) -> entries")
    s.add_argument("input"); s.add_argument("output")
    s.add_argument("--index", help="index file (default: sibling .HDT / DATA000.BIN)")
    s.add_argument("--mode", choices=["hdt", "toc"])
    s.set_defaults(fn=cmd_unpack)
    s = sub.add_parser("image", help="0x100D image -> PNG")
    s.add_argument("input"); s.add_argument("output"); s.set_defaults(fn=cmd_image)
    s = sub.add_parser("audio", help="ATRAC3plus RIFF / VAG -> .wav or .ogg (by output extension)")
    s.add_argument("input"); s.add_argument("output"); s.set_defaults(fn=cmd_audio)
    for n in PLACEHOLDERS:
        s = sub.add_parser(n, help="%s converter (delegates to hmconv.%s when present)" % (n, n))
        s.add_argument("input"); s.add_argument("output"); s.set_defaults(fn=_placeholder(n))
    s = sub.add_parser("sheets", help="gf.bin actor sheets (player/animal/object/NPC) -> PNG/JSON + sprite_index.json")
    s.add_argument("elf"); s.add_argument("gf"); s.add_argument("output"); s.set_defaults(fn=cmd_sheets)
    s = sub.add_parser("batch", help="convert everything convertible under assets/hm-raw")
    s.add_argument("--raw", default=os.path.join("assets", "hm-raw"))
    s.add_argument("--output", help="default: <raw>/converted")
    s.add_argument("--force", action="store_true", help="re-unpack containers")
    s.add_argument("--keep-going", action="store_true", help="exit 0 even if some items failed")
    s.set_defaults(fn=cmd_batch)
    return p


def main(argv=None):
    a = build_parser().parse_args(argv)
    try:
        return a.fn(a)
    except (OSError, ValueError, audio.AudioError) as ex:
        print("hmconv %s: error: %s" % (a.cmd, ex), file=sys.stderr)
        return 1
