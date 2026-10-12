"""Asset manifest writer. Mirrors unity/Assets/_HM/Importers/AssetManifest.cs (schema 1).

    {"schema": 1, "assets": [{"id", "type", "path", "tags", "license", "source"?}]}

id    ^[a-z][a-z0-9_]{0,63}$, unique
type  model | texture | audio | data
path  relative, forward slashes, no '..', extension: texture .png, audio .ogg, data .json,
      model .gltf/.glb
tags  each matches the id pattern
license required, non-empty (converters write "user-owned-copy")
source optional extra field (origin inside the game data); Unity's JsonUtility ignores it.
"""
import json
import os
import re

SCHEMA = 1
ID_RE = re.compile(r"^[a-z][a-z0-9_]{0,63}$")
EXTS = {"model": (".gltf", ".glb"), "texture": (".png",), "audio": (".ogg",), "data": (".json",)}
LICENSE = "user-owned-copy"


def entry(id, type, path, tags=(), source=None, license=LICENSE):
    e = {"id": id, "type": type, "path": path, "tags": list(tags), "license": license}
    if source:
        e["source"] = source
    return e


def validate(manifest):
    """Return a list of problems (empty = valid); same rules as AssetManifest.Validate."""
    problems, seen = [], set()
    if manifest.get("schema") != SCHEMA:
        problems.append("schema must be %d" % SCHEMA)
    for e in manifest.get("assets", []):
        who = e.get("id") or "<no id>"
        if not ID_RE.match(e.get("id", "")):
            problems.append("%s: bad id" % who)
        elif e["id"] in seen:
            problems.append("%s: duplicate id" % who)
        seen.add(e.get("id"))
        t = e.get("type")
        p = e.get("path", "")
        if t not in EXTS:
            problems.append("%s: unknown type %r" % (who, t))
        if not p:
            problems.append("%s: path required" % who)
        elif p.startswith("/") or ".." in p or "\\" in p or ":" in p:
            problems.append("%s: path must be relative, forward slashed" % who)
        elif t in EXTS and os.path.splitext(p)[1].lower() not in EXTS[t]:
            problems.append("%s: extension does not match type" % who)
        if not str(e.get("license", "")).strip():
            problems.append("%s: license required" % who)
        for tag in e.get("tags", []):
            if not ID_RE.match(tag):
                problems.append("%s: bad tag %r" % (who, tag))
    return problems


def write_manifest(out_dir, entries):
    m = {"schema": SCHEMA, "assets": sorted(entries, key=lambda e: e["id"])}
    bad = validate(m)
    if bad:
        raise ValueError("invalid manifest: " + "; ".join(bad[:5]))
    for e in m["assets"]:
        if not os.path.isfile(os.path.join(out_dir, e["path"])):
            raise ValueError("%s: file missing: %s" % (e["id"], e["path"]))
    with open(os.path.join(out_dir, "manifest.json"), "w") as f:
        json.dump(m, f, indent=1)
    return m
