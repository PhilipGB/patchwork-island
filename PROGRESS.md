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

## P8 — Final acceptance, packaging and handoff — PASS

Branch `p8-final-acceptance`, starting from merged P7.1 `ca55059`.

Clean-save walkthrough:

- Deleted all existing save, backup, corrupt and newer-version files and launched
  the game. The title showed **Start adventure** (no Continue), confirming a
  genuine fresh state. Used the normal New Game flow, not a developer scenario.
- Completed all ten jobs sequentially with real keyboard/mouse input:
  - M01: walked to the shop crate, collected exactly 3 apples, delivered to RED
    HOUSE. +10, red cap unlocked.
  - M02: collected 5 wood at the grove and 3 stones at the stone patch, handed
    all 8 to WORKSHOP. +12.
  - M03: opened the bridge worksite, clicked 3 support sockets and 4 plank
    sockets; the last plank created the deck collider. +20. Drove across the
    bridge later at x=68.
  - M04: bought 2 bread @3 and 1 milk @4 with the dedicated 12-coin purse;
    the panel showed Total 10 / 12 and Change 2. Delivered to TOWN HALL. +12.
  - M05: drove four ordered gates. The finish panel showed 365.7 s, no optional
    bonus, and completed normally. +15.
  - M06: collected 2 blue + 1 orange parcels, delivered both blue parcels to
    RED HOUSE and the orange parcel to WORKSHOP. +15.
  - M07: clicked 4 rope and 2 seat sockets across both swings; both frames
    visibly assembled. +18.
  - M08: placed bench 6 + flowerbed 4 + lamp 9 on the village green; the panel
    showed 19 spent, 1 job coin left. +20.
  - M09: replayed the four gates; finish panel showed 132.6 s, no optional
    bonus, and completed normally. +15.
  - M10: bought table 8 + 2 chairs 10 + plant 3 (21 coins from the wallet),
    placed all four pieces in the house, and completed. +20.
- Final wallet **136** = 157 total mission rewards − 21 furniture spend.
- Closing the M10 success panel showed **VILLAGE FIXED!** exactly once; bunting
  and celebration decor were visible in the world.
- Free roam remained available. Opened the job board: all ten cards showed
  **DONE**, no Start buttons, and the reward-free **Drive practice** panel was
  present. Completed a practice lap in 1:52; wallet stayed at 136.
- Save/quit/relaunch: the final save contained all ten completions, 136 coins,
  `CelebrationSeen=true`, 7 bridge pieces, 6 swing pieces, 3 park placements and
  4 furniture placements. Continue restored the world; the celebration did not
  replay. Drove across the restored bridge collider to the meadow and re-entered
  the house to see restored furniture.

Error/recovery handling exercised:

- Jumped and fell into the river; recovery teleported the player to the west
  bank and showed “Splash! Back to dry land.”
- Used R to reset the van after becoming stuck, and observed automatic recovery
  after a river crash.
- Clicked empty/wrong build locations during bridge and swing assembly; no
  pieces were consumed and the build bar stayed consistent.
- Paused/resumed throughout; saved from the pause menu before quitting.
- Partial-builder and corrupt/newer save handling were separately verified in
  P7/P7.1 (partial M03/M08/M10 reloads, backup recovery, newer-save preservation).

Automated/build verification at the P8 head:

```text
$ dotnet build PatchworkIsland.sln -> 0 warnings, 0 errors
$ dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
RESULT: 44 passed, 0 failed
$ godot --headless --path . --editor --quit -> exit 0
$ godot --headless --path . --quit-after 120 -> exit 0
```

Accessibility / graphics checks:

- Real-input play at 1280×720; title, HUD, job board, shop, build bars and
  prompts are readable with large targets.
- 1920×1080 run of the exported Linux build; title and job board scale and remain
  readable (`evidence/p8_104_1080p_title.png`, `p8_105_1080p_jobs.png`).
- WASD and arrow keys both move; Space jump raised the player from Y=-0.04 to
  Y=1.08; right-drag orbit changed the camera view.
- H hint displayed the current job/board hint with the marker active.
- Child usability: **NOT TESTED** (no child participants available).

Performance:

- Renderer: Mesa 26.0.8 llvmpipe, OpenGL 4.5 compatibility, 1280×720.
- Observed 11–13 FPS in this software-rendering environment. GPU performance
  is **NOT TESTED**. Scene node count remained ~2015–2044 across New Game
  resets, mission completion and reload; no growth from builder resets was seen.
- No large resource-loading spike or severe logic stutter was observed.

Export and packaging:

- Installed matching Godot 4.7.2 .NET export templates.
- Windows x64 export succeeded:
  `dist/windows/PatchworkIsland.exe` (PE32+ x86-64, self-contained .NET 8
  runtime pack, embedded PCK, `data/missions.json` and assemblies present).
- Linux x86_64 export succeeded:
  `dist/linux/PatchworkIsland.x86_64`.
- Linux exported build was launched under Xvfb, showed the title and Continue
  button, loaded the mission catalogue and final save, and opened the job board
  with all ten DONE (`evidence/p8_102`, `p8_103`). It also booted headless with
  exit 0.
- Windows startup is **NOT TESTED**: no Windows machine and no Wine in this
  environment. The export succeeded and was inspected; that is not the same as a
  Windows launch.
- Distributable zips and SHA-256 checksums were produced under `dist/`.

Documentation updated: `README.md`, `README_RUN.md`, `ACCEPTANCE.md`,
`PROGRESS.md`, `IMPLEMENTATION_PLAN.md` unchanged (scope completed),
`export_presets.cfg` committed and `.gitignore` updated.

Known limitations:

- No bundled audio; settings toggles persist for a future audio pass.
- Text scaling is fixed; layout scales to 1080p.
- Windows startup not tested; GPU performance not measured; child playtesting
  not possible.
- Practice-race progress intentionally resets on reload (reward-free).

## P8.1 — Reliability hardening (v1.0.1) — PASS

Branch `p8.1-reliability-hardening`, baseline `6855f1b35d9c3dbea60eb4bec9bce3509725e916`
(`v1.0.0`, PR #10). Baseline checks reproduced 0 warnings/0 errors and 44/44
tests before changes.

Confirmed findings before fixing:

- `SaveValidation.MissionObjectiveMatches` required a completed M10 to still
  have all furniture physically placed. `FurnitureBuilder` allows pickup and
  rearrangement after completion, so this legitimate state made every save
  fail. Reproduced with a completed M10 and one chair picked up; the old code
  threw `Refusing to write an invalid save: Mission 'M10' is marked complete but
  its world state is missing.`
- M05/M09 `elapsedMs` was capped at 3,600,000. A 66-minute race produced
  `Impossible progress counter 'M05.elapsedMs' = 3960000`, and the counter
  remained after completion. Reproduced fail-first.
- `Main.SaveNow` ignored the save result while `Hud` unconditionally changed the
  pause title to "Paused — Saved"; autosaves were silent.
- `ResetSession` set `_hasSaveFile/_canContinue` before checking the write.
- `Main` inherited `ProcessMode.Always` to gameplay nodes, so `_Process` and
  `_PhysicsProcess` kept running under pause even when gameplay input was off.
- Pause → New Game → Cancel closed the confirmation without reopening pause,
  unexpectedly resuming gameplay.
- `ValidateSwingPlacement` accepted unknown part types; `FurnitureRules.Buy`
  trusted the UI cap; `CompleteActive` accepted arbitrary bonus values.
- The mission JSON test compared only selected fields and ignored mechanics,
  category, site, goal, learning, world effect and top-level metadata.
- F12 and capture helpers were always active in release runs and wrote to the
  current working directory; `SaveGame(false)` failures were invisible.

Fail-first evidence (temporary restoration of the old validator before the fix):

```text
FAIL  M10 rearrangement remains saveable after completion:
      Refusing to write an invalid save: Mission 'M10' is marked complete but its world state is missing.
FAIL  later mission progress survives an unplaced post-completion chair:
      Refusing to write an invalid save: Mission 'M10' is marked complete but its world state is missing.
FAIL  long race times remain saveable and do not poison later saves:
      Refusing to write an invalid save: Impossible progress counter 'M05.elapsedMs' = 3600001.
RESULT: 50 passed, 3 failed
```

Implemented root-cause fixes:

- **M10 persistence:** completed-world validation now requires the owned table,
  two chairs and plant (`FurnitureRules.OwnsRequiredSet`) instead of requiring
  every piece to be placed. Placement slots, uniqueness and placed<=owned are
  still validated. `MissionRules.HasHouseFurniture` still gates actual mission
  completion. M03/M07/M08 retain strict physical-world validation.
- **Race persistence:** `RaceTimer` uses a double clock, saturates at
  `RaceTimer.MaxStoredMilliseconds` (`int.MaxValue` ms), rounds elapsed time up
  when storing so reload cannot shorten it, and validates against the same
  ceiling. Negative/overflow values clamp safely. Optional bonuses remain 5 at
  the 90 s / 75 s boundaries and 0 after them.
- **Save feedback:** added `SaveResult` (`Success`, `RecoverableFailure`,
  `InvalidState`), `SaveFeedbackPolicy`, and `SaveService.TrySave`. Manual saves
  always show success/failure inline in the pause modal; automatic failures show
  one throttled warning until a successful save; mission-completion, new-game,
  return-to-title and quit failures are always explicit. Return/quit offer a
  safe choice on failure. Window-close failures are logged because the platform
  is already closing.
- **New Game consistency:** `SessionSaveState` tracks the last persisted save
  independently of memory. Failed initial/replacement writes leave the previous
  Continue state and file intact and add a title notice. Newer-schema protection
  is preserved; a successful explicit replacement clears the notice.
- **Pause/runtime:** world, player, camera, van, marker, builders and pickups are
  explicitly `Pausable`; `Main` and `Hud` remain `Always`. Added
  `RuntimeSelfTest` with real Godot execution for title/modal pause, player and
  van physics, race clock, settings, New Game cancel/confirm, mission-completion
  pause, autosave/manual save and driving save/continue.
- **Modal cancellation:** `ConfirmNewGame` passes `ShowPauseMenu` as the cancel
  callback, so Pause → New Game → Cancel returns to the pause modal without a
  stale unpause. Title cancellation still returns to title.
- **Domain guards:** swing sockets require `rope`/`seat`; furniture purchase
  stops at the required quantity; `CompleteActive` rejects negative bonuses,
  bonuses on non-race jobs and bonuses above 5. Legitimate rewards and
  exactly-once completion are unchanged.
- **Content integrity:** added `MissionContentConsistency`, a recursive
  complete-JSON comparison including every object, array and metadata field.
  `MissionDefinition.Mechanics` now parses the authored object. Mechanics are
  documented as reference data; `MissionRules` remains the runtime authority.
- **Developer tooling:** F12/capture/scenario helpers require the editor,
  `--dev`, `--capture`, `--scenario-*` or `--selftest*`. Screenshots write to
  `user://screenshots` or `user://dev-captures`, not the working directory.
  Capture and file failures are caught and logged; no unobserved task exception
  path remains.
- **Tests/CI:** replaced the platform-skipped write failure with a real
  cross-platform `.tmp`-directory collision and retry. CI now runs 61 pure tests,
  both Godot runtime phases, imports/boots, exports a real Linux release,
  verifies embedded `data/missions.json` and `PatchworkIsland.dll`, and boots the
  exported binary. A `windows-latest` job runs the pure suite on Windows.

Architecture: small extractions only — `SaveResult.cs` (result/policy/session
state) and `MissionContentConsistency.cs`; no framework or mission-engine
rewrite. Existing Core/Build/Player/Vehicle/World/UI/Main boundaries remain.

Automated verification:

```text
$ dotnet build PatchworkIsland.sln
Build succeeded. 0 Warning(s), 0 Error(s)

$ dotnet run --project tests/PatchworkIsland.Tests.csproj
RESULT: 61 passed, 0 failed

$ godot --headless --path . --editor --quit     -> exit 0
$ godot --headless --path . --quit-after 120    -> exit 0
$ godot --headless --path . -- --selftest       -> 42 passed, 0 failed, exit 0
$ godot --headless --path . -- --selftest-resume -> 10 passed, 0 failed, exit 0
```

New pure regressions include: M10 pickup/save/reload, later mission with a chair
unplaced, M10 completion still requiring placements, long M05/M09 boundaries,
race saturation/round-trip, exact bonus boundaries, invalid bonus rejection,
unknown swing parts, furniture purchase caps, save-feedback truth/throttling,
New Game persisted-state flags, cross-platform write failure/retry, and
complete mission-file comparison with negative mutations.

Real-input GUI evidence (Xvfb 1280×720, `--dev`):

- **Scenario A (M10):** loaded a valid all-complete save; entered the house,
  opened furniture mode, picked up a chair, saved manually, quit and relaunched.
  Continue restored the chair as owned-but-unplaced; it was placed on pad
  `house4`, saved and verified in JSON. Evidence `p81_00`–`p81_08`.
- **Scenario B (long race):** loaded a controlled save with M05 active,
  `checkpoint=4`, `elapsedMs=3,960,000` (66 minutes), no M05/M06 completion.
  Continue completed M05 on load (+15), then M06 was played normally with real
  movement/pickups (+15). Manual save succeeded with the long counter still
  present; quit/relaunch restored both completions, `elapsedMs=3960000` and 130
  coins. The 66-minute value was injected for a deterministic boundary test; no
  real-time 66-minute session is claimed. Evidence `p81_10`–`p81_20`.
- **Save failure/retry:** with the save directory read-only, **Save now** showed
  **Save failed** and the exact access error, previous file unchanged; restoring
  permissions and retrying showed **Paused — Saved**. Evidence `p81_21`,
  `p81_22`, `p81_27`.
- **New Game failure:** with writes denied, confirmed New Game reset memory to
  0 coins but kept the 9-job/130-coin file on disk and reported the failure.
  Quit then offered **Quit without saving?**; choosing it preserved the old file,
  and relaunch/Continue restored that old save. Evidence `p81_23`–`p81_26`.

Save compatibility: schema remains version 1 and no new required fields were
added. A synthetic v1.0.0-shaped all-complete save loaded in the real GUI; the
long-race seed also loaded and saved. Newer-save preservation, corrupt-save
backup recovery and exactly-once rewards remain covered by the retained tests.

Exports and release verification are recorded in the final v1.0.1 section below.

## v1.0.1 release verification — PASS

- PR **#11** (`P8.1 — Reliability hardening and v1.0.1`) merged as
  **`cb60a80`**. The release-evidence documentation commit is merged separately
  before tagging; no code changes are made after the verified merge.
- Project version and Windows file/product metadata: **1.0.1** / **1.0.1.0**.
- Final checks at the merged commit:

```text
$ dotnet build PatchworkIsland.sln
Build succeeded. 0 Warning(s), 0 Error(s)
$ dotnet run --project tests/PatchworkIsland.Tests.csproj
RESULT: 61 passed, 0 failed
$ godot --headless --path . --editor --quit            -> exit 0
$ godot --headless --path . --quit-after 120           -> exit 0
$ godot --headless --path . -- --selftest              -> 42 passed, 0 failed
$ godot --headless --path . -- --selftest-resume       -> 10 passed, 0 failed
$ ./dist/linux/PatchworkIsland.x86_64 --headless --quit-after 120 -> exit 0
$ ./dist/linux/PatchworkIsland.x86_64 --headless -- --selftest     -> 42 passed, 0 failed
$ ./dist/linux/PatchworkIsland.x86_64 --headless -- --selftest-resume -> 10 passed, 0 failed
```

Exports (from merged `cb60a80`, matching Godot 4.7.2 .NET templates):

```text
dist/windows/PatchworkIsland.exe                 190,007,768 bytes  PE32+ x86-64
dist/linux/PatchworkIsland.x86_64                154,182,328 bytes  ELF x86-64
dist/PatchworkIsland-1.0.1-Windows-x86_64.zip     72,586,136 bytes
dist/PatchworkIsland-1.0.1-Linux-x86_64.zip       62,193,946 bytes
```

Both executables contain `data/missions.json`, `MISSIONS.json`,
`PatchworkIsland.dll`, the self-contained .NET 8 runtime pack and version
`1.0.1`. SHA-256:

```text
c0a3a547764f67edb5d7c7b978b478f502e9dd1057b5b2e4e2cdac39daa1e13f  windows/PatchworkIsland.exe
d68ad966c3f0fd0161467ba0cdff08f7d46d3b98f1353883bdf915360dd7fe17  linux/PatchworkIsland.x86_64
aa78df480ef5690a01973623a25405dc5147e33144561c05cc93ecf66354d4f5  PatchworkIsland-1.0.1-Windows-x86_64.zip
37802585dd177a26b2dd82d273bb1e4d5e90e2ed88dabca2ec602f353a59ec1c  PatchworkIsland-1.0.1-Linux-x86_64.zip
```

`sha256sum -c SHA256SUMS` passed for all four files.

- Linux exported build launched under Xvfb/Mesa llvmpipe: title with Continue,
  existing 9-job save, 130 coins and the job board loaded
  (`evidence/p81_30_v101_export_title.png`,
  `evidence/p81_31_v101_export_jobs.png`). The packaged binary also passed the
  two runtime phases headlessly, proving the selftest/assets survive export.
- F12 in the normal exported build produced no screenshot (23 files before and
  after), confirming the developer gate. Developer screenshots are available
  only with an explicit development argument.
- Windows x64 export: **PASS**. PE32+ x86-64, self-contained .NET runtime pack,
  embedded mission content and `PatchworkIsland.dll` present; version resource
  `1.0.1.0`. **Windows startup: NOT TESTED** — no Windows runtime or Wine is
  available. Static inspection is not runtime verification.
- Distributables: `dist/PatchworkIsland-1.0.1-Windows-x86_64.zip`,
  `dist/PatchworkIsland-1.0.1-Linux-x86_64.zip`, `dist/SHA256SUMS`,
  `dist/README.txt`.
- Remaining limitations unchanged: no bundled audio, fixed text scaling,
  Windows startup unverified, GPU performance and child usability untested.
  The practice race remains reward-free and intentionally unsaved.

## P8.2 — Session recovery hardening (v1.0.2) — PASS

Branch `p8.2-session-recovery`, baseline `e2aa46fb8a974c8f49dc88a73b2fc89538d8b8e3`
(`v1.0.1`, PRs #11/#12). Baseline reproduced: 0 warnings/0 errors, 61/61 pure
tests, 42 + 10 runtime assertions, editor import and headless boot exit 0.

Confirmed defects before fixing:

- `ContinueAdventure()` always called `StartAdventure()` on the current `_data`.
  After a failed New Game and Return to title → Return anyway, the title still
  offered Continue for the old file, but Continue resumed the fresh in-memory
  session. The startup notice promised the persisted village.
- `ResetSession()` could only pass `allowNewerReplacement: true` on its first
  save. A failed initial replacement left `NewerSavePresent` true while every
  later manual/automatic save was refused.
- Settings saves shared the automatic-failure throttle. A settings failure
  could be hidden behind its modal and suppress the next autosave warning.
- `MissionContentConsistency` assigned object properties into a dictionary,
  silently collapsing duplicate JSON keys.
- `CompleteActive(5)` accepted a race bonus without checking the persisted
  `elapsedMs`; a slow race could be awarded +5 by a direct caller.
- The discard notice always said Continue loads the saved village, even when no
  save existed or a newer save was protected and Continue was unavailable.

Fail-first evidence (temporary restoration of the old behaviour, then restored):

```text
FAIL  Continue requested a persisted-session reload
RUNTIME RESULT: 15 passed, 1 failed

FAIL  failed replacement keeps the confirmed permission for Save Now
FAIL  second confirmed replacement regains retry permission
FAIL  Save Now retries and replaces the newer save
... 12 failures ...
RUNTIME RESULT: 75 passed, 12 failed

FAIL  mission consistency rejects duplicate object properties: duplicate top-level property rejected
RESULT: 66 passed, 1 failed

FAIL  race bonuses are verified against the persisted race clock: M05 rejects a bonus on a slow race
RESULT: 66 passed, 1 failed

FAIL  settings failures are visible without suppressing autosave warnings: settings message separates memory from disk
RESULT: 66 passed, 1 failed
```

Implemented:

- `SessionSaveState` now tracks `MemoryMatchesPersisted` independently from
  `CanContinue`/`HasSaveFile`, plus session-scoped
  `NewerReplacementAuthorized`. Failed writes mark memory unsaved; discarding
  clears memory/authorisation; success clears newer protection and permission.
- `ContinueAdventure()` reloads the complete Godot scene when the current memory
  is not the persisted session. This rebuilds builders, pickups, world effects,
  player/van snapshots, driving state and camera from disk rather than swapping
  a `GameData` reference under stale scene objects.
- New Game confirmation authorises newer-save replacement for that session.
  The permission survives a failed write so Save Now and autosave may retry;
  cancellation never grants it, and discard/load/success clear it.
- Settings use a separate failure throttle and inline modal status. A failed
  toggle applies in memory but reports that it was not persisted; success
  replaces the message. Autosave keeps its own first-failure warning.
- `MissionContentConsistency` rejects duplicate object keys at every depth with
  JSON path/property diagnostics.
- `CompleteActive` verifies optional bonuses against persisted race
  milliseconds, preserving 90 s M05 / 75 s M09 boundaries and round-up.
- Added a path-specific process-local `SaveService` failure seam for tests. It
  is internal, never enabled by normal gameplay, and leaves bytes untouched.
- Discard notices now distinguish a valid old save, a protected newer save and
  no save at all. A no-save Start adventure resets rather than reusing discarded
  memory.

Automated verification at merged code commit `211c928`:

```text
$ dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
$ dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 67 passed, 0 failed
$ godot --headless --path . --editor --quit                -> exit 0
$ godot --headless --path . --quit-after 120               -> exit 0
$ godot --headless --path . -- --selftest                  -> 42 passed, 0 failed
$ godot --headless --path . -- --selftest-resume           -> 10 passed, 0 failed
$ godot --headless --path . -- --selftest-recovery         -> 98 passed, 0 failed
```

GitHub Actions run `42500008863` passed the Linux build/pure/runtime/export/
exported-boot job and the Windows pure-test job (67 tests).

The recovery integration phase uses `user://selftest-recovery-save.json`,
injects deterministic I/O failures, reloads the real scene eight times, and
covers: failed New Game → Continue, discarded movement → Continue, successful
New Game → Continue, no-save first failure, recovered backup, settings failure
and autosave warning independence, newer-save cancellation/failure/discard/
retry/preserved-copy/relaunch, and exact on-disk byte preservation.

Full ten-mission walkthrough was not repeated in P8.2; the retained v1.0.0/P8
walkthrough remains the gameplay baseline plus targeted real-input and runtime
regressions. No P8.1 regression was found.

## v1.0.2 release verification — PASS

- P8.2 code PR **#13** merged as **`211c928`**. Final checks at that commit:
  0 warnings/0 errors, 67 pure tests, editor/boot exit 0, runtime
  **42 + 10 + 98**.
- Project version `1.0.2`; Windows file/product version `1.0.2.0`.
- Exports built from the release tree with Godot 4.7.2 .NET templates:

```text
dist/windows/PatchworkIsland.exe                  190,050,088 bytes  PE32+ x86-64
dist/linux/PatchworkIsland.x86_64                 154,224,648 bytes  ELF x86-64
dist/PatchworkIsland-1.0.2-Windows-x86_64.zip       72,595,458 bytes
dist/PatchworkIsland-1.0.2-Linux-x86_64.zip         62,221,666 bytes
```

Both executables contain `data/missions.json`, `MISSIONS.json`,
`PatchworkIsland.dll`, the self-contained .NET 8 runtime pack and version
`1.0.2`. SHA-256:

```text
044e3e61320f4012a85096d3d4431e649fea9dda70f7416908253fc5a5014c8d  windows/PatchworkIsland.exe
4a95377b5bf88bc20bdedd35c1307286470b53279a9166ab281bb5423a5dcac5  linux/PatchworkIsland.x86_64
bb4eb027ef80327ede43ce0931f3cbce94cfc60bfb6eec22de9416b12ebcc846  PatchworkIsland-1.0.2-Windows-x86_64.zip
2650ae5dd4b8ecedab766265cd23512084e5e3d3b81fcfe3cfdc7b3e6f45df9e  PatchworkIsland-1.0.2-Linux-x86_64.zip
```

`sha256sum -c SHA256SUMS` passed for all four files.

- Linux exported binary:
  - `--headless --quit-after 120` exit 0.
  - `--selftest` 42 passed, 0 failed.
  - `--selftest-resume` 10 passed, 0 failed.
  - `--selftest-recovery` 98 passed, 0 failed.
  - Launched under Xvfb/Mesa llvmpipe: title with Continue, existing
    nine-job/130-coin save and full DONE job board
    (`evidence/p82_00_v102_export_title.png`,
    `evidence/p82_01_v102_export_jobs.png`).
  - F12 in the normal export produced no screenshot (23 files before/after).
- Windows x64 export: **PASS**; PE32+ x86-64, embedded mission content,
  assemblies and self-contained .NET 8 runtime, version `1.0.2.0`.
  **Windows startup: NOT TESTED** — no Windows runtime or Wine.
- Distributables: `dist/PatchworkIsland-1.0.2-Windows-x86_64.zip`,
  `dist/PatchworkIsland-1.0.2-Linux-x86_64.zip`, `dist/SHA256SUMS`,
  `dist/README.txt`.
- Remaining limitations unchanged: no bundled audio, fixed text scaling,
  Windows startup unverified, GPU performance and child usability untested.
  Practice racing remains reward-free and intentionally unsaved.

## P9 — Character choice, clothes shop and outfit customisation (v1.1.0)

**Status: implementation complete, including four review corrections; local
build, pure tests, runtime tests and graphical capture PASS.** Project/export
metadata is `1.1.0`; the save schema is **2** and is versioned separately from
the release.

### Scope and design

- Added `CharacterCustomisation` as a Godot-free domain (`src/Core/CharacterCustomisation.cs`)
  with two presets (`boy`, `girl`), five outfits (free Everyday plus four
  purchasable shared outfits), ownership/equip rules, exact coin deduction and
  shared colour/style definitions.
- Extended `GameData` with `CharacterPreset`, `OwnedOutfits` and
  `EquippedOutfit` and moved the persistence contract to **schema 2**. Schema 1
  saves migrate with every progression field intact; missing customisation
  fields default to **Boy + Everyday + no purchases**. Schema 3 and newer are
  rejected without modification, and ordinary saves cannot overwrite them.
  Explicit New Game replacement and `newer-vN` backup preservation remain.
- `SaveValidation` rejects unknown presets/outfits, duplicate ownership records,
  and equipping an outfit that is not owned. The starting outfit is always
  implicitly owned.
- New Game now opens a Boy/Girl chooser before the village starts. The choice
  is stored in the save; New Game reset clears purchases and the wallet.
- Added a real town clothes shop at `(24, 7)` on the north side of the main
  road, east of the market: `CLOTHES` sign, shirt icon, striped awning,
  `DRESS UP` sign, path, two mannequins and an `E` interaction. It is not a
  mission-gated building.
- Added `ClothesShopUi`: wallet, five outfit rows with prices, owned/wearing
  state, disabled insufficient-funds states, Buy/Wear actions, stylised 2D
  paper-doll preview (`CharacterDoll`) and an optional Boy/Girl switch.
- Buying deducts coins exactly once, records ownership and immediately wears
  the outfit; wearing an owned outfit is free. Purchases and preset changes
  save immediately through the existing `SaveGame` pipeline with a
  `Customisation` save reason.
- Player presentation uses modular primitive parts, not a rig rewrite: the girl
  has long hair/back locks, bow, dress, skirt hem and shoes; the boy has short
  hair, shirt, shorts and shoes. Both keep the existing walk/jump animation
  pivots and the M01 red-cap reward.

### Review corrections

1. **Forward-compatible saves.** `SaveService.CurrentVersion` is now 2.
   `Migrate` normalises schema 1 into schema 2 and fills the customisation
   defaults without touching progression. `ReadFile` reads `SchemaVersion`
   independently through `JsonDocument` before typed deserialisation, so a
   newer save whose other fields have future types is still classified as
   `UnsupportedNewer` rather than corruption. Loading schema 3/99 returns
   `UnsupportedNewer` without renaming, quarantining or rewriting the file.
   `SaveService.Save` now refuses to overwrite an unsupported newer save unless
   its explicit `allowNewerReplacement` flag is set; `Main.SaveGame` only sets
   that after the session's confirmed New Game / Save Now retry. The old backup
   and `newer-vN` preservation paths are unchanged.
2. **Preview every outfit.** Each row has an always-enabled **Preview** button
   (plus hover/focus preview). The mirror shows the selected outfit, price,
   wallet balance, owned/wearing state and **Not enough coins** when relevant.
   Buy/Wear availability is separate, so preview works when buying is disabled.
   Preview never mutates `Coins`, `OwnedOutfits` or `EquippedOutfit`.
3. **Real character buttons in runtime tests.** The chooser's actual
   `Choose_boy`, `Choose_girl` and `CharacterCancel` buttons are pressed. Tests
   cover Boy and Girl appearance, on-disk preset, Continue restoration, fresh
   cancellation and cancellation during replacement of an existing save. The
   replacement confirmation itself is also pressed through `ModalConfirm`.
4. **Save feedback correction.** A failed clothes-shop save sets the inline
   modal error; the next successful clothes save calls `ClearModalStatus()` so
   the stale error cannot survive. The runtime test injects a write failure,
   verifies the error, clears the failure, saves successfully and asserts the
   error is gone.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll  -> 78 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
godot --headless --path . -- --selftest                  -> 103 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 19 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
xvfb-run -a godot --path . -- --dev --capture            -> P9 screenshots written
godot --export-release "Linux" ...                       -> exit 0
godot --export-release "Windows Desktop" ...             -> exit 0
dist/linux/PatchworkIsland.x86_64 --headless --quit-after 120 -> exit 0
dist/linux/PatchworkIsland.x86_64 (three runtime phases) -> 103 + 19 + 101 passed
```

The exported Windows executable is PE32+ x86-64 with file/product version
`1.1.0.0`. The Linux exported binary passed the same three runtime phases from
the packaged payload.

Pure additions cover: fresh defaults, starting ownership, price deduction,
insufficient funds, duplicate purchase, free equip, unowned equip rejection,
preset switching, save/load round-trip, older saves without customisation
fields, invalid customisation rejection, new-game reset semantics and exact
mission reward accounting after a clothing purchase.

Runtime additions cover: the real chooser, Girl appearance (dress/long hair),
opening the real shop, pressing the actual Buy button, wallet display update,
insufficient/duplicate/unowned refusals, pressing the actual Wear button to
switch outfits, save while driving, Continue restoring the Girl preset and
Sunny Day outfit, and a post-reload New Game reset to Boy/Everyday/0 coins.
The recovery seed now carries a Girl/Berry appearance and asserts it after a
real scene reload.

### Fail-first evidence

Two controlled probes were run against the completed tests and then reverted:

- Disabling the insufficient-funds guard produced
  `FAIL insufficient coins cannot buy an outfit` and `RESULT: 76 passed, 1 failed`.
- Disabling the duplicate-ownership guard produced
  `FAIL job rewards stay exact after buying clothes` and
  `RESULT: 76 passed, 1 failed`.

Both probes were restored, rebuilt and re-run green at **77 passed, 0 failed**;
the review additions then raised the final total to **78 passed, 0 failed**.
A C# compile error from an invalid `Polygon2D.MouseFilter` usage in the first
paper-doll draft was also caught by the build and fixed.

### Evidence / graphical verification

Captured with the real Godot scene under Xvfb/Mesa llvmpipe at 1280×720:

- `evidence/p9_01_character_choice.png` — Boy/Girl chooser with both paper dolls.
- `evidence/p9_02_girl_at_clothes_shop.png` — girl in-world at the CLOTHES shop.
- `evidence/p9_03_clothes_shop_ui.png` — shop modal with enabled Preview controls, prices, wallet, owned/insufficient states and the changing room.
- `evidence/p9_04_outfit_owned_and_worn.png` — actual Buy feedback, wallet 2, Sunny Day worn, all rows visible.
- `evidence/p9_05_girl_sunny_outfit_world.png` — yellow Sunny Day dress in the world.
- `evidence/p9_06_boy_default_outfit_world.png` — matching boy presentation.

The first capture round exposed a clipped chooser footer/cancel button and a
shop status message below the scroll fold. The chooser was compacted (Cancel at
the top) and the shop status moved above the outfit rows. The review preview
round then exposed a clipped changing-room footer; the preview was compacted so
the price/wallet line, character buttons and every outfit row fit at 1280×720
without losing the mirror. The modal still scrolls if the window is shorter.

### Remaining limitations

- In-shop preview is a deliberately simple 2D paper doll, not a live 3D
  turntable; the in-world player is the authoritative 3D appearance.
- No skeletal rig or custom animation was added; the existing walk/jump motion
  is retained.
- Child playtesting and real-GPU performance testing remain unavailable.
- Windows startup remains untested (no Windows runtime/Wine). v1.1.0 was
  exported for Linux and Windows; the Linux packaged binary passed all runtime
  phases, while the Windows binary was inspected but not launched.

### Delivery

- Branch: `p9-character-customisation`
- Pull request: [#15 P9 — Character choice, clothes shop and outfit customisation](https://github.com/PhilipGB/patchwork-island/pull/15)
- GitHub Actions on the PR: **build-test PASS** (build, pure tests, import,
  boot, runtime phases, Linux export/boot) and **windows-tests PASS**.
- Source/package version: **1.1.0** (`project.godot`, `export_presets.cfg` and
  the Windows file/product version all report 1.1.0).

### Review follow-up delivery

- Commit `e602f14` on `p9-character-customisation`: schema 2 migration,
  schema 3/99 refusal, always-enabled outfit previews, real Boy/Girl/Cancel
  runtime tests and stale clothes-save error clearing.
- Final verification: **78** pure tests, **103 + 19 + 101** runtime assertions,
  v1.1.0 Linux export boot and packaged runtime phases, and a v1.1.0 Windows
  executable reporting file/product version `1.1.0.0`.
- GitHub Actions on the updated PR: **build-test PASS** and
  **windows-tests PASS**.
- Review corrections are recorded in `ACCEPTANCE.md` and
  `evidence/p9_verification.txt`.

### P2 schema-preflight correction

- Commit `1ac8a8c`: `ReadFile` now reads `SchemaVersion` with `JsonDocument`
  before deserialising into `GameData`. A future save that changes an existing
  field's type is therefore still `UnsupportedNewer`, not corruption.
- Regression fixtures for schema 3 and 99 now use incompatible values
  (`Coins` as a string, `CharacterPreset` as an array, `Player.X` as a string).
  Load and ordinary save leave the files byte-for-byte untouched.
- Verification after the change: **78** pure tests and **103 + 19 + 101**
  runtime assertions pass; the packaged Linux binary passes all three runtime
  phases. GitHub Actions: **build-test PASS**, **windows-tests PASS**.

## P10 — Original audio and real sound settings (v1.2.0)

**Branch:** `p10-original-audio`

### Delivered

- Imported all four supplied original OGG loops (`title`, `village`, `rally`,
  `festival`) and all 28 supplied WAV effects into `assets/audio/`. Music import
  files loop at offset 0; the two engine WAV loops use forward looping.
- Added `AudioCueCatalog` (pure, tested) and `AudioManager` (Godot). The manager
  creates `Music`, `SFX`, `UI` and `Vehicles` buses, keeps one music player with
  fade transitions and uses a bounded 14-voice SFX pool plus six spatial voices.
- Music contexts now follow title → roam → race/practice → roam and are saved by
  the existing settings. The Music toggle mutes only `Music`; the Sound toggle
  mutes `SFX`, `UI` and `Vehicles`. Settings are applied from disk before the
  title screen and again after Continue/New Game.
- Real triggers are wired for UI confirm/cancel/error/hover, pickups, shop
  purchases, clothing/equip, building, mission completion, chapter unlock,
  race checkpoints/finish, jumps/landings and surface-aware footsteps.
- The player van has real 3D idle and drive engine loops whose volume/pitch are
  driven by measured speed, on the `Vehicles` bus.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 82 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0, audio imported
godot --headless --path . -- --selftest                  -> 115 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . --quit-after 120               -> exit 0
xvfb-run godot --path . --quit-after 120                 -> exit 0 (dummy audio)
```

Runtime coverage now proves title music loads and its playback position
advances, the four buses exist, Music/SFX toggles mute exactly the intended
buses, saved mute preferences are applied after a real relaunch, real UI button
presses emit the UI cue, and the van starts/stops engine audio with the vehicle.
An audio device is not exposed in this environment (`/dev/snd` exists but ALSA
and PulseAudio cannot open a device), so audible speaker verification remains
**NOT TESTED** here; structural load/playback checks are **PASS**.

### Notes

- Supplied audio provenance is recorded in `docs/ASSET_USAGE.md`; all files are
  original synthetic assets generated for this project.
- Audio assets are included by the default export filter and will be checked
  again inside the packaged Linux binary during P15.

## P11 — Villagers and safe NPC traffic (v1.2.0)

**Branch:** `p11-living-village`

### Delivered

- Six named villagers (Milo, Ada, Pip, Fern, Ollie, Ivy) with distinct procedural
  paper-doll bodies, role clothes/hair/accessories, supplied PNG portraits,
  large billboard name labels, idle/walk animation and verified patrol loops.
- Generic conversations use the supplied portrait, a short role greeting and a
  readable "Tell me about the village" fact. The NPC stops while talking and
  resumes afterwards; dialogue is fully playable with sound off.
- Two active free-roam NPC drivers: Milo's post scooter on the west main road
  and Ivy's compact car on the east road. Both use closed authored waypoint
  loops, capped acceleration/turn speed, no player/van collision (layer 0),
  forward yield sensing, spatial engine loops and a rate-limited horn.
- Third `rally_rival` definition is authored for M19 only (no free-roam spawn).
- Pure route validation proves every waypoint is on the authored road network,
  every patrol is outside building footprints and every NPC/traffic waypoint
  keeps clear of critical job stations.
- New `--selftest-living` phase runs 24 seconds of real physics, then exercises
  yield/resume for both drivers and re-checks villager/station clearance.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 84 passed, 0 failed
godot --headless --path . -- --selftest                  -> 158 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
```

Runtime coverage proves six villagers exist with readable labels, real E opens
Ada's conversation and stops her patrol, both traffic routes move, the scooter
yields/stops/resumes for the player, villagers and traffic pause with the world,
and a 24-second drive keeps all traffic on the road without teleporting or
blocking a mission station.

### Visual evidence

Captured at 1280×720 under Xvfb/Mesa llvmpipe:
`p11_01_ada_market.png`, `p11_02_milo_post.png`, `p11_03_pip_workshop.png`,
`p11_04_fern_garden.png`, `p11_05_ollie_town_hall.png`,
`p11_06_ivy_green.png`, `p11_07_town_traffic.png`,
`p11_08_ada_dialogue.png` in the Godot user `dev-captures` directory.

### Notes

- The supplied GLB villager/vehicle models have world-space vertices in one
  shared mesh space rather than clean limb pivots, so this milestone uses
  procedural geometry matching the existing P9 player/van style. The supplied
  portraits are used in-world and in dialogue; mission icons will be used from
  P13. This decision is recorded in `docs/ASSET_USAGE.md`.

## P12 — World visual cleanup and expanded rally (v1.2.0)

**Branch:** `p12-rally-polish`

### Delivered

- Replaced the old five-rectangle rally meadow with a single closed
  Catmull-Rom road mesh (364 samples, ~206 m), welded shoulders, continuous
  edge lines/dashes, a connected bridge spur and one concave collision mesh.
- Preserved four-gate M05/M09 IDs, ordering, saved checkpoint semantics,
  `elapsedMs` and the 90/75-second optional bonus rules. The four gates moved
  to the new circuit; M19's six-gate layout is authored and hidden until M19.
- Gate number boards are billboard-readable from both sides; painted arrows now
  have a proper triangular head and point along the direction of travel.
- Removed the old paddock fence that crossed the new circuit, relocated trees
  that were within canopy distance of the road, moved the grandstand/flags into
  the infield and moved the start/finish signage clear of tarmac.
- Visual root-cause fixes: cloud/ocean shadows disabled, village road slabs get
  sub-millimetre height offsets, markings raised to a consistent proud plane,
  clothes-shop path raised, sign boards readable from the back.
- Added a real-physics autopilot verification mode (`--selftest-rally`) that
  drives the genuine van through both four-gate races, plus a software-renderer
  frame probe (`--perf-probe`).

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 86 passed, 0 failed
godot --headless --path . -- --selftest                  -> 161 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
```

Autopilot race times: **M05 18.98 s**, **M09 19.03 s**, both with four gates in
order and the existing optional bonus awarded. A part-completed M05 save with
`checkpoint=2` reloads with its active mission and timer preserved.

### Visual evidence

- 720p and true 1080p captures: `evidence/p12_720_*.png` and
  `evidence/p12_1080_*.png` (overview, rally overview, start line, gates 1/4,
  road join, clothes shop, village roads, town traffic, job board).
- Baseline before images: `evidence/p12_before_old_rally.png`,
  `evidence/p12_before_village_roads.png`.
- Reproducible defect list and root causes: `docs/VISUAL_QA.md`.

### Performance

Software-renderer (Mesa llvmpipe, **not** a desktop GPU claim): 1280×720 mean
86.25 ms / p95 97.15 ms; 1920×1080 mean 97.34 ms / p95 106.84 ms; 2581 nodes,
2997 draw calls, 60,647 primitives. The v1.1.0 baseline had no probe, so a
baseline frame-time comparison is **NOT TESTED**. Real Radeon 890M profiling is
**NOT TESTED** (hardware unavailable).

## P13 — Chapter two, part one: M11–M15 (v1.2.0)

**Branch:** `p13-next-chapter-1`

### Delivered

- Added M11–M15 to both mission sources and raised the catalogue integrity check
  from ten to twenty. M16–M20 remain authored and visible as **COMING SOON**;
  P13 marks only M11–M15 implemented so no player is offered an unfinished job.
- M11 **Meet the Neighbours:** three job-stock cards, readable villager names,
  wrong-name refusal with no card loss, three correct deliveries and a once-only
  12-coin reward.
- M12 **Bakery Busy Day:** separate bakery stock, three counted orders
  (2+1, 3+2, 1+3), stepper UI, wrong-count retry with no loss and a once-only
  14-coin reward. Filled bakery display slots persist in the world.
- M13 **Lost Library Books:** four unique physical books (`book_sun`,
  `book_moon`, `book_tree`, `book_star`), cover words, shelf matching, wrong-shelf
  retry with no book loss and a once-only 14-coin reward. Filled shelf slots
  persist visually.
- M14 **Road Sign Rescue:** three authored sockets, sign and arrow matching,
  removal/retry, permanent repaired sign boards free of traffic collision and a
  once-only 16-coin reward.
- M15 **Garden Patterns:** six pads separate from the M08 park pads, unlimited
  job seed stock, reversible colour correction, exact RED/BLUE pattern and a
  once-only 16-coin reward. All six flowers persist visually.
- Chapter-two job-board tabs (`Village jobs 1–10`, `New adventures 11–20`),
  new mission icons, chapter-two objective markers and progress rows. M12/M13
  branch from M11; M14 branches from M12; M15 branches from M13; M16 waits for
  both M14 and M15.
- Save schema **3** adds `FestivalSeen`. The original M10 `CelebrationSeen`
  keeps its old meaning and validation. Schema 1 and 2 saves migrate without
  losing coins, missions, pickups, player/van snapshots, outfits or settings.
- Fixed two P13 visual defects found by real captures: 128 px mission PNGs
  dwarfed 48 px SVG icons in world sprites, and the generic library pickup icon
  covered the large MOON/SUN cover word. See `docs/VISUAL_QA.md` V07/V08.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 97 passed, 0 failed
godot --headless --path . -- --selftest                  -> 161 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
godot --headless --path . -- --selftest-chapter          -> 217 passed, 0 failed
godot --headless --path . --editor --quit                -> PASS
godot --headless --path . --quit-after 120               -> PASS
```

Rally timings remained inside the original bonus boundaries: **M05 18.98 s**
and **M09 18.97 s**, both with the existing +5 optional bonus. The chapter
runtime phase starts a fresh village, builds the same valid world state as a
finished v1.1 save, then uses the real job-board station and buttons, real E
interactions, NPC dialogue buttons, book pickups, sign sockets and garden pads.
It deliberately saves after one M11 card and reloads the scene; phase two
continues from that save, finishes M11–M15, spends no personal coins on job
stock, and ends at 172 coins after exactly 12+14+14+16+16 reward coins. The
save file is re-read and checked for fifteen completions, five chapter-two
completions, correct wallet and `FestivalSeen=false`.

Pure coverage includes wrong card/order/shelf/sign/colour no-loss behavior,
objective counters, exactly-once rewards, branch unlocks, M12/M13 in either
order, M16 waiting for both M14 and M15, partial chapter save/reload, schema
1/2→3 migration, `CelebrationSeen` validation and the separate `FestivalSeen`
rule.

Fail-first mutation evidence (temporary local edits, immediately restored):

```text
SignPlacementMatches forced to accept every fit
  -> FAIL M14 validates sign and arrow...: wrong sign refused (95 passed, 1 failed)
HandWelcomeCard wrong-name guard disabled
  -> FAIL M11 grants cards once...: wrong card refused (95 passed, 1 failed)
Restored source -> 97 passed, 0 failed
```

### Visual evidence

- 720p and true 1080p captures: `evidence/p13_720_*.png` and
  `evidence/p13_1080_*.png` (chapter-two overview, bakery, library, sign
  sockets, community garden, MOON book, job-board tabs, Ada card dialogue,
  bakery/library/sign/garden UIs, repaired signs, planted garden).
- Capture command: `godot --path . -- --capture-p13` (add `--capture-1080` for
  the 1920×1080 set). Captures are real Xvfb/Mesa llvmpipe viewport captures.
- Real GPU and audible playback remain **NOT TESTED** (hardware unavailable).

### Notes

- M11–M15 use mission progress counters and `book_*` pickups only; the personal
  coin wallet is never a job-supply budget.
- The chapter-two runtime phase was added to CI as `--selftest-chapter` with a
  240-second timeout.

## P14 — Chapter two, part two: M16–M20 and festival (v1.2.0)

**Branch:** `p14-next-chapter-finale`

### Delivered

- M16 **The Delivery Round:** three parcels loaded at the post counter, a parked-van
  proximity check, wrong-recipient refusal with no parcel loss, once-only 20-coin
  reward and a persistent delivery-route board.
- M17 **Market Day Maths:** a separate 20-coin job purse, 2 bread + 1 milk + 2 apples,
  total 14 / change 6, reversible add/remove, no personal-wallet spend, once-only
  22-coin reward and persistent market stock.
- M18 **Mechanic's Toolbox:** four visible parts, inspect → two tyres → lamp →
  battery ordering, wrong-stage hints with no part loss, once-only 24-coin reward,
  and a persistent broken-to-repaired Pip car.
- M19 **The Grand Rally:** six ordered gates on the continuous circuit, friendly
  Pip rival, save/reload race clock, no mandatory time limit, optional 155-second
  medal (+5), once-only 25-coin base reward and a persistent podium/trophy.
  M05 (90 s) and M09 (75 s) four-gate rules remain unchanged.
- M20 **Village Festival:** three invitations → two tables → three lanterns,
  repeatable/undoable decoration placement, unlimited stage-safe job stock,
  once-only 30-coin reward, festival decorations, festival music when enabled and
  a separate `FestivalSeen` finale. The modal never reopens after Continue.
- Job board now marks M16–M20 implemented and shows “All 20 jobs complete — free
  roam unlocked!” when M20 is complete.
- Playtest follow-up for the reported stuck spots: added sloped collision skirts
  around the rally shoulders and a ramp at the bridge-to-connector seam. The new
  `--selftest-traversal` phase drives the van across the bridge at speed and in a
  slow pulsing approach, then walks/jumps from rest at the number-1 arches of both
  race layouts.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 104 passed, 0 failed
godot --headless --path . -- --selftest                  -> 161 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
godot --headless --path . -- --selftest-chapter          -> 217 passed, 0 failed
godot --headless --path . -- --selftest-finale           -> 140 passed, 0 failed
godot --headless --path . -- --selftest-traversal        -> 12 passed, 0 failed
godot --headless --path . --editor --quit                -> PASS
godot --headless --path . --quit-after 120               -> PASS
```

Rally timings: **M05 18.98 s**, **M09 18.92 s**, **M19 22.32 s**, each with the
existing +5 optional medal. The P14 finale phase reaches M19 through real UI and
physics, confirms the six-gate count, completes all 20 jobs at exactly 226 coins
(100 prepared + 20 + 22 + 24 + 30 + 30), re-reads the save, and confirms the
festival does not reopen after Continue. Pure coverage includes M16 wrong-recipient
no-loss, M17 zero-wallet/over-budget/refund paths, M18 wrong-stage no-loss,
M19 six-gate order and 155 s boundary, M20 stage ordering/undo, partial and full
save/reload, and a separate `FestivalSeen` rule.

### Visual evidence

- 720p and true 1080p captures: `evidence/p14_720_*.png` and
  `evidence/p14_1080_*.png` (delivery dialogue, post counter/board, market
  checkout and stock, broken/repaired Pip car, six-gate rally, festival setup
  and decor, all-20 job board, second festival finale, bridge approach, gate 1).
- Capture command: `godot --path . -- --capture-p14` (add `--capture-1080` for
  the 1920×1080 set).
- Real GPU and audible playback remain **NOT TESTED** (hardware unavailable).

### Notes

- The reported bridge/road and gate-1 catch had no discrete collider at the arch:
  the only nearby static body is the camera-only blocker on layer 16. The fix
  therefore targets the road/shoulder collision edges and adds explicit traversal
  regression coverage. If a playtest still reproduces the catch, the next useful
  evidence is the exact world position and approach direction.
- M20 keeps the original M10 `CelebrationSeen` untouched and uses only
  `FestivalSeen` for the second finale.
- All `PhysicsRayQueryParameters3D` / `PhysicsShapeQueryParameters3D` wrappers
  created by camera collision, build clicks, safe-exit checks and rally coverage
  are now disposed with `using`; the runtime phases exit 0 with zero leaked
  unsafe references instead of aborting during Mono shutdown.

## P15 — v1.2.0 release verification

**Branch:** `p15-v1.2-release` | **Base:** main `570e667` (P14, PR #20)

### Delivered

- Version metadata advanced to project `1.2.0` and Windows file/product
  `1.2.0.0` with description `Patchwork Island - Living Village Update`.
  `handoffs/*` is now excluded from both export presets.
- New `--selftest-release` runtime phase: writes a schema 2 / v1.1.0
  ten-complete save, reloads through the real scene, Continues at the title,
  and verifies 137 coins, girl + Berry outfit, red cap, bridge/park/furniture
  state, `CelebrationSeen=true`, `FestivalSeen=false`, M10 complete, M11
  available and M12 still locked. It then opens the real job board, starts M11,
  confirms stock grant, writes schema 3, and re-reads wallet/active mission.
  Wired into Linux CI.
- Pure tests now cover a complete clean-save M01–M20 walkthrough and a schema 2
  ten-complete migration that preserves possessions, settings and snapshots and
  unlocks M11.
- v1.2.0 Linux and Windows exports built from this source, packaged with a
  player `README.txt`, and zipped. Hashes recorded in `dist/SHA256SUMS` and
  `evidence/p15_verification.txt`.
- Release docs updated: README, README_RUN, ACCEPTANCE v1.2 gate,
  `docs/RELEASE_NOTES_v1.2.0.md`, `docs/SAVE_COMPATIBILITY.md`,
  `docs/VISUAL_QA.md` P14 final captures.

### Verification (source, exact P15 tree)

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 106 passed, 0 failed
godot --headless --path . -- --selftest                  -> 161 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
godot --headless --path . -- --selftest-chapter          -> 217 passed, 0 failed
godot --headless --path . -- --selftest-finale           -> 140 passed, 0 failed
godot --headless --path . -- --selftest-traversal        -> 12 passed, 0 failed
godot --headless --path . -- --selftest-release          -> 22 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
```

Rally timings on the final source pass: **M05 18.98 s**, **M09 18.99 s**,
**M19 22.31 s**, each with the optional +5 medal. Every runtime process exited 0
with zero leaked unsafe references.

### Real GPU and audio (this machine)

- Renderer: OpenGL 4.6, Mesa 26.0.8, **AMD Radeon 890M Graphics**
  (radeonsi, strix1, ACO, DRM 3.64).
- 1280×720: mean 16.69 ms, p95 17.35 ms, max 40.35 ms.
- 1920×1080: mean 16.67 ms, p95 17.28 ms, max 40.50 ms.
- 600 frames per resolution; 2950 nodes, 3399 draw calls, 66619 primitives.
- No v1.1.0 real-GPU probe exists, so a baseline comparison is **NOT TESTED**.
- Real Radeon P14 captures: `evidence/p15_radeon_00..14_*.png`.
- Audio: packaged Linux ran against PipeWire/PulseAudio
  (`alsa_output.pci-0000_c4_00.6.analog-stereo.monitor`). A 20.14 s
  `parecord` capture had peak 20165 / RMS 944.0 with nonzero PCM from 2 s on;
  6 s excerpt at `evidence/p15_packaged_audio_monitor_6s.wav`.
- **Human listening was not possible in this environment.** Subjective audio
  audition remains **NOT TESTED**.

### Packaged builds

- Linux: ELF 64-bit x86-64. Headless boot exit 0; packaged `--selftest` 161,
  rally 10, finale 140, traversal 12, release 22; all exit 0, zero leaked refs.
  Packaged rally times M05 18.97 s / M09 18.98 s; M19 22.38 s.
- Windows: PE32+ GUI x86-64, file/product `1.2.0.0`. Export succeeded.
  **Windows startup is NOT TESTED** (no Windows runtime or Wine).
- SHA-256 (`dist/`):
  - `linux/PatchworkIsland.x86_64` `76611594db25a62d8964afcaf7579d630a241a3bf2999f38023b32d5756a05d0`
  - `windows/PatchworkIsland.exe` `28da892a43dbbf0dd443916c89ee0a6e51e0f40e9e8224745c25b365a96112f2`
  - `PatchworkIsland-1.2.0-Linux-x86_64.zip` `93682196484c97bd57ba846d3ab67e126bba29af8f12fc0e459956895d3814dd`
  - `PatchworkIsland-1.2.0-Windows-x86_64.zip` `005730ff679e19e9339ec84fc7e3a1c71fc9049bca9074ae4740e2bd7706f6a8`
- Local `dist/` remains gitignored; archives are not committed.

### Publication blockers

1. Subjective human listening of the packaged audio has not been performed.
2. Windows startup has not been tested.

No `v1.2.0` tag was created.

### Exact-head record

- Feature/release verification commit: `4f78d2388859dbb2556b7db287699418d4c029b3`.
- PR: #21 (`P15 — v1.2.0 release verification`), based on `570e667`.
- CI at that exact head: run
  [42702983510](https://github.com/PhilipGB/patchwork-island/actions/runs/42702983510)
  — `build-test` **pass** (5m12s), `windows-tests` **pass** (49s).
- Final PR head: `21a0978cade7c79f4f94a53b8f5c3187e70de785`. CI rerun:
  [42703922861](https://github.com/PhilipGB/patchwork-island/actions/runs/42703922861)
  — `build-test` **pass** (5m38s), `windows-tests` **pass** (52s).
- P15 merged to `main` as `cd54d7a` (PR #21, merge commit).
- Tag/publish decision: **not tagged**. The two publication blockers above
  remain untested, so v1.2.0 is not published as fully verified.

## P15.1 — bridge seam and visual hotfix

**Branch:** `fix-bridge-seam-visuals`, based on main `ed81e64` (post-P15).

The 2026-11-09 playtest reported that the van crossed the bridge but still hit
an invisible obstacle, with a grey slab/floating rail at the join and a cap
that appeared to hover.

### Root causes and fixes

- The east road box began at `x=49`, 1.45 m inside the bridge deck, and the
  rally connector's ramp started at `x=48.1`. Both painted a grey surface over
  the wooden planks and left a raised collision edge. The east road now starts
  at `x=50.5`; the connector starts at `x=50.4` with the ramp removed; road and
  shoulder collision is 1 cm above the lawn (down from 6.5 cm).
- The P12 collision-coverage proof now checks the actual `RallyRoadCollision`
  body by name instead of assuming a raised hit (52/52 samples).
- Bank rocks at `(40.1, 0.55, 0)` and near the east exit are skipped; a dark
  deck underlay closes the 20 cm plank gaps; the side rails stop at the water
  span (`x=49`); the connector gained 0.8 m sandy shoulders; the translucent
  entrance glow discs were removed.
- The long-hair back now meets the lowered cap underside, removing the exposed
  skin strip that made the cap look detached.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 106 passed, 0 failed
--selftest                                               -> 161 passed, 0 failed
--selftest-resume                                        -> 21 passed, 0 failed
--selftest-recovery                                      -> 101 passed, 0 failed
--selftest-living                                        -> 238 passed, 0 failed
--selftest-rally                                         -> 10 passed, 0 failed
--selftest-chapter                                       -> 217 passed, 0 failed
--selftest-finale                                        -> 140 passed, 0 failed
--selftest-traversal                                     -> 18 passed, 0 failed
--selftest-release                                       -> 22 passed, 0 failed
```

Rally: M05 18.99 s, M09 18.97 s, M19 22.32 s, each +5. Every runtime process
exited 0 with zero leaked unsafe references. Traversal now covers a fast centre
crossing, a slow pulsing crossing, both lateral road edges, westbound travel, a
no-vertical-step assertion, walking/jumping at both gate-1 arches and a walk
from rest at the road lip.

### Evidence

- `evidence/p14_720_15_bridge_seam.png` and
  `evidence/p14_1080_15_bridge_seam.png` — driving viewpoint at the join.
- `evidence/p14_720_16_bridge_junction.png` and
  `evidence/p14_1080_16_bridge_junction.png` — overhead junction/deck.
- `evidence/p14_720_17_player_cap.png` and
  `evidence/p14_1080_17_player_cap.png` — seated cap from behind.
- The full P14 720p/1080p set was re-captured after the fix.

### Packaged rebuild

The Linux and Windows exports were rebuilt from the fixed tree (not reused from
P15):

```text
Linux  --headless --quit-after 120   -> exit 0
Linux  --selftest                    -> 161 passed, 0 failed
Linux  --selftest-rally              -> 10 passed, 0 failed
  M05 18.97 s +5, M09 18.98 s +5
Linux  --selftest-finale             -> 140 passed, 0 failed
  M19 22.33 s +5
Linux  --selftest-traversal          -> 18 passed, 0 failed
Linux  --selftest-release            -> 22 passed, 0 failed
```

Every packaged phase exited 0 with zero leaked unsafe references. A fresh
real-device monitor capture of the rebuilt Linux binary had peak 20165 / RMS
944.0; the 6 s excerpt replaced the old one.

New hashes:

- `linux/PatchworkIsland.x86_64` `f15ee255725f4c05b7352c423891c18d2a6b51fa7a722652cf56b4ed2c6c00f8`
- `windows/PatchworkIsland.exe` `24cea194d55ecb110216477f6a089a3455fad2876d0e6505e6a9ec4e48b00391`
- `PatchworkIsland-1.2.0-Linux-x86_64.zip` `79efdfe70bf546b7100c59cb053084f6364a928fe0c1b5419b0af080dc84d35a`
- `PatchworkIsland-1.2.0-Windows-x86_64.zip` `eeb111a520c4b15342e6d265f25e6e729b80bf0dc8e0365e6343df57b782e9a1`

### Release-gate status

- Packaged audio human listening: **PASS** (product-owner verified 2026-11-09).
- Windows startup: **NOT TESTED**, explicitly waived for the Linux-first
  release.
- v1.2.0 tagged at `d0eff9d` (merge of PR #23); its tree is identical to the
  green PR head `b7d3bb9`. Main CI run
  [42726477501](https://github.com/PhilipGB/patchwork-island/actions/runs/42726477501)
  passed.
- Published: [v1.2.0 release](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.2.0)
  with Linux/Windows ZIPs, `README.txt` and `SHA256SUMS`.

## P15.2 — M18 pickup persistence and traffic stop hotfix (v1.2.1)

**Branch:** `fix-m18-pickup-persistence`, based on main `c2b2e44`.

A post-release review found one P2 gameplay-state defect and one P3 hardening
gap.

### P2 — M18 parts respawned after save/reload

`TakeRepairPart` called `MissionService.CollectRepairPart(part)`, which only
incremented `has_<part>` and never recorded the physical pickup ID. A partial
M18 save therefore respawned the same tyre on Continue, allowing the same
pickup to supply both required tyres.

Fixes:

- `CollectRepairPart(part, pickupId)` now validates the ID against the part,
  refuses an already-collected ID and records it in `CollectedPickups` before
  incrementing the carried count. The real pickup passes `pickup.PickupId`.
- `FinaleRules.RepairPickupIds` / `RepairPickupPart` are the single source for
  the four IDs; `SpawnRepairParts` no longer builds IDs by string concatenation.
- `GameData.Normalise` repairs v1.2.0 partial saves by inferring acquired IDs
  from `has_* + *_fitted`. Already-recorded IDs win; the normaliser never
  over-marks and never invents more parts than were earned. Schema remains 3.

### P3 — off-island traffic kept processing

`TrafficCar` now has a terminal `Stopped` state and `StopEvents` counter. When a
vehicle falls below Y=-2 it zeroes speed/velocity, stops engine loops, disables
physics processing and logs once.

### Verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 107 passed, 0 failed
--selftest                                               -> 161 passed, 0 failed
--selftest-resume                                        -> 21 passed, 0 failed
--selftest-recovery                                      -> 101 passed, 0 failed
--selftest-living                                        -> 238 passed, 0 failed
--selftest-rally                                         -> 10 passed, 0 failed
--selftest-chapter                                       -> 217 passed, 0 failed
--selftest-hotfix                                        -> 48 passed, 0 failed
--selftest-finale                                        -> 140 passed, 0 failed
--selftest-traversal                                     -> 18 passed, 0 failed
--selftest-release                                       -> 22 passed, 0 failed
```

Runtime total **976**, every process exit 0 with zero leaked unsafe references.
Rally: M05 18.97 s, M09 18.98 s, M19 22.32 s, each +5. The new hotfix phase
saves mid-M18, reloads the real scene, proves the collected tyre is absent,
collects the remaining parts, completes the repair and checks the exactly-once
24-coin reward. It also drops a traffic car off-island and proves one stop event
and a stable resting position.

### Packaged v1.2.1

Linux and Windows exports were rebuilt with file/product version `1.2.1.0`.
Every packaged Linux phase passed, including `--selftest-hotfix` 48:

```text
boot 0, selftest 161, resume 21, recovery 101, living 238,
rally 10 (M05 18.97 s, M09 19.03 s), chapter 217, hotfix 48,
finale 140 (M19 22.32 s), traversal 18, release 22
```

All exit 0 with zero leaked unsafe references.

Hashes:

- `linux/PatchworkIsland.x86_64` `610e29c12067e8df90fba576e21c5ce941a41a88bdb1cdeaa31262f5c14ba6c1`
- `windows/PatchworkIsland.exe` `1421e456ce90dd5dadbb6451d536b34ecc71e2e1649fdee1f1b420c589308aba`
- `PatchworkIsland-1.2.1-Linux-x86_64.zip` `cb04e909db41ba19bb74199653c7c6018356e2904d4f6b092f8358aa1408fdf6`
- `PatchworkIsland-1.2.1-Windows-x86_64.zip` `ae40a90dfbb640b0cf61941831ce69b83800f2cade768668c4f93c34377d0ac5`

### Release-gate status

- M18 persistence defect fixed and covered by a real relaunch test.
- Off-island traffic stop covered by a one-shot terminal-state test.
- No save-schema, content, audio or rendering changes.
- Audio human listening remains PASS from v1.2.0; v1.2.1 was not separately
  auditioned.
- Windows startup remains NOT TESTED and waived for the Linux-first release.
- v1.2.0 remains published as the previous release.
- v1.2.1 tagged at `2d6647b` (merge of PR #25), whose tree is identical to the
  green PR head `49c7a62`. Main CI run
  [42735724391](https://github.com/PhilipGB/patchwork-island/actions/runs/42735724391)
  passed.
- Published: [v1.2.1 release](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.2.1)
  with Linux/Windows ZIPs, `README.txt` and `SHA256SUMS`.

## P16 — v1.3.0 scene-based town migration — PASS (parity migration)

**Branch/worktree:** no branch created; the additive asset kit was already
present in the v1.2.1 checkout. Runtime changes are uncommitted in this working
tree. No tag or release was created.

**Source baseline:** `2d6647b` (`v1.2.1` tree). **Source version:** `1.3.0`;
save schema stays **3**.

### Exact bake — executed successfully

```text
$ dotnet build PatchworkIsland.sln
Build succeeded. 0 Warning(s), 0 Error(s)

$ godot --headless --path . --editor --quit
exit 0

$ godot --headless --path . res://scenes/tools/SceneBakeTool.tscn
[SCENE BAKE] 32 reusable roots saved; 488 town roots; version source: 1.2.1.
[SCENE BAKE] OK: Town_Baked.tscn and reusable landmark parts written.
exit 0
```

Generated output: `scenes/world/baked/Town_Baked.tscn` (488 top-level roots),
32 reusable `parts/*.tscn`, 748 mesh instances, 59 static bodies and 59 collision
shapes. The baked rally part contains `RallyRoadCollision`.

### Runtime migration

- New `src/World/WorldSceneAdapter.cs` subclasses the retained procedural
  `WorldBuilder` data contract, instantiates `Town_Baked.tscn`, and exposes the
  exact existing gameplay handles/arrays to `Main` and the build systems.
- All **75** anchors in `GameplayAnchors.tscn` are cached and checked against the
  v1.2.1 coordinates. The adapter also checks all six building centres, yaws,
  wall heights/widths/depths, door anchors, swing frames, both race-gate sets,
  rally sample count/length, and every NPC/traffic waypoint against the authored
  catalogs.
- Missing or moved nodes fail loudly at load; there is no zero-coordinate or
  procedural fallback.
- `Main` now constructs `WorldSceneAdapter`. `WorldBuilder.Build()` remains only
  as the deterministic source passed to `SceneBakeTool`; it is not called at
  runtime.
- Dynamic bridge/swing/park/furniture/rally/finale state remains owned by the
  existing scripts and save model. No completed or partial state is baked as a
  permanent static duplicate.
- New `--selftest-restore` reloads seven real partial saves: M03 (5/7 bridge
  pieces), M07 (3 swing pieces), M08 (bench only), M10 (two furniture pieces),
  M15 (two planted pads plus completed M11–M14 visuals), M18 (one tyre, one
  collected pickup) and M20 (invites + one table + one lantern). It proves the
  scene world and all restored visuals, not just in-memory counters.
- `--selftest-release` now adds a real v1.2.1-shaped **158-coin** schema-3 save
  with bridge, swings, park, furniture, outfits, mission state and player/van
  transforms, reloads the scene and verifies exact restoration.

### Verification — source

```text
dotnet build PatchworkIsland.sln                   0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj   111 passed, 0 failed
godot --headless --path . --editor --quit          exit 0
godot --headless --path . --quit-after 120         exit 0, 0 unsafe leaks

--selftest                237 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
```

Source runtime total: **1124 passed, 0 failed**, every process exit 0 and zero
leaked unsafe references. The pure runner includes new scene/manifest tests for
all 75 anchors, all six building instances/wall dimensions, required dynamic
roots, rally collision and all NPC/traffic markers.

### Verification — packaged Linux

Final export (Godot 4.7.2 .NET, version `1.3.0`):

```text
dist/linux/PatchworkIsland.x86_64   157 MB
sha256 439083f34467e3bbfe7d1e93202745e246f2df68042ac903766e968096e2455a
```

Every packaged phase passed with the same counts as source (boot 0,
237 + 21 + 101 + 238 + 10 + 217 + 48 + 140 + 18 + 40 + 54 = **1124**),
exit 0, zero leaked unsafe references. `--quit-after 120` boot also exits 0.

### Performance — real AMD Radeon 890M, vsync 600 frames

v1.2.1 procedural baseline vs scene-backed build, same environment:

| Run | Resolution | Mean | p95 | Draw calls | Primitives |
|---|---|---|---|---|---|
| baseline | 1280×720 | 16.67–16.70 ms | 17.17–17.19 ms | 3394 | 66069 |
| scene | 1280×720 | 16.68–16.71 ms | 17.21–17.26 ms | 3394 | 66069 |
| baseline | 1920×1080 | 16.71 ms | 17.26 ms | 3394 | 66069 |
| scene | 1920×1080 | 16.68 ms | 17.21 ms | 3394 | 66069 |

Runtime node count rises from 2944 to 3087 (+143 marker/group nodes); rendered
draw calls and primitives are identical. No measurable GPU regression. Software
llvmpipe runs were also compared (same draw calls/primitives; ~5% mean delta at
1280×720 from the extra marker nodes).

### Visual verification

- 1280×720 tour: `p2_01_start`, `p2_02_village_overview` plus the full 18-shot
  P14 tour captured successfully.
- 1920×1080 P14 tour captured successfully (same 18 views).
- Captures copied to `evidence/v13_scene_world/`. The scene looks and behaves
  like v1.2.1; the known reference glitches (large foreground trees, close
  camera caps, sign occlusion, rally/through-road seams) are still present
  because this milestone intentionally preserves parity before polish.

### Remaining gaps / not claimed

- No automatic tag or release. Human visual/gameplay sign-off is still required.
- The optional v1.3.0 visual-polish pass (camera canopy fade, junction surfacing,
  sign occlusion) was deliberately **not** applied yet; parity came first.
- Windows export/startup was not produced or tested.
- One continuous human M01–M20 sitting remains **NOT TESTED**; the automated
  real-UI walkthrough and all mission rules remain green.
- GitHub CI has not run on a pushed commit; the exact CI command list was run
  locally, including the new `--selftest-restore` phase.

## P17 — v1.3.0 canonical editor-authoritative town — PASS

**Branch:** `v1.3.0-canonical-town`, stacked on `v1.3.0-scene-town` (`1b63c13`,
PR #27). Source version remains `1.3.0`; save schema remains **3**. No tag,
merge or release created.

### What changed

- Installed the canonical handoff assets with `install_assets.sh
  --replace-assets`. Every authored scene imports headlessly with exit 0.
- Added `scenes/world/Town.tscn` as the runtime copy of `Town_Assets.tscn`.
  It adds the three M14 sign sockets, a scene-owned `RaceStart` marker, and
  authored bakery/library display-slot instances. The candidate scene and baked
  snapshot remain reference-only.
- `WorldSceneAdapter` now instantiates `Town.tscn`, derives all gameplay
  transforms from scene markers, building `DoorMarker` children and gate nodes,
  and validates usability rather than former coordinates. It never calls
  `WorldBuilder.Build()` and never runs `SceneBakeTool`.
- NPC patrols and both free-roam traffic routes bind to
  `NpcPatrolMarkers` / `TrafficRouteMarkers`; M05/M09 and M19 checkpoints bind to
  the authored four- and six-gate roots.
- Player, six NPCs, van, two town traffic cars, the rally rival and all pickups
  now use the supplied GLB template scenes. Controllers, collision, camera,
  audio, save restoration and mission logic are unchanged. Character head/arm/leg
  parts and vehicle wheels are reparented under pivots with preserved global
  transforms, so existing walk/wave/jump and rolling animation still works.
- Fixed a canonical collision discontinuity: town-road collision sat 2–3.4 cm
  above terrain/bridge deck, creating vertical lips at junctions. The collision
  roadbed is now flush at `y=0`; visual slabs are unchanged. Bridge traversal
  passes fast, slow, both lateral offsets and both directions.

### Editor-move proof — ClothesShop +5 m

No C# coordinate constants were edited.

```text
original: centre=(24, 0, 7)  door=(24, 0, 3.5)  prompt PASS  outfit PASS
moved:    centre=(24, 0, 12) door=(24, 0, 8.5)  prompt PASS  outfit PASS
```

The moved scene also passed `--selftest-resume` (21) and `--selftest-restore`
(54), loading existing v1.2.1 saves. The scene was then restored to
`(24, 0, 7)` and the base suite re-ran green.

### Verification — source

```text
dotnet build PatchworkIsland.sln                          0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj   111 passed, 0 failed
godot --headless --path . --editor --quit                 exit 0
godot --headless --path . --quit-after 120                exit 0

--selftest                279 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
```

Source runtime total: **1166 passed, 0 failed**, every process exit 0.

### Verification — packaged Linux

```text
dist export: /home/philip/.cache/tmp/opencode/patchwork-export/PatchworkIsland.x86_64
size:        164,086,056 bytes
sha256:      f9e12b56d7c6db7e86d2c8e97d83337e7ba4e2c4e5d9a21a23d3610e9b514850
```

All 11 packaged phases passed with the same counts as source
(279 + 21 + 101 + 238 + 10 + 217 + 48 + 140 + 18 + 40 + 54 = **1166**),
including `--selftest-restore`; `--quit-after 120` boot exited 0. CI now runs the
packaged restore phase explicitly.

### Performance and visuals — real AMD Radeon 880M/890M

600-frame vsync probes, warm runs:

| Build | Resolution | Mean | p95 | Draw calls | Primitives | Nodes |
|---|---|---|---|---|---|---|
| v1.2.1 baseline | 1280×720 | 16.70–16.71 ms | 17.16–17.21 ms | 3394 | 66069 | 2944 |
| canonical | 1280×720 | 16.68–17.00 ms | 17.20–18.57 ms | 2403 | 49100 | 2458 |
| v1.2.1 baseline | 1920×1080 | 16.70 ms | 17.29–17.31 ms | 3394 | 66069 | 2944 |
| canonical | 1920×1080 | 16.70–17.08 ms | 17.29–19.22 ms | 2403 | 49142 | 2458 |

No measurable regression; the canonical scene renders fewer draw calls and
primitives. First-run shader compilation was excluded by the warm repeats.

- Full 18-shot P14 tour captured at exact 1280×720 and 1920×1080 (18/18 each).
- 10-shot P12 rally/road/shop tour captured at 1280×720 on the real GPU.
- Visual checks cover the bridge seam, junction, player silhouette, NPCs, van,
  traffic, pickups, festivals, rally gates and repaired Pip car.

### Remaining gaps / not claimed

- No tag, merge or release. Human visual/gameplay sign-off is still required.
- Windows export/startup remains **NOT TESTED**.
- One continuous human M01–M20 sitting remains **NOT TESTED**; automated real-UI
  coverage remains green.
- GitHub CI passed on the canonical commit:
  [run 42850939120](https://github.com/PhilipGB/patchwork-island/actions/runs/42850939120)
  (`build-test` 6m25s, `windows-tests` 2m13s), including packaged
  `--selftest-restore` (54 passed).
- Captures and logs stay in ignored `evidence/`/`references/` paths.

## P18 — v1.3.0 canonical visual audit and correction — PASS

**Branch:** `v1.3.0-canonical-town`. No tag, merge or release created.

A full 1080p visual audit of the canonical town was run from fixed district
cameras and gameplay tours. Every static `Label3D` was rendered from both sides,
and every district was inspected from the road, from behind, and from above.

### Defects found and corrected

| # | Defect | Fix | Visual confirmation |
|---|---|---|---|
| 1 | Bakery, Library, Job Board, Playground, Community Garden, Stone Patch, Race Meadow, Bridge Works, Sign Socket and Directional Sign text was mirrored from the viewer side. | Each sign now has a correctly rotated front label on the viewer side and a correctly rotated back label, both single-sided. Stone Patch's label was moved to the town-facing side of its panel. | `evidence/v13_canonical_visual/07_bakery_front.png`, `09_library_front.png`, `11_job_board_front.png`, `05_playground_sign_front.png`, `20_community_garden.png`, `19_stone_patch.png`, `21_rally_signs.png`, `24_bridge_site.png`, `37_market_sign_socket.png` |
| 2 | Playground, Community Garden, Stone Patch, Race Meadow and Bridge Works sign panels floated with no supports. | Added two wooden posts to the four wide signs and one post to the Bridge Works sign, positioned behind the panel. | `05_playground_sign_front.png`, `20_community_garden.png`, `19_stone_patch.png`, `21_rally_signs.png`, `24_bridge_site.png` |
| 3 | Both swing frames showed two overlapping BROKEN labels; the fixed one mirrored from the rear. | Removed the duplicate fixed label and kept one billboard-readable BROKEN label per frame. | `33_swing_front.png`, `34_swing_back.png` |
| 4 | A second legacy BRIDGE WORKS sign and its cones overlapped the canonical `BridgeSite`. | Removed the `BridgeWorksite` instance; kept the canonical `BridgeSite` anchor, sign and crate. | `24_bridge_site.png` |
| 5 | Street lamps stood inside the Town Hall, inside the playground sandpit, in the road, and across several sign sightlines. | Moved the road-edge lamps onto verges, moved the playground lamp outside the sand, moved the town-hall lamp to the front verge, and removed the redundant lamp at the direction post. | `31_west_top_down.png`, `32_east_top_down.png`, `13_direction_post_front.png` |
| 6 | The Playground sign stood in the main road; the direction post and a bench crowded the same crossing. | Moved the Playground sign to the playground entrance verge, moved the direction post and bench off the carriageway, and removed the redundant lamp. | `05_playground_sign_front.png`, `13_direction_post_front.png`, `30_top_down.png` |
| 7 | A bench stood on the rally start road. | Removed the misplaced bench. | `p12_1080_02_start_line.png` |
| 8 | Rally gate number panels were blank or edge-on from the approach. | Added larger, correctly rotated numbers on both faces of every gate panel. | `22_gate_one_front.png`, `23_gate_one_back.png`, `35_gate_two_front.png`, `36_gate_two_back.png` |
| 9 | Empty bridge/swing build ghosts and their `SUPPORT`/`ROPE`/`SEAT` labels were visible outside build mode. | Build sockets now hide ghost mesh/icon/label when their build mode is closed; a runtime regression asserts the clean state. | `p12_1080_02_start_line.png` |

### Verification

```text
dotnet run --project tests/PatchworkIsland.Tests.csproj   111 passed, 0 failed
--selftest                281 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
```

Source runtime total: **1168 passed, 0 failed**, every phase exit 0.

Visual evidence: 39 fixed audit views, 18/18 P14 finale/festival/bridge views,
10/10 P12 rally/road/shop views and 14/14 P13 chapter-two views, all at true
1920×1080 except the audit set at 1280×720. The full set is copied to the
ignored `evidence/v13_canonical_visual/` directory.

### Remaining gaps

- Human visual sign-off and one continuous M01–M20 sitting remain **NOT TESTED**.
- Windows startup remains **NOT TESTED**.
- Celebration bunting intentionally renders as stylised floating flags; no
  text or collision issue was observed.

## P19 — v1.3.0 gameplay-height visual defect pass (V25–V35) — PASS

**Branch:** `v1.3.0-canonical-town`. No tag, merge or release created.

A second manual tour at ordinary gameplay height found eleven remaining visual
defects that fixed inspection cameras had missed. Each was corrected and
re-rendered from the same viewpoint.

### Defects corrected

| # | Defect | Correction |
|---|---|---|
| V25 | `BRIDGE WORKS` clipped inside its panel, with a second tiny fragment above. | Two single-sided labels outside each face; posts moved behind the panel. |
| V26 | Open gable ends on RedHouse and other gabled buildings. | Two wall-coloured gables added to RedHouse, PlayerHouse, Workshop, Market, ClothesShop and TownHall. |
| V27 | Playground swings/sign/interaction marker encroached on the road. | Playground moved to z=-9.5, sand shortened, swings at z=-11.5, marker at z=-5.0 on sand; fix sign and bench moved. |
| V28 | Playground sign intersected the swing structure. | Entrance sign centred 8 m clear of the swings. |
| V29 | Rally gate numbers partly hidden by the crossbeam. | Panel raised/shortened to 2.4×1.45 m at y=5.95 on all six gates; dual-faced numbers. |
| V30 | Van cargo roof moiré; postal label detached. | Self-shadow cast disabled on the missing-normal vehicle GLBs, contact shadow added, label flush on rear door. Applied to all NPC/rival cars too. |
| V31 | Pip car label floated against the workshop wall. | Lowered to 1.82 m, font 40 with cream outline; repaired-car label matched. |
| V32 | Jobs Board junction had overlapping road patches, dark joins and lane lines across mouths. | Three stray patches removed, side roads trimmed to ±3.2, all slab tops flush at y=0.0175, junction dashes removed. |
| V33 | Crossing stripes overhung the side road. | Crossing narrowed from 4.2 m to 3.2 m. |
| V34 | Lamps in the carriageway/awning. | In-road lamp moved to a verge; Market lamp moved south of the awning. |
| V35 | World labels too small at gameplay distance. | Swing `BROKEN` 46 px outlined, car labels 40 px outlined, start flag enlarged to 2.8×1.5 m with two 54 px faces, postal label repositioned. |

The blue traffic car was also found to have the van's missing-normal roof moiré
during the P12 re-check; the fix now covers every `TrafficCar` visual.

### Verification

```text
dotnet run --project tests/PatchworkIsland.Tests.csproj   111 passed, 0 failed
--selftest                281 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
```

Source runtime total: **1168 passed, 0 failed**, every phase exit 0.

Packaged Linux (`dist/linux/PatchworkIsland.x86_64`): boot **PASS**; all 11
phases **1168 passed, 0 failed**; `--selftest` repeated **3/3 green**. The
extracted ZIP binary booted and passed `--selftest` (281/281). Windows binaries
were rebuilt but startup remains **NOT TESTED**.

### Evidence

Ignored `evidence/v13_canonical_visual/v13.1_recheck/`: 31 gameplay-height
probe views plus final 1920×1080 P12/P13/P14 tours. Defect table is in
`docs/VISUAL_QA.md` (V25–V35) and the acceptance gate in `ACCEPTANCE.md`.

### Remaining gaps

- Human visual sign-off and one continuous M01–M20 sitting remain **NOT TESTED**.
- Windows startup remains **NOT TESTED**.
- The Market gable is visually closed; at very oblique angles the blue roof
  slope remains visible beside the gable, which is normal roof geometry.

### P3 follow-up — road and playground scene consistency

**Road collision and surface classification**

- Removed the three stale `Collision_07/09/10` patches whose visible road
  meshes no longer exist.
- Repositioned and resized the five trimmed side-road colliders so all eight
  collision boxes now match the eight visible `RoadSlab` meshes in
  `TownRoads_Collidable.tscn` at the same X/Z centres and extents.
- Replaced the old eleven-rectangle `RoadNetwork` with the same eight authored
  rectangles. `SurfaceRules`, traffic validation and station checks now agree
  with the asphalt the player can actually see; grass beside the removed
  patches is no longer classified as road.
- `WorldSceneAdapter.ValidateCollisionContract` now requires one layer-1
  collider per visible `RoadSlab`, rather than accepting a fixed count.
- Added a pure regression test that parses both road scenes plus
  `RoadNetwork.Roads` and fails on any missing, extra or moved slab/collider.

**Playground scene authority**

- `Playground.tscn` now owns a `WorksiteMarker` at local `(0, 0.1, 4.5)`
  (`scene_binding_id = "PlaygroundWorksite"`), on the sand at the old station
  point.
- `GameplayAnchors.tscn` `PlaygroundCenter` now synchronises to the authored
  playground root at `(-38, 0, -9.5)`.
- `WorldSceneAdapter` resolves the anchor, rejects any drift from the
  `Playground` root, resolves the marker, and exposes that position to
  `Main.CreateStations`; the station no longer uses a hard-coded coordinate.
- Runtime parity checks prove the anchor follows the root, the marker stays on
  the sand, and the interaction station follows the marker.

### Verification

```text
dotnet run --project tests/PatchworkIsland.Tests.csproj   113 passed, 0 failed
--selftest                284 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
```

Source runtime total: **1171 passed, 0 failed**, every phase exit 0.

Packaged Linux (`dist/linux/PatchworkIsland.x86_64`): boot **PASS**; all 11
phases **1171 passed, 0 failed**; `--selftest` repeated **3/3 green** (284 each).
The extracted v1.3.0 ZIP binary booted and passed `--selftest` (284/284).
Windows binaries were rebuilt but startup remains **NOT TESTED**.

No visual pixel change is expected from this follow-up: the worksite marker
resolves to the same global `(-38, 0.1, -5.0)` station already shown in the
V27 evidence, and the road visuals themselves were not moved. The existing
`evidence/v13_canonical_visual/v13.1_recheck/` captures remain representative.

## P20 — Round-3 visual defect pass (V36–V48) — PASS

**Branch:** `v1.3.0-canonical-town`.

The third visual review found nine visible defects and four issues to verify.
All geometry and text changes are in the authored scenes; the procedural
`WorldBuilder` mirror was updated so a future bake cannot reintroduce them.

### Geometry and composition

- **V36 — gables.** The V26 gables were placed on the front/back eave walls
  even though the roof ridge runs along X. From the side they appeared as two
  white triangles outside the roof, with the actual pediment open. All six
  buildings now have `GableEast` / `GableWest` on the ridge ends, rotated 90°,
  sized to the building depth, and fitted under the ridge. A dedicated
  `StandardMaterial3D_Gable` uses `disable_receive_shadows = true` so the roof
  overhang no longer throws a dark triangle that reads like a hole. The pure
  test checks end placement, rotation, depth and material on all six.
- **V37/V38 — celebration banner.** The 8 m × 1.5 m floating panel that blocked
  the Town Hall was replaced by a 4.72 m × 0.75 m free-standing banner on two
  posts at the M20 festival entrance, `(15, 3.35, 19.8)`. The panel overlaps both
  posts by 1 cm, has two correctly oriented, non-billboard text faces, and uses
  a mild yellow emission so the shaded face still reads as celebration yellow.
  It stands in front of the festival bunting line, so no flag can silhouette
  across the panel from the village approach; the P14 festival camera sees the
  panel above the tables. The centre festival lantern was moved from
  `(15, 24.2)` to `(18.6, 24.2)` so its pole and lamp no longer cover the back
  text from the festival-side centreline. The banner is clear of the Town Hall
  frontage, the market alley and the benches.
- **V39 — directional signs.** The four boards were spread left/right around
  one post at `(-9.8, 4.2)` inside Milo's patrol area. They are now four
  consistent arms stacked on the right of one post at `(-5.0, 0, -4.5)` on
  the south verge, with front/back labels. The first independent render caught
  that the arrow glyphs were inverted on both faces; they were swapped and the
  pure test now asserts all eight strings. From each face the west destination
  points left and the three east destinations point right. The M17
  `MarketStock` moved from `(-2, 4.8)` to `(9.2, 3.8)` in front of the market,
  so the M16 delivery board no longer hides it.
- **V48 — GRAND RALLY podium.** The billboard label floated 0.3 m above the
  trophy. It is now mounted on a 2.9 m × 0.5 m board on two posts above the
  trophy, with front and back labels.

### World-space text and vehicles

- **V40 — GOLD LAP.** The single billboard label was replaced by two
  single-sided labels offset just outside the flag faces; text is now 56 px
  with a cream outline and stays attached at oblique angles. The independent
  pass found the race start/finish pole crossing the flag from the southwest,
  hiding `GO`; the flag moved from `(64.5, 0, -12)` to `(64.5, 0, -15.5)` and
  the pole no longer intersects it. The pure test keeps the flag more than 6 m
  from the race start pole.
- **V41 — Pip's car.** Both car variants have a scene-owned `StatusAnchor`;
  the status label is a child of that marker, lowered to 1.72 m and reduced to
  36 px so it reads against the cab instead of floating over the body.
- **V42/V47 — NPC labels and portraits.** Nameplates are 56 px instead of 68,
  use a smaller pixel size, have a 34 m visibility range and scale with camera
  distance (clamped 0.72–1.45×). Portraits are 0.0018 px/texel instead of
  0.0034, have a 24 m range and scale 0.75–1.20×. Together with the signpost
  move, NPC names no longer collide with the town-centre signage.
- **V43 — delivery van.** `PATCHWORK POST` is now 42 px with a 6 px outline
  at 1.05 m on the rear door; a glTF probe confirms it stays inside the door
  silhouette.
- **V44 — roadside dark dashes.** All 28 CelebrationDecor bunting meshes have
  `cast_shadow = 0`; the repeated rectangular ground shadows are gone. The
  independent pass found the same hard-shadow problem on the eight M20
  FestivalDecor bunting flags, which were splitting the celebration banner
  into a light and dark half; those eight flags also have `cast_shadow = 0`.

### Verification status

- Pure tests: **119 passed, 0 failed**.
- Source runtime: **1171 passed, 0 failed**, all 11 phases exit 0
  (`284/21/101/238/10/217/48/140/18/40/54`).
- Headless import and boot: exit 0.
- Packaged Linux (`dist/linux/PatchworkIsland.x86_64`, exported from this
  tree): boot **PASS**; all 11 phases **1171 passed, 0 failed**;
  `--selftest` repeated **3/3 green** (284 each); ZIP-extracted binary booted
  and passed `--selftest` (284/284).
- Windows binary rebuilt (PE32+ x86-64) but startup remains **NOT TESTED**.
- Final package hashes:
  - `linux/PatchworkIsland.x86_64` `ab31fa356372b4ba9607fe942370efba55956f83ff39bb889c0d7527fe8b4549`
  - `windows/PatchworkIsland.exe` `e2f1bd7a1fd12cb7cc23730eb9e1b8331cf27540713d0babf24e5d9531e57b6e`
  - `PatchworkIsland-1.3.0-Linux-x86_64.zip` `105bfc7753cfddf2adeba3b2caa68260e704f41538df8686a411211fff605ddb`
  - `PatchworkIsland-1.3.0-Windows-x86_64.zip` `448d6ce5af5677017e184ea0f6a294831dfaf7d6731ec8013617e8359ed83ea7`
- Visual probes: `evidence/v13_canonical_visual/v13.2_round3/` (24 gable
  views across all six buildings, town-level front/back/side probes, P11 NPC
  and P14 finale tours).
- Independent visual passes, final verdict at `1e57711`: V36 PASS (all six
  ends solid; the remaining eave band is the roof overhang shadow/underside,
  not an opening), V37/V38 PASS from the village and festival approaches
  (banner at the festival entrance, centre lantern off-axis), V39 PASS (all
  eight arrow strings point at the real destinations), V40 PASS (full flag
  text clear from the old southwest angle), V41 PASS, V42/V47 PASS, V44 PASS,
  V45 PASS, V48 PASS. No remaining clipping, floating text, z-fighting,
  protruding geometry or wrong-facing text in the reviewed angles.
- Exact-head CI: commit `1e57711` — `build-test` **success** and
  `windows-tests` **success**; PR #28 open and mergeable. An earlier
  `b65fc02` Windows run failed only because the new bunting test assumed LF
  line endings; the regex is now CRLF-safe and the rerun is green.

### Remaining gaps

- Human visual sign-off and one continuous M01–M20 sitting remain **NOT TESTED**.
- Windows startup remains **NOT TESTED**.
- V45 is confirmed intentional: the swing frame is the pre-M07 broken state,
  with crossbar, `BROKEN` labels and the runtime broken-label assertion.
- The celebration banner is free-standing at the festival entrance rather than
  mounted on the Town Hall; both faces and the P14 festival camera were
  re-rendered, but the final human composition sign-off is still **NOT TESTED**.

## P21 — Round-4 visual defect pass (V49–V57) — PASS

**Branch:** `v1.3.0-canonical-town`.

The fourth visual review found nine remaining defects. All nine were fixed in
the authored scenes and in the procedural `WorldBuilder` mirror, then captured
from front/back/side and gameplay-height angles.

### Geometry and composition

- **V49 — swing posts.** The two swing frames now have true A-frames in both
  planes: at each end the front leg tilts toward `+Z` and the back leg toward
  `-Z` (rotation `±0.20944` about X), while the left/right pair still splays
  inward about Z. The top corners now meet under the crossbar instead of
  crossing or leaving one end vertical.
- **V50 — van shadow.** The van keeps one soft contact shadow (`GroundShadow`,
  radius 1.0, scaled `1.18 × 1.78`, alpha 0.18) that covers the full footprint.
  The antenna, flag and every postal-mark piece stop casting shadows, so the
  detached blob beside the van is gone.
- **V51 — van side mark.** The white rectangle with three black lines is now a
  readable postal mark: cream panel with a navy border, envelope flap, red
  stamp and a `POST` label on both sides. It sits flush on the cargo body and
  reads correctly from either side.
- **V52 — Pip's car wheels.** All four cylinders on `PipCarFixed` and
  `PipCarBroken` are rotated `(0, 0, 1.5708)` so the wheel axis runs across the
  car; the wheels now read as vertical discs instead of horizontal pucks.
- **V53 — player hair.** The player rig gains a hair shell on the head pivot:
  back, left, right and fringe pieces, plus a longer bob for the girl preset.
  It is visible from the chase camera and does not cover the face; the cap
  still attaches above it.
- **V54 — library shelves.** Both ledges and all four book slots moved from
  local `+Z` to local `-Z`, the labelled street-facing side. Book slots are
  rotated 180° so covers and page blocks face the reader. The blank back face
  is now the side against the Town Hall.
- **V55 — bridge to circuit.** The 37.5 m east-road chord (`x 50.5..88`) and its
  short landing replacement are removed entirely: the grey slab no longer
  overlays the rally circuit or stops inside the oval. The authored wooden
  bridge deck (`x 39.55..50.45`) is the connector and lands on both the main
  street and the circuit's west edge. Its centre-line and edge markings were
  removed with the slab, `RoadNetwork` now has 7 rectangles and the pure test
  expects 7 authored slabs.
- **V56 — perimeter barrier.** The north/south sand strips and hedge walls are
  centred at `x=20` with length `150.2`, matching the west/east walls at
  `x=-54.5` and `x=94.5`. Both ends are now flush: no gap at one corner and no
  overlap at the other. Wall colliders use the same dimensions.
- **V57 — tree spacing.** `Tree_20` moved from `(68, -20)` to `(65.5, -23)` and
  `Tree_25` from `(-33, -41)` to `(-37, -38)`. The closest remaining pair is
  `Tree_11/Tree_22` at 4.47 m, so canopies no longer merge.

### Traffic follow-up required by V55

Removing the east road invalidated the delivery car's old loop across the
river. Both free-roam routes now stay on the main street, with lanes at
`z=+0.5` (westbound) and `z=-2.0` (eastbound). Those offsets keep every
waypoint at least 1.8 m from the critical stations and every sampled runtime
position at least 1.7 m away; the old `z=±1.5` lanes passed 1.0–1.2 m from the
shop and clothes-shop doors. The 20-point `TrafficRouteMarkers.tscn` routes,
`TrafficCatalog` and the v1.3.0 layout manifest were regenerated together.
While validating this, `TrafficCar` was changed to freeze its steering while
yielding: previously a stopped car kept swivelling toward its next waypoint and
could turn away from a waiting player, falsely resuming.

### Verification status

- Pure tests: **119 passed, 0 failed**.
- Source runtime after the final scene cleanup: **1171 passed, 0 failed**,
  all 11 phases exit 0 (`284/21/101/238/10/217/48/140/18/40/54`).
- Headless import and boot: exit 0.
- Visual probes: ignored
  `evidence/v13_canonical_visual/v13.3_round4/` — swing A-frames, bridge/circuit
  top and oblique, van side/rear (postal mark and contact shadow), both Pip
  cars, player hair front/back, library street/rear faces, all four perimeter
  corners, and both tree groups.
- Packaged Linux (`dist/linux/PatchworkIsland.x86_64`, exported from this
  tree): boot **PASS**; all 11 phases **1171 passed, 0 failed**, every phase
  exit 0; `--selftest` repeated **3/3 green** (284 each). The extracted
  v1.3.0 ZIP binary booted and passed `--selftest` (284/284).
- Windows binary rebuilt (PE32+ x86-64, file/product version `1.3.0.0`) but
  startup remains **NOT TESTED**.
- Final package hashes:
  - `linux/PatchworkIsland.x86_64` `83320e594950e9b7cf7ffea28d888d06a868b946a30d37fd50ee4773dbf8c389`
  - `windows/PatchworkIsland.exe` `0cd54018ac81cd09c751ac619f122381783b9183254ea6df467d47648b3f7695`
  - `PatchworkIsland-1.3.0-Linux-x86_64.zip` `23c816e87e157c3379517d55edade22c8e8461e11b8072a278dcee2adc1a1b5d`
  - `PatchworkIsland-1.3.0-Windows-x86_64.zip` `4c44ff4ccd478b24a680d69f9ddbc5ceed67614d4c3eea01dc751a3bdba72c2c`
  - `sha256sum -c dist/SHA256SUMS` passes for all four files.

### Remaining gaps

- Human visual sign-off and one continuous M01–M20 sitting remain **NOT TESTED**.
- Windows startup remains **NOT TESTED**.
- V45 remains intentional: the swing frame is the pre-M07 broken state with
  crossbar and `BROKEN` labels.
- The library's street face sits behind the JOBS board from some approach
  angles; this is the existing town layout, not a shelf orientation fault.

## P22 — Review follow-up: traffic separation and manifest history — PASS

**Branch:** `v1.3.0-canonical-town`.

Two review findings were addressed: long-term overlap between the two
free-roam traffic cars, and the accidental rewriting of the historical v1.2.1
layout manifest.

### P2 — car-to-car separation

Both free-roam vehicles traverse the same closed village loop in the same
direction, starting at different authored waypoints. The missing behaviour was
a following rule, so the faster scooter could eventually catch the delivery
car.

- Added a shared loop metric and following rule to `TrafficCatalog`:
  `VillageLoop`, `VillageLoopLength`, `ProgressOnVillageLoop`,
  `DistanceToVillageLoop`, `MinimumTrafficGap = 7 m`, and
  `ShouldYieldToTraffic`.
- A car yields only when another car is ahead by less than `MinimumTrafficGap`
  on the one-way loop. The leader always proceeds; at exactly equal progress
  the lexicographically larger route id yields. That makes the rule
  deterministic and deadlock-free: only the following car can ever stop.
- `TrafficCar.Initialize` now accepts the live traffic list,
  `Main.SpawnTraffic` passes `_trafficCars`, and `ShouldYield()` checks traffic
  after the player/van checks. `TrafficCatalog.Validate` rejects any free-roam
  waypoint that leaves the shared loop.
- Because progress is measured along the route rather than by a forward cone,
  the spacing remains correct through the U-turns and corners.
- Validation runs the same rule at 60 Hz for **six complete loops** at the
  authored 3.2/2.8 m/s speeds. The pure test asserts no deadlock, both cars
  travel more than three loops, and the minimum following gap stays at least
  `MinimumTrafficGap - 0.1 m`. A second test checks the equal-progress
  right-of-way rule in both directions.

### P3 — manifest history

- Restored `docs/layout_manifest_v1.2.1.json` to its original content from
  `1b63c13`: the delivery car has 12 points starting at `(66, -1.5)` and the
  scooter still uses the old `z=±1.5` lanes. This file is now an immutable
  historical baseline.
- Added `docs/layout_manifest_v1.3.0.json` for the canonical town, with the
  round-4 20-point routes, the current seven-slab `street_roads` layout and
  `source` set to tag v1.3.0.
- Current-scene tests (gables, signpost, baked buildings, NPC/traffic markers)
  now read the v1.3.0 manifest. The historical anchor-baseline test
  intentionally keeps reading v1.2.1.
- Added a pure test that guards the historical delivery endpoints and source,
  and that the v1.3.0 manifest matches the authored marker scene.

### Verification status

- Pure tests: **123 passed, 0 failed** (119 previous + four traffic/manifest
  checks).
- Source runtime: **1171 passed, 0 failed**, all 11 phases exit 0
  (`284/21/101/238/10/217/48/140/18/40/54`).
- Headless import and boot: exit 0.
- Packaged Linux (`dist/linux/PatchworkIsland.x86_64`, exported from this
  tree): boot **PASS**; all 11 phases **1171 passed, 0 failed**, every phase
  exit 0; `--selftest` repeated **3/3 green** (284 each). The extracted
  v1.3.0 ZIP binary booted and passed `--selftest` (284/284).
- Windows binary rebuilt (PE32+ x86-64) but startup remains **NOT TESTED**.
- Final package hashes:
  - `linux/PatchworkIsland.x86_64` `b0ab55eba61619bde6e59ab9e8674862b99f87523c7af61449a6b90244a663fc`
  - `windows/PatchworkIsland.exe` `01e8e88cae3e6b91fd5466bebd09046a1ae6b065f9290df98380ae51143e8cf1`
  - `PatchworkIsland-1.3.0-Linux-x86_64.zip` `37c7330bdb76ae323aad56cd5aa542d5a94cb289375f3256e5c51cd3e9ae4dff`
  - `PatchworkIsland-1.3.0-Windows-x86_64.zip` `63724b9197ef0536c6bcfebd2adb55ee13709bf48b24fd073bc3aa6ea4d2147d`
  - `sha256sum -c dist/SHA256SUMS` passes for all four files.

### Remaining gaps

- Human visual sign-off, one continuous M01–M20 sitting and Windows startup
  remain **NOT TESTED**.
- The long-horizon traffic check is an idealised pure simulation at a fixed
  60 Hz; it does not model acceleration or corner speed scaling. The 7 m gap
  is larger than the worst braking distance, and the 24 s runtime living test
  still passes.

## P23 — v1.3.0 asset-driven world: WorldBuilder and bake removed — PASS

The static town is now authored only in Godot scenes. Future development edits
`scenes/world/Town.tscn` and `scenes/world/assets/` directly; there is no
procedural builder, bake tool or baked snapshot to keep in sync.

### Changes

- Extracted the runtime data contract from `WorldBuilder` into
  `src/World/WorldModel.cs` (`abstract partial class WorldModel : Node3D`).
  It keeps all 69 gameplay-facing properties (anchors, spawns, lists, scene
  handles) and declares `abstract void Build()`.
- `WorldSceneAdapter` now derives from `WorldModel`; `Main` names the instance
  `Village`. `BridgeBuilder` and `SwingBuilder` take `WorldModel` parameters.
- Deleted `src/World/WorldBuilder.cs`, `tools/SceneBakeTool.cs`,
  `scenes/tools/SceneBakeTool.tscn`, the unused `tools/WorldSceneAnchors.cs`
  helper and all 34 files under `scenes/world/baked/`.
- `Town_Assets.tscn` and `Town_Preview.tscn` remain as reference/preview scenes
  only. `Town.tscn` is the single runtime level.
- Rewrote the two baked-reference tests against the authored assets:
  - building centres come from `Town.tscn` instances and wall sizes from
    `scenes/world/assets/buildings/<id>.tscn` direct `MeshInstance3D` walls;
  - static collision and finale roots are read from `Town.tscn`,
    `RallyRoad_Collidable.tscn` and `assets/buildings/PlayerHouse.tscn`.
- Extended `ParseWallSize` to accept both direct wall meshes and wall parents
  with mesh children; removed the now-unused `ParseInstanceOrigin` helper.
- `docs/layout_manifest_v1.3.0.json` replaces `exact_baking` with
  `scene_authority`; the immutable v1.2.1 manifest is untouched.
- Updated `README.md`, `README_RUN.md`, `README_START_HERE.md`,
  `IMPLEMENTATION_PLAN.md`, `docs/CANONICAL_TOWN.md`,
  `docs/TECHNICAL_GUIDANCE.md`, `docs/ASSET_CATALOGUE.md`,
  `docs/scene_bindings.json` and `docs/MIGRATION_AGENT_HANDOFF.md`.
  Remaining `WorldBuilder`/`Town_Baked` mentions are historical provenance
  statements only.

### Verification status

- Build: 0 warnings, 0 errors.
- Pure tests: **123 passed, 0 failed**.
- Source runtime: **1171 passed, 0 failed**, all 11 phases exit 0
  (`284/21/101/238/10/217/48/140/18/40/54`).
- Headless editor import and boot: exit 0.

### Packaged gates

- Package: `dist/linux/PatchworkIsland.x86_64`, `dist/windows/PatchworkIsland.exe`,
  `dist/PatchworkIsland-1.3.0-Linux-x86_64.zip`,
  `dist/PatchworkIsland-1.3.0-Windows-x86_64.zip`.
- Packaged Linux boot **PASS**; all 11 phases **1171 passed, 0 failed**, every
  phase exit 0; `--selftest` repeated **3/3 green** (284 each).
- The extracted v1.3.0 ZIP binary booted and passed `--selftest` (284/284).
- `sha256sum -c dist/SHA256SUMS` passes for all four files.
- Hashes:
  - `linux/PatchworkIsland.x86_64` `2e6134801a2511e4e0d172452114b013ba03c4de0c2bdd205279ca40c55a9f89`
  - `windows/PatchworkIsland.exe` `d40470fbbc4b538f50e7332770dca3ab6ee13de21b308d4ac39840cdea847d3d`
  - `PatchworkIsland-1.3.0-Linux-x86_64.zip` `d6d735ad8f59d2b4ef3d106597294913313c33060ee0de66ecb6b9980456fc90`
  - `PatchworkIsland-1.3.0-Windows-x86_64.zip` `96695e213e0ccb29b201dbbd662b9ece274ed5c17b6a7dae0c4662f3162fe159`
- Windows binary rebuilt (PE32+ x86-64) but startup remains **NOT TESTED**.
- Exact-head CI on `7b47c1a`: `build-test` (6m29s) and `windows-tests` (1m07s)
  passed in run
  [43029252909](https://github.com/PhilipGB/patchwork-island/actions/runs/43029252909).

### Remaining gaps

- Human visual sign-off, one continuous M01–M20 sitting and Windows startup
  remain **NOT TESTED**.

## P24 — v1.4.0 City Expansion — test release published

**Branch/worktree:** `v1.4.0-city-expansion`, based on `0dbd0c6`
(`v1.3.0-asset-driven`). The PR is stacked on open PR #31. The verified source
is published as the **v1.4.0 test pre-release** from `21b226e`
(<https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.0>). The
working tree also contains the local, gitignored `dist/` 1.4.0 builds.

Source version is **1.4.0**; save schema is **4**. The village remains the
M01–M20 game. The city is a second authored space reached only through the new
M21 gate job.

### Delivered

- **M21 Roadworks Rescue** — three safety cones, two barrier panels and one
  toolbox are authored pickups at the workshop. Delivering them to the town-side
  worksite opens a 3 + 2 + 1 counting and loading-order task. Completion pays 25
  coins once, hides the barrier and fences, disables every gate collision shape
  and persists the open road. The operation is idempotent on every load.
- **Drivable town-to-city join** — authored bridge/causeway in
  `scenes/city/TownCityApproach.tscn`, terrain east-wall opening, a physical
  gate at world `x=96`, and return driving. The approach surfaces are flush with
  the rally ground; the earlier raised slab/guard-rail line across the rally was
  found by the regression suite and removed.
- **Independent city jobs** — C01 City Courier (numbered/order delivery), C02
  Market Stock-Up (six crates and 2 × 3) and C03 Signals and Signs (red/amber/
  green sequence plus engineer sign-off). Separate catalogue, separate save
  fields, shared coin wallet, exactly-once rewards.
- **Tiered city runtime** — authored detail and 36 HLOD proxies in nine cells,
  18 m hysteresis, near/mid actor pool, no city Node3Ds or collision while the
  district is hidden. A deterministic 24-vehicle / 42-pedestrian logical
  simulation advances O(1) while the city is offscreen.
- **City board and UI** — a separate teal CITY JOBS board at local `(0,0.4,22)`
  facing the plaza, with C01–C03 cards, wallet, progress and accessible keyboard
  focus. The village board's second tab now reads `New adventures (11–21)`.
- **Save schema 4** — city roadworks flags, completed city jobs, city counters,
  city seed and logical clock. Schema 3 migrates without changing town
  progression. Newer schemas remain untouched and protected.
- **Tests** — pure rules/save/scene contracts and `--selftest-city` with real
  scene reloads, the physical gate at full van speed, mid-drive save/continue,
  LOD/actor cleanup and return trips.
- **CI** — `--selftest-city` added to the source runtime list and to the
  packaged Linux smoke check in `.github/workflows/build-and-test.yml`.

### City regression found and fixed during the gate

The first full run after adding the city exposed `--selftest-rally` and
`--selftest-finale` failures: the gate at world `x=90` and the raised approach
slab sat on the eastern edge of the authored rally circuit. The van hit the
gate/fence in the M05/M09/M19 autopilot runs. The fix:

- moved the gate to world `x=96`, just east of the island boundary and clear of
  the rally's paved width;
- lowered every approach surface below the rally surface (top ≈ 0.01 m versus
  rally top ≈ 0.022 m) and lowered the approach collision to the same floor;
- shortened the visible guard rails to the bridge/causeway, moved the notice
  sign and crew to the south verge, and added a gate apron under the fences;
- updated the city gate-stop thresholds and the scene-contract regression.

After the fix, M05 **19.03 s**, M09 **18.98 s**, M19 **22.32 s**, all four/six
gates in order with the optional bonus. No village mission or rally rule
changed.

### Post-PR playtest fix — M21 delivery interaction

A playtest with **6 / 6** supplies showed the HUD still pointing back to the
workshop, and pressing E there could not deliver because the items were already
collected.

Root cause: `Main.InteractRoadworks()` checked `SuppliesDelivered` but never
called `MissionService.DeliverRoadworksSupplies()`. The objective router also
used only the already-delivered flag, so it kept selecting the workshop until a
delivery that could never happen.

Fix:

- the real gate interaction now calls `DeliverRoadworksSupplies()` when all six
  are carried, consumes them once, saves, shows the delivery toast and opens the
  crew loading check;
- the objective switches to `ROADWORKS DELIVERY` at the closed gate as soon as
  `HasAllSupplies` is true, before delivery (and `ROADWORKS CREW` afterwards);
- the progress row says “Now drive to the closed CITY ROAD gate” at 6/6;
- the roadworks station radius is 5.0 m so the prompt is reachable from the
  bridge approach.

Regression coverage in `--selftest-city` now uses the **real interaction**:
all six carried → objective points at the gate → E delivers and consumes the
items → the loading-check modal opens. City runtime count rose from 76 to 82.

### City traversal remediation — sidewalks, plazas and NPC routes

A driving playtest reported that the van still could not enter the city and that
raised paved areas crossed the grid, stalling NPC cars.

Captured in the running scene before the fix:

```text
CITY_DEFECT van_entry x=182.16 y=0.05 z=0.00 speed=0.00
```

- `evidence/v14_city/defects/p24_01_city_entry_side.png` …
  `p24_07_city_grid_overview.png`
- `evidence/v14_city/defects/city-defects-before.log`

Root causes:

- every 4 m sidewalk strip was a continuous 350 m mesh/collider at 0.33 m,
  crossing all four perpendicular roads and sealing the city entrance;
- the nine 68 m plaza slabs overlapped the 24 m roads by 6 m on each side;
- building footprints at ±18 m plots extended up to 5 m into the roads;
- lane dashes and crosswalks floated 0.2 m above the road and cast detached
  shadows;
- the approach/city seam had a 7 cm collision lip.

Implemented:

- rebuilt all 16 sidewalk strips as 81 authored segments between road crossings,
  with a 16 m entrance gap in the westmost strip at z=0;
- shrank the nine plazas and shared collision to 56 m so they stop at the road
  edge;
- scaled the 36 building instances to 0.62 and copied that scale to their HLOD
  proxies;
- lowered city road surfaces/base collision to 0.01–0.02 m and markings to
  0.04–0.045 m; disabled shadow casting on 152 road-marking meshes;
- moved the board, benches, trees and city-job roots onto the 0.29 m plaza;
- restored the traffic signals to the 0.3 m sidewalk level.

New regressions: the van crosses the former entrance barrier (`x > 188`), every
near city car stays on the road grid and at least two keep moving through
intersections, and the player walks from the road onto the central plaza. Pure
scene contracts reject sidewalk segments crossing a road or plazas overlapping
one. City runtime is now **111 passed, 0 failed**.

After-fix evidence:

- `evidence/v14_city/defects-fixed/p24_01_city_entry_side.png` …
  `p24_07_city_grid_overview.png`
- `evidence/v14_city/defects-fixed/city-defects-after.log`

The after-fix van log reaches `x=189.35` on the city road; the car log shows
continued movement along the inner and outer routes. Full analysis:
`docs/CITY_TRAVERSAL_REMEDIATION.md`.

### Review follow-up — traversal, traffic and activation

The PR review requested stronger evidence and policy decisions:

- **Full traversal:** the city selftest now drives the van with a sustained
  throttle autopilot from the approach through the first intersection, around
  the central block and back out. It asserts all four phases (entered, first
  intersection, central block, returned).
- **Traffic policy:** city NPC traffic is explicitly cooperative/decorative.
  Cars collide with static geometry but not with the player/van and yield when
  the player is ahead. Static yield tests and a controlled stop/stream test
  cover the policy.
- **Reconciliation:** near cars keep a bounded 18 m visual lag. A yielding car
  is not snapped forward through the player; its lag is stored and applied when
  it streams back in. Mid actors follow logical pose directly, and near actors
  catch up with bounded speed. The test holds, streams out, returns and
  re-yields a car.
- **Collision scope:** city-ground collision now activates only within 185 m of
  the city centre, while 335 m visibility remains for HLOD/streaming. At the
  town centre the district can stay visible with zero active collision bodies;
  a runtime assertion covers this.
- **Weak assertion:** the car regression now requires at least two near cars and
  at least two moving cars.

City runtime is now **111 passed, 0 failed**; the full source total is
**1282 passed, 0 failed**.

### Verification — source

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj  -> 132 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
--selftest                284 passed, 0 failed
--selftest-resume          21 passed, 0 failed
--selftest-recovery       101 passed, 0 failed
--selftest-living         238 passed, 0 failed
--selftest-rally           10 passed, 0 failed
--selftest-chapter        217 passed, 0 failed
--selftest-hotfix          48 passed, 0 failed
--selftest-finale         140 passed, 0 failed
--selftest-traversal       18 passed, 0 failed
--selftest-release         40 passed, 0 failed
--selftest-restore         54 passed, 0 failed
--selftest-city            111 passed, 0 failed
```

Source runtime total: **1282 passed, 0 failed**, all twelve phases exit 0.
Every process exits 0; the known Mono shutdown resource warnings are unchanged.

### Packaged Linux 1.4.0 — CityFix

A release export was produced from the city-grid remediation commit and tested
from the local package:

```text
dist/linux/PatchworkIsland.x86_64        163,777,128 bytes
sha256 53fd33ee33f680c6ae560ff022ccae8088f3f545071bc43a7f42f9b76f52f97d
dist/windows/PatchworkIsland.exe         199,602,048 bytes
sha256 bf99de93914473859de9043fb24447737f9b6b4a1825164daf798f16df76e656
```

The packaged binary booted headlessly and passed all twelve runtime phases with
the same counts as source:

```text
boot 0
--selftest              284 passed, 0 failed
--selftest-resume        21 passed, 0 failed
--selftest-recovery     101 passed, 0 failed
--selftest-living       238 passed, 0 failed
--selftest-rally         10 passed, 0 failed
--selftest-chapter      217 passed, 0 failed
--selftest-hotfix        48 passed, 0 failed
--selftest-finale       140 passed, 0 failed
--selftest-traversal     18 passed, 0 failed
--selftest-release       40 passed, 0 failed
--selftest-restore       54 passed, 0 failed
--selftest-city          111 passed, 0 failed
```

Packaged total: **1282 passed, 0 failed**, all phases exit 0. The matching local
CityFix ZIPs and `SHA256SUMS.v1.4.0-cityfix` are in the gitignored `dist/`.
The existing GitHub v1.4.0 pre-release remains pinned to the earlier `21b226e`
build; this remediation should be published as a new v1.4.1 tag/release.

### Performance — real AMD Radeon 890M

Final-tree probe, vsync disabled, 240 frames per stage. Full method and
caveats: `docs/05_CITY_PERFORMANCE.md`; raw data:
`evidence/city_perf_1280x720.jsonl`, `evidence/city_perf_1920x1080.jsonl`.

| Resolution | Stage | p50 ms | p95 ms | Draw calls | Primitives | Physics | Detail/HLOD | Near/mid actors |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 1280×720 | town-only | 1.752 | 2.909 | 1343 | 27,774 | 10 | 0 / 0 | 0 / 0 |
| 1280×720 | city approach | 2.132 | 3.246 | 611 | 8,900 | 10 | 0 / 16 | 0 / 20 |
| 1280×720 | city street | 2.875 | 4.509 | 1255 | 18,292 | 19 | 20 / 16 | 19 / 17 |
| 1280×720 | return to town | 1.750 | 3.354 | 1333 | 27,048 | 10 | 0 / 0 | 0 / 0 |
| 1920×1080 | town-only | 2.978 | 4.864 | 1343 | 27,798 | 10 | 0 / 0 | 0 / 0 |
| 1920×1080 | city approach | 2.474 | 4.397 | 611 | 8,704 | 10 | 0 / 16 | 0 / 21 |
| 1920×1080 | city street | 3.360 | 5.601 | 1255 | 18,438 | 24 | 20 / 16 | 19 / 17 |
| 1920×1080 | return to town | 3.059 | 4.944 | 1333 | 27,102 | 10 | 0 / 0 | 0 / 0 |

- Hidden-city logical catch-up: **0.002–0.003 ms** for 1,800 s, zero actor nodes.
- Town-only and return stages: zero city actors, zero city physics and zero
  detailed/HLOD city content.
- Performance target 60 fps at 720p: **PASS** in these stages (worst p95
  4.51 ms). A same-method v1.3.0 baseline comparison was **not** run, so the
  “no more than +1.0 ms in town” target is **NOT INDEPENDENTLY
  BASELINE-COMPARED**. A >33 ms transition hitch was not observed but is not
  instrumented.
- Real Radeon 890M on one Linux/Mesa machine; Windows and other GPUs remain
  **NOT TESTED**.

### Visual evidence

Final real GPU screenshots are in:

- `evidence/v14_city/720/` — ten 1280×720 captures.
- `evidence/v14_city/1080/` — ten 1920×1080 captures.

Views: closed and open roadworks gate with readable signage, city approach,
city street, HLOD overview, plaza and board, CITY JOBS modal, C01 progress,
C03 cabinets/engineer, and the return to the village. The first capture round
found mirrored board text and an unreadable side-on roadworks sign; both were
fixed (board labels face the plaza; the sign faces the approaching driver on the
south verge) and re-captured.

### Compatibility and docs

- `docs/CITY_EXPANSION_COMPATIBILITY.md` — schema 3 → 4 migration, city fields,
  exactly-once rules, town invariants and validation.
- `docs/05_CITY_PERFORMANCE.md` — method, raw results, targets and caveats.
- `docs/04_COORDINATES_AND_AUTHORING.md` — city origin, board/interaction
  coordinates, gate/causeway, flush-surface rule, job markers and routes.
- `docs/03_ACCEPTANCE.md` — city acceptance checklist.
- `docs/SAVE_COMPATIBILITY.md`, `README.md` and `README_RUN.md` — schema 4 and
  v1.4.0 status.
- `data/city_jobs_proposed.json` remains the input proposal;
  `data/city_missions.json` is the runtime catalogue.

### Not claimed / remaining gaps

- No continuous human M01–M21 → C01–C03 sitting. Automated real-UI and runtime
  coverage is green.
- Human composition/visual sign-off of the city screenshots is still open.
- Windows export/startup is **NOT TESTED**.
- PR [#32](https://github.com/PhilipGB/patchwork-island/pull/32) is open and
  stacked on #31. Exact-head CI at `21b226e` passed `build-test` and
  `windows-tests` in run
  [43075355414](https://github.com/PhilipGB/patchwork-island/actions/runs/43075355414).
- The v1.4.0 test release uses the packaged Linux and Windows exports in
  `dist/`: the Linux package booted and passed all twelve runtime phases, and
  the Windows PE32+ executable reports version `1.4.0.0`. Windows startup is
  still **NOT TESTED**.
- The approach road still crosses the rally meadow visually at z=0. It is flush
  and below the rally surface, and the rally/autopilot tests pass, but it is a
  deliberate layout compromise rather than a separate ring road.

### v1.4.0 test release — PASS

Published as a GitHub pre-release from annotated tag **`v1.4.0`** at
**`21b226e`**:
<https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.0>.

Release assets:

```text
PatchworkIsland-1.4.0-Linux-x86_64.zip
PatchworkIsland-1.4.0-Windows-x86_64.zip
README.txt
SHA256SUMS
```

Hashes:

```text
19bc6ee6c7b87172c68fa03ee70980aafc56fa7d3c34f2c54c12a2757f3058dd  linux/PatchworkIsland.x86_64
bd0632feceaa34ef8800522d6e9e8c6c2ef47f4fb3642e0cd8c4a56d246728a7  windows/PatchworkIsland.exe
3bf67da64ef80669841dac68439d7cb2f599deadbc8cadd02ee4cf1803422dba  PatchworkIsland-1.4.0-Linux-x86_64.zip
56542f617d831656bc5c5f7ff8269754902973aed46f325ab5fadae2df920577  PatchworkIsland-1.4.0-Windows-x86_64.zip
```

The packaged Linux binary booted headlessly (exit 0) and passed all twelve
runtime phases (**1253 passed, 0 failed**). The extracted ZIP binary was
byte-identical and passed its boot and `--selftest-city` (**82/0**) checks. The
Windows export is a self-contained PE32+ x86-64 executable reporting file and
product version `1.4.0.0`; **Windows startup is NOT TESTED**. The release is
marked pre-release so it does not replace v1.3.0 as the latest stable release.
