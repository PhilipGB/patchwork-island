# Linux quality evidence — 2026-11-12

See [the report](../../docs/LINUX_QUALITY_PASS.md) for scope and limitations.
These are game captures and executed checks, not reference-board images.

## Rendered input progression

`completed-graphical-save.json` is the normal final save from the rendered
fresh-save input run. Each row links its completion capture and raw log.
There were harness failures and Continue restarts; this is not a claim of an
uninterrupted human playthrough. M15/M18/M20 include explicit save checkpoints.

| Mission | Wallet after completion | Capture | Log |
| --- | ---: | --- | --- |
| M01 | 10 | [M01.png](M01.png) | [walkthrough.txt](walkthrough.txt) |
| M02 | 22 | [M02.png](M02.png) | [walkthrough.txt](walkthrough.txt) |
| M03 | 42 | [M03.png](M03.png) | [walkthrough-2.txt](walkthrough-2.txt) |
| M04 | 54 | [M04.png](M04.png) | [walkthrough-2.txt](walkthrough-2.txt) |
| M05 | 74 | [M05.png](M05.png) | [walkthrough-3.txt](walkthrough-3.txt) |
| M06 | 89 | [M06.png](M06.png) | [walkthrough-3.txt](walkthrough-3.txt) |
| M07 | 107 | [M07.png](M07.png) | [walkthrough-3.txt](walkthrough-3.txt) |
| M08 | 127 | [M08.png](M08.png) | [walkthrough-3.txt](walkthrough-3.txt) |
| M09 | 147 | [M09.png](M09.png) | [walkthrough-3.txt](walkthrough-3.txt) |
| M10 | 146 | [M10.png](M10.png) | [walkthrough-4.txt](walkthrough-4.txt) |
| M11 | 158 | [M11.png](M11.png) | [walkthrough-4.txt](walkthrough-4.txt) |
| M12 | 172 | [M12.png](M12.png) | [walkthrough-4.txt](walkthrough-4.txt) |
| M13 | 186 | [M13.png](M13.png) | [walkthrough-5.txt](walkthrough-5.txt) |
| M14 | 202 | [M14.png](M14.png) | [walkthrough-5.txt](walkthrough-5.txt) |
| M15 | 218 | [M15.png](M15.png) | [walkthrough-7.txt](walkthrough-7.txt) |
| M16 | 238 | [M16.png](M16.png) | [walkthrough-9.txt](walkthrough-9.txt) |
| M17 | 260 | [M17.png](M17.png) | [walkthrough-9.txt](walkthrough-9.txt) |
| M18 | 284 | [M18.png](M18.png) | [walkthrough-10.txt](walkthrough-10.txt) |
| M19 | 314 | [M19.png](M19.png) | [walkthrough-10.txt](walkthrough-10.txt) |
| M20 | 344 | [M20.png](M20.png) | [walkthrough-11.txt](walkthrough-11.txt) |
| M21 | 369 | [M21.png](M21.png) | [walkthrough-11.txt](walkthrough-11.txt) |
| C01 | 389 | [C01.png](C01.png) | [walkthrough-city-fixed.txt](walkthrough-city-fixed.txt) |
| C02 | 413 | [C02.png](C02.png) | [walkthrough-last-city.txt](walkthrough-last-city.txt) |
| C03 | 441 | [C03.png](C03.png) | [walkthrough-last-city.txt](walkthrough-last-city.txt) |

`failure-*.png` and the failing `walkthrough*.txt` entries preserve driver/cursor/
approach/route problems. `curb-baseline.txt` is a genuine product failure before
the step fix; `curb-fixed.txt` and the final quality logs show the correction.

## Final source checks and replay

- `pure.txt`: 134 pure test passes. `build-final.txt`: clean source build.
- `selftest*.txt`: all twelve source runtime phases; total 1300 passes.
- `quality-1280x720.txt`, `quality-1920x1080.txt`: 17 graphical checks each.
- `fast-replay-1.txt` through `fast-replay-5.txt`: second progression replay,
  graphical backend with drawing disabled; no visual/performance claim. Pass 3
  aimed the van through park furniture and failed. The route was corrected and
  the same normal save completed all 24 jobs. `completed-post-refactor-save.json`
  is the resulting 441-coin save.
- `wall-shadow-before.png` / `wall-shadow-after-720.png` /
  `wall-shadow-after-1080.png`: targeted material comparison, software renderer.
- `replay-shutdown-verbose.txt`: retained native WAV/OGG teardown warnings.

Runtime failure-injection messages in recovery/hotfix phases are expected test
fixtures. Earlier export logs deliberately retain .NET publish failures; only
`export-final.txt` and subsequent package execution count as export verification.
GPU/device logs and hardware captures are identified separately in the report.

## Exported Linux executable

`export-final.txt` has no logged errors/warnings. `package-boot.txt` records a
successful actual executable launch; `package-selftest*.txt` contains all twelve
runtime phases, **1300 passed / 0 failed**. `package-quality.txt` contains **17/0**
graphical checks on the actual release executable. `package-sha256.txt` identifies
the local review build (gameplay commit `b3295e8`); binaries are not committed.
All GPU raw runs, both sampling sets and hardware images are in `gpu/`.
`gpu/baseline-instrumentation.patch` applies only measurement changes to `a429791`.
