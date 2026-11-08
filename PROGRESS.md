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

## P3 — Bridge construction and grocery maths — PASS

Implemented on branch `p3-bridge-market`:

- M03 seven-socket bridge: three supports first, then four planks. Floating
  labelled ghost sockets, a dedicated bridge build camera, large click areas,
  support/plank part buttons, live HUD counts and no consumption of invalid
  parts. Completing all seven builds a real walk/drive collider, side rails,
  hides the closure barrier and opens the east meadow.
- Incomplete bridge has no deck collider; falling into the river triggers a
  friendly return to the west bank. Completed bridge is traversable on foot.
- M04 Market Basket: dedicated 12-coin job purse, bread 3 / milk 4, add/remove
  purchase UI, running total and change, disabled over-budget buttons, separate
  from the permanent wallet, delivery at TOWN HALL and +12 reward.
- Added authored bread/milk/rope/seat/bench/flowerbed/lamp/table/chair/plant
  SVG UI icons in `assets/icons/`.

Automated tests:

```text
$ dotnet run --project tests/PatchworkIsland.Tests.csproj
PASS  grocery arithmetic and task budget
PASS  M04 uses only the task purse and awards 12 once
PASS  bridge snap order, invalid placement and completion
PASS  bridge rules are independent and enforce seven sockets
... 16 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> 0 errors
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI checks (Xvfb/llvmpipe, xdotool; developer scenario args used to
reach the relevant state quickly, while unlocks are covered by the pure tests):

- Started M03 with 22 coins, opened the worksite with E, clicked three floating
  SUPPORT sockets (bar updated 1/3 → 3/3, HUD updated too), then four PLANK
  sockets. Last plank showed success panel +20 coins, wallet 42, and opened the
  bridge. Walked from x=34.2 to x=52.3 across the river; no fall/recovery fired.
- Restarted an unfinished bridge, walked into the gap: recovered to x=35.5 with
  `Splash! Back to dry land.` and kept the 1/3 support progress.
- M04: opened shop at 10 wallet coins; added 2 bread + 1 milk; UI showed
  Total 10 / 12 and Change 2 with green "Perfect basket", Add buttons disabled,
  core wallet still 10. Walked to TOWN HALL, delivered, success +12 and wallet 22.
- Genuine evidence: `evidence/p3_01_bridge_worksite.png` …
  `evidence/p3_06_market_delivered.png`.

Known polish issues: bridge ghost rows overlap in perspective and need patience
at some camera angles (labels and part counts mitigate); several standalone
signs still float without posts; camera can be close to some walls. Deferred to
P7 polish. No save yet.

## P4 — Vehicle, checkpoint course and two-stop post — PASS

Implemented on branch `p4-van-race-post`:

- Arcade yellow delivery van (`CharacterBody3D`) with W/S accelerate-brake-reverse,
  A/D steering, toy-like turn-in-place at a standstill, wheel spin, driver figure,
  camera auto-follow, E enter/exit (low-speed exit), R reset, parking sign recall
  and automatic recovery from a water crash.
- Safe exit-position search (ground ray + capsule overlap check, bridge/water
  aware); if no space is safe the van is parked first, so the player cannot be
  trapped. Player can still walk and access all missions after leaving the van.
- M05 Village Road Rally: four numbered checkpoints in order, real gate
  proximity detection only while driving, live timer in the job card, optional
  90 s bonus, no completion time limit, bridge-first waypoint routing.
- M06 Two-Stop Post: two blue parcels and one orange parcel on the town-hall
  post counter, colour-word HUD, correct-destination hand-ins, wrong-attempt
  gating, +15 reward.
- Job board now unlocks/starts M05 and M06. Van parking pad/sign added.

Automated tests:

```text
$ dotnet run --project tests/PatchworkIsland.Tests.csproj
PASS  checkpoint gates must be taken in order
PASS  optional race bonus never blocks completion
PASS  two-stop post enforces parcels and destination types
...
RESULT: 19 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> succeeded, 0 errors
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI checks (Xvfb + llvmpipe + xdotool):

- Started the van from its parking pad with E; HUD switched to driving prompt,
  auto camera followed, first-use driving tutorial shown.
- Drove north to the main road, turned with tank-pivot + driving steering,
  crossed the intact bridge (van x=46.3, y≈0 mid-bridge) and reached the meadow.
- Passed gates 1 → 2 → 3 → 4 in order with the HUD advancing Gate 1/4 … 4/4.
  Finished in 107.4 s (over the optional 90 s), success panel showed +15 coins
  (wallet 69) and explicitly said any pace is winning — no forced timer.
- Tested reverse (van speed −3.0 while reversing), low-speed E exit to a safe
  spot with player control restored, and R reset back to parking.
- M06: collected 2 blue + 1 orange parcels at POST COUNTER, delivered 2 blue to
  RED HOUSE (2/2), then 1 orange to WORKSHOP (1/1); success panel +15 coins,
  wallet 69, correct-destination copy.
- Genuine evidence: `evidence/p4_01_van_entry.png` …
  `evidence/p4_06_van_exit.png`.

Known polish issues: camera may enter gate-arch/roof decoration geometry at
close range (blocking the view temporarily); optional fast-lap bonus was verified
by pure rule tests but not earned in the slow GUI run; no save yet. Camera
occlusion treatment is scheduled for P7.

## P5 — Playground, park and home customisation — PASS

Implemented on branch `p5-playground-park-home`:

- M07 Fix the Swings: two frames with six labelled sockets. Four ropes must be
  tied before the two seats can go on; wrong pieces are never consumed. The
  completed playground swaps to hanging, animated swings; pressing E at either
  seat makes the player swing, and the playground prompt reports the repaired
  state.
- M08 Park Planner: separate 20-coin job budget, bench 6 / flowerbed 4 / lamp 9,
  six snap pads, live spent/left counters, Undo last with refund, and a
  completed village green. Completion spends 19, leaves 1 job coin and awards
  +20 career coins. Placed decorations remain visible after completion.
- M10 Home Sweet Home: walkable house interior, furniture catalogue stand and
  six floor pads. Buy a table (8), two chairs (5 each) and a plant (3) from the
  wallet (21 total) or use the free basic starter set if the wallet is short.
  Placed furniture can be picked up and moved after completion. Completion
  awards +20.
- The player-house doorway is now a walkable low threshold with an invisible
  camera blocker, so the third-person camera stays inside instead of clipping
  through the front wall/sign.
- Fixed a shared build-mode bug where completing a mission hid every placed
  socket and its visual. Placed objects now remain visible, and placed furniture
  pads stay clickable for free rearrangement.
- Added a standalone furniture catalogue stand, unlimited-ish placement pads,
  and job-card markers/prompts for M07, M08 and M10.

Automated tests:

```text
$ dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
PASS  M07 swing assembly enforces ropes and seats
PASS  M08 park budget, pad uniqueness and undo
PASS  M10 furniture cost and house prerequisites
PASS  M10 free starter set prevents a currency softlock
PASS  game state survives a JSON round trip
...
RESULT: 23 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> 0 errors, 0 warnings
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI checks (Xvfb + llvmpipe + xdotool; developer scenarios used only
to reach mission states quickly; the pure tests cover unlock order):

- M07: opened the playground with E, placed four ropes then two seats. Success
  panel showed +18 and wallet 72; after Continue both blue seats hang from the
  frames and `E: Have a swing` appears at a seat.
- M08: opened Park Planner, placed bench (6), flowerbed (4), tested Undo (back
  to 10 spent), replaced them, placed the lamp (19 spent, 1 left). Success panel
  showed +20 and wallet 92. A subsequent build-mode screenshot shows all three
  decorations still on their pads.
- M10: walked through the newly walkable doorway to the FURNITURE stand, bought
  table + 2 chairs + plant (wallet 92 → 71), scrolled to Place furniture and
  placed all four items. Success panel showed +20 and wallet 91. After
  continuing, all furniture stayed visible in the room. Reopened the catalogue,
  picked up a chair (ready count 1), and moved it to another pad.
- Genuine evidence: `evidence/p5_01_swings_complete.png` …
  `evidence/p5_09_furniture_rearranged.png`.

Known polish issues: the furniture shop's Place button is below the fold at
720p and needs scrolling; the park camera can sit inside the Village Green sign
when standing right under it. Both are scheduled for the P7 camera/UI polish
pass. No save yet.

## P6 — Optional mastery and complete progression — PASS

Implemented on branch `p6-race-celebration`:

- M09 Race the Clock is now on the job board. It reuses the four ordered meadow
  gates, awards the normal 15 coins at any pace, and offers an optional +5 bonus
  only when the lap is under 75 seconds. A gold course pennant appears by the
  start/finish after completion.
- All ten missions share one tested unlock graph: M09 unlocks after M05; M10
  unlocks after M04 + M08; every other prerequisite comes from `MISSIONS.json`.
  The graph has no dead-end ordering, and every reward remains exactly-once.
- After M09, the job board offers a **Drive practice** race. It has its own HUD,
  counts gates 1-2-3-4 in order, starts a practice timer at gate 1, awards no
  coins, and can be ended by starting any normal mission. Free driving remains
  available at all times.
- Finishing all ten missions shows a one-time **VILLAGE FIXED!** celebration
  modal with free-roam, practice-race and job-board choices. Colour bunting and
  a "VILLAGE FIXED!" banner are visible in the village; the celebration flag is
  stored in `GameData.CelebrationSeen` for the P7 save system.
- M09 has its own completion copy and reward feedback; the job card shows the
  optional 75-second target without ever forcing a time limit.

Automated tests:

```text
$ dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
PASS  M09 stays locked until M05
PASS  all ten jobs form a completable graph
PASS  M09 optional bonus boundaries are 75 s
...
RESULT: 24 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> 0 errors, 0 warnings
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI checks (Xvfb + llvmpipe + xdotool):

- M09: started the developer meadow scenario, entered the van and drove the
  four ordered gates. Gate toasts advanced 1 → 4; finish showed the M09 success
  panel, "Four gates finished in 245.5s. Any pace is a winning pace — the gold
  flag is yours!", +15 coins and wallet 69 → 84. The optional bonus was not
  awarded because the lap was over 75 s.
- Practice: opened the job board with J after M09, saw the **Drive practice**
  card, started it, drove through gate 1 and confirmed the separate HUD showed
  `Gate 1 / 4`, `Practice time 0:00` and the next-gate marker, with no coins.
- Celebration: prepared nine completed jobs with `--scenario-final`, furnished
  the house (wallet 116 → 136 after the +20 reward) and confirmed the one-time
  **VILLAGE FIXED!** modal. Closed it and walked to the job board; coloured
  celebration bunting was visible above the square.
- Genuine evidence: `evidence/p6_01_m09_complete.png` …
  `evidence/p6_04_celebration_world.png`.

Known polish issues: gate arches can still block the camera at very close range
(P7), and the celebration bunting is deliberately simple primitive geometry.
No save yet.

## P7 — Save/load, onboarding, accessibility and polish — PASS

Implemented on branch `p7-save-load-polish`:

- Versioned JSON save at `user://save.json` with a `.bak` previous save.
  Writes go to a temporary file, flush to disk, then atomically move into place.
  Missing saves start a fresh adventure; unreadable saves are renamed to
  `save.json.corrupt-<timestamp>` and the backup is loaded with a player-facing
  recovery notice. Saves newer than the current schema are rejected safely.
- Title flow now distinguishes **Continue adventure** from **Start a new game**.
  New Game asks for confirmation when a save exists, and cancelling returns to
  the title. The title layout was tightened so Continue/New Game/Settings/Quit
  all fit at 1280×720 without scrolling.
- Saved state includes player and van positions/facing, driving state, wallet,
  inventory, quest counters, active/completed missions, tutorials, hints,
  cosmetics, bridge/swing pieces and colliders, park and furniture placements,
  owned furniture, celebration flag and settings. Rebuilding the bridge from a
  save recreates its physical deck collider, not just its visuals.
- Autosave every 20 seconds while playing, after mission start/completion, on
  pause-menu Save, Return to title and Quit. Pause menu Save still writes on
  demand. New Game rebuilds all four build systems and clears every world effect
  instead of only resetting the bridge.
- Settings modal with persisted Music, Sound-effects and Reduced-motion toggles.
  Reduced motion stops marker bobbing/spinning. There are no audio assets yet,
  so visual feedback remains the complete feedback channel; the toggles are
  stored for the future audio pass.
- First-use tutorials for walking and building (driving already had one), the
  existing layered hints/waypoint marker, and camera-only blockers on signboards
  and race-gate posts to stop close-range camera clipping.
- Furniture catalogue's **Place furniture** action now sits above the item list
  so it is visible at 720p without scrolling. Park Planner now enforces exactly
  one of each requested item and auto-selects the next missing piece.
- Fixed restored furniture/park visuals being invisible after loading (placed
  sockets now become visible on restore) and kept only furniture pads clickable
  for rearrangement so bridge/swing sockets are not blocked.

Automated tests:

```text
$ dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
PASS  save files round trip atomically and keep a backup
PASS  damaged save recovers the backup and quarantines the bad file
PASS  missing and newer saves are handled safely
PASS  M08 park budget, pad uniqueness and undo
...
RESULT: 27 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> 0 errors, 0 warnings
$ godot --headless --path . --quit-after 120 -> exit 0
```

Real-input GUI checks (Xvfb + llvmpipe + xdotool):

- M03 partial save: placed 3/3 supports, paused and used **Save now**, quit and
  relaunched, chose **Continue adventure**; the bridge opened with 3/3 supports
  visibly restored. Added the four planks, completed +20, relaunched and walked
  across the restored completed bridge from x=34.2 to x=57.5.
- M08 partial save: placed bench and flowerbed (10/20 spent), saved, quit and
  continued; both objects were visible on their pads and the budget/counts were
  still 10/20. Undid the deliberate duplicate-bench check, selected the lamp and
  completed +20 (wallet 72 → 92).
- M10 save: bought and placed the full 21-coin set, completed +20, quit and
  continued; all table/chairs/plant visuals were present in the room and the
  saved wallet was 91.
- Settings: toggled Music off and Reduced motion on; the values persisted in the
  save file. New Game confirmation replaced the whole save with a fresh state
  (0 coins, empty bridge/furniture, no completed missions).
- Corrupt save: replaced the main file with invalid JSON while a backup existed;
  relaunching recovered the backup and showed “The main save was damaged.
  Recovered the previous save…” with the bad file kept aside.
- Genuine evidence: `evidence/p7_01_title_continue.png` …
  `evidence/p7_08_new_game.png`.

Known limitations: no original audio files are bundled yet (all feedback is
visual and the sound toggles are persisted), and text scaling is fixed rather
than adjustable. The full ten-mission clean-save walkthrough is the P8 release
gate.

## P7.1 — Stability and architecture hardening — PASS

Branch `p7.1-stability-hardening`, starting from `df2e470`.

Confirmed review findings before changing code:

- `SaveService.Load` accepted syntactically valid JSON with `"ParkPlacements":[null]`; restoration would dereference a null placement.
- A save with `SchemaVersion:99` was moved aside as `save.json.corrupt-*`.
- `StartAdventure()` unconditionally enabled the player, so continuing a saved driving
  session left the hidden player processing movement and interaction.
- Restoring an active M05/M09 job did not initialise the race clock from `elapsedMs`.
- Modal close events could fire after a new modal opened, producing a stale unpause.
- Mission loading silently substituted a one-mission catalogue.
- `WorldMarker.ShowAt`, `Hud.SetMission` and `Geo.IconTexture` rebuilt resources/controls
  on frequently refreshed paths.

Implemented:

- **Save validation** (`SaveValidation`): schema, nested null records, unknown mission
  and counter references, negative/impossible counters, unknown/duplicate placement
  slots and item types, impossible park/furniture ownership, unknown bridge/swing
  sockets, missing physical state for completed world jobs, non-finite and
  out-of-island positions, and driving without both snapshots. Optional historical
  fields still receive defaults, and a minimal `SchemaVersion:0` save loads safely.
- **Explicit load outcomes** (`SaveLoadStatus`): Loaded, Missing, Corrupt,
  UnsupportedNewer, RecoveredBackup, Unrecoverable. Newer saves are never renamed or
  quarantined; the original is left untouched and the title explains why. Explicitly
  starting a new game first writes a `save.json.newer-vN-<timestamp>` preservation copy.
- **Backup safety**: main and backup writes both use flush-then-atomic-move. A valid
  previous save becomes the backup; a corrupt or newer primary is preserved separately
  and never replaces a good backup. Failed temporary writes clean up and leave the
  previous save byte-for-byte intact.
- **One control model** (`ControlMode`): Title → Modal → Building → Driving → Walking
  precedence. `SyncControlMode()` is the single place that enables the player or van,
  captures/show mouse input, and pauses the world. Exiting the van now has a short
  cooldown so the same E press cannot instantly re-enter it.
- **Race restore** (`RaceTimer`): M05/M09 timers are restored from `elapsedMs` on load
  and mission start, stored while driving, reset only at gate 1, and use the restored
  time for optional bonuses. Paused time is not counted.
- **Modal lifecycle**: `Hud` reports only real open/closed transitions; opening a new
  modal cannot be undone by a stale close event. Pause → Settings returns to Pause;
  Title → Settings returns to Title. Confirmation cancellation returns to the correct
  screen.
- **Mission integrity**: startup now fails explicitly if `data/missions.json` is
  missing, malformed, has fewer/more than ten missions, a bad prerequisite graph or an
  unreachable job. The one-mission fallback was removed. A test compares runtime
  content against `MISSIONS.json` field-for-field so the two files cannot drift.
- **Restore reconciliation**: an active job whose physical objective was already
  satisfied is completed exactly once on load, preventing a crash-window softlock.
- **Performance**: icon textures, marker materials, marker parameter updates, mission
  HUD rows, coin text, prompt text, destination text and cap materials are cached or
  diffed instead of rebuilt on every refresh. F12 logging now records node count and
  control mode.
- **CI**: `.github/workflows/build-and-test.yml` pins Godot 4.7.2 .NET, builds, runs the
  pure tests, imports headlessly and boots the main scene headlessly.

Automated tests:

```text
$ dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
...
PASS  structurally invalid nested records are rejected
PASS  unknown and conflicting placements are rejected
PASS  invalid mission references and counters are rejected
PASS  a corrupt main save never replaces a good backup
PASS  saving over a corrupt main keeps the good backup
PASS  a failed write leaves the previous save intact
PASS  save/reload cannot duplicate a mission reward
PASS  control ownership has one deterministic mode
PASS  race elapsed time survives save and reload
PASS  mission catalogue is complete and matches the content source
PASS  missing mission content fails integrity validation
PASS  invalid numeric data is refused before writing
PASS  explicit overwrite preserves a newer save
PASS  missing main save recovers the backup
PASS  unrecoverable corruption is reported, not loaded
PASS  completed world jobs require their physical state
RESULT: 44 passed, 0 failed
```

Build/headless:

```text
$ dotnet build PatchworkIsland.sln -> 0 errors, 0 warnings
$ godot --headless --path . --editor --quit -> exit 0
$ godot --headless --path . --quit-after 120 -> exit 0
```

Content-failure check: temporarily renamed `data/missions.json`; headless startup
printed `FATAL: Patchwork Island could not load its mission content:
res://data/missions.json is missing or empty.` and exited **1**. The file was then
restored byte-for-byte.

Real-input GUI checks (Xvfb :99, Openbox, llvmpipe, xdotool):

- Pause → Settings → Close returned to Pause and remained paused; Title → Settings →
  Close returned to Title. New Game confirmation Cancel returned to Title.
- Driving save: entered the van under `--scenario-m05`, drove to z=-4.76, paused,
  **Save now**, quit, relaunched and chose Continue. The van resumed driving, the
  player stayed hidden (`CONTROL=False`, position unchanged), W moved only the van,
  and E exited to walking (`DRIVING=False`, `CONTROL=True`); W then moved only the
  player. Evidence `p71_09`–`p71_11`.
- Race save: after gate 1 the HUD showed `Time 0:21`; Save/quit/Continue restored
  gate 1 and showed `Time 0:41` from `elapsedMs=39472` rather than 0:00. Evidence
  `p71_14`–`p71_16`.
- Build mode: E at the bridge set `CONTROL=False` with the build bar; Esc returned
  `CONTROL=True`. Evidence `p71_18`.
- Newer save: `SchemaVersion:99` left the file untouched, showed an incompatibility
  notice with no Continue button, and an explicitly confirmed new game preserved
  `save.json.newer-v99-*` before writing a fresh v1 save. Evidence `p71_19`–`p71_22`.
- Corrupt structured save: `ParkPlacements:[null]` was quarantined and the valid
  backup loaded with the player-facing recovery message. Evidence `p71_25`.
- Two consecutive New Game resets both reported `NODES=2044`, so no scene-tree growth
  was observed after rebuilding all four builder systems.
- Renderer: Mesa 26.0.8 llvmpipe (software), OpenGL 4.5 compatibility, 1280×720.
  Observed 11–13 FPS. This is a software-rendering environment, not GPU-representative;
  no severe stutter or logic hitch was observed, but performance on real graphics
  hardware is **NOT TESTED** here.

Remaining P7.1 limitations: audio assets are still not bundled; text scaling is
fixed; practice-race progress is deliberately not persisted (it is reward-free and
restarts on reload).

## P8

TODO. See IMPLEMENTATION_PLAN.md.
