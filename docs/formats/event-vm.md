# Event script VM (HM.Events)

Runtime interpreter for the map scripts decoded in `event.md`. Code: `unity/Assets/_HM/Events/`
(asmdef `HM.Events`, tests in `Events/Tests`). The VM is pure C# (no UnityEngine in the VM, model,
state or host interface), deterministic, and talks to the world only through `IEventHost`.
No game text or data is in the repo; converted JSON is read from a git-ignored local path.

## Pieces
| File | Role |
|---|---|
| `EventModel.cs` | `ScriptCommand`, `EventScript`, `EventFile` (JsonUtility mirror of schema 1; `yield` is `@yield`) |
| `EventState.cs` | 20 script vars, event flags (`Flags[0x400]`), 64 slots of 3, per-opcode unhandled counters |
| `IEventHost.cs`, `EventHostBase.cs` | World effects: fields, dialog, actors, camera, audio, items, clock, RNG. Base class = no-op defaults, waits complete at once |
| `ScriptVM.cs` | `Tick()` runs commands until one yields, the script ends, or 1024 steps are spent |
| `OpInfo.cs` | Opcode names and confidence, generated from `tools/hmconv/script.py` |
| `EventLoader.cs` | Reads `mapNNN.json`; never throws; `Available` is false when the data is absent |
| `EventRunner.cs` | Region trigger -> script; `CanFire` gate, optional queue while busy, start/finish events |
| `DialogueBridge.cs` | Op 2 -> existing `HM.Dialogue.DialogueRunner` by message id only (`IMessageCatalog.Find`) |

## Semantics implemented
- 16-byte commands; handler result 1 -> `on_done`, 0 -> `on_wait`, `0xff` = next; op `0xff` ends.
  Yield bit ends the frame after the pc moves. Wait idiom: `on_wait == own index`, `yield = 1`.
  A multi-frame op therefore advances the pc on the frame it completes, and the next command runs
  on the following tick.
- Compare modes 0 `>=`, 1 `>`, 2 `<=`, 3 `<`, 4 `==`, other = true. Arith modes 1 add, 2 sub,
  3 mul, 4 div, 5 mod, other = assign (div/mod by zero yields 0).
- Field kinds: `0x31..0x31F` event flag (`kind - 0x30`), 10 hour, 11 weekday, 12 year, 13 season,
  14 day, 15 map, 21..23 player x/y/z, 26 minute. Actor `0x29` player, `0x2A` all NPCs.
- Op 2 dialog: channel 0..4, entry = message id (or variable when `p4 != 0`), result variable `p3`.
- Implemented ops: 0-7, 9, 12-17, 19-24, 26-32, 34-38, 42, 43, 46, 47, 51-53, 55, 60, 61, 64, 66,
  70, 71, 76, 78, 79, 87, 88, 91, 94, 97, 103. Everything else goes through `Unimpl`: counted in
  `EventState.Unhandled`, reported to `IEventHost.Unimplemented`, logged in `ScriptVM.Log`, and
  treated as done. Nothing crashes on unknown ops.

## Assumptions (not confirmed by the decode)
- Region event id -> script: the mapping is undocumented. Default resolver picks the script whose
  `block == eventId`, else the script at that index. `EventRunner.Resolver` is pluggable.
- Frame timer (op 3) counts `p1` frames, then completes on the following frame.
- Op 76 with `p3 == 0` waits a relative duration from `TotalMinutes()` at first execution; with
  `p3 != 0` it waits for an absolute clock time.
- Dialogue choice results are the 0-based index of the last choice taken (0 when none).
- Missing message ids complete immediately and are recorded in `DialogueBridge.Missing`.

## Using it
```
var runner = new EventRunner(host);                    // host : IEventHost (derive EventHostBase)
if (EventLoader.TryLoadMap(mapId, out var file)) runner.SetMap(file);   // else triggers do nothing
runner.OnRegionEntered(eventId);                       // from the world's region trigger
runner.Tick();                                         // once per frame (or fixed step)
```
Data directory: `EventLoader.OverrideDir`, else env `HM_EVENTS_DIR`, else
`<repo>/assets/hm-converted/events`.

## Tests
`Events/Tests/ScriptVMTests.cs` (EditMode, synthetic scripts): arith/compare, branching, flags,
clock gates, wait idiom, yield, timers, dialog and result variable, unknown-op logging, step budget,
script end, determinism, runner triggers/queue, dialogue bridge, loader fallback and parsing. One
test runs every real converted script against a stub host when the data is present locally.
