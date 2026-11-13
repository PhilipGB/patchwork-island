# Patchwork Island v1.5.0 — Driving Academy (test build)

This is a **pre-release test build** of the Driving Academy update on top of the
merged v1.4.2 city hotfix and the v1.4.3 Linux quality pass. The v1.4.x tags
and releases are unchanged.

## New

- A driving school on the south lawn (`32, 48`) with three sortable cones, a
  15-coin one-time enrolment, and six lessons on an isolated authored course:
  L01 start and stop, L02 steering gates, L03 stop line, L04 traffic lights and
  zebra crossings, L05 reverse parking, L06 checkpoint circuit.
- The final assessment allows three minor notes and zero major errors. Passing
  issues the official licence, pays a one-time 5-coin graduation gift and keeps
  the school open for free practice.
- The learner car reuses the van physics with an "L" plate. The course has a
  working signal cycle (red, red+amber, green, amber), a pedestrian who waits
  and crosses during the first 3.5 s of green, 20 mph signs and a zebra
  crossing.
- A lesson HUD shows the stage, step counter, hint, live speed, signal state
  and a "Next marker: N m direction" wayfinding line. Directions use the car's
  own left and right (fixed after merge review), and the panel sits top-right
  so the signal head is visible from the stop line.
- New v5 saves require an earned licence before the regular van can be driven;
  schema 1-4 saves migrate and keep a legacy permit. Enrolment, lessons,
  practice and retries are free.

## Verification

- Build 0 warnings/errors; pure **147/147**.
- Source runtime **1457/1457** across all 13 phases; packaged Linux runtime
  **1457/1457**; source and packaged graphical quality **22/22** each;
  packaged graphical academy **146/146** under Xvfb.
- Real-input fresh-save walkthrough (keyboard/mouse/menu only): cone job,
  enrolment, L01–L06 and TEST01 completed with the licence earned on the first
  attempt ("Perfect drive"), including the 5-coin gift and replayable lesson
  board.
- The tested code is named in both `head.txt` files before execution; export
  SHA-256 is recorded in `packaged/hash.txt`; `SHA256SUMS` covers all evidence
  files.

## Known gaps

- Windows builds and passes CI, but **Windows gameplay startup was not tested**
  in this environment.
- No human or child usability study has been run.
- Performance figures predate the academy fixes and were not remeasured.
- The full-campaign graphical walkthrough remains PARTIAL at M16 (pre-existing
  route-planner limitation).

## Controls

WASD/arrows move, Space jump, E interact, J jobs, H hint, Esc/P pause. Near the
van press E to drive; R resets it. In the academy, A/D steer and R restarts the
current stage.

## SHA-256

```text
62b40e45a712ec12acd64f4190d16e7ccc4505b1d4542c9ad0048961f18e3938  linux/PatchworkIsland.x86_64
438030dac63348554d49f185cf354606adc70b4f3ab0a80176b529016038a0aa  windows/PatchworkIsland.exe
ec389f6df2b0957cf3c4385b9beb2b140ba2cfe17c07a868dce24e4adf1b8969  PatchworkIsland-1.5.0-Linux-x86_64.zip
ebea74f4083e13f1f5d6ab35592bf2766abe2ab4a4ee9972c6318e9c9173e650  PatchworkIsland-1.5.0-Windows-x86_64.zip
```

## Release build

The assets are the `dist/` release exports of code commit `b5becdb`
(`project.godot` version 1.5.0, Windows file/product version 1.5.0.0). The
exported Linux binary passed the packaged academy runtime gate (146/146) from
the release artifact itself before upload.
