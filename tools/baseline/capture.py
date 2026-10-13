#!/usr/bin/env python3
"""Baseline capture driver for the original game running in PPSSPP.

Launches ULUS10142 in /Applications/PPSSPPSDL.app, plays the new-game intro
through the debugger's virtual pad, then records:

  * screenshots of the game window (needs the macOS Screen Recording permission)
  * about 60 s of window video
  * RAM traces (clock, player position candidates) and CPU tick counts
  * an outdoor walk/run test: per-direction speed in raw units per 30 Hz update, for walk and
    for the run button (circle), as the slope of position against emulated time

Everything is written to OUTDIR (default assets/hm-baseline, which is git-ignored).
Nothing here needs the Accessibility permission: input goes through the PPSSPP
debugger WebSocket (input.buttons.press), not through synthetic key events.

Usage:
  capture.py --iso /path/to/game.iso --state assets/hm-baseline/farm.ppst
                                                # unattended: load the farm-outdoors
                                                # savestate at launch, capture, quit
  capture.py --iso /path/to/game.iso            # no state: boot the title, take CONTINUE
                                                # if a save exists, else NEW GAME
  capture.py --attach                           # reuse a running instance that is
                                                # already outdoors with control
Options: --out DIR, --video-seconds N, --skip-video, --keep (leave PPSSPP running)

Whatever the entry path, the script then makes sure the player has control (a cutscene or
dialogue is cleared with a bounded cross loop) before measuring.  PPSSPP is killed at the end
unless --keep is given.

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
EYE = 0x08950418             # s32 x3: camera eye (world units)
POS = 0x08950424             # s32 x3: player position (verified by walking, see docs/baseline.md)
TICKS_PER_S = 222e6          # PSP CPU clock: emulated seconds = ticks / this
HOLD_S = 1.0                 # seconds each held-direction measurement lasts


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
        """Player position (x, y, z), s32 each."""
        return struct.unpack("<iii", self.d.read_mem(POS, 12))

    def eye(self):
        return struct.unpack("<iii", self.d.read_mem(EYE, 12))

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
        # screencapture cannot write onto the external volume: temp dir, then move.
        tmp = os.path.join(tempfile.gettempdir(), "hm_shot_" + os.path.basename(name))
        subprocess.run([os.path.join(HERE, "winshot.sh"), tmp])
        if os.path.exists(tmp):
            shutil.move(tmp, os.path.join(self.out, name))

    def hold(self, buttons, seconds):
        """Hold PSP buttons for `seconds` of wall time (the pad drops a hold unless refreshed
        about every 30 ms).  Returns (emulated_seconds, player_xyz) samples."""
        btn = {k: True for k in buttons}
        rows = []
        end = time.time() + seconds
        while time.time() < end:
            self.d.call("input.buttons.send", buttons=btn)
            rows.append((self.ticks() / TICKS_PER_S, self.pos()))
            time.sleep(0.02)
        self.d.call("input.buttons.send", buttons={k: False for k in btn})
        time.sleep(0.6)
        return rows

    def has_control(self):
        """True when a short d-pad hold moves the player (tries both directions: walls)."""
        for btn in ("right", "left"):
            a = self.pos()
            self.hold([btn], 0.4)
            if self.pos() != a:
                return True
        return False

    def ensure_control(self, rounds=60):
        """Clear a cutscene or dialogue with a bounded cross loop until the player moves."""
        for i in range(rounds):
            if self.has_control():
                log("player has control (after %d cross presses)" % i)
                return
            self.press("cross", 1.0)
        raise RuntimeError("no player control after %d cross presses" % rounds)


def has_save():
    """True when PPSSPP's memory stick holds ULUS10142 save data (so CONTINUE works)."""
    for root in (os.path.expanduser("~/.config/ppsspp/PSP/SAVEDATA"),
                 os.path.expanduser("~/Library/Application Support/PPSSPP/PSP/SAVEDATA")):
        try:
            if any(n.startswith("ULUS10142") and os.listdir(os.path.join(root, n))
                   for n in os.listdir(root)):
                return True
        except OSError:
            pass
    return False


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
    if has_save():
        # CONTINUE is the default cursor row and works when a save exists: take it and let
        # ensure_control() clear the confirmations and the load fade.
        log("save data found: CONTINUE")
        s.press("cross", 4)
        s.shot("continue.png")
        s.ensure_control(rounds=40)
        return
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
    s.ensure_control()


def slope(ts, vs):
    n = len(ts)
    mt, mv = sum(ts) / n, sum(vs) / n
    den = sum((x - mt) ** 2 for x in ts)
    return sum((x - mt) * (v - mv) for x, v in zip(ts, vs)) / den if den else 0.0


def walk_test(s, rows):
    """Outdoor speed: hold each direction, walk then run (circle), and take the slope of the
    player position against emulated time.  Directions alternate so the player stays in the
    yard; a hold that cannot move (wall, menu) is logged with speed 0, not hidden."""
    s.hold(["right", "circle"], 1.5)               # leave the wall/barn edge for open yard
    for run in (False, True):
        for btn in ("down", "up", "left", "right"):
            keys = [btn] + (["circle"] if run else [])
            r = s.hold(keys, HOLD_S)
            r = r[len(r) // 5:]                    # drop the lead-in
            ax = 0 if btn in ("left", "right") else 2
            # A wall can stop the player part way through a hold, so a whole-hold slope would
            # understate the speed.  Use the 75th percentile of |slope| over sliding windows.
            w = 6
            win = sorted(abs(slope([e for e, _ in r[i:i + w]], [p[ax] for _, p in r[i:i + w]]))
                         for i in range(0, max(1, len(r) - w)))
            per_s = win[int(len(win) * 0.75)] * (1 if btn in ("right", "up") else -1)
            per_update = per_s / 30.0              # logic runs at 30 Hz
            rows.append([btn, "run" if run else "walk", len(r), round(per_s, 1), round(per_update, 2),
                         *r[0][1], *r[-1][1]])
            log("%-5s %-4s %7.1f units/s = %6.2f per 30 Hz update" % (btn, "run" if run else "walk", per_s, per_update))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--iso")
    ap.add_argument("--attach", action="store_true")
    ap.add_argument("--state", help="savestate to load at launch; skips the new-game key sequence")
    ap.add_argument("--keep", action="store_true", help="leave PPSSPP running at the end")
    ap.add_argument("--out", default=os.path.join(HERE, "..", "..", "assets", "hm-baseline"))
    ap.add_argument("--video-seconds", type=int, default=60)
    ap.add_argument("--skip-video", action="store_true")
    a = ap.parse_args()
    out = os.path.abspath(a.out)
    os.makedirs(out, exist_ok=True)

    if not a.attach:
        if not a.iso:
            ap.error("--iso is required unless --attach is given")
        subprocess.run([os.path.join(HERE, "launch.sh"), a.iso, out] + ([a.state] if a.state else []), check=True)
    s = Session(out) if a.attach else None
    if not a.attach:
        time.sleep(1)
        s = Session(out)
        if a.state:
            # wait until the state is live: the in-game clock advances (state load is async)
            deadline = time.time() + 120
            while True:
                if time.time() > deadline:
                    raise RuntimeError("savestate did not come up (clock not advancing)")
                try:
                    c0 = s.clock()
                    time.sleep(2)
                    if any(c0) and s.clock() != c0:
                        break
                except Exception:
                    time.sleep(2)
                    try:
                        s.reconnect()
                    except Exception:
                        pass
            log("state loaded, clock %s" % s.clock())
            s.ensure_control()
        else:
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
                    "x", "y", "z", "eye_x", "eye_y", "eye_z"])
        start = time.time()
        while time.time() - start < a.video_seconds:
            w.writerow([round(time.time() - start, 3), s.ticks(), *s.clock(), *s.pos(), *s.eye()])
            time.sleep(1.0)
    if vid:
        vid.wait()
        if os.path.exists(tmpmov):
            shutil.move(tmpmov, os.path.join(out, "baseline60.mov"))

    rows = []
    walk_test(s, rows)
    with open(os.path.join(out, "walk.csv"), "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["direction", "gait", "samples", "units_per_s", "units_per_update",
                    "x0", "y0", "z0", "x1", "y1", "z1"])
        w.writerows(rows)
    s.shot("after_walk.png")
    json.dump({"cpu_ticks_per_wall_second": tick_rate}, open(os.path.join(out, "summary.json"), "w"))
    log("done; outputs in %s" % out)
    if not a.keep and not a.attach:
        subprocess.run(["pkill", "-f", "PPSSPPSDL.app/Contents/MacOS/PPSSPPSDL"])


if __name__ == "__main__":
    main()
