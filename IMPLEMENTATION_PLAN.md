# End-to-end implementation plan

Agent should implement **P0 through P8** in order, updating `PROGRESS.md` with concrete evidence after each milestone. Each gate must be tested before continuing. The plan is incremental, not a series of planning-only tasks.

## P0 — Environment, repository, runnable skeleton

**Build:** inspect current repo, tools and permissions; pin Godot 4.7.2 .NET and available SDK; add project.godot, C# solution and project, input actions, app icon, .gitignore, dev/test commands and README_RUN.md. Use Compatibility renderer at 1280×720 with appropriate scaling. Start with a bootable main scene that contains a camera, light, ground, an avatar placeholder and a working pause/quit overlay. Create `PROGRESS.md`.

**Gate:** project imports; `dotnet build` succeeds; Godot headless editor import passes; `godot --headless --path . --quit-after 120` (or correct equivalent) starts scene without script errors. Verify real tool path/version; no pretending a non-.NET Godot is valid.

## P1 — Coherent village and movement

**Build:** world boundary, visible five buildings/signs, river/bridge route, paths, landmarks, trees, simple character model, walk/jump/follow camera, interaction target highlight, readable job board, markers, nearby prompt. Use reusable primitive scene components and simple material palette. Add safe teleport back to land from water.

**Gate:** GUI test walking home→town board→red house→workshop; camera remains usable; no player falling through floor or invisible barriers. Headless main scene still imports/starts.

## P2 — Mission framework and first delivery

**Build:** mission definitions/content loading; mission state (locked/available/active/completed), quest tracker, world marker, interactables, inventory counting, pickup/submit, coin rewards, job board. Implement M01 (3 apples to RED house) and M02 (5 logs + 3 stones to workshop) in full. Distinct item meshes, counted pickup confirmations and optional tiered hints.

**Gate:** from new game, M01 and M02 can both complete; progress counters update; coins awarded once only; UI does not require typed arithmetic answer. Add pure logic tests for unlocks, duplicate pickup, repeat completion and exactly-once reward.

## P3 — Bridge construction and grocery maths

**Build:** M03 bridge parts 3 supports + 4 planks with seven snap sockets, preview/placement/validation, completed bridge mesh and collider, new east area reachable. Implement M04 shop interaction with fixed request of 2 loaves at 3 each and 1 milk at 4, task purse 12, clear running total and change 2, market→town-hall handoff. Do not deduct core wallet for M04.

**Gate:** walking bridge is impossible until built, possible after final piece; invalid placements not consumed; player cannot run out of necessary parts; M04 completion reflects physical purchases and a correct cost, not a typed quiz. Test arithmetic and budget calculations.

## P4 — Vehicle, checkpoint course, second delivery

**Build:** a drivable recoverable van with arcade motion; enter/exit with E; appropriate UI hints; seats/occupant; van reset. M05 four ordered wide numbered checkpoints, finish regardless of time, optional faster bonus. M06 two-stop parcel delivery with readable house names and coloured parcel markings. Make first-use guidance very short.

**Gate:** driver can enter, steer, reverse, exit, reset after becoming stuck; leaving the van cannot trap the player. M05 checks actual checkpoint sequence; M06 tracks correct destinations; no forced time limits. Test checkpoint ordering and second delivery state.

## P5 — Playground, park and home customisation

**Build:** M07 swings with 4 ropes + 2 seats on two frames; visible repaired playground. M08 town green: place bench (6), flowerbed (4), lamp (9) using separate 20-coin budget, preview and cancel/undo. M10 small accessible player house and placement of a table (8), two chairs (5 each), plant (3); purchase / place / move / save. Implement cosmetic purchase/unlock when mission reward permits.

**Gate:** each placed object visibly remains in world and cannot be double-spent or duplicated. M08 succeeds with total 19 and 1 coin change; can undo/retry. M10 can complete without currency softlock. Test placement grid and budget logic.

## P6 — Optional mastery and complete progression

**Build:** M09 shortcut/time-trial challenge on existing route with optional 75-second goal and a discoverable safe shortcut; finish without meeting optional time still completes. Complete ten-mission unlock graph. Add celebration when all ten complete, replayable course and remaining free play. Finish all real job feedback, labels and rewards.

**Gate:** perform all ten missions in designed order from a clean save. Check unlock logic at every stage; completion does not allow double rewards. No dead-end if player tackles available jobs in different orders.

## P7 — Save/load, onboarding, accessibility and polish

**Build:** versioned atomic save writes, missing/corrupt save handling, continue/new game, settings, tutorials for walking/building/driving, tooltips/hints, simplified map/waypoint, readable UI for 720p, polish to lighting/colours/signs, simple original sound effects if possible. Save positions, wallet, quest state, placed objects, van, bridge and settings. Adjust based on first-time playthrough observation.

**Gate:** save mid-M03 and restart (partly built bridge remains correct); finish M03, reload and cross bridge; restart mid-M08 and preserve purchases/placements; completed missions and coins do not reset or re-award. Test fresh and corrupt save. Verify mouse focus/pause flows.

## P8 — Final verification, package and handoff

**Status: complete in v1.0.0. P8.1 reliability hardening (v1.0.1) addressed
post-completion furniture and long-race save invalidation, truthful save
feedback, New Game persistence state, runtime-verified Godot pause behaviour,
pause-confirmation cancellation, domain guards, complete mission-content
comparison, developer-tool gating, runtime integration tests and release smoke
verification. P8.2 (v1.0.2) corrects persisted-session restoration for Continue,
session-scoped newer-save replacement retries, settings-modal save feedback,
duplicate mission-JSON detection, domain-verified race bonuses and adds
deterministic Godot failure-recovery integration coverage. See `PROGRESS.md`
and `ACCEPTANCE.md` for the executed commands, pass/fail matrix, artifacts and
remaining environment limitations.**

**Build:** automated tests for mission state machine, prerequisites, pricing, inventory, building snap, and save serialization. Headless Godot smoke tests and final dotnet build. Real GUI walkthrough if display available. Add README_RUN.md with prerequisites, commands, data paths, gameplay instructions, troubleshooting. Package a Windows x64 build **if appropriate export tools are installed** and verify startup if possible. Genuine screenshots in `evidence/` if rendering available; document unavailable capabilities otherwise. Commit final work and make status clear.

**Gate:** `ACCEPTANCE.md` pass/fail matrix backed by commands and game observation; all 10 missions playable from a fresh start; no critical crashes; new/continue work; final folder contains source, no mystery external dependencies. Report exactly which tests were executed and what remains unverified.

## Architecture guidance (keep lightweight)

Suggested scene tree/modules; adjust pragmatically:

```
Main.tscn
├── World (WorldBuilder / static props, river, buildings, collision)
├── Player (CharacterBody3D, CameraRig, interaction sensor)
├── Van (simple arcade vehicle)
├── Interactables (collectables, stations, doors, triggers)
├── Construction (bridge, swings, park, house snap grids)
├── RaceCourse (start, gate1..4, finish)
└── HUD (job card, coin counter, prompt, board, shop, pause)

GameState.cs       // mission progress, wallet, unlocks, collected/placed state
MissionService.cs  // prerequisites, state transitions, exactly-once rewards
Interaction.cs     // single interaction contract + nearby target resolution
Inventory.cs       // typed item counts; never silently discard necessary items
BuildService.cs    // snap slots, validation, placement/undo/persistence
VehicleController.cs
SaveService.cs     // versioned model, atomic write, safety fallback
WorldState.cs      // keep visible world synchronized with saved progress
```

Prefer a small number of cohesive scenes and clear contracts; use C# POCOs for testable rules. Keep Godot node references and game logic separate enough to test numerical and mission behaviour without rendering. Avoid 3000-line monolithic main class or an excessive generalised ECS.

## Prioritisation and risk cuts

1. **Never cut:** compile/start, walking, job board, M01–M10 completion paths, van, bridge, budget/shop, save/load, readable UI and recovery from stuck states.
2. **Cut first:** moving NPC AI, intricate building interiors, particle effects, elaborate animation, voice acting, minimap (retain a simple marker/arrow), detailed terrain meshes.
3. **If blocked by desktop export environment:** complete project, run build+headless tests, document export instructions and exact missing tools; do not call it an exported binary.
4. **If limited time:** concentrate on observable game loops and stable world-state over procedural generation complexity. Fixed small village is acceptable and preferable to endless automatic world generation.
