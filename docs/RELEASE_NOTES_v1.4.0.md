# Patchwork Island v1.4.0 — City Expansion

**This is a test build / pre-release.** It is published so the new city
expansion can be tried before the final v1.4.0 release. The Linux package has
been booted and exercised here; Windows launch is still unverified.

Release: <https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.0>

Built from commit `21b226e` (PR #32).

## Highlights

- **M21 Roadworks Rescue** — the 21st village job unlocks after all twenty
  existing jobs. Collect three safety cones, two barrier panels and one
  toolbox, then solve the 3 + 2 + 1 counting and cone → panel → toolbox
  loading-order check. Completion pays 25 coins once, removes the barrier,
  disables the gate collider and keeps the road open in the save.
- **A drivable town-to-city join** — an authored bridge/causeway, terrain
  opening and physical gate at world `x=96`. The road is genuinely closed
  until M21 is complete and can be driven in both directions afterwards.
- **Three independent city jobs** on the teal CITY JOBS board in the plaza:
  - **C01 City Courier** — deliver parcels to numbered stops in order.
  - **C02 Market Stock-Up** — collect six crates and check the 2 × 3 total.
  - **C03 Signals and Signs** — repair red/amber/green cabinets in sequence and
    get the engineer's sign-off.
  Each pays its reward exactly once and shares the village coin wallet.
- **A tiered city** — 36 authored buildings, 36 HLOD proxies and nine cells
  with 18 m hysteresis. Detailed content and a bounded near/mid actor pool load
  only around the player. A deterministic 24-vehicle / 42-pedestrian logical
  simulation keeps the city alive while it is offscreen without per-agent
  Godot nodes or physics.
- **Save schema 4** — city roadworks flags, completed city jobs, counters,
  seed and logical clock. Schema 1–3 saves migrate without changing village
  progress; newer-schema saves remain protected and untouched.
- **Version metadata** — `1.4.0` in the project and `1.4.0.0` in the Windows
  file/product resource.

## Getting started with the new content

The village remains the complete M01–M20 game. Finish those twenty jobs, then
start **M21 Roadworks Rescue** from the village JOBS board. Carry all six
supplies to the closed **CITY ROAD** gate and press **E**. Once the road opens,
drive east to the city plaza and use the **CITY JOBS** board for C01–C03.

## What to test

- Complete or load a save with all twenty village jobs, then play **M21** and
  confirm the 6/6 delivery at the closed gate, the 3 + 2 + 1 loading check and
  the permanently open road.
- Drive to the city, play **C01–C03**, and check that their rewards are paid
  once and survive save/reload.
- Save mid-drive in the city, quit and choose **Continue adventure**; confirm
  the van, city position, active city job and logical clock are restored.
- Report any visual, collision or interaction issue with the approximate
  location and the job you were doing.

## Compatibility

- Save schema **4**. Schema 1, 2 and 3 saves migrate in place; the previous file
  is kept as `save.json.bak`.
- Existing coins, outfits, character choice, all twenty village jobs, bridge,
  swings, park, furniture, both finale states and the practice race are
  preserved.
- A schema 5 or newer save is never loaded, downgraded, discarded or
  overwritten automatically. Only an explicitly confirmed New Game may replace
  it, and a preservation copy is written first.
- Completed M10 saves validate the owned furniture set, so post-completion
  rearrangement remains saveable. Long race times remain within the stored
  integer-millisecond limit.

## Verification for this build

Built from commit `21b226e` (PR #32), which includes the M21 delivery fix.

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 132 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
source runtime, 12 phases                                -> 1253 passed, 0 failed
packaged Linux v1.4.0, boot + 12 phases                  -> 1253 passed, 0 failed
```

Runtime phase counts: `284 / 21 / 101 / 238 / 10 / 217 / 48 / 140 / 18 / 40 /
54 / 82`. Exact-head CI on `21b226e` passed both `build-test` and
`windows-tests` in run
[43075355414](https://github.com/PhilipGB/patchwork-island/actions/runs/43075355414).

The packaged Linux build also booted headlessly with exit 0 and passed
`--selftest-city` (82/0) after extraction from the published ZIP. City
performance on the reference AMD Radeon 890M machine measured p95 4.51 ms at
1280×720 and 5.60 ms at 1920×1080 for the city street stage; see
`docs/05_CITY_PERFORMANCE.md`.

## Known gaps

- **Windows startup is NOT TESTED.** The Windows export is a valid PE32+ x86-64
  self-contained executable with version `1.4.0.0`, but this environment has no
  Windows runtime or Wine. Static inspection is not a launch test.
- No continuous human M01–M21 → C01–C03 playthrough and no human visual
  sign-off of the city yet. Automated source, packaged, UI and scenario checks
  are green.
- Real-GPU performance was measured on one Linux/Radeon 890M machine only;
  Windows and other GPUs are unmeasured.
- A same-method v1.3.0 town baseline and explicit >33 ms transition-hitch
  instrumentation were not measured.
- Child usability testing remains a manual follow-up.

## Packages

| Asset | Contents |
|---|---|
| `PatchworkIsland-1.4.0-Linux-x86_64.zip` | Self-contained Linux x86-64 executable + README |
| `PatchworkIsland-1.4.0-Windows-x86_64.zip` | Self-contained Windows x86-64 executable + README |
| `README.txt` | Run, control, save-location and test-build notes |
| `SHA256SUMS` | SHA-256 for both ZIPs and the extracted executables |

## SHA-256

```text
19bc6ee6c7b87172c68fa03ee70980aafc56fa7d3c34f2c54c12a2757f3058dd  linux/PatchworkIsland.x86_64
bd0632feceaa34ef8800522d6e9e8c6c2ef47f4fb3642e0cd8c4a56d246728a7  windows/PatchworkIsland.exe
3bf67da64ef80669841dac68439d7cb2f599deadbc8cadd02ee4cf1803422dba  PatchworkIsland-1.4.0-Linux-x86_64.zip
56542f617d831656bc5c5f7ff8269754902973aed46f325ab5fadae2df920577  PatchworkIsland-1.4.0-Windows-x86_64.zip
```
