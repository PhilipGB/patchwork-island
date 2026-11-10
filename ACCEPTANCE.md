# First playable acceptance contract — final result

**Rule:** Do not mark an item PASS unless exercised with a real game build or an
appropriate automated test. This file records the P8 release result and the P9
v1.1.0 character addendum.
Legend: **PASS**, **FAIL**, **NOT TESTED (environment)**, **NOT IMPLEMENTED**.
Screenshot reference boards are not in-game evidence.

Release baseline: `main` after the P8 final-release PR. P7.1 stability hardening
was merged first (PR #9). P8.1 reliability hardening is recorded below and in
`PROGRESS.md`; P8.2 session recovery follows it. P9 character choice, clothes
shop and outfits are recorded at the end. Evidence is under `evidence/p8_*.png`,
`evidence/p81_*.png`, `evidence/p82_*.png` and `evidence/p9_*.png`.

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

## P8.1 — Reliability hardening (v1.0.1)

All items below were exercised with the actual project or the dependency-free
test assembly. No item was marked PASS from code inspection alone.

- [x] M10 remains saveable after completion while furniture is picked up or
  unplaced. **PASS** — automated regression completes M10, picks up a chair,
  saves/reloads, replaces it, saves/reloads, then completes M06 with the chair
  unplaced and reloads. Real GUI run: `p81_02`–`p81_07`; the saved file held
  three placements and `FurnitureOwned` still had two chairs. Initial M10
  completion still requires all physical placements (test).
- [x] Long race times cannot invalidate a save. **PASS** — tests cover 59:59,
  60:00, 60:00.001, 66:00, `int.MaxValue` ms, M05 and M09 independently,
  save/reload, completion, later mission completion and no duplicate reward.
  Real GUI run used a controlled `elapsedMs=3,960,000` state (documented
  injection, not a real-time 66-minute session), completed M05 on load, played
  and completed M06 normally, saved, quit and reloaded with both completions and
  130 coins (`p81_10`–`p81_20`). This is a controlled boundary test, not a
  claim of waiting 66 real minutes.
- [x] Manual save success and failure are truthful. **PASS** — write-denial
  under a read-only save directory showed **Save failed** with the diagnostic,
  left the previous file intact, and a retry after restoring permissions showed
  **Paused — Saved** (`p81_21`, `p81_22`, `p81_27`). Automated policy tests
  assert failure never displays success text.
- [x] Automatic save failures are visible but throttled. **PASS** — automated
  tests cover first-failure notification, suppression of repeats across
  automatic reasons, quiet automatic success, and re-notification after
  recovery. Mission-completion failure bypasses the autosave throttle.
- [x] Return-to-title and quit failures are not silent. **PASS** — real GUI
  write-denial produced a **Quit without saving?** choice with the exact error
  and preserved the old save after choosing quit (`p81_25`, `p81_26`).
- [x] A failed New Game does not claim persistence. **PASS** — real GUI run:
  memory reset to 0 coins/new missions while the disk kept the previous 9-job
  130-coin save; the error was explicit and the old save loaded after relaunch
  (`p81_24`–`p81_26`). Automated `SessionSaveState` tests cover first-ever,
  replacement, newer-schema and recovered-backup cases.
- [x] Godot pause semantics are verified, not inferred. **PASS** — two headless
  runtime phases (42 + 10 assertions) use the real scene. Player and van process
  modes are `Pausable`; airborne player physics and van motion stop while
  paused; driving input is released; race time stops; HUD/modal controls keep
  working; closing the final modal resumes the correct mode.
- [x] Pause → New Game → Cancel returns to Pause. **PASS** — runtime assertion
  plus real-input modal checks; Title → New Game → Cancel still returns to
  Title, and Pause/Title → Settings → origin works.
- [x] Domain invariants are enforced below the UI. **PASS** — swing placement
  rejects unknown part types; furniture purchase is capped at the required set;
  negative/excessive race bonuses are rejected without changing the wallet;
  legitimate +5 bonuses and exactly-once rewards still work.
- [x] Mission content cannot silently drift. **PASS** — `MissionContentConsistency`
  deep-compares every scalar, array, object and top-level metadata field in
  `MISSIONS.json` and `data/missions.json`. A negative test mutates `learning`,
  `mechanics.required` and `world` and proves each is detected. Mechanics are
  documented as authored reference data, not runtime balance authority.
- [x] Developer tools are gated and safe. **PASS** — F12 and screenshot tours
  require the editor, `--dev`, `--capture` or a scenario argument; screenshots
  write to `user://screenshots` or `user://dev-captures`; capture failures are
  caught and logged. Normal release sessions do not expose F12.
- [x] The failed-write test is genuine on every platform. **PASS** — the test
  occupies the atomic `.tmp` path with a directory, producing a real I/O failure
  on Linux and Windows, asserts `RecoverableFailure`, checks the previous bytes
  are unchanged, removes the obstacle and verifies a successful retry.
- [x] Existing save compatibility is preserved. **PASS** — schema version is
  unchanged; v1.0.0-shaped saves load in the real GUI (all-complete synthetic
  schema-1 save and the long-race seed). Newer saves remain untouched, corrupt
  saves recover from backup, and no new required save fields were added.
- [x] Build, pure tests, Godot import/boot and runtime tests pass. **PASS** —
  `dotnet build`: 0 warnings/0 errors; tests **61 passed, 0 failed**; editor and
  boot exit 0; runtime **42 + 10 passed, 0 failed**. CI adds a Linux export and
  exported-binary boot plus a Windows pure-test job.
- [x] Exported Linux build launches after the fixes. **PASS** — v1.0.1 export
  (`cb60a80`) launched under Xvfb; title, Continue, 9-job save, 130 coins and
  job board loaded (`p81_30`, `p81_31`); headless export boot exit 0; the
  packaged binary itself passed 42 + 10 runtime assertions. F12 produced no
  screenshot in the normal export (23 files before/after), confirming the
  developer gate. Windows export produced and inspected with version `1.0.1.0`;
  **Windows startup NOT TESTED** (no Windows/Wine). SHA-256 checksums verified.

No P8 regression was found: all ten missions, bridge/swing/park restoration,
furniture movement, free roam, reward-free practice, celebration-once and
exactly-once rewards remain covered by the retained P8 evidence and tests.

## P8.2 — Session recovery hardening (v1.0.2)

All items below were exercised with the real Godot scene, the dependency-free
test assembly, or both. Failure injection is a process-local internal seam and
is not active in normal gameplay.

- [x] Continue restores the persisted session after a failed New Game. **PASS**
  — runtime recovery phase: seed nine-mission/130-coin save → failed New Game →
  Return to title → Return anyway → Continue. The scene reloaded and restored
  130 coins, nine completions, the active M10 job, bridge, six swing pieces,
  three park placements and the saved player position. The save bytes were
  unchanged after reload. Fail-first: the old Continue path produced
  `FAIL Continue requested a persisted-session reload`.
- [x] Unsaved gameplay changes are discarded at the title. **PASS** — moved the
  player, forced the return save to fail, chose Return anyway, and Continued.
  The persisted position and wallet were restored, not the unsaved movement.
- [x] A successful New Game becomes the new persisted session. **PASS** — New
  Game succeeded after a persisted nine-mission village, wrote a 0-coin save,
  and Continue loaded the new village while `MemoryMatchesPersisted` was true.
- [x] A failed first-ever save creates no false Continue. **PASS** — no-save
  title → forced first-write failure → Return anyway; no Continue was offered,
  no file was created, and the notice did not promise a nonexistent Continue.
  `Start adventure` then created a genuinely fresh saved session.
- [x] Recovered backup still continues the recovered state. **PASS** — a valid
  backup-only save was recovered, a failed New Game left the backup untouched,
  and Continue restored the recovered nine-mission village.
- [x] Authorised newer-save replacement is retryable. **PASS** — schema-99 save
  → cancellation left permissions clear and bytes unchanged → confirmed New
  Game failed → permission survived → Return anyway cleared it → second
  confirmed New Game failed → Save Now after clearing the simulated I/O error
  replaced the save with schema 1. Exactly one `newer-v99` preserved copy
  existed and matched the original bytes. Fail-first: removing explicit
  authorisation produced 12 runtime failures, including
  `failed replacement keeps the confirmed permission for Save Now`.
- [x] Unauthorised newer saves remain protected. **PASS** — stepped
  `NewerSavePresent`/`NewerReplacementAuthorized` assertions cover load,
  cancellation, failure, discard, success and relaunch. No caller override
  remains in `SaveGame`.
- [x] Settings failures are visible inside the settings modal. **PASS** — real
  runtime phase opened Pause → Settings, injected a write failure, toggled
  Music, and observed the inline “could not be saved” message while the modal
  stayed open. The toggle took effect in memory.
- [x] Settings failures do not suppress the next autosave warning. **PASS** —
  immediately after the settings failure, an injected autosave failure showed
  “Autosave failed” in the HUD. Pure policy tests also assert separate
  throttles.
- [x] Successful settings retry clears the failure message. **PASS** — reopened
  Settings after recovery, toggled successfully, observed “Setting applied and
  saved.”, and returned to Pause correctly.
- [x] Duplicate mission JSON properties are rejected at any depth. **PASS** —
  `MissionContentConsistency` uses `TryAdd` and reports the JSON path/property.
  Tests cover duplicate top-level keys, nested mechanics keys and array-element
  objects, plus order-independent objects, order-sensitive arrays, invalid
  JSON and the authored files. Fail-first with the old dictionary assignment:
  `FAIL duplicate top-level property rejected`.
- [x] Optional race bonuses are verified by the domain. **PASS** — M05/M09
  reject +5 when persisted elapsed time is 120 s, while exact 90 s / 75 s
  boundaries succeed. The millisecond round-up contract and long-race
  saturation are unchanged. Fail-first: `FAIL M05 rejects a bonus on a slow
  race`.
- [x] Deterministic write failure injection works without permissions. **PASS**
  — pure test injects an `IOException` for a specific path, verifies the exact
  previous bytes remain, clears the hook and verifies a successful retry.
- [x] Godot integration coverage executes the real recovery transitions.
  **PASS** — `--selftest-recovery` uses a separate `user://` save, reloads the
  real scene eight times, and passes **98 assertions**. Existing pause/resume
  phases remain **42 + 10**. No assertion counts an unexecuted test.
- [x] Build, imports, runtime and CI remain green. **PASS** — build 0 warnings/
  0 errors; pure **67 passed, 0 failed**; editor/boot exit 0; runtime
  **42 + 10 + 98 passed, 0 failed**; GitHub Actions Linux build/runtime/export/
  exported-boot and Windows pure tests pass.
- [x] Save compatibility is preserved. **PASS** — schema remains version 1 with
  no new required fields. Newer-save protection, corrupt/backup recovery,
  exactly-once rewards, M10 unplaced furniture and long race counters remain
  covered by the retained tests.
- [x] Linux release export launches and passes smoke tests. **PASS** — v1.0.2
  export (version `1.0.2`) was launched under Xvfb, loaded the existing
  nine-job/130-coin save and job board (`p82_00`, `p82_01`), passed
  `42 + 10 + 98` runtime assertions from the packaged executable, and F12 wrote
  no screenshot. **Windows export: PASS** — PE32+ x86-64, version `1.0.2.0`,
  mission content, assemblies and self-contained .NET 8 runtime present.
  **Windows startup: NOT TESTED** (no Windows runtime or Wine). SHA-256
  checksums verified for both executables and both ZIPs.

No P8.1 regression was found: the pause/modal, M10 rearrangement, long-race,
save-feedback and domain-guard coverage remains in place. A full fresh-save
ten-mission walkthrough was **not** repeated in P8.2; the retained v1.0.0/P8
walkthrough remains the gameplay baseline, supplemented by the targeted
real-input and runtime regressions above.

## P9 — Character choice, clothes shop and outfits (v1.1.0)

Status: **PASS** on the v1.0.2 baseline, including the four review corrections.
Project and export metadata report `1.1.0`; the save schema is **2** and is
versioned separately from the release.

- [x] The player chooses a Boy or Girl presentation before a new game.
  **PASS** — New Game opens a large two-card chooser; Boy and Girl have distinct
  paper-doll previews, and the choice can be changed later at the clothes shop.
  Runtime tests press the real **Play as Boy** and **Play as Girl** buttons,
  verify each saved choice through Continue, and press the real **Cancel** both
  for a fresh game and while replacing an existing save.
- [x] The girl reflects the child sketch's spirit. **PASS** — long hair silhouette,
  bow accessory, flared dress, simple shoes, blocky face and flat-shaded
  primitives; readable at gameplay distance. The boy has matching short hair,
  shirt, shorts and shoes.
- [x] A clothes shop exists in town. **PASS** — a pink/lavender building with a
  **CLOTHES** sign, striped awning, `DRESS UP` sign and two mannequins sits on
  the north side of the main road east of the market, with a short path and a
  working interaction prompt.
- [x] The shop sells outfits for coins. **PASS** — Sunny Day 8, Meadow Green 10,
  Berry Pink 12, Starry Night 15, plus the free Everyday outfit. All are shared
  by both presets. Buying deducts the exact price once and wears the outfit.
- [x] Already-owned outfits are not re-purchased; owned outfits can be worn
  free. **PASS** — duplicate purchases, unowned equips and insufficient funds
  are rejected by the domain; the shop disables those buttons and displays
  **Not enough coins (have/price)**.
- [x] Every outfit is previewable regardless of ownership or coins. **PASS** —
  every row has an always-enabled **Preview** control plus hover/focus preview;
  the mirror shows the selected outfit, its price, the current wallet and
  **Not enough coins** when applicable. Buying stays disabled when unaffordable.
  Runtime checks prove previewing never deducts coins, buys or equips anything.
- [x] Equipped clothing visibly changes the player. **PASS** — the 3D character
  rebuilds its outfit colours and silhouette while retaining the chosen
  gender; runtime assertions compare player preset/outfit and appearance
  revision before/after.
- [x] Character and clothing persist through save/load. **PASS** — pure JSON
  round-trip and real Continue/relaunch checks restore the preset, owned list
  and equipped outfit. Wallet deduction survives the same save.
- [x] A failed clothes-shop save does not leave a stale error after recovery.
  **PASS** — runtime injects a write failure, sees the inline modal error, then
  succeeds on the next wear and confirms the old message is gone.
- [x] Older v1.0.0–v1.0.2 saves load and migrate safely. **PASS** — schema 1
  loads into schema 2 with wallet, missions, tutorials, pickups, inventory,
  player position and settings intact; absent customisation data defaults to
  Boy + Everyday + no purchases. The original schema 1 file remains as `.bak`.
  Unknown or unowned customisation IDs are rejected by save validation.
- [x] Unsupported newer schemas are refused without modification. **PASS** —
  schema 3 and 99 loads return `UnsupportedNewer` and leave every byte, backup
  and filename untouched, even when their other fields use types incompatible
  with the current model (`Coins` as a string, `CharacterPreset` as an array).
  The version is read with `JsonDocument` before typed deserialisation. An
  ordinary save over either file is refused before writing; only an explicitly
  confirmed New Game replacement preserves a `newer-vN` copy and may proceed.
- [x] New game reset behaves correctly. **PASS** — runtime reset after a reload
  clears owned outfits, returns to Everyday, clears the wallet and applies the
  newly chosen Boy silhouette.
- [x] Reward accounting remains exact. **PASS** — pure test buys an 8-coin outfit
  after M01, then completes M02 and verifies the exact 14-coin balance and that
  an owned outfit cannot be re-purchased.
- [x] Automated tests cover the new systems. **PASS** — pure runner **78 passed,
  0 failed**; runtime **103 + 19 + 101 passed, 0 failed**. The runtime phase
  presses the actual Boy/Girl/Cancel, Preview, Buy and Wear buttons, checks the
  mirror, wallet display, insufficient funds, ownership, visual appearance,
  save-failure recovery and Continue.
- [x] Existing tests continue passing. **PASS** — all retained mission, save,
  recovery, pause, driving and mission-catalogue assertions remain green.
- [x] The game builds and runs. **PASS** — `dotnet build` 0 warnings/0 errors;
  Godot editor import and headless boot exit 0; Xvfb capture run produced P9
  screenshots without script errors.
- [x] Save validation and schema compatibility are documented. **PASS** —
  schema 2 is current; schema 1 migrates; `CharacterPreset` is `boy`/`girl`;
  `OwnedOutfits` stores purchasable IDs; `EquippedOutfit` must be known and
  owned (Everyday is always owned).
- [x] Fail-first coverage is meaningful. **PASS** — controlled probe removing
  the insufficient-funds guard failed `insufficient coins cannot buy an outfit`;
  removing the duplicate-ownership guard failed the reward-accounting repeat
  purchase assertion. Both files were restored before the final green run.

**Evidence:** `evidence/p9_01_character_choice.png`,
`p9_02_girl_at_clothes_shop.png`, `p9_03_clothes_shop_ui.png`,
`p9_04_outfit_owned_and_worn.png`, `p9_05_girl_sunny_outfit_world.png`,
`p9_06_boy_default_outfit_world.png`; build/test log summary in
`evidence/p9_verification.txt`.

**Export verification:** v1.1.0 Linux and Windows exports were produced from
the final source. The Linux packaged binary booted headlessly and passed
**103 + 19 + 101** runtime assertions; the Windows executable is PE32+ x86-64
with file/product version `1.1.0.0`. **Windows startup was not tested** (no
Windows runtime/Wine).

**Remaining graphical gaps:** the in-shop preview is a deliberately simple 2D
paper doll rather than a live 3D turntable; no skeletal rig was introduced;
Windows startup and real-GPU/child-playtesting remain untested.

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

## v1.2.0 — Living Village Update (P15 release gate)

This section records the v1.2.0 release verification. It does not replace the
earlier P9 acceptance; it adds the second chapter, audio, villagers, traffic,
rally expansion, packaged builds and the remaining blockers.

### Feature acceptance

- [x] All twenty authored missions are implemented and completable. **PASS** —
  pure full-walkthrough test completes M01–M20 in one clean service state and
  round-trips the save. Real-UI phases cover M11–M15 (`--selftest-chapter`) and
  M16–M20 (`--selftest-finale`), including exactly-once rewards and the second
  finale.
- [x] A completed v1.1.0 save keeps its village and unlocks M11 immediately.
  **PASS** — `--selftest-release` loads a schema 2 ten-complete save with 137
  coins, girl + Berry outfit, red cap, bridge/park/furniture state and the
  original celebration already seen; Continue opens the job board, M11 starts,
  the original celebration does not repeat and the save rewrites at schema 3.
  Pure migration tests also preserve settings, player/van snapshots and
  possessions.
- [x] M05/M09 four-gate IDs, ordering, counters and 90 s / 75 s optional bonuses
  remain compatible. **PASS** — pure boundary tests and real-physics rally phase
  (`M05 18.97 s`, `M09 18.98 s`, both +5). M19 adds six gates and a 155 s medal
  without changing M05/M09.
- [x] Six named NPC villagers exist with readable labels, portraits, patrols
  and dialogue. **PASS** — living phase and P11 captures.
- [x] Two safe NPC drivers stay on authored roads, yield and do not block
  stations. **PASS** — 24-second living phase, 238 assertions.
- [x] Music/SFX are bundled and settings persist. **PASS** — all loops/effects
  decode, buses exist, Music/SFX/UI/Vehicles routing is verified, and a
  20-second capture of the real PipeWire sink monitor during the packaged Linux
  build contained nonzero PCM (peak 20165, RMS 944.0). **Human listening:
  PASS** — the product owner verified the packaged build on the target
  Linux/PipeWire system on 2026-11-09.
- [x] World visual cleanup and larger continuous rally track. **PASS** — P12
  captures/tests, collision road mesh, gate readability, icon scaling, P14
  visual fixes V09–V12, and P15.1 bridge/cap fixes V13–V15.
- [x] Real-GPU visual and performance check. **PASS** — AMD Radeon 890M (Mesa
  26.0.8, OpenGL 4.6): 1280×720 mean 16.69 ms / p95 17.35 ms; 1920×1080 mean
  16.67 ms / p95 17.28 ms over 600 frames. Real-Radeon P14 captures are in
  `evidence/p15_radeon_*.png`. No v1.1.0 real-GPU probe exists, so a baseline
  comparison is **NOT TESTED**.
- [x] GUI readability and collision QA. **PASS** for automated/real-render
  evidence; child playtesting and a subjective accessibility review remain
  **NOT TESTED**.
- [x] Save schema 3 and migration documented. **PASS** — schema 1→2→3; current
  schema 3; schema 4+ refused untouched.
- [x] Full CI and packaged Linux execution. **PASS** — pure **106**, runtime
  161 + 21 + 101 + 238 + 10 + 217 + 140 + **18** + 22; packaged Linux passed
  the same phases with exit 0 and zero leaked unsafe references.
- [x] Windows compilation/export. **PASS (compile/export only)** — PE32+ x86-64,
  file/product version `1.2.0.0`. **Windows startup is NOT TESTED** (no Windows
  runtime or Wine); the product owner waived Windows startup for this
  Linux-first release.
- [x] Version metadata and release docs. **PASS** — project version `1.2.0`,
  export metadata `1.2.0.0`, updated README/README_RUN/release notes.
- [x] Licensed-source audit. **PASS** — supplied audio/PNG assets are original
  handoff-generated work; GLB bodies were inspected and intentionally not used
  for animated bodies; provenance remains in `docs/ASSET_USAGE.md`.
- [x] SHA-256 archives. **PASS (local artifacts)** — Linux and Windows v1.2.0
  ZIPs and `dist/SHA256SUMS` generated from the exported binaries.

### P15.1 bridge and visual hotfix

The 2026-11-09 playtest reported that the van could cross the bridge but still
met an invisible obstacle, plus grey-slab/rail artefacts and a floating cap.

- Bridge/road seam: the east road box started 1.45 m inside the bridge deck and
  the rally connector ramp started under the planks, painting a grey slab over
  the wood and leaving a raised edge. The road now starts at the bridge edge,
  the connector starts at x=50.4 with its ramp removed, road/shoulder collision
  is 1 cm above the lawn (down from 6.5 cm), and the collision proof checks the
  `RallyRoadCollision` body by name (52/52 rays).
- Visuals: bank rocks are kept ≥6 m from the bridge, a dark deck underlay
  closes the plank gaps, side rails end at the water span, the connector has
  sandy shoulders, and the long-hair back meets the lowered cap.
- Traversal: **18 passed** — fast and slow centre crossings, both lateral road
  edges, westbound, no vertical step, both gate-1 walk/jump checks and the
  from-rest lip walk.
- Full source gates after the hotfix: build 0/0; pure **106**; selftest 161;
  resume 21; recovery 101; living 238; rally 10 (M05 18.99 s, M09 18.97 s, +5);
  chapter 217; finale 140 (M19 22.32 s, +5); traversal **18**; release 22.
  No leaked unsafe references.
- New evidence: `p14_*_15_bridge_seam.png`, `p14_*_16_bridge_junction.png`,
  `p14_*_17_player_cap.png`; the full P14 set was re-captured at exact 720p and
  true 1080p.

### Release severity

**BLOCKER (publication):** none for the Linux-first release after the P15.1
hotfix. Human listening of the packaged audio is **PASS** (product-owner
verified). Windows startup remains **NOT TESTED** but was explicitly waived for
this release; it is recorded as an untested platform, not claimed working.

**SHOULD FIX:** none known in code or saves.

**OPTIONAL:** child playtesting, accessibility review, a v1.1.0 real-GPU
baseline probe, Windows startup when a Windows machine is available.

**Tag/publish status:** **v1.2.0 tagged and published.** Tag `v1.2.0` points at
`d0eff9d` (merge of PR #23), whose tree is identical to green PR head
`b7d3bb9`. Main CI run
[42726477501](https://github.com/PhilipGB/patchwork-island/actions/runs/42726477501)
passed. Release:
[v1.2.0](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.2.0)
with Linux/Windows ZIPs, `README.txt` and `SHA256SUMS`. Package hashes and
verification logs are in `PROGRESS.md` and `evidence/p15_verification.txt`.

### v1.2.1 M18 persistence and traffic hotfix

A focused follow-up to the v1.2.0 review. No save-schema, content or rendering
changes.

- **P2 fixed:** `CollectRepairPart` now requires the physical pickup ID, records
  it in `CollectedPickups`, refuses a repeat collection and rejects a pickup
  whose part type does not match. The real M18 pickup path passes the ID, and
  `SpawnRepairParts` uses the shared `FinaleRules.RepairPickupIds` list.
- **Legacy saves repaired:** `GameData.Normalise` infers acquired M18 pickup IDs
  from `has_* + *_fitted` counts for saves written by v1.2.0. Already-recorded
  IDs win; the normaliser never over-marks and never adds more than were
  earned. Save schema remains 3.
- **P3 fixed:** `TrafficCar` gains a terminal `Stopped` state. Off-island
  vehicles zero their speed/velocity, stop engine loops, disable physics
  processing and log once. `StopEvents` proves the failure is not repeated.
- **Real relaunch regression:** new `--selftest-hotfix` (48 assertions) starts
  M18 through the real board, collects one tyre, saves, reloads the scene,
  confirms the pickup is absent, collects the other three parts, completes the
  repair and checks the 24-coin reward. It also drops a traffic car off the
  island and verifies one stop event and a stable resting position.
- **Pure gates:** build 0/0; pure **107** (new partial-save/legacy tests).
- **Source runtime:** 161 + 21 + 101 + 238 + 10 + 217 + 48 + 140 + 18 + 22 =
  **976 passed, 0 failed**, every process exit 0 with zero leaked unsafe
  references.
- **Packaged Linux:** all ten phases passed (including hotfix 48), exit 0 and
  zero leaked references. Rally M05 18.97 s, M09 19.03 s, M19 22.32 s (+5).
- **Exports:** Linux ELF x86-64; Windows PE32+ with file/product version
  `1.2.1.0`. Windows startup remains **NOT TESTED** and waived.
- **Audio:** unchanged from v1.2.0; packaged human listening remains **PASS**
  from the v1.2.0 verification. v1.2.1 was not separately auditioned.
- **Package hashes:**
  - `linux/PatchworkIsland.x86_64` `610e29c12067e8df90fba576e21c5ce941a41a88bdb1cdeaa31262f5c14ba6c1`
  - `windows/PatchworkIsland.exe` `1421e456ce90dd5dadbb6451d536b34ecc71e2e1649fdee1f1b420c589308aba`
  - `PatchworkIsland-1.2.1-Linux-x86_64.zip` `cb04e909db41ba19bb74199653c7c6018356e2904d4f6b092f8358aa1408fdf6`
  - `PatchworkIsland-1.2.1-Windows-x86_64.zip` `ae40a90dfbb640b0cf61941831ce69b83800f2cade768668c4f93c34377d0ac5`

**Tag/publish status:** v1.2.0 remains published. The v1.2.1 tag will point at
the green merge head of the focused persistence PR; the exact commit and
release URL are recorded in `PROGRESS.md` and
`docs/RELEASE_NOTES_v1.2.1.md`.

### Manual walkthrough status

The complete M01–M20 domain walkthrough is automated and green. The real-UI
phases collectively cover M11–M20; the original M01–M10 real-input walkthrough
from P2/P9 remains the prior evidence. One continuous human M01–M20 sitting is
**NOT TESTED** in this environment.
