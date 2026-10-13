#!/usr/bin/env python3
"""Sample the in-game clock against wall time and CPU ticks (idle, outdoors).
usage: measure_clock.py OUT.csv [seconds]   (attach to a running PPSSPP)"""
import sys, os, csv, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capture as c
out = sys.argv[1]; secs = float(sys.argv[2]) if len(sys.argv) > 2 else 300
s = c.Session(os.path.dirname(out))
with open(out, "w", newline="") as f:
    w = csv.writer(f); w.writerow(["wall_s", "ticks", "year", "season", "day", "weekday", "hour", "minute"])
    t0 = time.time()
    while time.time() - t0 < secs:
        w.writerow([round(time.time() - t0, 3), s.ticks(), *s.clock()]); f.flush()
        time.sleep(0.5)
