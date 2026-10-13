#!/usr/bin/env python3
"""Export the outdoor farm yard rows (maps/map_NNN.json, scenes/farm_NN.glb, scenes_textured/farm_NN.glb).

Rows 0..7 are the yard (other rows, e.g. the 80..95 interiors, export the same way without an entry) (gf.farm_row: season + 4 * building variant). Each map JSON carries `entry`, the raw
position where the player stands after leaving the farmhouse, read from region 0 of the matching interior row
(gf.house_row), which stores it in the yard frame. Output is git-ignored game data.
Usage: farm_export.py <hmpsp.prx.elf> <seg000.bin> <out-dir> [row ...]
"""
import json, os, sys
import gf, scene, scene_glb, texture_glb


def entry_for(seg, table, row):
    if row >= gf.FARM_ROWS * 2:
        return None  # only yard rows have a farmhouse interior that stores their entry
    hb = seg[table[gf.house_row(row % gf.FARM_ROWS)][0]:table[gf.house_row(row % gf.FARM_ROWS)][1]]
    for g in scene.parse_scene(hb)["regions"]:
        if g["index"] == 0:
            return list(g["arrival"])
    return None


def export(elf, seg, out, rows):
    t = gf.read_table(elf)
    for d in ("maps", "scenes", "scenes_textured"):
        os.makedirs(os.path.join(out, d), exist_ok=True)
    for r in rows:
        sb, img = seg[t[r][0]:t[r][1]], seg[t[r][1]:t[r][2]]
        js = scene.to_json(scene.parse_scene(sb), sb, entry_for(seg, t, r))
        with open(os.path.join(out, "maps", "map_%03d.json" % r), "w") as f:
            json.dump(js, f, indent=1)
        with open(os.path.join(out, "scenes", "farm_%02d.glb" % r), "wb") as f:
            f.write(scene_glb.build_glb(sb))
        glb, stats = texture_glb.build_textured_glb(sb, img)
        with open(os.path.join(out, "scenes_textured", "farm_%02d.glb" % r), "wb") as f:
            f.write(glb)
        print("row %d: entry %s, %d textures" % (r, js["entry"], len(stats)))


if __name__ == "__main__":
    e = open(sys.argv[1], "rb").read()
    s = open(sys.argv[2], "rb").read()
    export(e, s, sys.argv[3], [int(x) for x in sys.argv[4:]] or list(range(8)))
