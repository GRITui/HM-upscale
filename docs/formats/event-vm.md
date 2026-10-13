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
  70, 71, 76, 78, 87, 88, 91, 94, 97, 103, plus the game-logic ops below. Everything else goes through `Unimpl`: counted in
  `EventState.Unhandled`, reported to `IEventHost.Unimplemented`, logged in `ScriptVM.Log`, and
  treated as done. Nothing crashes on unknown ops.

## Region event id -> script (resolved, medium-high confidence)
Every converted map carries exactly one resident script (loaded by FUN_00058da0 into the RAM image,
run from command 0, polled every frame). The event-trigger table (0x24-byte entries, walked by
FUN_00053ee8) checks time, place, flags and FUN_00054354, then queues `id & 0x3ff` through
FUN_0003b890 and sets flag `(id & 0x3ff) + 0xfb` as "seen". That id is a MAP id: the game loads
that map's script and runs it from command 0. Op 50 queues an event the same way (`queue_event`).
The JSON `block` field is a block index inside the map file, so the old `block == eventId` default
was a coincidence. `EventRunner.EventMapLoader(id)` returns the target map's `EventFile`; the default
resolver takes `scripts[0]` of a single-script file, and only falls back to the legacy block/index
match for multi-script files (tests).

## Game-logic ops (decoded from the interpreter handlers)
Ops with engine-only effects (camera, emote, actor motion, scroll, BGM, ui wait, big-switch cases
without a pure-state effect) call `IEventHost.Engine(op, p1..p4, begin, state)`; returning false
keeps the op waiting. `EngineId` names the values read through `IEventHost.EngineValue`. Pure-state
ops run natively on `EventState.Bank` (mode, player, scratch, engine words), `Pairs` (op 100),
`Pending` (op 11) and flags.
- 56 mode==3 check; 69 save_bits (test/set/clear mask on the 32-bit mode word, or the 16-bit player
  word when p4 != 0); 73 actor_pos_to_vars; 74 event_pos_to_vars; 100 register_pair (5 slots, -1 empty);
  104 actor_move_rel (offsets doubled); 84 sets flags 4, 5, 0xb0; 105 gate on flags 0x129 / 0x25.
- 10/11 camera_release and sleep_trigger; 40 animal state tests; 54 slot_offset; 57/58 item
  preference and item pair to var; 59 state compare; 99 mode wait.
- 79 stores the return address (next command index, or the on_wait byte if set) into a variable; 80
  rewrites its own on_done from a variable (computed jump). The VM keeps the override in a per-run
  table and never mutates the shared command array. The earlier "script index" reading was wrong.
- 81 big switch: cases 6, 7, 9, 0xb, 0x12, 0x13, 0x16, 0x17, 0x19, 0x1b are native; the rest go
  to the host. Still unknown in detail: the engine-side effect of 8, 25, 62, 63, 72, 77, 82, 83 and
  most of 81, which are delegated rather than emulated.

## Assumptions (not confirmed by the decode)
- Frame timer, clock and dialogue assumptions below remain unconfirmed.
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
