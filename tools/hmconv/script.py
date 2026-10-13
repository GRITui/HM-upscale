#!/usr/bin/env python3
"""Map-script (event) command decoder. See docs/formats/event.md.

A script block (map.py block kind "script") is an array of 16-byte commands:
  u8 op, u8 on_done, u8 on_wait, u8 yield_flag, i16 p1, i16 p2, i32 p3, i32 p4.
The interpreter (FUN_000596e0) runs one command at a time. A handler returning
1 jumps to index on_done (0xff = next); returning 0 jumps to on_wait (0xff = next).
yield_flag bit 0 ends the frame after the command. A command with on_wait equal to
its own index and yield_flag 1 is the wait-until-done idiom. Op byte 0xff ends.

Pure data plus a parser; no game data is stored here.
Usage: script.py <hmpsp.prx.elf> <event_s.bin> <out_dir>
"""
import json, os, struct, sys

NEXT = 0xFF
CMD = 16

# op -> (name, params, confidence). params: p1,p2,p3,p4 meaning, "-" unused.
# confidence: high = handler fully read, med = read with some branches unclear,
# low = purpose inferred from partial reading.
OPS = {
    0: ("move_axis", "actor, dir(0 +z,1 +x,2 -z,3 -x,-1 +y,-2 -y), target, speed", "high"),
    1: ("move_anim", "actor, dir, target, anim", "high"),
    2: ("dialog", "channel(0-4), entry, result_var(-1 none), entry_is_var", "high"),
    3: ("frame_timer", "frames", "med"),
    4: ("set_pos", "actor, y, x, z", "high"),
    5: ("anim", "actor, anim, param, immediate", "high"),
    6: ("set_hidden", "actor, hide(0 hides), actor_is_var, -", "high"),
    7: ("actor_map", "actor, map(-1 current), store_var(-1 none), reset", "med"),
    8: ("camera_mode", "flags", "low"),
    9: ("camera_move", "speed_sel, y, x, z", "med"),
    10: ("camera_release", "flags", "low"),
    11: ("sleep_trigger", "-", "low"),
    12: ("face_player", "actor, actor_is_var", "med"),
    13: ("cond_field", "kind, index, cmp_mode, value", "high"),
    14: ("cond_field_var", "kind, index, cmp_mode, value_var", "high"),
    15: ("cond_var", "var, -, cmp_mode, value", "high"),
    16: ("cond_var_var", "var, var2, cmp_mode, -", "high"),
    17: ("cond_range", "kind, index, min, max", "high"),
    18: ("op18", "unread", "unknown"),
    19: ("set_field", "kind, index, value, arith_mode", "high"),
    20: ("set_field_var", "kind, index, value_var, arith_mode", "high"),
    21: ("var_set", "var, value_is_var, value, arith_mode", "high"),
    22: ("var_from_field", "kind, index, var, arith_mode", "high"),
    23: ("wait_transition", "-, -, start, -", "med"),
    24: ("bgm_play", "track, also_sfx, mode(1 start,-1 stop), -", "med"),
    25: ("bgm_default", "-", "low"),
    26: ("bgm_fade", "frames, phase(0 start,1 wait,2 force), -, -", "med"),
    27: ("screen_fade", "variant, duration, query_done, speed", "med"),
    28: ("input_lock", "0 sets lock, else clears", "med"),
    29: ("item_count", "item, bag, count, remove_flag", "med"),
    30: ("item_count2", "item, bag, count, remove_flag", "med"),
    31: ("item_give", "item, -, -, -", "med"),
    32: ("item_give_notify", "item, -, -, -", "med"),
    33: ("name_input_wait", "-", "low"),
    34: ("near_player", "dist_x, dist_z, x, z", "high"),
    35: ("actor_reset", "actor(42 = all npcs, 45 = flag)", "med"),
    36: ("time_passed", "hour, minute, var_index_bits, -", "high"),
    37: ("script_end", "variant, -, flag, cleanup_bits", "med"),
    38: ("random", "range, store_var, cmp_mode, cmp_value", "med"),
    39: ("op39", "unread", "unknown"),
    40: ("animal_state", "id, state, -, -", "low"),
    41: ("animal_state4", "id", "low"),
    42: ("screen_tint", "-, r, g, b", "med"),
    43: ("actor_effect", "actor, effect", "med"),
    44: ("lookup_store", "id, store_var", "low"),
    45: ("large_routine", "unclear", "low"),
    46: ("inventory_stash", "0 saves, else restores", "med"),
    47: ("print_number", "-, slot, value, value_is_var", "med"),
    48: ("spawn_set", "-", "low"),
    49: ("spawn_one", "-", "low"),
    50: ("event_flag_helper", "flag_or_var, -, -, -", "low"),
    51: ("camera_follow", "actor, actor_is_var, move_camera, speed", "med"),
    52: ("slot_set", "slot, y, x, z", "med"),
    53: ("slot_flag", "slot, set(0 clears)", "med"),
    54: ("slot_offset", "slot, actor, dir, absolute", "low"),
    55: ("slot_to_var", "slot, store_var", "med"),
    56: ("check_mode3", "-", "low"),
    57: ("npc_pref_var", "pref_index, store_var, index_is_var", "low"),
    58: ("item_pair_to_var", "store_var(-1 none), use_alt_pair", "low"),
    59: ("check_state", "value(-1 resets)", "low"),
    60: ("set_pos_vars", "actor, y_var, x_var, z_var", "high"),
    61: ("set_player_pos", "mode(0 imm,1 vars,2 reset), y, x, z", "high"),
    62: ("camera_scroll", "dir_bits, axis, target, var_bits", "low"),
    63: ("emote", "mode, anim, id, param", "low"),
    64: ("set_face", "face, actor", "med"),
    65: ("gift_event", "value, value_is_var, param", "low"),
    66: ("event_flag", "mode(0 set, else get), value, flag, -", "high"),
    67: ("npc_unlocked", "npc, npc_is_var", "low"),
    68: ("call_vars", "var, var, var, var", "low"),
    69: ("save_bits", "mode(0 test,1 set,else clear), wide, mask, -", "low"),
    70: ("sfx", "id", "med"),
    71: ("actor_clear_flags", "actor(42 all), mode", "med"),
    72: ("call_vars2", "a, var, var, var", "low"),
    73: ("actor_lookup", "actor, actor2", "unknown"),
    74: ("time_to_vars", "mode, var, var, var", "low"),
    75: ("pending_check", "mode, index, store_var", "low"),
    76: ("wait_until_time", "hour, minute, absolute, bits(1,2 = vars)", "high"),
    77: ("ui_wait", "mode, id, flag", "low"),
    78: ("actor_init", "actor", "med"),
    79: ("script_index_to_var", "var", "med"),
    80: ("script_index_from_var", "var", "med"),
    81: ("big_switch", "case, sub, a, b", "low"),
    82: ("dialog_helper", "a, b, c, d", "unknown"),
    83: ("actor_move_dir", "actor, anim, param, mode", "low"),
    84: ("flag_triplet", "0/1/other", "med"),
    85: ("table_lookup", "var, var", "low"),
    86: ("scatter", "actor, var, -, var", "low"),
    87: ("cond_field_by_var", "kind, index_var, cmp_mode, value", "high"),
    88: ("set_field_by_var", "kind, index_var", "med"),
    89: ("op89", "unread", "unknown"),
    90: ("party_choreo", "var", "low"),
    91: ("set_field_all", "kind, -, value, value_alt", "med"),
    92: ("lookup_a", "type, index, flag, var_flag", "low"),
    93: ("call3", "-, a, var, var", "low"),
    94: ("actor_cell", "actor, actor_is_var, x_var, z_var", "high"),
    95: ("op95", "unread", "unknown"),
    96: ("big_switch2", "var, sub", "low"),
    97: ("set_field_visible", "kind, -, value, value_alt", "med"),
    98: ("menu_result", "id, flag, store_var", "low"),
    99: ("mode_wait", "mode, -", "low"),
    100: ("register_pair", "id, value, value_is_var", "low"),
    101: ("pick_candidates", "mode, -", "low"),
    102: ("npc_interact", "var, var, var", "low"),
    103: ("face_actor", "actor, flags(1,2 vars), -, actor2", "med"),
    104: ("op104", "unread", "unknown"),
    105: ("cond_flag_gate", "value, is_var", "low"),
    106: ("op106", "unread", "unknown"),
    107: ("minigame_step", "mode, var", "low"),
    108: ("show_face", "actor, var", "low"),
    109: ("projectile", "var, var, -, flag", "low"),
    110: ("rank_query", "mode, -", "low"),
    111: ("rank_message", "var, var, var", "low"),
    112: ("coord_table", "var, var, var", "low"),
}

# Condition compare modes (value op cmp). 5 or more is always true.
CMP = {0: ">=", 1: ">", 2: "<=", 3: "<", 4: "=="}
# Arithmetic modes for set/var ops; any other value assigns.
ARITH = {1: "add", 2: "sub", 3: "mul", 4: "div", 5: "mod"}

# Field kinds for ops 13/14/17/19/20/22/87/88. 0x31..0x31F is event flag (kind - 0x30).
FIELD_KINDS = {
    0: "gold", 1: "stat_a", 2: "stat_b", 3: "player_cell_x", 4: "player_cell_z",
    5: "counter0", 6: "counter1", 7: "counter2", 8: "counter3", 9: "counter4",
    10: "hour", 11: "weekday", 12: "year", 13: "season", 14: "day", 15: "map",
    19: "mode", 20: "held_item", 21: "player_x", 22: "player_y", 23: "player_z",
    26: "minute", 34: "actor_pos", 35: "actor_motion", 36: "actor_anim",
    37: "actor_flags", 44: "actor_flag_0x400", 45: "actor_flag_0x200", 46: "actor_flag_0x10",
}
FLAG_FIRST, FLAG_LAST, FLAG_BIAS = 0x31, 0x31F, 0x30

# Dialog channel -> segment id of the message archive (messege0..4).
DIALOG_SEGMENTS = (2, 3, 4, 5, 6)
DIALOG_ENTRY_SHIFT = 13   # entry bytes = entry << 13 (text.ENTRY)
ACTOR_PLAYER, ACTOR_ALL_NPCS = 0x29, 0x2A

COND_OPS = frozenset((13, 14, 15, 16, 17, 87))
CLOCK_KINDS = {10: "hour", 11: "weekday", 12: "year", 13: "season", 14: "day", 26: "minute"}


def parse(d):
    """Return the command list of a script block (stops at op 0xff)."""
    out = []
    for i in range(0, len(d) - CMD + 1, CMD):
        op, b1, b2, b3, p1, p2, p3, p4 = struct.unpack_from("<4B2h2i", d, i)
        if op == 0xFF:
            break
        out.append({"i": i // CMD, "op": op, "done": b1, "wait": b2, "yield": b3 & 1,
                    "p": [p1, p2, p3, p4]})
    return out


def describe(c):
    name = OPS.get(c["op"], ("op%d" % c["op"], "", "unknown"))[0]
    d = dict(c, name=name)
    p = c["p"]
    if c["op"] == 2:
        d["dialog"] = {"channel": p[0] if 0 <= p[0] <= 4 else 0, "entry": p[1],
                       "entry_is_var": bool(p[3]), "result_var": p[2]}
    if c["op"] in (13, 14, 17, 19, 20, 22):
        k = p[0]
        d["field"] = ("flag_%d" % (k - FLAG_BIAS) if FLAG_FIRST <= k <= FLAG_LAST
                      else FIELD_KINDS.get(k, "kind_%d" % k))
    return d


def time_gates(cmds):
    """Clock conditions (hour/weekday/season/day) a script tests, with the command index."""
    g = []
    for c in cmds:
        if c["op"] in (13, 17) and c["p"][0] in CLOCK_KINDS:
            g.append({"i": c["i"], "field": CLOCK_KINDS[c["p"][0]], "op": c["op"],
                      "cmp": c["p"][2], "value": c["p"][3]})
        elif c["op"] == 36:
            g.append({"i": c["i"], "field": "time", "hour": c["p"][0], "minute": c["p"][1]})
    return g


def convert_map(m):
    """m: map.parse_map result. Returns schema-v1 JSON dict."""
    scripts = []
    for b in m["blocks"]:
        if b["kind"] != "script":
            continue
        cmds = parse(b["data"])
        scripts.append({"block": b["index"], "commands": [describe(c) for c in cmds],
                        "time_gates": time_gates(cmds),
                        "dialogs": [c["p"][:2] + [c["p"][3]] for c in cmds if c["op"] == 2]})
    return {"schema": 1, "scripts": scripts}


def main(argv):
    import map as hmmap  # tools/hmconv/map.py
    elf = open(argv[1], "rb").read()
    seg = open(argv[2], "rb").read()
    os.makedirs(argv[3], exist_ok=True)
    t = hmmap.index(elf)
    n = 0
    for i in range(len(t) - 1):
        if t[i + 1] <= t[i]:
            continue
        try:
            m = hmmap.parse_map(seg[t[i]:t[i + 1]])
        except Exception:
            continue
        j = convert_map(m)
        if not j["scripts"]:
            continue
        with open(os.path.join(argv[3], "map%03d.json" % i), "w") as f:
            json.dump(j, f, separators=(",", ":"))
        n += 1
    print("wrote %d script files" % n)


if __name__ == "__main__":
    main(sys.argv)
