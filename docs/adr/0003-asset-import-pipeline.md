# ADR 0003: Asset import pipeline

Status: accepted

## Context
Original game data cannot be committed. Converters under `tools/` read the user's own copy of the game and produce open formats.

## Decision
- Converters output glTF (models), PNG (textures), OGG (audio), and JSON (tables, strings) into a folder that is git-ignored (`assets/` at the repo root, or any path the user chooses).
- Unity never reads original game files. Editor importers in `HM.Importers` read only the converted output.
- The converted folder location is a per-machine setting kept in `EditorPrefs` (`HM/Importers/Set Converted Assets Folder...`), never committed.
- Importers copy or reference converted files into a git-ignored area of the Unity project, so `Assets/` content committed to git stays limited to code, scenes, and original placeholder art.
- Format contracts between converters and importers live in `docs/formats/`.

## Consequences
- A fresh clone builds and runs with placeholder art only.
- Importer code can be tested against small synthetic fixtures committed to the repo.
