# Independent repository and gameplay review — 2026-11-12

Reviewed source at `a429791`. This is an evaluation, not a bug-fix change.

## Opinion

This is a substantial, working small game with unusually serious save-recovery and regression coverage. The separation of pure rules from Godot nodes, authored scenes, atomic saves, exactly-once rewards, and isolated runtime test scenarios are real strengths. The project builds without warnings. M01 is playable from a fresh start, with readable objectives and correct rewards.

The project is ready for a focused stabilisation and usability pass, rather than further content expansion. Its implementation and documentation have accumulated layers faster than they have been consolidated. More green assertions will not by themselves resolve camera handling, visual readability or whether a five-year-old understands the next action.

## Findings, ordered by practical priority

### 1. Medium: camera drag remains active after pausing (reproduced)

`src/Player/CameraRig.cs:70–96` stores `_dragging` from mouse-button events. The camera is pausable; it also ignores events when the mouse is not captured. A release during a modal therefore does not clear the drag.

Reproduction in the graphical game:

1. Hold the right mouse button while walking.
2. Press Esc to pause, then release the right button while paused.
3. Press Esc to resume.
4. Move the mouse without pressing a button: the camera orbits anyway.

The player remained at exactly the same coordinates between `camera_before.png` and `camera_after_unheld_mouse.png`, but the view rotated markedly. This also changes the direction of camera-relative walking, so it is more disruptive than a cosmetic issue. A subsequent right-click/release recovers it.

Clear drag state on entering a modal, losing focus or releasing capture; also require the physical right button to be held when processing orbit motion. Add an input-level regression that crosses the pause boundary.

### 2. Medium: two UI controllers have unmanaged Node lifetimes (code finding)

`src/UI/GroceryUi.cs:7,38` and `src/UI/FurnitureShopUi.cs:8,31` inherit `Node` but are created with `new`, never parented to the scene tree, and never explicitly freed. Their widgets belong to the HUD modal, but the controller Nodes do not. Closing the modal only frees the widgets. This creates an orphan-Node lifetime problem on repeated visits; scene-tree node-count checks will miss these orphans.

Make these ordinary C# controller classes, as `ClothesShopUi` already is, or parent them to a modal-owned node and free them with it. Verify with an open/close soak measuring orphan nodes and object count. The growth rate was not measured in this review.

### 3. Medium: roof shadows visibly degrade the presentation (observed)

At 1280×720, roof shadows produce conspicuous jagged dark bands across the cream building walls. See `first_job.png` and `camera_before.png`. This distracts from the otherwise coherent toy-town look. The sun configuration is in `src/Main.cs:870–879`; the exact contribution of shadow resolution, bias, mesh normals and software rendering needs a hardware comparison before choosing a fix.

Compare the same stationary view on supported hardware. Tune shadows against near-wall and town-overview captures rather than disabling them indiscriminately. This environment used llvmpipe, so this is an observed visual issue here, not proof of identical output on every GPU.

### 4. Low: duplicate interaction key and distracting inactive-job targets (observed)

The HUD renders a separate E keycap and receives strings already starting with `E:` (`src/UI/Hud.cs:237–252`, pickup prompts in `Main.cs`). The result is visibly “E  E: A library book for Ollie”. Use action-only prompt text with one keycap owner.

During M01, the nearby MOON book from a much later mission immediately became the highlighted interaction after collecting the apples. Its large label, glow and prompt compete with the current task. Prefer active-job targets when nearby, and reduce visual prominence of locked-job objects. Exploration should remain possible; first-time progression needs clearer visual priority.

## Maintainability and performance risks

- `src/Main.cs` is 6,062 lines, mixing save/session orchestration, mission-specific interactions, UI composition, world updates, developer scenarios, captures and test hooks. `MissionService.cs` is another 1,128 lines. Extract by responsibility in small behaviour-preserving steps: developer tooling first, then city/chapter controllers and session management. Keep existing tests green after each extraction.
- Mission mechanics are declared in JSON but explicitly treated as documentation by `MissionCatalog.cs:19–28`. Prices, counts and mission-specific rules live in C#. The complete-file consistency test compares the two JSON copies; it does not generally prove JSON mechanics agree with executable rules. Establish one authoritative source for tunable numbers, or add explicit rule/content contract tests. This is a drift risk, not a currently demonstrated wrong mission total.
- `PlayerController.ScanInteractables` scans the entire interaction group every 0.1 seconds and computes full distances. Adequate for today's small town, but it scales with all loaded interactions. Profile before changing it; a local Area3D candidate set and squared distances are straightforward options if it becomes costly.
- The camera creates a Godot RID array and ray-query wrapper every rendered frame (`CameraRig.cs:128–134`). Reuse query state if allocation profiling identifies it as material. Do not call this a proven frame-rate bottleneck without measurements.
- Positive: material/icon caches, throttled HUD refresh, bounded city actor pools, deterministic offscreen simulation and tiered city detail already address meaningful costs. Preserve those rather than undertaking a wholesale rewrite.
- Documentation is contradictory: `README_RUN.md:333–334` says no bundled audio, while the source and audio manifest tests include audio. The guide also advertises v1.4.0 while newer release notes exist. Original specs mandate exactly ten jobs; the implemented product now has M01–M21 plus three city jobs. Clearly distinguish historical baseline requirements from the current release contract.

## Executed verification

Environment: Godot `4.7.2.stable.mono.official.ed1daf0bf`, .NET SDK `10.0.115` building the net8.0 target. GUI at 1280×720 on Xvfb with Mesa llvmpipe; audio fell back to a dummy driver.

| Command | Result |
| --- | --- |
| `dotnet build PatchworkIsland.sln` | PASS, 0 warnings / 0 errors |
| `dotnet run --project tests/PatchworkIsland.Tests.csproj` | 132 passed / 0 failed |
| `XDG_DATA_HOME=/tmp/patchwork-review-tests godot --headless --path . -- --selftest` | 284 passed / 0 failed |
| Same environment, `--selftest-traversal` | 18 passed / 0 failed |
| Same environment, `--selftest-city` | 129 passed / 0 failed |

The initial runtime-test attempt used the default home save directory, which is outside this sandbox's writable roots: 150 passed, four failures, ending in a missing test-save exception. Rerunning with writable isolated user data passed. This initial failure is retained in `selftest.log`, not concealed as a product regression or counted as a passing run.

Actual graphical input exercised the title, character selection, job board, M01 selection, walking, three apple pickups, RED HOUSE delivery (+10), pause/resume and right-drag camera handling. A separate prepared M03 scenario exercised entry to building mode and placement of all three supports and four planks, completing the bridge and awarding +20 coins. Prepared scenarios are not a fresh-save progression walkthrough.

Evidence: `evidence/review_2026-11-12/` contains screenshots and runtime logs. No gameplay source was changed. Existing unrelated untracked handoff files were left alone.

Not verified: a continuous M01–M21/C01–C03 human playthrough; every runtime phase; Windows startup; hardware GPU performance or transition hitches; audible output; child usability. Existing historical evidence was read but is not presented as newly executed verification.

## Suggested next work

1. Fix camera drag state and the two UI controller lifetimes; add focused regressions and an orphan-count soak.
2. Run one uninterrupted fresh-save playthrough through both village chapters and the city, including mission switching, mid-job saves and recovery. Test the actual Windows package on Windows.
3. Observe a small number of children playing the first 15 minutes without coaching. Record navigation confusion, reading burden and misinterpreted prompts; use that evidence for the next UX pass.
4. Fix shadow quality and inactive-object clutter, then profile frame-time percentiles, transition spikes and allocations on a modest supported GPU.
5. Consolidate release documentation and incrementally split `Main`. Defer more missions or districts until these quality gates are satisfied.
