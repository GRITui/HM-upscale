# 0x100D image container (textures, portraits, font)

Status: container layout SOLVED for face.bin and Font_all.tex (high); gf.bin (seg000)
NOT solved.

## Layout (u16 little endian, own description)

Header, 16 bytes:

| off | field |
|-----|-------|
| 0 | magic 0x100D |
| 2 | n, chunk count (seg007: 3, last is an empty terminator) |
| 4 | clut_bytes (32 = 16 colours; 0x200 = 256) |
| 6 | 0x0600, unknown |
| 8,10 | CLUT VRAM x, y (PS1 VRAM position) |
| 12 | ncolors |
| 14 | 1, unknown |

Then ncolors RGB555 words (PS1 order: R in the low 5 bits; 0x0000 = transparent), then
chunks, each: u16 size (payload bytes), u16 flag, PS1 RECT x,y,w,h (u16; w counted in
16-bit VRAM words, so 8bpp pixel width = 2w, 4bpp = 4w), then size payload bytes.
Payload is RLE (compression.md) or stored.

## Files

- face.bin (seg001): 274 fixed 0x1800-byte blocks. Each block is a header, a 256-colour
  CLUT at VRAM (0,490), and one chunk RECT (960,175,40,80): an 80x80 8bpp image, flag
  0x0100, then zero padding. Decoded and viewed: dialogue character portraits
  (several expressions per character). Best candidate for a vertical slice.
- Font_all.tex (seg007): 16-colour CLUT, chunk 1 stored at (960,0,63,252), chunk 2
  RLE at (896,0,63,252); 4bpp glyph sheets, 252 x 252 pixels each. Glyph cell layout
  is NOT yet worked out.
- gf.bin (seg000): SOLVED container. Images use the same 0x100D header but the header
  count n is items = chunks + group headers. A group header (6 u16: clut_bytes, 0x0600,
  clut_x, clut_y, ncolors, 1) starts a new CLUT followed by ncolors u16; chunks after it
  belong to that group. Items are 2-byte aligned (1 pad byte after odd payloads); a u16 0
  is skipped. Example: image at 0xb9800 has n=166 = 101 chunks + 65 groups. The image ends
  at the next sector. gf.bin itself is indexed by a 261x4 u32 table (see gf.py): column 1
  is the image, columns 0/2/3 are scene data (unknown structs), plus a tail of res_0e
  sprite tables (sprite.md). Parser: decompress.parse_image (returns "groups").

## Unknown

The 0x0600 and trailing 1 header fields, the exact meaning of flag values 0x0000,
0x0020, 0x0100, 0x0400.
