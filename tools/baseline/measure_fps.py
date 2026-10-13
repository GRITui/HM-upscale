#!/usr/bin/env python3
"""Frame period from the emulated CPU tick counter: hold an inert button for N frames
(the debugger blocks until the hold ends) and compare tick deltas for two N values;
the difference cancels call overhead.   usage: measure_fps.py [BUTTON]   (attach)"""
import sys, os, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import capture as c
btn = sys.argv[1] if len(sys.argv) > 1 else "select"
s = c.Session(".")
HZ = 222_000_000.0  # PSP CPU clock; PPSSPP counts ticks at this rate
res = {}
for n in (600, 1800, 600, 1800):
    k0 = s.ticks(); s.d.press(btn, n); k1 = s.ticks()
    res.setdefault(n, []).append((k1 - k0) / HZ)
    print(n, "frames ->", round((k1 - k0) / HZ, 4), "s emulated", flush=True)
a = sum(res[600]) / 2; b = sum(res[1800]) / 2
period = (b - a) / 1200
print("frame period %.6f s = %.3f fps" % (period, 1 / period))
