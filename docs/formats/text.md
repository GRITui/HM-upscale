# Text (messege0..4 segments) - PARTIAL

Source: BOY_FILE segments seg002..006 (messege0..4). English edition.
Tool: `tools/hmconv/text.py` (decoder + JSON dump; output must never be committed).

## Layout
- Segments are runs of 0x800-byte sectors. Image sectors (0x100D containers) are
  interleaved with text sectors.
- A text sector is a stream of little-endian u16 codes. It begins with a control word
  of the form 0x0Fxx and holds several strings, each terminated by 0xFFFF.
  seg002 yields 1798 text sectors.

## Codes
| code | meaning | confidence |
|------|---------|-----------|
| 0x0000-0x0019 | A-Z | high |
| 0x0020-0x0039 | a-z | high |
| 0x0FFD | space | high |
| 0xFFFE | line break | high |
| 0xFFFF | end of string | high |
| 0x0040-0x0049 | digits 0-9 (number printer FUN_00004680 emits code = digit + 0x40; leading zeros become 0x0FFD) | high |
| 0x0069 | minus sign (same routine) | high |
| 0x62, 0x63, 0x66, 0x6C, 0x8C | ? , : . ! (from context only; `guess=True`) | medium |
| 0x0E25-0x0E2F | colour/palette select, value = 0x0E2F - code (0..10) | medium |
| 0x0E38-0x0FEF | face id = 0x0FEF - code; takes effect at the next 0x0FF7 | high |
| 0x0FF7 | show/refresh the face portrait | high |
| 0x0FF6 / 0x0FF4 | clear / set state flag 0x58 | medium (purpose unknown) |
| 0x0FF5, 0x0FF3, 0x0FF1 | set state flags 0x4C, 0x68, 0x7C | medium (purpose unknown) |
| 0x0FFC | wait for key press | medium |
| 0x0FF8-0x0FFB, 0x0FF2 | ignored by the glyph renderer (pause/timing handled elsewhere) | low |
| 0x4000 \| n | inline substitution slot n (n = code & 0xFFF), resolved by FUN_00004868 | high |
| other < 0xE00 | glyph, see rendering | high |

Rendering (FUN_00004dfc / FUN_00004c88): every glyph below 0x0E00 is one 8x12 cell of a
128-pixel-wide font page, column = code & 0xF, row = code >> 4 (so 16 per row, 12 px per
row). The pen advances by a fixed 8 px plus a global spacing value; a line break adds
12 px plus a global leading value. The text font is therefore a separate 8x12
monospace page, not the 12x12 Japanese Font_all grid (that one has no lowercase Latin).
The page itself is not yet located.

Substitution slots (0x4000|n), from the switch in FUN_00004868: 0x0A-0x0E fixed name
buffers (0x0A is the player name, medium), 0x0F conditional on a mode flag, 0x10-0x15 and
0x21 4-digit numbers (gold, date, etc., value source differs per slot), 0x16/0x17 NPC
record names by index (0x5C-byte stride), 0x18 pointer variable, 0x19/0x1A and 0x22 first
matching record with a flag bit, 0x1B-0x20 six formatted values. A code with 0x4000 set
but unhandled falls back to a table of nested message pointers (DAT_0014b91c).

## String ids (solved for dialogue)
messege0..4 (seg002-seg006) are arrays of fixed 0x2000-byte entries:
`i32 face_id` (-1 none, else block index into face.bin), a copy of that face block
(0x17FC bytes), then one 0x800 text sector at entry offset 0x1800. The string id is
(segment, entry index). Verified: for seg002 and seg005 and seg006 every entry's face id
equals the face decoded from the leading control run; seg003/seg004 differ in 4 of ~2600
entries (unscripted override). `text.parse_entries()` implements this. Entries with
text: seg002 1798, seg003 1957, seg004 631, seg005 1387, seg006 56. Strings contain
0xFFFE line breaks, one string per entry.

## Not solved
- The English 8x12 font page location.
- How MesEv (seg029, gf.bin column 3) selects an entry index (op 2 uses messege0..4 only, see event.md; MesEv linkage still unknown).
- Remaining glyph codes 0x1B-0x1E, 0x6D-0x6F, 0x71, 0x82-0x8A (icons/symbols), and the
  apostrophe ligatures 0x50-0x56.
- Other languages and the kana/kanji range (English build only seen).

seg010, seg030, seg031 hold ASCII debug strings only, not dialogue.
