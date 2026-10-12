# ADR 0001: Engine and render pipeline

Status: accepted

## Context
This is a personal, non-commercial Unity remake of a PSP farming game. The target is Apple Silicon Macs (M1 and newer) at 60 fps. No original game data is stored in the repository; all committed art is original placeholder content.

## Decision
- Unity 6000.0.83f1, project in `unity/`.
- Universal Render Pipeline (URP). HDRP is rejected unless a spike shows it holds 60 fps on M1.
- Input System as the only active input backend (`activeInputHandler: 1`); actions are created in code for now.
- Cinemachine 3 for the third-person camera, Addressables for per-area content, Timeline for cutscenes, Localization for extracted string tables, Test Framework for EditMode/PlayMode tests.
- Standalone macOS, arm64. 60 fps via `Application.targetFrameRate` with vsync off, set in `GameStartup`.
- Code is split into assembly definitions: `HM.Core` (clock, calendar, save), `HM.World`, `HM.Characters`, `HM.UI`, `HM.Importers` (editor only), `HM.Tests.EditMode`. Dependencies point towards `HM.Core`; `HM.Core` depends on nothing in the project.
- Time, calendar and save logic is plain C# so it can be tested without entering play mode.

## Consequences
- Project settings and scenes are reproducible with `HM.Importers.ProjectSetup.Run` (menu `HM/Setup`).
- A performance budget document is still required (draw calls, triangles, texture memory) before the phase closes.
