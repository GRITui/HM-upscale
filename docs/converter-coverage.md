# Converter coverage

Output of the first full `python -m hmconv batch` run (0 errors, 714 manifest assets, 64 MB).

| Asset type | Status | Converted / total | Notes |
|---|---|---|---|
| Boy portraits (face.bin) | done | 274 / 274 | PNG, transparent CLUT 0 |
| Girl portraits | done | 281 / 281 | segment numbering assumed same as boy |
| Fonts (boy, girl) | done | 4 / 4 | 2 per side |
| BGM tracks (DATA001 1-51 ATRAC3plus) | done | 51 / 51 | ogg via ffmpeg |
| DATA001 entry 0 (VAG bank) | done | 103 / 103 | split and decoded, ogg |
| DATA001 audio total | done | 52 / 52 entries | 51 tracks + SE bank |
| gf.bin graphics (64 MB) | partial | images parse (group-aware) | col 0 (3D scenes) decoded and exported to GLB; cols 2,3 undecoded |
| Sprites (res_0e) | blocked | 0 / unknown | format partial |
| Maps | blocked | 0 / unknown | format partial |
| Text and tables | blocked | 0 / unknown | format partial |
| sound.stm ('pBAV' stream) | blocked | 0 / 1 | not converted |
| Slp.Bin (TIM) | blocked | 0 / 1 | not converted |
