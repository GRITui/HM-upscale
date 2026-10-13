# Face portraits (face.bin)

- Source: `BOY_FILE.BIN` segment 1 (`seg001.bin`); same for the girl file. The segment is a run of
  0x1800-byte blocks, each a 0x100D image (CLUT group + RLE or stored chunk).
- 274 blocks decode (boy), 80x80 px, 8bpp indexed with a 256-entry RGB555 CLUT; word 0 transparent.
- Tool: `tools/hmconv/face.py`, `export(data, out_dir, side)` writes `face_NNN.png` and
  `faces_index.json` (id, file, width, height, failed blocks). Output is git-ignored under
  `assets/hm-converted/faces/<side>/`.
- Unknown: which character or expression each id maps to (referenced by dialog commands, see
  `text.md` and `event.md`).
