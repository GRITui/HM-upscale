# Converter coverage

Counts as of 2026-10-13, from the local, git-ignored outputs. Two output roots exist:
`assets/hm-raw/converted/` (written by `hmconv batch`, with `manifest.json` and
`batch_report.json`: 0 errors, 714 manifest assets) and `assets/hm-converted/` (written by the
scene, map, sheet and event exporters, read by the Unity farm slice).

| Asset type | Status | Converted / total | Notes |
|---|---|---|---|
| Boy portraits (face.bin) | done | 274 / 274 | PNG, transparent CLUT 0 |
| Girl portraits | done | 281 / 281 | numbering assumed same as boy (unverified) |
| Fonts (boy, girl) | done | 4 / 4 | 2 per side; code-to-cell mapping still open |
| BGM tracks (DATA001 1-51, ATRAC3plus) | done | 51 / 51 | ogg via ffmpeg |
| DATA001 entry 0 (VAG bank) | done | 103 / 103 | split and decoded, ogg |
| Scene meshes (gf.bin column 0) | done for farm rows | 16 / 16 farm rows (80..95) | GLB with collision; textured variant 16 / 16 |
| Maps | done for farm rows | 16 / 16 farm rows | map_080..map_095 JSON; town map ids not yet identified |
| Event scripts | done | 439 files | event bytecode to JSON (script.py) |
| Sprites (res_0e) | done | 21 / 22 blobs parse | 1569 files (sheets, JSON, index) |
| Actor sheets (gf tail) | done | 2588 files | player, animals, objects; girl sheet unidentified |
| Text | decoder done | no count kept | dump stays local, never committed |
| Tables (economy, calendar, crops) | done | parsed in code | see docs/rules and tables.md |
| gf.bin columns 2 and 3 | not started | 0 | object list and shared blob undecoded |
| sound.stm ('pBAV' stream) | waived for now | 0 / 1 | see waiver list |
| Slp.Bin (TIM-like) | waived for now | 0 / 1 | see waiver list |

## Waiver list (definition of "100 percent" for issue 6)

"100 percent of in-scope files converted or explicitly waived" means every file class above is
either converted, or appears here with a reason. Items on this list are out of scope until the
named trigger occurs.

| Item | Reason | Revisit when |
|---|---|---|
| glTF animations | The game uses sprite actors; no skeletons or skinned meshes were found (ADR 0006). Sprite animations are exported as JSON frames instead. | A skeletal model is found |
| Skinned character models | None found | As above |
| sound.stm ('pBAV') | Not part of the vertical slice; format unspecified | Phase 10 audio polish |
| Slp.Bin (TIM-like) | Not used by the slice | A screen needing it is built |
| gf.bin columns 2 and 3 | Object list and shared blob are not needed for the farm slice | Town maps are converted |
| Town and interior map rows (outside 80..95) | Slice is the farm only | Phase after the vertical slice |
| Girl-side sprite sheets | Girl sheet not identified | #5 identifies it |
| Texture tpage and CLUT bank mapping | Partial; farm scenes render acceptably | A scene shows wrong colours |
| Upscaled assets | Produced later from converted output | Upscale phase |

Any file class not in the table or in this list counts as a gap, not a waiver.
