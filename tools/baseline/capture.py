#!/usr/bin/env python3
"""Baseline capture driver for the original game running in PPSSPP.

Launches ULUS10142 in /Applications/PPSSPPSDL.app, plays the new-game intro
through the debugger's virtual pad, then records:

  * screenshots of the game window (needs the macOS Screen Recording permission)
  * about 60 s of window video
  * RAM traces (clock, player position candidates) and CPU tick counts
  * a short walk test with per-step position deltas

Everything is written to OUTDIR (default assets/hm-baseline, which is git-ignored).
Nothing here needs the Accessibility permission: input goes through the PPSSPP
debugger WebSocket (input.buttons.press), not through synthetic key events.

Usage:
  capture.py --iso /path/to/game.iso            # launch, boot, capture
  capture.py --attach                           # reuse a running instance that is
                                                # already in the farmhouse
Options: --out DIR, --video-seconds N, --skip-video

Stdlib only.  See docs/baseline.md for what the numbers mean.
"""
import argparse
import csv
import json
import os
import struct
import shutil
import subprocess
import tempfile
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ppsspp_ws as pw  # noqa: E402

BASE = 0x08804000            # RAM address of ELF vaddr 0 (module load base)
CLOCK = BASE + 0x1AE1A0      # u16 x6: year, season, day, weekday, hour, minute
POS = 0x08950418             # s32 x3 (candidate position block, see docs)
POS_Z = 0x08950454           # s32 (second candidate)


def log(msg):
    print(time.strftime("%H:%M:%S"), msg, flush=True)


class Session:
    def __init__(self, out):
        self.out = out
        self.d = None
        self.reconnect()

    def reconnect(self):
        """(Re)open the debugger socket; PPSSPP drops it while the game boots."""
        for _ in range(30):
            try:
                self.d = pw.Debugger(pw.find_port(), timeout=20)
                return
            except Exception:
                time.sleep(2)
        raise RuntimeError("debugger socket unavailable")

    def clock(self):
        return [self.d.u16(CLOCK + 2 * i) for i in range(6)]

    def pos(self):
        b = self.d.read_mem(POS, 0x40)
        x, y, z = struct.unpack_from("<iii", b, 0)
        return x, y, z, struct.unpack_from("<i", b, 0x3C)[0]

    def ticks(self):
        return self.d.call("cpu.status")["ticks"]

    def press(self, btn, wait=1.5, frames=4):
        try:
            self.d.press(btn, frames)
        except (EOFError, OSError):
            self.reconnect()
            self.d.press(btn, frames)
        time.sleep(wait)

    def shot(self, name):
        subprocess.run([os.path.join(HERE, "winshot.sh"), os.path.join(self.out, name)])


def boot_to_farmhouse(s):
    """Natsume logo -> Boy/Girl title -> Back to Nature title -> new game -> first morning.

    Timings come from a measured run: logo about 5-10 s, Boy/Girl title about
    35-40 s after launch.  Waits are generous and use tick polling, not luck.
    """
    log("waiting for the Boy/Girl title (about 40 s after launch)")
    time.sleep(46)
    s.press("start", 8)      # choose Boy; fade to the Back to Nature title
    s.press("start", 3)      # PRESS START (a second press is sometimes needed)
    s.press("start", 3)
    s.shot("boot_menu.png")
    s.press("up", 1.5)       # cursor from CONTINUE to NEW GAME (CONTINUE reads 'no data' on a fresh install)
    s.press("cross", 3)
    # Dialogue and the four entry screens (name, birthday, farm, dog) share one
    # rhythm: cross advances or enters a letter, start confirms a name (and is
    # ignored elsewhere), cross answers Yes.  About 100 rounds reach the
    # farmhouse; extra presses in the house are harmless.
    for i in range(100):
        s.press("cross", 1.0)
        s.press("start", 1.0)
        s.press("cross", 1.0)
        if i % 10 == 9:
            s.shot("intro_%03d.png" % i)
            log("intro round %d" % (i + 1))
    time.sleep(2)
    s.shot("farmhouse.png")
    log("clock after intro: %s" % s.clock())


def walk_test(s, rows):
    """Hold each direction for several lengths; log position before and after."""
    for dur in (20, 40, 60):
        for btn in ("up", "down", "left", "right"):
            a = s.pos()
            t0, k0 = time.time(), s.ticks()
            s.d.press(btn, dur)
            time.sleep(dur / 60.0 + 0.6)
            b = s.pos()
            t1, k1 = time.time(), s.ticks()
            rows.append([btn, dur, round(t1 - t0, 3), k1 - k0, *a, *b])
            log("walk %-5s %2df: %s -> %s" % (btn, dur, a[:3], b[:3]))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--iso")
    ap.add_argument("--attach", action="store_true")
    ap.add_argument("--out", default=os.path.join(HERE, "..", "..", "assets", "hm-baseline"))
    ap.add_argument("--video-seconds", type=int, default=60)
    ap.add_argument("--skip-video", action="store_true")
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)

    if not a.attach:
        if not a.iso:
            ap.error("--iso is required unless --attach is given")
        subprocess.run([os.path.join(HERE, "launch.sh"), a.iso, out], check=True)
    s = Session(out) if a.attach else None
    if not a.attach:
        time.sleep(1)
        s = Session(out)
        boot_to_farmhouse(s)

    # Frame/tick evidence: CPU tick counter against wall time while idle.
    t0, k0 = time.time(), s.ticks()
    time.sleep(10)
    t1, k1 = time.time(), s.ticks()
    tick_rate = (k1 - k0) / (t1 - t0)
    log("cpu ticks per wall second: %.4g" % tick_rate)

    # RAM trace while idle (and, in parallel, the window video).
    vid = None
    if not a.skip_video:
        # screencapture fails to write straight onto some external volumes: record
        # to the system temp dir, move into OUTDIR afterwards.
        tmpmov = os.path.join(tempfile.gettempdir(), "hm_baseline60.mov")
        vid = subprocess.Popen([os.path.join(HERE, "winshot.sh"), "-v",
                                str(a.video_seconds), tmpmov])
    trace = os.path.join(out, "trace.csv")
    with open(trace, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["wall_s", "ticks", "year", "season", "day", "weekday", "hour", "minute",
                    "x", "y", "z", "aux"])
        start = time.time()
        while time.time() - start < a.video_seconds:
            w.writerow([round(time.time() - start, 3), s.ticks(), *s.clock(), *s.pos()])
            time.sleep(1.0)
    if vid:
        vid.wait()
        if os.path.exists(tmpmov):
            shutil.move(tmpmov, os.path.join(out, "baseline60.mov"))

    rows = []
    walk_test(s, rows)
    with open(os.path.join(out, "walk.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["button", "frames_held", "wall_s", "ticks", "x0", "y0", "z0", "aux0",
                    "x1", "y1", "z1", "aux1"])
        w.writerows(rows)
    s.shot("after_walk.png")
    json.dump({"cpu_ticks_per_wall_second": tick_rate}, open(os.path.join(out, "summary.json"), "w"))
    log("done; outputs in %s" % out)


if __name__ == "__main__":
    main()
