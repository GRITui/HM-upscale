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
# Tool stamina table: 5 rows (tool slot (slot-1)//5) x 5 shorts, indexed by charge level
# (button-held frames // 20); vaddr 0x13760A, read in FUN_0004966c, applied by FUN_000517f8.
TOOL_COST_VADDR = 0x13760A
TOOL_COST_ROWS = 5
TOOL_COST_COLS = 5
CHARGE_TILES_VADDR = 0x137600   # tiles worked per charge level: 1, 3, 6, 9, 25
HOURLY_FATIGUE_VADDR = 0x1376E8  # (hour, extra fatigue) pairs, FUN_0004d978
FESTIVAL_VADDR = 0x13ED1C
FESTIVAL_ROWS = 18
SLEEP_STAMINA_BASE, SLEEP_STAMINA_PER_HOUR, SLEEP_STAMINA_CAP = 50, 7, 127
SLEEP_FATIGUE_BASE, SLEEP_FATIGUE_PER_HOUR, SLEEP_FATIGUE_FLOOR = 5, 2, -128

# Seed index -> wiki crop name. Every spring/summer/fall row was matched on seed price, sell
# price and days to grow against the wiki. 19 (red Magic Flower) and 21 are by elimination.
SEED_NAMES = ["Turnip", "Potato", "Cucumber", "Strawberry", "Cabbage", "Tomato", "Corn", "Onion",
              "Pumpkin", "Pineapple", "Eggplant", "Carrot", "Sweet Potato", "Spinach",
              "Green Pepper", "Grass", "Moondrop Flower", "Pink Cat Flower",
              "Magic Flower (blue)", "Magic Flower (red)", "Toy Flower", "Unknown (8 d, 1000 G)"]
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


def tool_costs(data):
    """Stamina cost table: rows of 5 charge levels, values stored negative, returned positive."""
    off = TOOL_COST_VADDR + ELF_BIAS
    return [[-v for v in struct.unpack_from("<5h", data, off + r * 10)]
            for r in range(TOOL_COST_ROWS)]


def tool_stamina_cost(data, tool_slot, held_frames=0):
    """Cost of one tool use. Row = (slot-1)//5, column = min(held_frames//20, 4)."""
    row = max(0, min(TOOL_COST_ROWS - 1, (tool_slot - 1) // 5))
    col = max(0, min(TOOL_COST_COLS - 1, held_frames // 20))
    return tool_costs(data)[row][col]


def regrow_days(crop):
    """Days between harvests of a renewable crop, or None. Sum of the stage-day values from the
    regrow stage to the last stage, plus one (the wiki counts the harvest day as day 0). Matches
    the wiki's 3/4/6/9 for all nine renewable crops."""
    r = crop["regrow_stage"]
    if r >= crop["stages"]:
        return None
    return sum(crop["stage_days"][r:crop["stages"]]) + 1


def festivals(data):
    """Festival table rows (season 0-3, day 0-based, event id or -1). Day is index, wiki day-1."""
    off = FESTIVAL_VADDR + ELF_BIAS
    return [struct.unpack_from("<3h", data, off + 6 * i) for i in range(FESTIVAL_ROWS)]


def sleep_recovery(hours):
    """Normal sleep: (stamina gained, fatigue change) for `hours` slept (FUN_0004d72c/d630/d65c)."""
    st = min(SLEEP_STAMINA_CAP, hours * SLEEP_STAMINA_PER_HOUR + SLEEP_STAMINA_BASE)
    fa = max(SLEEP_FATIGUE_FLOOR, -(hours * SLEEP_FATIGUE_PER_HOUR + SLEEP_FATIGUE_BASE))
    return st, fa


def action_fatigue(base, hour, stamina, night_flag=False, half=False, immune=False):
    """Fatigue per action (FUN_0004853c)."""
    if night_flag:
        base += 1
    if 22 <= hour <= 23:
        base += 1
    if 0 <= hour < 6:
        base += 2
    if stamina == 0:
        base += 2
    if half:
        base //= 2   # C int division truncates toward zero; base >= 0 here
    return 0 if immune else base


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
