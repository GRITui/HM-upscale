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
- gf.bin (seg000): same magic and a 16-colour CLUT at (896,251), but the chunk list
  does not follow the simple layout after the second chunk (sizes become nonsense), and
  the file is 64 MB. Probably a nested container. Unsolved.

## Unknown

The 0x0600 and trailing 1 header fields, the exact meaning of flag values 0x0000,
0x0020, 0x0100, 0x0400.
