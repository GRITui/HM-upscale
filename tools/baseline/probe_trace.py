"""Hold a combo with input.buttons.send, sample (ticks,x,z) every ~0.1s wall."""
import sys, time
sys.path.insert(0, '.')
import capture as c
s = c.Session('.')
combo = sys.argv[1]; hold = float(sys.argv[2])
b = {k: True for k in combo.split(',')}
rows = []
def samp(): rows.append((s.ticks(),) + (s.pos()[0], s.pos()[2]))
samp()
s.d.call("input.buttons.send", buttons=b)
t = time.time()
while time.time() - t < hold: samp(); time.sleep(0.05)
s.d.call("input.buttons.send", buttons={k: False for k in b})
for _ in range(10): samp(); time.sleep(0.05)
t0 = rows[0][0]
for r in rows: print('%.3f' % ((r[0]-t0)/222e6), r[1], r[2])
