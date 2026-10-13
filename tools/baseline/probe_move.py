"""Measure speed with input.buttons.send held for a timed interval.
usage: probe_move.py btn[,btn...] ...   (each arg is one held combination)"""
import sys, time
sys.path.insert(0, '.')
import capture as c
s = c.Session('.')
def run(combo):
    b = {k: True for k in combo.split(',')}
    p0 = s.pos(); t0 = s.ticks()
    s.d.call("input.buttons.send", buttons=b)
    time.sleep(0.8)
    s.d.call("input.buttons.send", buttons={k: False for k in b})
    time.sleep(0.4)
    p1 = s.pos(); t1 = s.ticks()
    # settle ticks after release excluded: use position at release unknown, so report slope incl. tail
    dt = (t1 - t0) / 222e6
    print(combo, 'dx', p1[0]-p0[0], 'dz', p1[2]-p0[2], 'emu_s %.3f' % dt, flush=True)
for a in sys.argv[1:]:
    run(a); time.sleep(0.3)
