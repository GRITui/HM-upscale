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
| 0x0E00-0x0FFF (not 0x0FFD) | control codes (0x0FEC, 0x0FF3, 0x0FF4, 0x0FF5, 0x0FF7 common; 0x0E2F frequent) | semantics unknown |
| 0x62, 0x6C, 0x8C, 0x7F, 0x08 | frequent punctuation-like glyphs; 0x6C and 0x62 probably ',' and '!' | unverified |

Digits and remaining punctuation are not mapped; the decoder emits `<xxxx>`.
Glyph indices refer to Font_all.tex (seg007), so the full charset follows from the
font page layout once that is decoded.

## Not solved
- String ids: how a message id maps to (sector, string index). MesEv (seg029, the same
  0x12800 blob as gf.bin column 3) is the likely script that references them.
- Meaning of the 0x0Fxx control words (speaker, window, pause, choice, variables).
- Digits and punctuation mapping.
- Other languages and the kana/kanji range (English build only seen).

seg010, seg030, seg031 hold ASCII debug strings only, not dialogue.
