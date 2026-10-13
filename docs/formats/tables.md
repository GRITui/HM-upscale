# Data tables (items, seeds, prices)

Status round 4: item/shop/seed price table located and parsed (`tools/hmconv/tables.py`).
Crops growth days, calendar, affection and stamina tables are NOT located.

## Item/shop table
In `hmpsp.prx.elf` at file offset 0x12763C (Ghidra address = offset - 0x74): 331 rows of
0x28 bytes, each 20 little-endian u16 fields.

| field | meaning | confidence |
|---|---|---|
| f0 | signed small delta (stamina/fatigue?) | low |
| f4 | kind word: 0x4000 item/tool, 0x4040 seed, 0x8000/0x9000/0x2000/0x0200 other classes | medium |
| f5 | low byte class (2 = seed); for seeds high byte = seed index (skips 0x0F, 0x13) | high |
| f11 | item id (-1 none) | medium |
| f12 | buy price | high |
| f13 | sell / value | medium |

Extractable: 20 seed buy prices (rows 33-53), about 8 shop item prices (rows 25-32),
about 100 sell/value entries (rows 64-159), plus large-price rows (3000-20000) that are
likely livestock/machines/house items (unlabelled).

An unidentified 20-byte-row id table (ids 0x41-0x55) sits at 0x127434-0x1275EC.

## Not solved
Crop growth days, calendar/festival dates, affection values, stamina costs. No code
references to the table have been traced (the program is not analysed in Ghidra).

## Correction and crop table (economy.py)

The item table actually starts at file offset 0x12761C (vaddr 0x1275A8), 376 rows of 20
little-endian shorts; the 0x12763C base above is 0x20 bytes high. Kind in short 0: 0x4040
seed (short 1 low byte 2, high byte = seed index; short 8 = buy price), 0x9000 produce
(short 9 = sell price), 0x8000 tool. The crop table is at vaddr 0x127448, 22 rows of 8 shorts
indexed by seed index: season, stage count, regrow stage, four stage-day values, produce row.
Growth days = sum of the first `stage count` stage-day values. tools/hmconv/tables.py is left
unchanged (its tests pin the old base); use economy.py for correct values.
