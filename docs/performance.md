# Performance budget (60 fps on Apple M1)

Target: 60 fps, which is 16.67 ms per frame. Vsync off in dev builds
(`GameStartup` sets target 60); frames above 1.05 x 16.67 ms count as over budget.

| Item | Budget |
|------|--------|
| Frame time (CPU and GPU, p95) | <= 16.6 ms; p99 <= 20 ms |
| Draw calls | <= 1500 per frame |
| SetPass calls | <= 150 |
| Triangles | <= 1.5 M visible |
| Texture memory | <= 1.5 GB (M1 unified memory is shared; leave headroom) |
| Individual texture | <= 2048 px, compressed (ASTC) |
| Shadows | one directional light, 2 cascades, 2048 map, 60 m distance, soft |
| Realtime lights | 1 main + at most 4 additional per object |
| GC allocations | zero per frame in steady state |

These are starting budgets, not measurements of shipped content. Revisit when
real converted areas exist.

## Measuring

`FrameTimeLogger` (HM.Core) is created automatically when the player is
launched with `-hm-perf`:

```
"HM Upscale" -hm-perf -hm-perf-seconds 20 -hm-perf-interval 5 -logFile perf.log
```

It prints `[HM-PERF]` lines with avg, p95, p99, max, over_budget, draw calls,
setpass, triangles and GPU name, then quits after `-hm-perf-seconds`. F3
toggles periodic logging at runtime. Draw call and triangle counters may read
zero in release builds.

Build: menu `HM/Build/macOS arm64 (development)` or
`-executeMethod HM.Importers.BuildScript.BuildMacDevelopment` (output
`unity/Build/`, git-ignored).

## Measured (slice scene, development build, M1, 1280x720, 20 s run)

Final window: 283 frames, avg 16.78 ms (59.6 fps), p95 17.57 ms, p99 17.59 ms,
max 22.40 ms, 17 frames over budget, 17 draw calls, 6 SetPass, about 3.9 k
triangles. The first frames include a multi-second shader warm-up spike
(max 3.4 s), so exclude the first interval when judging. This scene is
trivial; it proves the harness, not the budget.

## Capture mode (Phase 6)

`PerfCapture` (HM.Core) records a fixed window after a warmup that is
excluded from the statistics:

```
"HM Upscale" -hm-perf-capture 30 -hm-perf-warmup 5 -hm-perf-out perf.json -hm-perf-note "idle, load 0.8"
```

`-hm-perf-capture SECONDS` enables it, `-hm-perf-warmup` defaults to 5 s,
`-hm-perf-out` is the JSON path (use an ignored directory) and
`-hm-perf-note` is copied into the JSON so the run conditions travel with the
numbers. The JSON holds frames, avg/p95/p99/max ms, avg fps, over-budget
frames, draw calls, SetPass calls, triangles, and for the warmup its frame
count, its worst frame and how many warmup frames exceeded 100 ms. The
statistics are unit tested (PerfCaptureSessionTests); the player-side wiring
has been compiled but not yet run in a build.

Extra flags: `-hm-perf-skipday` jumps the game clock one full day halfway
through the capture window, `-hm-perf-render` renders the main camera into an
offscreen 1280x720 target every frame, and `-hm-target-fps N` overrides the
60 fps cap (`-1` = uncapped, diagnostics only). `-hm-no-prewarm` disables the
shader warmup for A/B runs.

## Phase 6 gate measurement

Conditions, stated plainly: measured under background load of about 3 (not an
idle-machine figure; loginwindow, WindowServer and the wallpaper extension use
CPU while the session is locked). `uptime` before: 3.10 3.26 3.05, after:
2.74 3.10 3.01. The screen was locked, so no OS window could be presented.
The player ran with `-batchmode` plus `-hm-perf-render` (offscreen 1280x720
camera render each frame on the real Apple M1 Metal device, development build,
HUD not included in the render). Raw JSON: `perf.json` in the session scratchpad
(not committed).

`-hm-perf-capture 130 -hm-perf-warmup 5 -hm-perf-skipday -hm-perf-render`, with
one in-game day skipped mid-capture:

| Metric | Result | Target | Met |
|--------|--------|--------|-----|
| Frames | 7493 over 130 s | | |
| Average | 17.35 ms (57.6 fps) | <= 16.7 ms | no |
| p95 | 18.60 ms | | |
| p99 | 22.09 ms | <= 20 ms | no |
| Max | 87.5 ms | | |
| Frames over budget | 2061 of 7493 | | |
| Draw calls avg / max | 160 / 162 | <= 1500 | yes |
| SetPass avg / max | 5 / 5 | <= 150 | yes |
| Triangles avg / max | 10.8 k / 11.1 k | <= 1.5 M | yes |

The frame-time target was missed in this run. Evidence on the cause, from the
same build and conditions:

- Uncapped (`-hm-target-fps -1 -hm-perf-render`, 15 s): avg 0.41 ms, p99 0.54 ms,
  max 0.93 ms, about 160 draw calls and 10.8 k triangles per frame. That is
  CPU-side render submission only; GPU execution time was not measured.
- Every capped run lands on avg 17.0 to 17.35 ms regardless of content (20 s
  runs with and without prewarm, 12 s runs, 130 s run), which points to the
  60 fps limiter overshooting in a headless player under load, not scene cost.
- The p99 and max spikes (22 ms, 87 ms) coincide with machine load and
  were not attributed to any in-game system.
- Not verified: a windowed player at 60 fps on an idle machine. No setting was
  changed to make the number pass; re-measure in a real window with the session
  unlocked before treating the gate as passed or failed.

An earlier capture without `-hm-perf-render` (cameras do no work in
`-batchmode`) is not a render measurement and should be ignored; its render
counters read 0. With `-hm-perf-render` they read correctly.

### Shader prewarm A/B

`GameStartup` calls `Shader.WarmupAllShaders()` after the first area loads
(`-hm-no-prewarm` skips it). Capture 12 s after a 6 s warmup, two runs each:

| Run | Warmup max frame | Warmup frames over 100 ms | Capture max |
|-----|------------------|---------------------------|-------------|
| no prewarm 1 | 116.6 ms | 2 | 20.8 ms |
| prewarm 1 | 132.6 ms | 2 | 20.7 ms |
| no prewarm 2 | 315.3 ms | 2 | 19.9 ms |
| prewarm 2 | 165.1 ms | 2 | 20.4 ms |

Result: inconclusive. The warmup window still contains two frames over 100 ms
with and without the prewarm, and run-to-run variance (117 to 315 ms without)
is larger than any difference. The old 3.4 s first-frame spike was not seen in
these batchmode runs. The prewarm is kept because it is harmless, but it has
not been shown to remove the first-seconds hitch.

### HUD and framing check

Verified from an in-engine render of the main camera to a PNG (`-hm-shot`),
not an OS window screenshot (the locked session prevents one). The uGUI HUD
draws date, time, phase, gold, stamina and held tool; the farmhouse, trees,
fences, NPC and player are visible. The farmhouse roof is clipped at the top
edge of the frame at the default camera distance of 20.
