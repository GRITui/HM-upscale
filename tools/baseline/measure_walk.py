#!/usr/bin/env python3
"""Outdoor movement probe against a running PPSSPP (attach).

The debugger's virtual-pad press blocks until the hold ends, but its duration is not an exact
frame count (31 to 44 'frames' per emulated second were seen), and movement continues a frame
or two past the release.  So speed is taken as the slope of position against the emulated CPU
tick counter across holds of different lengths: the fixed lead/lag cancels in the slope.

usage: measure_walk.py OUT.csv case [case ...]       case = btn[+btn]:frames   (default 40)
Writes one row per hold: case, ticks_before, ticks_after, x0, z0, x1, z1, then waits for the
player to come to rest before reading the end position."""
import sys, os, csv, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capture as c

s = c.Session(".")
def settle():
    last = None
    for _ in range(20):
        p = s.pos()[:3:2]
        if p == last: return p
        last = p; time.sleep(0.15)
    return last
with open(sys.argv[1], "a", newline="") as f:
    w = csv.writer(f)
    for case in sys.argv[2:]:
        btns, _, n = case.partition(":"); n = int(n or 40)
        x0, _, z0, _ = s.pos()
        k0 = s.ticks()
        for b in btns.split("+"):
            s.d.press(b, n)          # sequential: the second hold starts when the first ends
        k1 = s.ticks()
        x1, z1 = settle()
        w.writerow([case, k0, k1, x0, z0, x1, z1]); f.flush()
        print(case, "ticks %.3fs" % ((k1 - k0) / 222e6), (x0, z0), "->", (x1, z1), flush=True)
