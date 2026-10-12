# Container formats (Phase 2/3 first pass)

Scope: how game data is packed on the disc. Verified against the retail PSP image by `tools/hmconv/unpack_container.py`.
All integers are little-endian. Sector size is 0x800 (2048).

Confidence key: HIGH = parsed end to end with consistency checks, MEDIUM = structure seen, semantics guessed, LOW = hypothesis.

## Files and roles

| File | Size | Role |
|---|---:|---|
| `SYSDIR/EBOOT.BIN` | 988816 | `~PSP` wrapper (header 0x150 + encrypted body, entropy 8.0) of the main module |
| `SYSDIR/BOOT.BIN` | 988477 | The same main module in the clear: PRX (ELF type 0xFFA0, MIPS, 53 section headers, module name `PceEmu`) |
| `USRDIR/hmpsp.prx`, `hmGirl.prx` | ~1.66 MB each | Plain PRX game modules (boy / girl version), same ELF type |
| `USRDIR/BOY_FILE.BIN` + `.HDT`, `GIRL_FILE.BIN` + `.HDT` | 134 MB each | Segmented data banks (below) |
| `USRDIR/DATA000.BIN` + `DATA001.BIN` | 864 B / 24.6 MB | Sound-effect archive: TOC + payload (below) |
| `audiocodec.prx`, `libatrac3plus.prx`, `sc_sascore.prx` | small | `~PSP` encrypted Sony system modules (not game code) |

EBOOT: no cryptanalysis needed. `BOOT.BIN` is the decrypted equivalent and loads as a plain ELF; copies are in `assets/hm-raw/eboot/` (git-ignored). Ghidra load not yet attempted.

## HDT segment table (BOY_FILE / GIRL_FILE) - HIGH

`*.HDT` is an array of `u32` byte offsets into the matching `.BIN`:

```
u32 offset[N+1];   // offset[0] = 0, strictly ascending, all multiples of 0x800
                   // offset[N] == size of the .BIN file (end sentinel)
```

Segment `i` spans `[offset[i], offset[i+1])`. BOY has 33 offsets (32 segments), GIRL has 36 (35 segments). Both end exactly at the file size. The HDT carries no names and no types; the game executable references segments by index, and the executable also contains file-name strings such as `objcg\gf.bin`, `obj\GF_FARM.bin`, which suggests each segment is a packed bank of named logical files whose sub-index lives elsewhere (in the segment itself or in the PRX). Sub-segment layout is NOT yet resolved.

Segments 0 to 9 are large (64 MB, 1.6 MB, 15 MB, ...) and the rest are small (2 KB to 75 KB). BOY and GIRL share the same segment order and kinds, only sizes differ.

First-guess segment kinds (heads only; see `classification.md`):

- `0x100D` stream (segments 0, 1, 6+4, 7): starts `0d 10 <u16 n> 20 00 00 06 ...` or follows a `u32 0x68` prefix (segment 6). Body looks like a compact bitstream/RLE of 16-bit pixels; likely compressed bitmaps. LOW.
- Sparse tables (segments 2 to 5): first word `ff ff ff ff`, then roughly 53 percent zero bytes. LOW.
- Pointer tables: `u32 count` then `u32` values of the form `0x800bXXXX` (runtime RAM addresses, relocated at load). These precede data blobs. MEDIUM.
- `res_0e` tables: `u32 0x0e`, six `u16`, then `u32` offsets starting at 0x24. MEDIUM.
- One genuine TIM-style texture head (segment 16: `10 00 00 00 08 00 00 00 2c 00 00 00 40 01 00 00`, 4-bit with CLUT). MEDIUM.
- Segment 9 begins with the bytes `70 42 41 56`, the ASCII `pBAV` (a byte-swapped `VAGp`): sound data, 6 MB. MEDIUM.
- Segment 28/29 (BOY) and 12 contain MIPS prologues (`e0 ff bd 27`, addiu sp,sp,-32): code overlays or scripts. LOW.
- Text tables: a `u32` count followed by printable strings (debug and memory-card messages, English). MEDIUM.

## DATA000 / DATA001 sound archive - HIGH

`DATA000.BIN` is the table of contents (864 bytes) for `DATA001.BIN`:

```
offset 0x00: u32 header_size = 0x20
        0x04: u32 value = 1        (number of payload files)
        0x08: u32 n_entries = 52
        0x0C: u32 reserved = 0
        0x10: char[16] payload name "DATA001.BIN"
offset 0x20: n_entries * { u32 a; u32 b; u32 offset; u32 size }   // a and b are 0 in all 52 records
```

(864 - 32) / 16 = 52, matching `n_entries`. Offsets are sector aligned (0x800), ascending, and `offset + size` stays inside the 24,602,624-byte payload. Entry 0 is a `VAGp` file (1.7 MB, name field `ALLSE1_00_00.wav`). Entries 1 to 51 are RIFF/WAVE with format tag 0xFFFE (WAVE_FORMAT_EXTENSIBLE) carrying ATRAC3plus (Sony ATRAC3+ GUID, 2 channels, 44100 Hz, block align 0x178). They can be played or converted with ffmpeg or `at3tool` once extracted.

## Extractor

```
python3 tools/hmconv/unpack_container.py hdt  BOY_FILE.BIN BOY_FILE.HDT -o OUT
python3 tools/hmconv/unpack_container.py toc  DATA001.BIN  DATA000.BIN  -o OUT
```

Writes `segNNN.<ext>` or `entNNN.<ext>` plus `manifest.json` (offset, size, SHA-1, type tag). Tests: `python3 -m unittest discover -s tools/hmconv/tests` (synthetic fixtures only).

## Open questions

1. Sub-index inside the HDT segments (how `objcg\gf.bin` and friends map to byte ranges). Trace file-open and segment-load calls in `BOOT.BIN` / `hmpsp.prx` in Ghidra.
2. The `0x100D` compression scheme (candidate: custom RLE plus delta; header fields `n`, 0x0020, 0x0600).
3. Whether the sparse `ff ff ff ff` segments are maps, collision grids or animation banks.
