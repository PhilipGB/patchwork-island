# First playable acceptance contract — final result

> Current unreleased Linux quality results and the baseline comparison are in
> [the Linux quality report](docs/LINUX_QUALITY_PASS.md).
> The release-specific results below are retained as historical evidence.


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

**Tag/publish status:** **v1.2.1 tagged and published.** Tag `v1.2.1` points at
`2d6647b` (merge of PR #25), whose tree is identical to green PR head
`49c7a62`. Main CI run
[42735724391](https://github.com/PhilipGB/patchwork-island/actions/runs/42735724391)
passed. Release:
[v1.2.1](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.2.1)
with Linux/Windows ZIPs, `README.txt` and `SHA256SUMS`. v1.2.0 remains
published as the previous release.

### Manual walkthrough status

The complete M01–M20 domain walkthrough is automated and green. The real-UI
phases collectively cover M11–M20; the original M01–M10 real-input walkthrough
from P2/P9 remains the prior evidence. One continuous human M01–M20 sitting is
**NOT TESTED** in this environment.

## v1.3.0 — scene-based town migration (P16 gate)

Scope: replace the runtime procedural static town with the exact baked
`Town_Baked.tscn` while keeping v1.2.1 gameplay, schema-3 saves and mission
content byte-for-byte compatible. Source version is `1.3.0`; no tag or release
was created. Baseline is `2d6647b` (v1.2.1).

| # | Handoff parity requirement | Result |
|---|---|---|
| 1 | `Town_Baked` imports/opens with no missing resources | **PASS** — editor import exit 0; scene opens and every packaged phase loads it. |
| 2 | Source placement/yaw/wall dimensions/doors match manifest ≤0.01 m | **PASS** — adapter enforces six centres/yaws and wall boxes; all 75 door/pad/pickup anchors match. Pure scene tests repeat the checks. |
| 3 | All critical anchors resolve exactly once | **PASS** — 75/75 anchors cached, unique and validated; wrong count fails loudly. |
| 4 | Equivalent collision; camera-only blockers layer 16 | **PASS** — layer-1 ground/wall/rally collision rays, open river gap, layer-16 door blocker checks, plus the real traversal suite across both bridge directions. |
| 5 | World object/scene IDs match historical saves; no renames | **PASS** — dynamic roots keep source names; `--selftest-restore` proves partial bridge/swing/park/furniture/finale states reload. |
| 6 | All 20 missions complete through stations | **PASS** — 1124 source runtime assertions and every packaged phase. |
| 7 | Partial M03/M07/M08/M10/M15/M18/M20 saves restore visuals | **PASS** — new `--selftest-restore` (54 assertions) reloads seven real fixtures. |
| 8 | v1.2.1 158-coin save with placements/outfits/bridge restores | **PASS** — release phase writes and reloads a schema-3 158-coin fixture with bridge, swings, park, furniture, outfits and player/van transforms. |
| 9 | NPC patrols and both free-roam traffic routes unchanged | **PASS** — adapter validates every waypoint against `NpcCatalog`/`TrafficCatalog`; pure tests validate the same marker scenes. |
| 10 | Linux export boot, CI, packaged runtime pass | **PASS (local)** — packaged Linux build passes all 11 phases and `--quit-after 120`; CI command list updated. GitHub CI not yet run on a pushed commit. |
| 11 | Comparable GPU FPS/frame time, no large regression | **PASS** — real Radeon 890M: identical 3394 draw calls / 66069 primitives; 16.68–16.71 ms mean at 720p and 1080p vs 16.67–16.71 ms baseline. |
| 12 | Visual check at 1280×720 and 1920×1080 | **PASS (capture)** — full 18-shot P14 tours captured at both sizes in `evidence/v13_scene_world/`; known v1.2.1 glitches remain by design. |

**Build/test gates:** build 0/0; pure **111 passed**; source runtime **1124
passed, 0 failed**; packaged Linux runtime **1124 passed, 0 failed**; zero leaked
unsafe references; packaged boot exit 0.

**Final package:** `dist/linux/PatchworkIsland.x86_64`, SHA-256
`439083f34467e3bbfe7d1e93202745e246f2df68042ac903766e968096e2455a`.

**Remaining severity:** no migration blocker known. Visual polish, Windows
startup and one continuous human M01–M20 sitting remain untested/deferred; they
are not claimed. No tag or release has been created.

## v1.3.0 — canonical editor-authoritative town (P17 gate)

Scope: replace the baked/procedural runtime town with the supplied canonical
scene kit while preserving schema-3 saves, M01–M20 and existing controllers.
Baseline remains `2d6647b` (v1.2.1); branch `v1.3.0-canonical-town` stacks on
PR #27. No tag, merge or release created.

| # | Canonical handoff requirement | Result |
|---|---|---|
| 1 | Import every authored scene without errors | **PASS** — editor import exit 0; every new `.tscn` loaded headlessly with no failures. |
| 2 | Create canonical `Town.tscn` from `Town_Assets.tscn`; keep bake as reference | **PASS** — runtime loads `Town.tscn`; `Town_Assets.tscn` and `Town_Baked.tscn` are reference-only. No runtime `WorldBuilder.Build()` or `SceneBakeTool`. |
| 3 | Scene-authoritative doors and markers; no C# position equality | **PASS** — 75 anchors, six building `DoorMarker`s, gate nodes and build/sign/festival markers are read from scene transforms. |
| 4 | NPC/traffic routes bind to scene marker groups | **PASS** — all six NPC routes and both free-roam traffic routes resolve from `NpcPatrolMarkers`/`TrafficRouteMarkers`; M05/M09 and M19 gates resolve from authored roots. |
| 5 | Keep dynamic actor controllers/collision; replace visual subtrees | **PASS** — GLB templates now render player, six NPCs, van, two town cars, rally rival and all pickups; animation pivots, wheel spin and save state are retained. |
| 6 | Keep stateful roots, built pieces, display slots and recovery | **PASS** — bridge/swing/park/furniture ownership unchanged; bakery/library slots and sign sockets are authored scene nodes; restore phase is green. |
| 7 | Collision: ground/roads/rally layer 1, camera blockers layer 16 | **PASS** — road roadbed is flush at `y=0`, removing junction/bridge lips; all traversal checks pass in both directions. |
| 8 | Editor-move proof: ClothesShop +5 m, interaction follows, old save loads | **PASS** — door moved `3.5 → 8.5` with centre `7 → 12`; prompt, Sunny Day purchase, resume (21) and restore (54) all passed, then layout restored. |
| 9 | Save schema 3, v1.2.1 full/partial saves, M18 pickups, celebrations | **PASS** — unchanged schema and all historical phases green. |
| 10 | 11 Godot runtime phases including restore | **PASS** — source and packaged Linux: **1166 passed, 0 failed**, every exit 0. |
| 11 | 720p/1080p screenshots and real-GPU frame time | **PASS** — P14 18/18 at 1280×720 and 1920×1080, P12 10/10 at 720p; canonical 16.68–17.08 ms vs baseline 16.70–16.71 ms with fewer draws/primitives. |
| 12 | CI runs packaged restore; exact-head green | **PASS** — [run 42850939120](https://github.com/PhilipGB/patchwork-island/actions/runs/42850939120) passed `build-test` (6m25s) and `windows-tests` (2m13s), including packaged `--selftest-restore` (54 passed). |

**Build/test gates:** build 0/0; pure **111 passed**; source runtime **1166
passed, 0 failed**; packaged Linux runtime **1166 passed, 0 failed**; packaged
boot exit 0.

**Deviations documented:** no scooter GLB (stable `town_scooter` route uses the
green town car), four scene-authored `BROKEN` labels (two per swing frame), and
authored sign-socket/display-slot scenes added where the candidate scene lacked
existing gameplay IDs. See `docs/CANONICAL_TOWN.md` and
`docs/scene_bindings.json`.

**Remaining severity:** no canonical migration blocker known. Human visual and
one continuous M01–M20 sitting remain **NOT TESTED**; Windows startup remains
**NOT TESTED** and is not claimed. No tag or release created.

## v1.3.0 — canonical visual audit (P18 gate)

A full 1080p audit covered every district, all static signs from both sides,
the rally gates, celebration/festival state and build-mode previews.

| # | Visual check | Result |
|---|---|---|
| 1 | No mirrored static text from viewer-facing sides | **PASS** — Bakery, Library, Job Board, Playground, Community Garden, Stone Patch, Race Meadow, Bridge Works, Sign Socket and Directional Sign labels corrected and re-captured. |
| 2 | Freestanding signs have visible supports | **PASS** — posts added to Playground, Community Garden, Stone Patch, Race Meadow and Bridge Works. |
| 3 | No duplicated BROKEN or BRIDGE WORKS assets | **PASS** — duplicate fixed BROKEN label and legacy BridgeWorksite instance removed. |
| 4 | No lamps inside buildings, sandpits or sign sightlines | **PASS** — road-edge lamps moved to verges; town-hall and playground lamps relocated; redundant direction-post lamp removed. |
| 5 | No signs or benches standing in roads | **PASS** — Playground sign moved to its entrance verge; direction post/bench moved off the carriageway; rally-start bench removed. |
| 6 | Rally gate numbers readable from both approaches | **PASS** — both faces of all six gate panels carry correctly rotated numbers. |
| 7 | Build ghosts hidden outside build mode | **PASS** — SUPPORT/PLANK/ROPE/SEAT ghosts stay hidden until the matching build mode opens; regression added. |
| 8 | Physics/traversal unchanged | **PASS** — all 18 traversal assertions pass, including bridge crossings in both directions. |

**Gates:** pure **111 passed**; source runtime **1168 passed, 0 failed** across
all 11 phases. Packaged Linux is rebuilt and verified in `PROGRESS.md`.

**Evidence:** ignored `evidence/v13_canonical_visual/` (39 audit views plus P12,
P13 and P14 1080p tours). No generated images are committed.

**Remaining severity:** no visual blocker known. Human sign-off, one continuous
M01–M20 sitting and Windows startup remain **NOT TESTED**. No tag or release.

## v1.3.0 — gameplay-height defect re-check (V25–V35)

| # | Visual check | Result |
|---|---|---|
| 1 | Bridge Works sign readable from both sides, with no clipped text | **PASS** — two single-sided labels, posts behind the panel. |
| 2 | Gabled buildings closed at both ends | **PASS** — two wall-coloured gables added to RedHouse, PlayerHouse, Workshop, Market, ClothesShop and TownHall. |
| 3 | Playground, swings, sign and interaction marker clear of the road | **PASS** — playground moved south, marker on sand, swings 8 m from the road. |
| 4 | Playground sign clear of the swing structure | **PASS** — entrance sign centred, no intersection. |
| 5 | All six rally gate numbers readable from both approaches | **PASS** — panels raised above the beam and dual-faced. |
| 6 | No vehicle roof moiré or detached postal label | **PASS** — van and all traffic/rival cars use a contact shadow instead of self-shadowing; label flush on the rear door. |
| 7 | Pip car label reads as attached to the vehicle | **PASS** — lowered, enlarged, outlined. |
| 8 | Road junctions continuous with no stray patches or crossing lane lines | **PASS** — slabs trimmed and levelled, markings cleared at mouths. |
| 9 | Crossing stripes stay inside the side road | **PASS** — crossing narrowed to 3.2 m. |
| 10 | Lamps clear of carriageway and awnings | **PASS** — two lamps relocated. |
| 11 | World labels readable at gameplay distance | **PASS** — swing, car, flag and postal labels enlarged/outlined. |

**Gates:** pure **111 passed**; source runtime **1168 passed, 0 failed** across
all 11 phases. Packaged Linux verification is recorded in `PROGRESS.md`.

**Evidence:** ignored `evidence/v13_canonical_visual/v13.1_recheck/` (31
gameplay-height probe views plus final 1080p P12/P13/P14 tours).

**Remaining severity:** no visual blocker known. Human sign-off, one continuous
M01–M20 sitting and Windows startup remain **NOT TESTED**. No tag or release.

## v1.3.0 — road and playground consistency follow-up (P3)

| # | Consistency check | Result |
|---|---|---|
| 1 | Every removed visual road patch has its collider removed | **PASS** — stale `Collision_07/09/10` and their shapes removed; 8 slabs have 8 colliders. |
| 2 | Every remaining collider matches a visible slab | **PASS** — five side-road colliders repositioned/resized to the trimmed mesh extents; pure test matches every centre and half-extent within 0.01 m. |
| 3 | `RoadNetwork` matches the authored asphalt | **PASS** — eight rectangles mirror `TownRoads_Collidable.tscn`; grass beside the former stray patches is no longer classified as road. |
| 4 | Runtime rejects count drift | **PASS** — `WorldSceneAdapter` requires one layer-1 collider per visible `RoadSlab`. |
| 5 | Playground interaction position is scene-owned | **PASS** — `Playground/WorksiteMarker` drives the station; no hard-coded playground coordinate remains. |
| 6 | Existing `PlaygroundCenter` anchor is synchronised | **PASS** — anchor is `(-38,0,-9.5)` and is rejected at load if it drifts from the Playground root. |
| 7 | Regression coverage | **PASS** — pure test parses both authored road scenes and `RoadNetwork`; runtime test covers anchor, marker and station. |

**Gates:** pure **113 passed**; source runtime **1171 passed, 0 failed** across
all 11 phases. Packaged verification is recorded in `PROGRESS.md`.

**Remaining severity:** no known consistency blocker. Human visual sign-off,
one continuous M01–M20 sitting and Windows startup remain **NOT TESTED**.

## v1.3.0 — round-3 visual defects (V36–V48)

| # | Visual check | Result |
|---|---|---|
| 1 | V36 all six buildings have closed gable ends | **PASS** — `GableEast`/`GableWest` on the ridge ends, rotated 90°, sized to each building depth, with shadow-free gable material; pure test checks all six. |
| 2 | V37 banner no longer dominates the Town Hall | **PASS** — reduced from 8×1.5 m floating panel to 4.72×0.75 m free-standing banner at the festival entrance `(15,3.35,19.8)`; Town Hall frontage completely clear and the centre lantern moved to `x=18.6` so it cannot cover the back text. |
| 3 | V38 banner readable from both sides | **PASS** — two single-sided text labels on opposite faces, no billboard, offsets outside the panel; yellow emission keeps the shaded face readable. |
| 4 | V39 directional signs no longer cluster over NPCs/Town Hall | **PASS** — four arms on one post at `(-5.0,0,-4.5)`, outside Milo's patrol by >2.5 m; all eight front/back arrow strings now point at the real destinations. |
| 5 | V40 GOLD LAP text stays attached and readable | **PASS** — two single-sided 56 px labels just outside each flag face; flag moved to `(64.5,0,-15.5)` so the start/finish pole no longer hides `GO`. |
| 6 | V41 Pip car status reads as vehicle-attached | **PASS** — scene-owned `StatusAnchor`, 36 px label at 1.72 m. |
| 7 | V42 NPC nameplates do not overlap central signage | **PASS** — 56 px labels, distance scaling, 34 m range; signpost moved out of Milo's route. |
| 8 | V43 delivery van lettering readable from behind | **PASS** — 42 px, 6 px outline, 1.05 m; glTF rear-door probe confirms it fits. |
| 9 | V44 no repeated dark rectangles on Town Hall roadside | **PASS** — all 28 CelebrationDecor and all 8 FestivalDecor bunting meshes stop casting shadows. |
| 10 | V45 swing frame is intentionally broken before M07 | **PASS** — crossbar, two `BROKEN` labels and the existing runtime broken-label assertion; no missing geometry. |
| 11 | V46 alley sightlines clear of banner/signs | **PASS** — banner moved to the festival entrance; the signpost is now a single stack on the south road verge, clear of Milo and the M16/M17 props. |
| 12 | M16 delivery board and M17 market stock do not hide each other | **PASS** — market stock moved to the market front `(9.2,3.8)`, more than 11 m from the delivery board. |
| 13 | V47 NPC portraits no longer dominate foreground | **PASS** — portrait pixel size reduced 47%, 24 m range and distance scaling. |
| 14 | V48 GRAND RALLY label attached to podium | **PASS** — 2.9×0.5 m board on two posts above the trophy, front and back labels. |

**Gates:** pure **119 passed**; source runtime **1171 passed, 0 failed** across
all 11 phases; headless import and boot exit 0; packaged Linux boot plus all 11
phases **1171 passed, 0 failed**, 3× `--selftest` green, ZIP-extracted
`--selftest` 284/284; exact-head CI on `1e57711` green for `build-test` and
`windows-tests`. Windows rebuilt, startup **NOT TESTED**.

**Evidence:** ignored `evidence/v13_canonical_visual/v13.2_round3/`.

**Remaining severity:** no known visual blocker. The independent pass found
and we fixed the inverted V39 arrows, the start/finish pole crossing GOLD LAP,
the banner post gap, a bunting silhouette on the banner, and the centre
festival lantern covering the back text. The final pass at `1e57711` is PASS
on every V36–V48 item. Human sign-off, one continuous M01–M20 sitting and
Windows startup remain **NOT TESTED**.

## v1.3.0 — round-4 visual defects (V49–V57)

| # | Visual check | Result |
|---|---|---|
| 1 | V49 swing posts meet correctly at both ends | **PASS** — front/back legs splay in opposite X directions and the left/right pair splays inward; all four tops meet under the crossbar. |
| 2 | V50 van shadow sits under the vehicle | **PASS** — one soft contact shadow covers the footprint; antenna, flag and postal pieces no longer cast detached shadows. |
| 3 | V51 van side mark reads as postal identity | **PASS** — cream panel, navy border, envelope flap, red stamp and `POST` on both sides. |
| 4 | V52 Pip's car wheels are vertical discs | **PASS** — all eight wheel cylinders on both variants rotated `(0,0,1.5708)`. |
| 5 | V53 hair on the back of the head | **PASS** — back, side and fringe hair plus a girl's bob, visible from the chase camera without covering the face. |
| 6 | V54 library shelves face the reader | **PASS** — ledges and book slots moved to the labelled `-Z` front; book slots rotated 180°. |
| 7 | V55 bridge-to-circuit road no longer overlays the circuit | **PASS** — 37.5 m east-road chord and its landing removed; the wooden bridge deck is the connector; 7 authored slabs and 7 `RoadNetwork` rectangles. |
| 8 | V56 perimeter barrier has no corner gap or overlap | **PASS** — north/south walls and sand at `x=20`, length `150.2`, flush with the west/east walls; colliders match. |
| 9 | V57 trees no longer overlap | **PASS** — `Tree_20` and `Tree_25` moved; closest remaining pair is 4.47 m. |
| 10 | Delivery traffic stays clear of stations after the road removal | **PASS** — both free-roam routes moved to `z=+0.5/-2.0`; no waypoint below 1.8 m and no sampled runtime position below 1.7 m from a critical station. |

**Gates:** pure **119 passed**; source runtime **1171 passed, 0 failed** across
all 11 phases; headless import and boot exit 0; packaged Linux boot plus all 11
phases **1171 passed, 0 failed**, 3× `--selftest` green, ZIP-extracted
`--selftest` 284/284. Windows rebuilt, startup **NOT TESTED**.

**Evidence:** ignored `evidence/v13_canonical_visual/v13.3_round4/` (17 views:
swing A-frames, bridge/circuit, van, Pip cars, player hair, library, all four
perimeter corners and both tree groups).

**Remaining severity:** no known visual blocker. Human visual sign-off, one
continuous M01–M20 sitting and Windows startup remain **NOT TESTED**.

## v1.3.0 — review follow-up: traffic separation and manifest history (P2/P3)

| # | Check | Result |
|---|---|---|
| 1 | P2 free-roam cars keep a safe following gap over several loops | **PASS** — the shared route-progress rule holds a 7 m minimum gap; the pure simulation runs six loops at 60 Hz and asserts the gap stays ≥6.9 m. |
| 2 | P2 following rule is deterministic with a single right of way | **PASS** — only the car behind yields; at equal progress the lexicographically larger route id yields. Both directions are tested. |
| 3 | P2 no deadlock | **PASS** — the leader always proceeds, so two cars cannot yield to each other; the simulation records zero mutual-yield steps and both cars complete more than three loops. |
| 4 | P2 runtime integration | **PASS** — `Main.SpawnTraffic` supplies the live `_trafficCars` list; `TrafficCar.ShouldYield()` checks traffic after the player/van checks; all 11 source and packaged phases pass. |
| 5 | P3 historical v1.2.1 manifest preserved | **PASS** — restored to the original `1b63c13` content: 12-point delivery route starting at `(66,-1.5)`, old scooter lanes. A pure test guards the endpoints and source string. |
| 6 | P3 separate v1.3.0 manifest | **PASS** — `docs/layout_manifest_v1.3.0.json` carries the canonical town and round-4 20-point routes; `source` is tag v1.3.0. |
| 7 | P3 tests use the correct manifest | **PASS** — current-scene tests read v1.3.0; the historical anchor-baseline test intentionally reads v1.2.1. |

**Gates:** pure **123 passed**; source runtime **1171 passed, 0 failed** across
all 11 phases; headless import and boot exit 0; packaged Linux boot plus all 11
phases **1171 passed, 0 failed**, 3× `--selftest` green, ZIP-extracted
`--selftest` 284/284. Windows rebuilt, startup **NOT TESTED**.

**Remaining severity:** no known gameplay blocker. The long-horizon traffic
check is a pure fixed-step simulation; the runtime 24 s living phase remains
green. Human sign-off, one continuous M01–M20 sitting and Windows startup
remain **NOT TESTED**.

## v1.3.0 — asset-driven world, WorldBuilder and bake removed (P23 gate)

Scope: make the authored Godot scenes the only source of truth. The procedural
`WorldBuilder`, the one-shot `SceneBakeTool` and the generated
`scenes/world/baked/` snapshot are removed. Baseline is `e1ca753`; no tag or
release is created by this gate.

| # | Asset-driven requirement | Result |
|---|---|---|
| 1 | Runtime level is the authored `scenes/world/Town.tscn` | **PASS** — `WorldSceneAdapter` instantiates it and reads anchors, doors, gates and route markers from scene nodes. |
| 2 | No procedural fallback or hard-coded placement path | **PASS** — missing anchors, doors or markers throw at load; there is no code path that regenerates scenery. |
| 3 | Shared gameplay data contract preserved | **PASS** — `WorldModel` carries all 69 properties used by `Main`, `BridgeBuilder`, `SwingBuilder` and the mission systems. |
| 4 | `WorldBuilder` and bake pipeline removed | **PASS** — `WorldBuilder.cs`, `SceneBakeTool.cs`/`.tscn`, the unused `WorldSceneAnchors` helper and all 34 baked files are deleted; no runtime or test reference remains. |
| 5 | Tests inspect the authored assets, not the baked snapshot | **PASS** — building centres come from `Town.tscn`; wall dimensions from `assets/buildings/*.tscn`; collision/finale roots from `Town.tscn`, `RallyRoad_Collidable.tscn` and `PlayerHouse.tscn`. |
| 6 | Gameplay, schema-3 saves and M01–M20 unchanged | **PASS** — pure **123 passed**; source runtime **1171 passed, 0 failed** across all 11 phases; restore/release/recovery phases green. |
| 7 | Current docs and manifests describe scene authority | **PASS** — `layout_manifest_v1.3.0.json` carries `scene_authority`; `README*`, `IMPLEMENTATION_PLAN.md` and `docs/` describe direct scene editing. The v1.2.1 manifest is unchanged. |
| 8 | Future development edits scenes directly | **PASS** — no bake or regeneration step is documented or required; editor import and headless boot exit 0. |

**Build/test gates:** build 0/0; pure **123 passed**; source runtime **1171
passed, 0 failed**; headless import and boot exit 0.

**Remaining severity:** no asset-driven migration blocker known. Human visual
sign-off, one continuous M01–M20 sitting and Windows startup remain **NOT
TESTED**. Packaged gates and hashes are recorded in `PROGRESS.md` P23.

## v1.4.0 — City Expansion (P24 gate)

Scope: add the gated Roadworks Rescue mission, a drivable town-to-city join,
three independent city jobs, tiered city simulation/HLOD and schema-4 saves
without changing the village's M01–M20 gameplay. Baseline `0dbd0c6`; no tag or
release is created by this gate.

| # | City requirement | Result |
|---|---|---|
| 1 | M21 is the only new gate and requires all twenty village jobs | **PASS** — M21 prerequisites are M01–M20; practice racing and bonus racing are not prerequisites. Pure tests cover the chain and exactly-once 25-coin reward. |
| 2 | Supplies and counting/order task are required | **PASS** — 3 cones + 2 panels + 1 toolbox, then 3 + 2 + 1 counting and cone→panel→toolbox order. Wrong quantities/order never consume supplies. |
| 3 | Physical gate blocks until completion and then opens atomically | **PASS** — authored collider spans the 16.6 m opening; completion hides barrier/fences, clears the layer and disables every shape. A full-speed runtime van is stopped while closed and passes when open. |
| 4 | Road state survives save/reload and cannot reopen/reward twice | **PASS** — schema-4 fields and validation; `--selftest-city` reloads the real scene and repeats the open state. |
| 5 | City is drivable and returnable without seam traps | **PASS** — authored bridge/causeway at world x≈49..188, flush surfaces, gate at x=96. M05/M09/M19 autopilot races still complete after the fix. |
| 6 | Independent CITY JOBS board and C01–C03 | **PASS** — separate catalogue/service/UI; the board lists only city jobs. Pure and runtime tests complete all three through their real state machines. |
| 7 | City progress, rewards and world state persist | **PASS** — separate save fields + schema-4 round-trip; mid-drive city save/continue covered by `--selftest-city`. |
| 8 | City logical time advances while hidden | **PASS** — deterministic pose function; 1,800 s advanced in 0.002–0.003 ms with zero actor nodes. No per-agent catch-up loop. |
| 9 | Tiered detail/HLOD with hysteresis and bounded actors | **PASS** — 20 detailed buildings + 16 HLOD proxies on the street; 16 HLOD only on the approach; zero city nodes in town. Near/mid pools are capped and deduplicated. |
| 10 | Measured performance at 720p/1080p | **PASS** — worst p95 4.51 ms at 720p and 5.60 ms at 1080p with vsync off on Radeon 890M; town stage has no city content. Method/results in `docs/05_CITY_PERFORMANCE.md`. |
| 11 | Existing game, saves and runtime phases remain green | **PASS** — pure **145 passed**; source runtime **1373 passed, 0 failed** across 13 phases; packaged Linux also boots and passes all 13 phases; headless import/boot exit 0. |
| 12 | CI entry exists for the city phase | **PASS (source)** — `--selftest-city` is in `.github/workflows/build-and-test.yml` for source and packaged Linux. **CI has not run on this uncommitted tree.** |

**Not tested / not claimed:** one continuous human M01–M21 → C01–C03 sitting;
human visual sign-off; Windows startup; real-GPU performance outside this one
Linux/Mesa Radeon 890M machine; a same-method v1.3.0 town baseline; explicit
>33 ms transition hitch instrumentation; a packaged v1.4.0 export. No PR or tag
exists for this gate.
