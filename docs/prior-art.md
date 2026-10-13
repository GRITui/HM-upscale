# Prior art

Searched 2026-10-13 for public reverse-engineering work on this game (the PSP Harvest Moon
title in this project) before doing our own.

## Findings

- No public format documentation for its archives, image containers, scene buffers, sprite
  resources or event bytecode was found.
- No public decompilation or reimplementation project for this PSP title was found.
- Community material is limited to player-facing wikis: item, crop, festival and character
  data. These are useful as a cross-check of decoded tables, not as a source of formats.

## How we use it

- Wiki values (festival dates, regrow days, prices) validate our decoded tables. The wiki
  describes a different platform version, so a disagreement is recorded as a finding in
  `docs/rules/`, and neither side is silently corrected.
- Generic PSP knowledge applies: GU pixel formats, swizzled textures, ATRAC3plus and VAG
  audio. Tooling used for these: ffmpeg for audio, Ghidra for MIPS analysis.
- All format specs under `docs/formats/` are derived from our own analysis of a user-owned
  copy and are written in our own words.

## Re-check

Repeat the search when a spec stalls. Add any hit here with its date and what it covered.
