# Classification of unpacked entries

Entries come from `unpack_container.py` (32 BOY segments, 35 GIRL segments, 52 DATA001 entries = 119). Types are decided by `tools/hmconv/classify.py` from the first 64 bytes only, so they are first guesses (see `container.md` for confidence). Counts:

| Tag | Meaning | BOY | GIRL | DATA001 | Total |
|---|---|---:|---:|---:|---:|
| audio_riff_extensible_at3p | RIFF WAVE, ATRAC3plus | 0 | 0 | 51 | 51 |
| audio_vag | `VAGp` | 0 | 0 | 1 | 1 |
| audio_vag_le | `pBAV` (VAG, byte-swapped magic) | 1 | 1 | 0 | 2 |
| texture_tim | TIM-style 4bpp+CLUT head | 1 | 1 | 0 | 2 |
| stream_100d | `0x100D` compressed stream (bitmap guess) | 3 | 3 | 0 | 6 |
| sparse_ff_table | `ff ff ff ff` then zeros | 4 | 4 | 0 | 8 |
| ptr_table | `u32 n` + `0x800bXXXX` pointers | 9 | 9 | 0 | 18 |
| res_0e | `u32 0x0e` header table | 6 | 9 | 0 | 15 |
| counted_table | `u32 n` + other data | 2 | 2 | 0 | 4 |
| text_table | `u32 n` + printable strings | 3 | 3 | 0 | 6 |
| unknown | no rule matched (models, maps, code overlays?) | 3 | 3 | 0 | 6 |
| Total | | 32 | 35 | 52 | 119 |

Not found at the top level: PNG, GIM, AT9, plain ELF (the PNG files on the disc are only `ICON0/PIC0/PIC1` metadata images). Models, animations, scripts and maps are expected inside the segments (sub-index unresolved), so these counts describe segments, not individual game files.

Next step: resolve the in-segment index, then re-classify sub-files.
