"""Portrait (face.bin) decoding: 0x1800-byte blocks of one 80x80 8bpp 0x100D image each.

See docs/formats/face.md.
"""
import json
import os

try:
    from . import texture_png as tp
except ImportError:
    import texture_png as tp

BLOCK = 0x1800
SIZE = 80


def split_blocks(data):
    return [data[i * BLOCK:(i + 1) * BLOCK] for i in range(len(data) // BLOCK)]


def export(data, out_dir, side="boy"):
    """Write face_NNN.png for every decodable block and faces_index.json. Returns the index."""
    os.makedirs(out_dir, exist_ok=True)
    index = {"side": side, "size": [SIZE, SIZE], "faces": [], "failed": []}
    for i, blk in enumerate(split_blocks(data)):
        try:
            pngs = tp.image_to_pngs(blk)
            png, info = pngs[0]
        except Exception as ex:
            index["failed"].append({"id": i, "error": str(ex)})
            continue
        name = "face_%03d.png" % i
        with open(os.path.join(out_dir, name), "wb") as f:
            f.write(png)
        index["faces"].append({"id": i, "file": name, "width": info["width"], "height": info["height"]})
    with open(os.path.join(out_dir, "faces_index.json"), "w") as f:
        json.dump(index, f, indent=1)
    return index
