# ADR 0002: Game logic is reimplemented, not ported

Status: accepted. Premise corrected by ADR 0006: the original scenes are 3D, so the remake rebuilds real geometry and layout from the extracted data; only game logic is reimplemented from observation.

## Context
The original game ships as compiled PSP code. Decompiling or translating that code is out of scope and would tie the project to the original binary.

## Decision
Gameplay logic (crop growth, shop prices, schedules, events, and so on) is reimplemented in C# from two sources only:
1. Observed behaviour of the original game, recorded as written rules and test cases.
2. Data tables extracted by the tools under `tools/` (items, crops, animals, NPCs, schedules, shops) and stored as JSON, then turned into ScriptableObjects.

Correctness is checked with rule-parity tests: each rule has a test that encodes the observed outcome (for example, "a crop planted on day N is harvestable on day N+k") and runs in EditMode against the reimplemented logic and the extracted tables.

## Consequences
- No original code is copied into the repository.
- A rule without an observed outcome or a data source is marked as unverified and gets a placeholder value.
- Logic stays in pure C# classes where possible so parity tests run quickly in batch mode.
