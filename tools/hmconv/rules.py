"""Calendar and clock rules decoded from the hmpsp.prx ELF image.

Everything here is structural knowledge (see docs/formats/event.md). Only the festival
table is read from the image; clock constants come from reading the tick function and
are recorded as plain constants. Output is JSON for the git-ignored assets/hm-converted/.
File offset = vaddr + ELF_BIAS.
"""
import json
import struct

ELF_BIAS = 0x74
FESTIVAL_VADDR = 0x13ED1C
FESTIVAL_N = 18
FESTIVAL_SCANNED = 14  # the lookup routine only walks the first 14 entries

SEASONS = 4
DAYS_PER_SEASON = 30
WEEKDAYS = 7
YEAR_CAP = 0x62
DAY_START_HOUR = 6      # the day counter advances when the clock reaches 06:00
HOURS_PER_DAY = 24
NEW_GAME = {"year": 0, "season": 0, "day_index": 1, "weekday": 1, "hour": 6, "minute": 0}
# One in-game minute elapses every (period*30)//60 frame ticks, period = 30 by default.
TICK_PERIOD = 30


def s16(v):
    return v - 0x10000 if v >= 0x8000 else v


def parse_festivals(data, vaddr=FESTIVAL_VADDR, n=FESTIVAL_N):
    """Rows of three shorts: season, day_index (0-based), event id (-1 = none)."""
    off = vaddr + ELF_BIAS
    out = []
    for i in range(n):
        p = off + i * 6
        if p + 6 > len(data):
            break
        season, day, ev = struct.unpack_from("<3h", data, p)
        out.append({"index": i, "season": season, "day": day + 1, "event": ev,
                    "scanned": i < FESTIVAL_SCANNED})
    return out


def minute_period_ticks(period=TICK_PERIOD):
    return (period * 30) // 60


def clock():
    return {"seasons": SEASONS, "days_per_season": DAYS_PER_SEASON, "weekdays": WEEKDAYS,
            "year_cap": YEAR_CAP, "day_start_hour": DAY_START_HOUR,
            "hours_per_day": HOURS_PER_DAY, "new_game": NEW_GAME,
            "minute_period_ticks": minute_period_ticks()}


def convert(in_path, out_path):
    with open(in_path, "rb") as fh:
        data = fh.read()
    out = {"clock": clock(), "festivals": parse_festivals(data)}
    with open(out_path, "w") as fh:
        json.dump(out, fh, indent=1)
