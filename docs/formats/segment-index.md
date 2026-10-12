# Segment index (names and roles of HDT segments)

Status: name table SOLVED (high confidence). Per-segment internal layouts: see the
last section; most are NOT solved.

## Finding

The HDT files (see container.md) only hold byte offsets. The segment NAMES live in the
game executable `hmpsp.prx`, as a static table (PRX vaddr 0xeee30, file offset
vaddr + 0x74 in the extracted ELF). There is no generic index inside the segments:
the game treats each segment as a PS1 CD "file" and each file type has its own layout,
interpreted by game code or by an overlay.

## Name table layout (own description)

33 entries, stride 0x28 bytes:

| off  | type      | meaning |
|------|-----------|---------|
| 0x00 | u32       | kind flag: 1 = data file (objcg\*, font, sound), 0 = code overlay (obj\*) |
| 0x04 | char[32]  | original path, NUL padded |
| 0x24 | u32       | runtime byte offset, 0 in the file; filled at boot from the HDT |

At boot (FUN_00007df8) the game opens BOY_FILE.BIN, reads BOY_FILE.HDT and copies the
first 33 offsets into entry+0x24. Segment N therefore equals table entry N. Entry 32
("NOTHING") is the end sentinel. Segment size = off[N+1] - off[N] (rounded up to 4 by
FUN_00007d9c). Reads use sector units (offset >> 11) through a PS1 libcd style API.

The GIRL HDT has 36 offsets, so GIRL_FILE uses another table (probably in the girl
PRX). Unverified; names for it are not recorded.

## BOY_FILE segment names

| idx | name | flag |
|-----|------|------|
| 0 | objcg\gf.bin | 1 |
| 1 | objcg\face.bin | 1 |
| 2-6 | objcg\messege0..4.bin | 1 |
| 7 | data\font\Font_all.tex | 1 |
| 8 | obj\DEBUG.bin | 0 |
| 9 | objcg\sound.stm | 1 |
| 10 | obj\PdaSamp.bin | 0 |
| 11 | obj\GF_FARM.bin | 0 |
| 12 | objcg\event_s.bin | 1 |
| 13 | objcg\evch.bin | 1 |
| 14 | ObjCg\Status.Bin | 1 |
| 15 | Obj\saveload.bin | 0 |
| 16 | ObjCg\Slp.Bin | 1 |
| 17,19,21,23,25 | Obj\GF_MG1..5.Bin | 0 |
| 18,20,22,24,26 | ObjCg\MG1..5.Bin | 1 |
| 27 | Obj\GF_TITLE.Bin | 0 |
| 28 | Obj\GF_STAFF.Bin | 0 |
| 29 | ObjCg\MesEv.Bin | 1 |
| 30 | Obj\GF_swind.bin | 0 |
| 31 | Obj\gf_mcard.bin | 0 |
| 32 | NOTHING | sentinel |

The same list is embedded as `BOY_SEGMENT_NAMES` in tools/hmconv/unpack_container.py
(`--boy-names`).

## Observed internal layouts (empirical)

- Overlays (flag 0): u32 count, a table of 0x800bXXXX pointers, then MIPS code. The
  0x800b load address is PS1 style.
- ObjCg\Status/MG*.Bin (flag 1): begin with u32 0x0e, followed by a 14-entry offset
  table ("res_0e", guessed name).
- Font_all.tex, gf.bin, face.bin, messege*: start with the 0x100D image header, see
  compression.md.
- Slp.Bin: a TIM-like header (`10000000 08000000 2c000000 ...`).
- seg002-005 (messege0..3): sparse 0xFF tables, structure not examined.
- sound.stm: VAG audio.
