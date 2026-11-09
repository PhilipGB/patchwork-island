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
   defaults without touching progression. Loading schema 3/99 returns
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
