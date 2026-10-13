# Event ids, conditions and game state (partial)

Status: calendar/clock decoded; event opcode interpreter, MesEv linkage and NPC schedules NOT yet decoded.
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
and date. The region event id in scene headers (docs/formats/map.md, scene.py) indexes this.

## Unknown
Map-script opcode semantics (16-byte commands), the MesEv entry selection, NPC schedule tables,
forced-sleep hour (hours 0-5 are treated as a night band by several systems, e.g. FUN_0004853c).
