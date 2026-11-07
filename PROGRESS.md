# Patchwork Island — progress log

Durable status log. Only evidence that was actually produced is recorded here.
Environment: Linux x86_64, Godot **4.7.2 stable mono** (`ed1daf0bf`), .NET SDK
8.0.427 and 10.0.114 installed. Display `:0` is available; graphical checks are
recorded separately from headless checks.

Status legend: **PASS**, **FAIL**, **NOT TESTED**, **IN PROGRESS**, **TODO**.

## P0 — Environment, repository, runnable skeleton — PASS

- Pinned runtime: `/home/philip/opt/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64` (also symlinked as `godot`).
- Project: Godot 4.7.2 .NET/C#, `net8.0`, Compatibility renderer, 1280×720 base viewport.
- Boot scene has camera, directional sun, sky, ground, avatar placeholder, working pause overlay (Esc) and quit.
- Created private GitHub repository `PhilipGB/patchwork-island`; handoff imported on `main`.
- P0 implemented on branch `p0-skeleton`.

Commands and results:

```text
$ godot --version
4.7.2.stable.mono.official.ed1daf0bf

$ dotnet build PatchworkIsland.sln
Build succeeded. 0 Warning(s), 0 Error(s)

$ godot --headless --path . --editor --quit
exit 0; assets imported (SVG icons and reference PNGs)

$ godot --headless --path . --quit-after 120
exit 0; no script errors printed
```

Known gaps: no village or gameplay yet (P1 onward). Graphical screenshot pending
world implementation.

## P1 — Coherent village and movement — PASS (gate scope)

Implemented on branch `p1-village`:

- Contiguous 160×120 m island, split by a river at X≈45 with walk-blocking water gap and unfinished bridge/barrier.
- Five recognizable buildings: player house with a real walk-in interior, town hall with clock, red-gabled RED HOUSE, striped SHOP, green-roof WORKSHOP. Each has large readable signage and icons.
- Main road, driveways, footpaths, crossings, player house yard, playground, grove and stone patch, village green, bridge worksite, four-gate race meadow, ~40 trees/firs, bushes, flowers, benches, lamps, fences, crates, clouds and ocean edge.
- Third-person `CharacterBody3D` player with chibi model, camera-relative walking (WASD/arrows), forgiving Space jump, walk animation, camera-relative movement and optional mouse orbit/zoom.
- Follow camera with wall-collision pulling and indoor mode; generous-proximity interaction with a visible gold ground ring and bottom-centre `E` prompt.
- Job board physical station with readable JOBS board and floating star marker; pause overlay (Esc/P) with mouse and keyboard focus; title screen.
- Safe fallback from river/out-of-world to the nearest bank.

Commands and results:

```text
$ dotnet build PatchworkIsland.sln
Build succeeded. 0 Warning(s), 0 Error(s)

$ godot --headless --path . --quit-after 120
exit 0

$ godot --path . -- --capture-title
SCREENSHOT .../evidence/p1_00_title.png -> Ok

$ godot --path . -- --capture
SCREENSHOT p1_01_start.png ... p1_07_race_meadow.png -> Ok
```

Real-input GUI checks (Xvfb + Openbox, 1280×720, llvmpipe, xdotool):

- Title Return opened the world; holding W for 1.5 s moved the player from
  `(-38, 9.5)` to `(-30.10, 6.93)` at ~6.1 m/s.
- P opened the pause menu; player velocity became zero and the menu was usable
  with mouse focus. Resuming restored control.
- Walked to the relocated JOBS board (`(-22, 5)`), saw the gold highlight and
  `E: Read the job board` prompt, pressed E and the job modal opened.
- Screenshots are genuine viewport captures produced by the running game:
  `evidence/p1_00_title.png` … `evidence/p1_07_race_meadow.png`.

Limitations / deferred:

- Continuous spawn→job board→Red House→Workshop walk was only exercised to the
  job board with real keys; Red House/Workshop compounds were inspected via
  in-game teleports in the capture harness. Full continuous route will be part
  of the final clean-save walkthrough.
- Job board is intentionally a read-only preview until P2.
- Audio device is unavailable in this environment; Godot falls back to dummy
  audio. No audio was claimed.

## P2 — Mission framework and first delivery — PASS

Implemented on branch `p2-missions`:

- `GameData` + `MissionService` pure state machine loaded from `data/missions.json`
  (all ten missions, authored titles, hints, rewards and prerequisites).
- Job board/journal (E at the board, J anywhere) with rows for all ten jobs,
  status (available, current, done, locked, coming-in-next-milestone), rewards,
  prerequisites and Start buttons. Keyboard-focusable.
- World objective marker/beam/icon and top-centre destination arrow + distance,
  mission-specific progress rows, Hint button/H with layered authored hints.
- M01 "The Red House Delivery": exactly three pickable apples on the market
  crate, inventory count 0→3, hand-in at the RED HOUSE, +10 coins, red cap
  visible reward, delivery-only completion.
- M02 "Workshop Supplies": five wood at the grove + three stones at the stone
  patch, 5+3 counter feedback, submission at WORKSHOP, +12 coins.
- Exactly-once rewards, duplicate-pickup rejection, inactive-job pickup gating,
  mission switching with preserved progress, success panel, dismissal.
- Pickups now have item icons/floating labels and a generous 3.9 m pick radius.

Automated tests (new dependency-free runner, exit code checked):

```text
$ dotnet run --project tests/PatchworkIsland.Tests.csproj
PASS  catalog contains exactly ten missions
PASS  initial availability and prerequisites
PASS  starting a job and switching preserves progress
PASS  duplicate pickup is rejected and not consumed twice
PASS  M01 cannot complete early; completion awards 10 exactly once
PASS  M01 unlock opens M04
PASS  M02 counts 5 wood + 3 stone and awards 12 once
PASS  locked mission cannot start
PASS  completed mission cannot be restarted for another reward
PASS  picking items for an inactive job is rejected
PASS  hints progress through the authored layers
PASS  game state survives a JSON round trip
RESULT: 12 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln        -> succeeded, 0 warnings, 0 errors
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI walkthrough (Xvfb 1280×720, Openbox, llvmpipe, xdotool):

- Started a clean game, opened job board with J, clicked M01 Start.
- Walked to the SHOP crate with WASD, picked exactly 3 apples with E; HUD
  counters advanced to 3/3 and objective switched from SHOP APPLES to RED HOUSE.
- Walked to RED HOUSE, pressed E: success panel showed +10 coins and the cap
  turned red; objective/coins updated. Duplicate delivery is gated by completion.
- Opened journal with J, started Workshop Supplies through keyboard focus
  (Tab/Enter), walked to grove, collected five wood (5/5), crossed to the stone
  patch, collected three stones (3/3), objective switched to WORKSHOP.
- Pressed E at WORKSHOP: success panel showed 5 wood + 3 stone / +12 coins;
  total wallet 22 and M03 prerequisites unlocked (tested in pure tests).
- Genuine screenshots: `evidence/p2_01_start.png` …
  `evidence/p2_09_workshop.png`, including job board, crate, delivery and
  both success panels.

Outstanding visual issues (not blockers for P2): camera can get close to walls or
an awning in tight corners; standalone sign boards need visible support posts;
stone-patch approach needs a clearer path. These are scheduled for P5/P7 polish.
No save/load yet; that is P7. M03+ are shown as locked/coming-soon in this
milestone and are implemented next.

## P3–P8

TODO. See IMPLEMENTATION_PLAN.md.
