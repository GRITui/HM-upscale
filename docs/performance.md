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

No fps figure is claimed for the Phase 6 slice: the machine was not idle when
this was written (load average above 3), so a number would not be honest.
Run the capture on an idle machine and record `uptime` in the note.

The 3.4 s first-frame spike measured above is a shader warm-up. A
ShaderVariantCollection prewarm is the intended fix but has not been
generated or measured yet.
