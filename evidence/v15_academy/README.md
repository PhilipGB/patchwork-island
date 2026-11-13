# Evidence — v1.5.0 Driving Academy

Refreshed source, packaged and screenshot gates use the code commit recorded in
`source/head.txt` and `packaged/head.txt`. These identify tested code, not the
later evidence/documentation commit. Godot 4.7.2 .NET (mono), Linux x86_64, .NET 8.
The retained walkthrough and performance logs are historical, pre-review evidence
and do **not** verify the corrected safety rules. See `PROVENANCE.md`.

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
`docs/DRIVING_ACADEMY_REPORT.md`). The old academy half completed under the
pre-review rules; this is not a passing corrected-code or full-campaign run.

These walkthrough logs predate the UI compass fix and the review's pedestrian,
amber and crossing-checkpoint fixes. Their licence result used the defective
rules; it is not a corrected-code real-input academy pass. The new runtime gates
exercise those fixes, but a fresh real-input academy/campaign walkthrough has
not been repeated. The full-campaign status remains PARTIAL.

## Packaged Linux

`packaged/` contains the exported binary's boot, runtime, graphical and hash
evidence. See `packaged/summary.txt` and `packaged/hash.txt`.

## Screenshots

`screenshots/` holds nine categories at 1280x720 and 1920x1080:

1. school office in the town
2. enrolment UI
3. lesson board
4. L01 in the authored course
5. signal/crossing rules
6. parking bay
7. final assessment
8. licence card
9. moving crossing pedestrian

Captures use developer setup (including domain lesson completion and licence
state setup); they demonstrate rendering, not independent gameplay completion.

## Performance

`performance.log` is the retained **pre-review** frame-interval probe (`--academy-perf`) on the AMD
Radeon 890M with vsync enabled: 300 town frames, 600 academy frames and 300
frames after returning. p50/p95/max, draw calls and primitives are recorded per
resolution. The academy-entry maximum includes the one-time authored course
load and is not a steady-state figure. Performance has not been remeasured after
the safety fixes; these numbers must not be presented as current-head profiling.

## What this evidence does not prove

- No Windows gameplay startup was tested (no Windows runtime available here).
- No human or child usability study, accessibility audit or independent visual
  sign-off was performed.
- The full 24-mission graphical walkthrough is not complete (M16 route planner
  limitation above); the 24 missions are covered by their dedicated runtime
  phases and the city phase.
