"""python3 -m tools.modelgen [--out DIR]  (run from repo root)"""
import argparse
import os

from .models import all_models
from .readme import write_readme

DEFAULT_OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Assets", "_HM", "Art", "Models")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=DEFAULT_OUT)
    args = ap.parse_args()
    out = os.path.normpath(args.out)
    os.makedirs(out, exist_ok=True)
    rows = []
    for m in all_models():
        data = m.to_glb()
        with open(os.path.join(out, m.name + ".glb"), "wb") as f:
            f.write(data)
        rows.append((m.name, m.tri_count(), len(data), [p.name for p in m.parts]))
        print("%-16s %5d tris %7d bytes" % (m.name, m.tri_count(), len(data)))
    write_readme(out, rows)
    print("total tris (one of each): %d" % sum(r[1] for r in rows))


if __name__ == "__main__":
    main()
