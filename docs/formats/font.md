# Fonts

Two font resources are decoded. Tool: `tools/hmconv/font.py` (`export(seg000, font_all, out_dir)`).

## 8x12 text page (decoded)

- Source: `BOY_FILE.BIN` segment 0 (`seg000.bin`), a 0x100D image. One chunk rect (896,0,32,120)
  words in the 1024x512 VRAM: 128x120 px at 4bpp (low nibble first), word 0 transparent.
- Layout: 16 columns x 10 rows of 8x12 glyphs. For a text code below 0xE00 (see `text.md`):
  column = `code & 0xF`, pixel x = column * 8, row = `code >> 4`, pixel y = row * 12 (verified
  visually for the Latin, digit and punctuation rows; the last rows hold symbols).
- Colour variants: the CLUT rows at VRAM (896, 250..254), 16 words each. The text engine picks a
  row with the table `[254,250,250,252,253,251]` indexed by text colour. The exporter writes one
  PNG per row (`font8_clut<row>.png`).
- Four 24x24 icon chunks follow at VRAM x = 928, 934, 940, 946 (6 words wide, 24 rows), written as
  `icon_<n>.png` with the first colour row.

## Font_all.tex (partially decoded)

- Source: `seg007.bin`, a 0x100D image with two 252x252 4bpp chunks. These are dense kanji-style
  glyph sheets; the exporter writes `font_all_<n>.png` and slices them on a 21x21 grid of 12x12
  cells (441 per sheet, 882 total).
- Unknown: the code to cell mapping, and the draw routine for 12x12 glyphs (Ghidra found no
  xrefs for the string `Font_all`; files are reached by table index). The 21x21 grid is a
  hypothesis from the sheet size, not confirmed by the drawing code.

Output index: `font_index.json` next to the PNGs (git-ignored `assets/hm-converted/font/<side>/`).
