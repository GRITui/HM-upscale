# hmconv

Converts extracted HM PSP assets into engine-ready files. Stdlib only (ffmpeg optional, for audio).

## Usage

    python -m hmconv unpack <bin> <outdir>      # BOY_FILE/GIRL_FILE/DATA001 container -> segNNN.bin / entries
    python -m hmconv image  <in> <out.png>      # 0x100D image container -> PNG (indexed, tRNS)
    python -m hmconv audio  <in> <out.ogg|wav>  # VAG or ATRAC3plus -> ogg (wav if no encoder)
    python -m hmconv sprite|map|text|tables <in> <out>   # hooks, see below
    python -m hmconv batch [--raw assets/hm-raw] [--output DIR] [--force] [--keep-going]

`batch` unpacks into `<raw>/extracted_entries`, converts faces, fonts, BGM tracks and the SE bank
into `<raw>/converted/`, and writes `manifest.json` plus `batch_report.json`.

## Manifest (schema 1)

Mirrors `unity/Assets/_HM/Importers/AssetManifest.cs`:
`{"schema":1,"assets":[{"id","type","path","tags","license"}]}`.
`id` matches `^[a-z][a-z0-9_]{0,63}$` and is unique; `type` is texture (.png), audio (.ogg) or data (.json);
`path` is relative to the manifest. `license` defaults to `user-owned-copy`. `manifest.validate` checks all
of this and that files exist.

## Placeholder hooks

`sprite`, `map`, `text`, `tables` import `hmconv.<name>` if present and call `convert(in_path, out_path)`.
Optionally `batch_convert(entries_root, out_dir)` returns a list of manifest entry dicts, which `batch` merges.
Missing module: prints "not implemented yet", exit code 2.

## Audio fallback chain

ATRAC3plus: ffmpeg native `atrac3plus` -> `vgmstream-cli` -> `at3tool` (wav only).
VAG: decoded in pure Python to WAV, then ffmpeg encodes ogg.
Ogg encoder: libvorbis if available, otherwise ffmpeg native `vorbis` (`-strict -2 -ac 2 -b:a 160k`;
native encoder is stereo only, so mono is upmixed).

## Tests

    python -m unittest discover -s tools/hmconv/tests
