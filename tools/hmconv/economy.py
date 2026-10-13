"""Farming economy rules decoded from the hmpsp.prx ELF image.

Reads the item table (seed/produce rows) and the crop table, and records the structural
constants found in the player-init and fatigue code. Output goes to the git-ignored
assets/hm-converted/; nothing extracted is committed. File offset = vaddr + ELF_BIAS.

Note: tables.py uses a base 0x20 bytes too high. This module uses the corrected base.
"""
import json
import struct

ELF_BIAS = 0x74
ITEM_VADDR = 0x1275A8
ITEM_STRIDE = 0x28
ITEM_ROWS = 376
CROP_VADDR = 0x127448
CROP_STRIDE = 16
CROP_N = 22

KIND_SEED = 0x4040
KIND_PRODUCE = 0x9000
KIND_TOOL = 0x8000

STARTING_GOLD = 500
START_STAMINA = 100
START_MAX_STAMINA = 100
STAMINA_CAP = 255
FATIGUE_MAX = 100
# Pass-out sleep: (min fatigue, hours slept). Day rolls over at 06:00.
PASSOUT_HOURS = [(80, 6), (70, 4), (50, 2)]
SEASON_NAMES = ["spring", "summer", "autumn", "winter"]


def item_rows(data, vaddr=ITEM_VADDR, n=ITEM_ROWS):
    off = vaddr + ELF_BIAS
    rows = []
    for i in range(n):
        p = off + i * ITEM_STRIDE
        if p + ITEM_STRIDE > len(data):
            break
        rows.append(struct.unpack_from("<20H", data, p))
    return rows


def parse_crops(data, vaddr=CROP_VADDR, n=CROP_N):
    """Crop table rows of 8 shorts: season, stages, regrow stage, 4 stage-day values, produce row."""
    off = vaddr + ELF_BIAS
    out = []
    for i in range(n):
        p = off + i * CROP_STRIDE
        if p + CROP_STRIDE > len(data):
            break
        v = struct.unpack_from("<8H", data, p)
        stages = v[1]
        out.append({"seed_index": i, "season": v[0], "stages": stages, "regrow_stage": v[2],
                    "stage_days": list(v[3:7]), "grow_days": grow_days(v[3:7], stages),
                    "produce_row": v[7]})
    return out


def grow_days(stage_days, stages):
    return sum(stage_days[:stages])


def seed_prices(rows):
    """seed index -> buy price, from seed rows (kind 0x4040, low byte of field 1 == 2)."""
    out = {}
    for r in rows:
        if r[0] == KIND_SEED and (r[1] & 0xFF) == 2:
            out[r[1] >> 8] = r[8]
    return out


def sell_price(rows, produce_row):
    if 0 <= produce_row < len(rows) and rows[produce_row][0] == KIND_PRODUCE:
        return rows[produce_row][9]
    return None


def passout_hours(fatigue):
    for floor, hours in PASSOUT_HOURS:
        if fatigue >= floor:
            return hours
    return 0


def crop_survives(crop_season, current_season):
    """Daily-update rule: a crop dies when the season no longer matches its own."""
    return crop_season == current_season


def constants():
    return {"starting_gold": STARTING_GOLD, "start_stamina": START_STAMINA,
            "start_max_stamina": START_MAX_STAMINA, "stamina_cap": STAMINA_CAP,
            "fatigue_max": FATIGUE_MAX, "passout_hours": PASSOUT_HOURS}


def crops_with_prices(data):
    rows = item_rows(data)
    buy = seed_prices(rows)
    out = []
    for c in parse_crops(data):
        c = dict(c)
        c["seed_price"] = buy.get(c["seed_index"])
        c["sell_price"] = sell_price(rows, c["produce_row"])
        out.append(c)
    return out


def convert(in_path, out_path):
    with open(in_path, "rb") as fh:
        data = fh.read()
    with open(out_path, "w") as fh:
        json.dump({"constants": constants(), "crops": crops_with_prices(data)}, fh, indent=1)
