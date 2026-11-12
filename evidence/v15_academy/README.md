# Evidence — v1.5.0 Driving Academy

All runs below use the exact source tree at the commit recorded in
`source/head.txt`. Godot 4.7.2 .NET (mono) on Linux x86_64, .NET 8.

## Environment

```text
OS        Linux (Fedora)
CPU/GPU   AMD Ryzen + AMD Radeon 890M (Mesa 26.0.8, OpenGL 4.5 compatibility)
Display   Wayland/X11 desktop for GPU runs; Xvfb 1280x800x24 for graphical CI runs
Audio     Dummy driver for all automated runs
Isolation XDG_DATA_HOME / XDG_CONFIG_HOME / XDG_CACHE_HOME are disposable per run
```

Xvfb uses llvmpipe software rendering. Its frame times are recorded separately
and are **not** used as GPU evidence.

## Source gates

| Gate | Command | Log |
|---|---|---|
| Import | `godot --headless --path . --editor --quit` | `source/../import.log` |
| Pure tests | `dotnet run --project tests/PatchworkIsland.Tests.csproj` | `source/pure-tests.log` |
| Base | `godot --headless --path . -- --selftest` | `source/runtime-base.log` |
| Resume | `-- --selftest-resume` | `source/runtime-resume.log` |
| Recovery | `-- --selftest-recovery` | `source/runtime-recovery.log` |
| Living village | `-- --selftest-living` | `source/runtime-living.log` |
| Rally | `-- --selftest-rally` | `source/runtime-rally.log` |
| Chapter two | `-- --selftest-chapter` | `source/runtime-chapter.log` |
| Hotfix | `-- --selftest-hotfix` | `source/runtime-hotfix.log` |
| Finale | `-- --selftest-finale` | `source/runtime-finale.log` |
| Traversal | `-- --selftest-traversal` | `source/runtime-traversal.log` |
| Release/migration | `-- --selftest-release` | `source/runtime-release.log` |
| Restore | `-- --selftest-restore` | `source/runtime-restore.log` |
| City | `-- --selftest-city` | `source/runtime-city.log` |
| Academy | `-- --selftest-academy` | `source/runtime-academy.log` |
| Graphical quality | `xvfb-run godot --path . --audio-driver Dummy -- --selftest-quality` | `source/quality.log` |

`source/summary.txt` and `packaged/summary.txt` record every result line.

## Graphical walkthrough

`graphical/walkthrough-phase1.log` and `graphical/walkthrough-phase2.log` are a
fresh-save run of `--dev --playthrough` under Xvfb, acting only through movement,
E, mouse clicks and menus. Phase 1 earns the driving licence, completes M01–M14
and stops at the deliberate M15 checkpoint (the game requires a save/reload
there). Phase 2 continues from that save, completes M15 and fails at M16 on a
pre-existing walkthrough route-planner limitation (see
`docs/DRIVING_ACADEMY_REPORT.md`). The academy half of phase 1 is complete and
passing; this is recorded as a limitation, not as a passing full-campaign run.

## Packaged Linux

`packaged/` contains the exported binary's boot, runtime, graphical and hash
evidence. See `packaged/summary.txt` and `packaged/hash.txt`.

## Screenshots

`screenshots/` holds eight categories at 1280x720 and 1920x1080:

1. school office in the town
2. enrolment UI
3. lesson board
4. L01 in the authored course
5. signal/crossing rules
6. parking bay
7. final assessment
8. licence card

## Performance

`performance.log` is the frame-interval probe (`--academy-perf`) on the AMD
Radeon 890M with vsync enabled: 300 town frames, 600 academy frames and 300
frames after returning. p50/p95/max, draw calls and primitives are recorded per
resolution. The academy-entry maximum includes the one-time authored course
load and is not a steady-state figure.

## What this evidence does not prove

- No Windows gameplay startup was tested (no Windows runtime available here).
- No human or child usability study, accessibility audit or independent visual
  sign-off was performed.
- The full 24-mission graphical walkthrough is not complete (M16 route planner
  limitation above); the 24 missions are covered by their dedicated runtime
  phases and the city phase.
