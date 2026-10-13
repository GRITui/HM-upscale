# Event ids, conditions and game state (partial)

Status: calendar/clock, map-script interpreter, dialog linkage decoded; about 15 rarely used opcodes, MesEv linkage and a standalone NPC schedule table not found.
Addresses are ELF vaddrs (file offset = vaddr + 0x74); Ghidra names are FUN_/DAT_ labels.

## Game state globals
| label | meaning |
|---|---|
| DAT_001ae1a0 | year, 0..0x62 |
| DAT_001ae1a2 | season 0..3 |
| DAT_001ae1a4 | day index 0..29 (display = index + 1) |
| DAT_001ae1a6 | weekday 0..6 |
| DAT_001ae1a8 / 1aa | hour 0..23 / minute 0..59 |

Clock tick FUN_000934c8: minute rolls into hour, hour 24 wraps to 0, day/weekday advance when the
clock reaches 06:00. Day 30 rolls the season, season 4 rolls the year. New-game init FUN_00093b08
sets year 0, season 0, day index 1, weekday 1, 06:00.
Event flags set from the date: 0x0b-0x11 weekday, 0x17-0x1a season, 0x78-0x95 day of month.
Flag helpers: FUN_00007aa8 get(id), FUN_00007af4 set(id, value).

## Festival table (vaddr 0x13ed1c)
18 rows of 3 shorts: season, day index, event id (-1 none). Looked up by FUN_0009335c and
FUN_00093c2c (returns row + 1). Only the first 14 rows are scanned. Parsed by tools/hmconv/rules.py.

## Event condition predicate
FUN_00054354: switch over about 115 event ids returning 1 when the event may fire. Inputs are NPC
affection values (compared with thresholds 20000..60000 depending on year), NPC location, flags
and date. It gates the trigger; the queued id is a map id whose single script then runs (see
event-vm.md, "Region event id -> script").

## Map-script commands (interpreter FUN_000596e0)
A script block (map.py block kind `script`) is an array of 16-byte commands, copied to RAM (max 206).
Layout: `u8 op, u8 on_done, u8 on_wait, u8 yield (bit 0), i16 p1, i16 p2, i32 p3, i32 p4`.
A handler returns 1 to jump to index `on_done`, 0 to jump to `on_wait`; 0xff means "next command".
`yield` ends the frame after the command. Wait idiom: `on_wait` = own index with `yield` = 1.
Op byte 0xff ends the script. Handler table at vaddr 0x1379b0 (ops 0..112). Script variables:
int[20] (DAT_0016af50), event flags via FUN_00007aa8/7af4.

Compare modes (conditions): 0 >=, 1 >, 2 <=, 3 <, 4 ==, 5 or more always true.
Arith modes: 1 add, 2 sub, 3 mul, 4 div, 5 mod, anything else assigns.

Field kinds (ops 13/14/17/19/20/22/87/88): 0 gold, 10 hour, 11 weekday, 12 year, 13 season,
14 day, 15 map, 21..23 player x/y/z, 26 minute, 27..39 per-actor fields (34 position, 35 motion,
36 anim, 37 flags, bit 0x800 = hidden; stride 0x1d4 from 0x1b3730). Kinds 0x31..0x31F are event
flag (kind - 0x30). Accessor table at ELF 0x1383a0 (20-byte entries). Full list: script.py.

### Opcode table (name, p1..p4, confidence)
| op | name | params | confidence |
|---|---|---|---|
| 0 | move_axis | actor, dir(0 +z,1 +x,2 -z,3 -x,-1 +y,-2 -y), target, speed | high |
| 1 | move_anim | actor, dir, target, anim | high |
| 2 | dialog | channel(0-4), entry, result_var(-1 none), entry_is_var | high |
| 3 | frame_timer | frames | med |
| 4 | set_pos | actor, y, x, z | high |
| 5 | anim | actor, anim, param, immediate | high |
| 6 | set_hidden | actor, hide(0 hides), actor_is_var, - | high |
| 7 | actor_map | actor, map(-1 current), store_var(-1 none), reset | med |
| 8 | set_next_location | target, season_relative, flag_a, flag_b | med |
| 9 | camera_move | speed_sel, y, x, z | med |
| 10 | camera_release | flags | low |
| 11 | sleep_trigger | - | low |
| 12 | face_player | actor, actor_is_var | med |
| 13 | cond_field | kind, index, cmp_mode, value | high |
| 14 | cond_field_var | kind, index, cmp_mode, value_var | high |
| 15 | cond_var | var, -, cmp_mode, value | high |
| 16 | cond_var_var | var, var2, cmp_mode, - | high |
| 17 | cond_range | kind, index, min, max | high |
| 18 | op18 | unread | unknown |
| 19 | set_field | kind, index, value, arith_mode | high |
| 20 | set_field_var | kind, index, value_var, arith_mode | high |
| 21 | var_set | var, value_is_var, value, arith_mode | high |
| 22 | var_from_field | kind, index, var, arith_mode | high |
| 23 | wait_transition | -, -, start, - | med |
| 24 | bgm_play | track, also_sfx, mode(1 start,-1 stop), - | med |
| 25 | bgm_default | - | low |
| 26 | bgm_fade | frames, phase(0 start,1 wait,2 force), -, - | med |
| 27 | screen_fade | variant, duration, query_done, speed | med |
| 28 | input_lock | 0 sets lock, else clears | med |
| 29 | item_count | item, bag, count, remove_flag | med |
| 30 | item_count2 | item, bag, count, remove_flag | med |
| 31 | item_give | item, -, -, - | med |
| 32 | item_give_notify | item, -, -, - | med |
| 33 | name_input_wait | - | low |
| 34 | near_player | dist_x, dist_z, x, z | high |
| 35 | actor_reset | actor(42 = all npcs, 45 = flag) | med |
| 36 | time_passed | hour, minute, var_index_bits, - | high |
| 37 | script_end | variant, -, flag, cleanup_bits | med |
| 38 | random | range, store_var, cmp_mode, cmp_value | med |
| 39 | op39 | unread | unknown |
| 40 | animal_state | id, state, -, - | low |
| 41 | animal_state4 | id | low |
| 42 | screen_tint | -, r, g, b | med |
| 43 | actor_effect | actor, effect | med |
| 44 | lookup_store | id, store_var | low |
| 45 | large_routine | unclear | low |
| 46 | inventory_stash | 0 saves, else restores | med |
| 47 | print_number | -, slot, value, value_is_var | med |
| 48 | spawn_set | - | low |
| 49 | spawn_one | - | low |
| 50 | event_flag_helper | flag_or_var, -, -, - | low |
| 51 | camera_follow | actor, actor_is_var, move_camera, speed | med |
| 52 | slot_set | slot, y, x, z | med |
| 53 | slot_flag | slot, set(0 clears) | med |
| 54 | slot_offset | slot, actor, dir, absolute | low |
| 55 | slot_to_var | slot, store_var | med |
| 56 | check_mode3 | - | low |
| 57 | npc_pref_var | pref_index, store_var, index_is_var | low |
| 58 | item_pair_to_var | store_var(-1 none), use_alt_pair | low |
| 59 | check_state | value(-1 resets) | low |
| 60 | set_pos_vars | actor, y_var, x_var, z_var | high |
| 61 | set_player_pos | mode(0 imm,1 vars,2 reset), y, x, z | high |
| 62 | camera_scroll | dir_bits, axis, target, var_bits | low |
| 63 | emote | mode, anim, id, param | low |
| 64 | set_face | face, actor | med |
| 65 | gift_event | value, value_is_var, param | low |
| 66 | event_flag | mode(0 set, else get), value, flag, - | high |
| 67 | npc_unlocked | npc, npc_is_var | low |
| 68 | call_vars | var, var, var, var | low |
| 69 | save_bits | mode(0 test,1 set,else clear), wide, mask, - | low |
| 70 | sfx | id | med |
| 71 | actor_clear_flags | actor(42 all), mode | med |
| 72 | camera_scroll_vars | a, var, var, var | low |
| 73 | actor_lookup | actor, actor2 | unknown |
| 74 | time_to_vars | mode, var, var, var | low |
| 75 | pending_check | mode, index, store_var | low |
| 76 | wait_until_time | hour, minute, absolute, bits(1,2 = vars) | high |
| 77 | fade_wait | mode, id, flag | low |
| 78 | actor_init | actor | med |
| 79 | script_index_to_var | var | med |
| 80 | script_index_from_var | var | med |
| 81 | big_switch | case, sub, a, b | low |
| 82 | move_run | actor, dir, target, anim | med |
| 83 | actor_stop_face | actor, anim, param, mode | low |
| 84 | flag_triplet | 0/1/other | med |
| 85 | table_lookup | var, var | low |
| 86 | scatter | actor, var, -, var | low |
| 87 | cond_field_by_var | kind, index_var, cmp_mode, value | high |
| 88 | set_field_by_var | kind, index_var | med |
| 89 | op89 | unread | unknown |
| 90 | party_choreo | var | low |
| 91 | set_field_all | kind, -, value, value_alt | med |
| 92 | lookup_a | type, index, flag, var_flag | low |
| 93 | call3 | -, a, var, var | low |
| 94 | actor_cell | actor, actor_is_var, x_var, z_var | high |
| 95 | op95 | unread | unknown |
| 96 | big_switch2 | var, sub | low |
| 97 | set_field_visible | kind, -, value, value_alt | med |
| 98 | menu_result | id, flag, store_var | low |
| 99 | mode_wait | mode, - | low |
| 100 | register_pair | id, value, value_is_var | low |
| 101 | pick_candidates | mode, - | low |
| 102 | npc_interact | var, var, var | low |
| 103 | face_actor | actor, flags(1,2 vars), -, actor2 | med |
| 104 | op104 | unread | unknown |
| 105 | cond_flag_gate | value, is_var | low |
| 106 | op106 | unread | unknown |
| 107 | minigame_step | mode, var | low |
| 108 | show_face | actor, var | low |
| 109 | projectile | var, var, -, flag | low |
| 110 | rank_query | mode, - | low |
| 111 | rank_message | var, var, var | low |
| 112 | coord_table | var, var, var | low |

Ops marked `unknown` were not read. Names with `low` are inferred from partial reading.

## Dialog linkage and speaker
Op 2 (FUN_00059f7c): p1 channel 0..4, p2 entry (var[p2] when p4 != 0), p3 result var (-1 none).
Channel selects message segment {2,3,4,5,6} = messege0..4 (short table at vaddr 0xef38c); the
entry is read at `entry << 13` (see text.md). The speaker is the face id in the entry's leading
i32. Special remap: when mode DAT_001b81b0 == 2 and channel 0, entries 694,706,729,779,925,982,
1002,1791,735 become 0x731 + index.

## NPC schedules
No standalone schedule table exists. Placement is script-driven: map scripts test the clock (ops
13/15/17 on kinds 10..14 and 26, op 36 time passed, op 76 wait-until-time) and then place actors
(ops 4, 60, 94). FUN_00054354 gates whether an event may fire by weekday, season, day and flags.
`time_gates` in the converted JSON lists every clock test per script.

## Converted JSON (schema 1)
`python3 tools/hmconv/script.py <hmpsp.prx.elf> <event_s.bin> assets/hm-converted/events` writes
`mapNNN.json`: `{"schema":1,"scripts":[{"block":int,"commands":[{i,op,name,done,wait,yield,p:[4],
field?,dialog?}],"time_gates":[{i,field,...}],"dialogs":[[channel,entry,entry_is_var]]}]}`.
Output is git-ignored; it contains game data.

## Unknown
MesEv (seg029) entry selection (op 2 does not use it), ops marked unknown, the large ops 45, 81,
85, 86, 90, 96, 102 (case tables), forced-sleep hour (hours 0-5 are a night band, FUN_0004853c).

Runtime interpreter for these scripts: `event-vm.md`.
