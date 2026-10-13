#!/usr/bin/env python3
"""Hold PSP buttons for a wall-clock interval and report position slope per emulated second.

Holds are re-sent every ~25 ms (the virtual pad only holds if refreshed), while the camera eye
(0x08950418), the camera target-frame point (0x08950424) and the CPU tick counter are sampled.
Speed = linear-regression slope of position against emulated seconds (ticks / 222e6), then
divided by 30 for raw units per logic update.

usage: hold_probe.py SECONDS combo [combo ...]    combo = btn[,btn...]   e.g. right right,square
"""
import sys, os, time, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capture as c

s = c.Session(".")
EYE, TGT = 0x08950418, 0x08950424


def sample():
    e = struct.unpack("<iii", s.d.read_mem(EYE, 12))
    t = struct.unpack("<iii", s.d.read_mem(TGT, 12))
    return s.ticks() / 222e6, e, t


def slope(ts, vs):
    n = len(ts); mt = sum(ts) / n; mv = sum(vs) / n
    den = sum((t - mt) ** 2 for t in ts)
    return sum((t - mt) * (v - mv) for t, v in zip(ts, vs)) / den if den else 0.0


def run(combo, secs):
    btn = {k: True for k in combo.split(",")}
    rows = []
    end = time.time() + secs
    while time.time() < end:
        s.d.call("input.buttons.send", buttons=btn)
        rows.append(sample())
        time.sleep(0.02)
    s.d.call("input.buttons.send", buttons={k: False for k in btn})
    time.sleep(0.6)
    ts = [r[0] for r in rows]
    # drop the first 20 percent: acceleration / turning lead-in
    k = len(rows) // 5
    ts, rows = ts[k:], rows[k:]
    out = {}
    for name, idx in (("eye_x", (1, 0)), ("eye_z", (1, 2)), ("tgt_x", (2, 0)), ("tgt_z", (2, 2))):
        v = [r[idx[0]][idx[1]] for r in rows]
        out[name] = slope(ts, v)
    print("%-16s n=%3d  eye dx/s %8.1f dz/s %8.1f | per 30Hz update: x %6.2f z %6.2f | tgt x %8.1f z %8.1f" % (
        combo, len(rows), out["eye_x"], out["eye_z"], out["eye_x"] / 30, out["eye_z"] / 30,
        out["tgt_x"], out["tgt_z"]), flush=True)


secs = float(sys.argv[1])
for combo in sys.argv[2:]:
    run(combo, secs)
    time.sleep(0.5)
