# First playable acceptance contract — final result

**Rule:** Do not mark an item PASS unless exercised with a real game build or an
appropriate automated test. This file records the P8 release result.
Legend: **PASS**, **FAIL**, **NOT TESTED (environment)**, **NOT IMPLEMENTED**.
Screenshot reference boards are not in-game evidence.

Release baseline: `main` after the P8 final-release PR. P7.1 stability hardening
was merged first (PR #9). Full evidence is recorded in `PROGRESS.md` and
`evidence/p8_*.png`.

## A. Boot / controls / visual

- [x] Fresh clone / unpack, exact engine and .NET prerequisites documented;
  `dotnet build` passes. **PASS** — README/README_RUN document Godot 4.7.2 .NET
  and .NET 8; build gives 0 warnings/0 errors.
- [x] Godot editor imports and main scene launches without missing-script,
  missing-resource or node-path errors. **PASS** — `godot --headless --path .
  --editor --quit` exit 0; `--quit-after 120` exit 0; real GUI launch verified.
- [x] 3D village shows five distinguishable buildings, roads, river/bridge,
  trees, player and van. **PASS** — real-input walkthrough and screenshots
  `p8_00`, `p8_02`, `p8_86`, `p8_101`; village overview in earlier evidence.
- [x] Avatar walks with WASD and arrows; Space jumps; camera auto-follows;
  mouse orbit works. **PASS** — P8 movement used WASD throughout; arrows moved
  the player; Space raised Y from −0.04 to 1.08; right-drag orbit changed the
  view (`p8_107_camera_orbit_000.png`).
- [x] E picks up, interacts and enters/exits van in context. Clear target
  highlight and prompt; no precise crosshair aiming. **PASS** — P8 collected all
  pickups, used stations, entered/exited the van; prompts visible in evidence.
- [x] Esc pause/cancel returns control correctly; cursor available in menus;
  keyboard focus and buttons work. **PASS** — P7.1 and P8 pause/settings/GUI
  checks; modal buttons and keyboard close tested.
- [x] Van can drive, steer, reverse, stop, exit and recover if stuck; no loss of
  access to missions. **PASS** — P8 drove all gates, reversed, used R reset,
  auto-recovered after a river crash, and exited safely.
- [x] Accessible at 1280×720; readable mission text, coin count, prompts, signs
  and shop prices; UI scales to 1080p. **PASS** — P8 screenshots at 1280×720 and
  1920×1080 (`p8_104`, `p8_105`); text and targets remain readable.

## B. Complete game: clean-save walkthrough

- [x] M01: exactly 3 apples, deliver to RED HOUSE, +10 and completed state.
  **PASS** — `p8_05`, `p8_07`; wallet 10.
- [x] M02: 5 wood + 3 stone at workshop; total/count feedback; +12. **PASS** —
  `p8_13`, `p8_16`; wallet 22.
- [x] M03: after M02, place 3 supports and 4 planks; incomplete bridge blocks
  crossing and completed bridge permits crossing; +20. **PASS** — `p8_21`–
  `p8_23`; later crossed the river by driving to x=68.
- [x] M04: 2 bread @3 and 1 milk @4 with a 12-coin budget; total 10, change 2;
  deliver to town hall; +12. **PASS** — `p8_26`–`p8_28`; wallet 54.
- [x] M05: four ordered checkpoints, complete even if slow; +15. **PASS** —
  `p8_39`–`p8_49`; completed in 365.7 s with no speed bonus; wallet 69.
- [x] M06: two blue parcels to Red House and one orange to Workshop; correct
  destinations; +15. **PASS** — `p8_53`–`p8_56`; wallet 84.
- [x] M07: 2 swing seats and 4 ropes, visibly repaired playground; +18. **PASS**
  — `p8_58`–`p8_63`; wallet 102.
- [x] M08: bench 6 + flowerbed 4 + lamp 9 within 20-coin task budget; total 19,
  1 left, objects visible; +20. **PASS** — `p8_65`–`p8_67`; wallet 122.
- [x] M09: complete replay course regardless of optional 75-second target;
  optional bonus only if earned; +15. **PASS** — `p8_68`–`p8_76`; completed in
  132.6 s with no bonus; wallet 137.
- [x] M10: table 8 + 2 chairs 5 each + plant 3, total 21; visibly furnished
  home; +20. **PASS** — `p8_79`–`p8_83`; wallet 136 after spending 21.
- [x] Every mission unlocks only when prerequisites are met. When all 10 are
  complete, village celebration and free-roam/replay remain. **PASS** — job
  board showed locked prerequisites until met; `p8_84` celebration;
  `p8_98` all DONE + practice panel.
- [x] Completion reward cannot be claimed twice by talking to a station again,
  reloading, or revisiting the job board. **PASS** — all ten job cards show
  DONE with no Start button after reload; wallet remained 136; exactly-once
  reward regressions pass in the test runner.

## C. Education / error handling

- [x] No mandatory maths text entry or long dialogue; learning proved by
  actions and quantities. **PASS** — all jobs complete through movement, pickups
  and clicks; no typed answer required.
- [x] Short readable instructions, icons and labels; correct answer never
  required to be spoken or typed. **PASS** — HUD/instructions at 720p/1080p.
- [x] Each mission has layered hints and an active location marker, without a
  vague 'figure it out' softlock. **PASS** — objective marker and compass shown
  throughout; H hint displayed at `p8_106`; authored hint layers tested.
- [x] Unwanted/wrong purchases or placements can be cancelled/undone without
  irreversibly exhausting a task budget. **PASS** — M08 undo/refund and M10
  pick-up/move were verified in P5/P7 real-input checks; wrong/empty build
  clicks in P8 never consumed pieces or budget.
- [x] Fallen player and stuck car recover to a safe position. Lost quest resource
  can be recollected. **PASS** — P8 fell into the river and recovered to
  (35.58, 8.46) with “Splash! Back to dry land.” (`p8_88`); R reset and river
  crash recovery worked; pickups remain collectible while the job is active.
- [x] No combat, death, forced timed fail, money penalties or login needed.
  **PASS** — races complete at any pace; no failure state observed; fully
  offline.

## D. Persistence and data integrity

- [x] New Game / Continue works and doesn't destroy save without confirmation.
  **PASS** — fresh start used the normal title flow; New Game confirmation and
  Cancel verified in P7.1/P8.
- [x] Save during M03 (partly built bridge), reload, and observe the same slots
  filled and quest progress intact. **PASS** — P7 real-input evidence
  `p7_02_partial_bridge_reload.png`; structural validation and partial builder
  state covered by tests.
- [x] Complete M03, restart and drive/walk across intact bridge. Persist the
  collider, not only visible material. **PASS** — P7 bridge crossing and P8
  post-reload drive across the bridge to x=68 (`p8_99`).
- [x] Save during M08 and M10, reload with chosen objects still correctly
  placed. **PASS** — P7 real-input evidence `p7_04_park_reload.png`,
  `p7_05_furniture_reload.png`; P8 reloaded furniture visible (`p8_100`).
- [x] Inventory, player/vehicle position, coins, unlocked cosmetics, completed
  and active missions persist. **PASS** — final save contains all ten
  completions, 136 coins, celebration flag, 7 bridge pieces, 6 swing pieces,
  3 park placements and 4 furniture placements; reload verified.
- [x] A missing, invalid or old-version save is handled safely with appropriate
  user feedback and a recoverable start. **PASS** — automated tests cover
  missing/old/corrupt/newer/unrecoverable saves; P7.1 GUI verified backup
  recovery (`p71_25`) and newer-save preservation (`p71_20`–`p71_22`).

## E. Delivery and testing evidence

- [x] Pure state/logic tests cover mission prerequisites, duplicate claim
  prevention, grocery cost/change, park budget, house subtotal, ordered
  checkpoints, snap-slot validation, serialisation round trip and corrupted
  save. **PASS** — 44 passed, 0 failed; see `PROGRESS.md` for the full list.
- [x] Headless project import and startup smoke verification recorded with exact
  commands and exit codes. **PASS** — editor import exit 0; main-scene boot exit
  0; missing-content failure exit 1.
- [x] Real graphical gameplay walkthrough performed. **PASS** — full fresh-save
  M01–M10 walkthrough with real keyboard/mouse input; evidence `p8_00`–`p8_107`.
- [x] README_RUN.md provides setup, running, controls, testing, export, save path
  and known issues. **PASS** — `README.md` and `README_RUN.md` updated.
- [x] Windows x64 export built and launched where the environment allows,
  otherwise documented unambiguously. **PASS (export) / NOT TESTED (startup)** —
  `dist/windows/PatchworkIsland.exe` is a valid PE32+ x86-64 self-contained
  build; no Windows runtime or Wine is available. The Linux export was launched
  and played successfully.
- [x] Source and working assets present; no missing proprietary downloads or
  runtime remote resources. **PASS** — all 3D and UI assets are generated or
  local SVGs; no network dependency; no bundled audio (documented limitation).

## Additional P8 checks

- **Performance:** measured 11–13 FPS at 1280×720 with Mesa 26.0.8 llvmpipe
  (software OpenGL 4.5 compatibility). Scene node count stayed at ~2015–2044
  across New Game resets and completion. **GPU performance: NOT TESTED
  (environment)**.
- **Child usability:** **NOT TESTED (no participants)**. No claim of child
  playtesting is made.
- **Audio:** **NOT IMPLEMENTED** — no bundled audio; visual feedback is complete
  and the persisted sound toggles are ready for a future pass.
- **Text scaling:** **NOT IMPLEMENTED** — fixed text size; layout scales to
  1080p and remains readable.

## Release severity

No release blockers remain.

**BLOCKER:** none found.
**SHOULD FIX:** none known.
**OPTIONAL:** audio, adjustable text scaling, real-GPU performance profiling,
child playtesting.

## Manual test scenario — result

Fresh save → M01 apples → M02 resources → M03 bridge → M04 groceries → M05
checkpoint drive → M06 two-stop delivery → M07 swings → M08 park spending → M09
optional trial → M10 home furniture → restart → verify structures/missions/coins
→ enter free roam. **PASS**, with real inputs and genuine screenshots. During or
alongside the walkthrough: empty/wrong build clicks did not consume pieces,
water recovery returned the player to land, the van reset/recovered after
getting stuck, pause/resume worked, partial saves were verified in P7.1, and the
final save was reloaded with the completed world intact.
