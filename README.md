# Patchwork Island — v1.4.0 City Expansion

A happy little 3D village adventure for players aged roughly 5–10. A storm has
scattered the village's supplies. Deliver parcels, repair the bridge, drive the
post van, rebuild the playground, decorate the village green and furnish your
own home. Finish all twenty village jobs, clear the roadworks, then drive into
Patchwork City for three more independent jobs. Every completed job visibly
changes the island.

This repository contains the complete Godot 4.7.2 .NET / C# source, automated
tests, real-input GUI evidence and release build instructions.

## v1.4.0 — City Expansion test build

The city is a second authored play space at world `(320, 0, 0)`, reached by
driving the delivery van through the east roadworks gate. The village remains
the same game: M01–M20, both finales, practice racing, NPC traffic, cosmetics and
all existing saves keep their behaviour.

- **M21 Roadworks Rescue** is the gate job: deliver 3 safety cones, 2 barrier
  panels and 1 toolbox, then solve the crew's 3 + 2 + 1 counting and load-order
  task. Completing it removes the barrier, disables its collider and opens the
  road once. The 25-coin reward and open state are saved.
- **City JOBS** is an independent board with C01 City Courier, C02 Market
  Stock-Up and C03 Signals and Signs. City progress and rewards are separate
  from the village mission graph and use the same coin wallet.
- **Tiered city simulation**: 24 vehicles and 42 pedestrians are a deterministic
  function of seed + logical time. The hidden city advances O(1) without agent
  nodes; authored detail/HLOD cells use distance hysteresis, and near/mid actors
  are phased in from the pool.
- Save schema **4** adds city state and migrates schema 3 saves without changing
  coins, mission progress, outfits or world state. Newer saves remain protected.
- Measurements on AMD Radeon 890M: p95 ≤ 5.7 ms in all four probe stages at
  720p and 1080p with vsync disabled; a 30-minute hidden-city advance costs
  0.002–0.003 ms. See `docs/05_CITY_PERFORMANCE.md`.
- **Test build published.** Download the v1.4.0 pre-release from
  <https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.0>
  (commit `21b226e`). The published tag predates the city-grid remediation on
  PR #32; use the CityFix local build or a future v1.4.1 release for the fixed
  grid. Human visual sign-off, one continuous M01–M21/C01–C03 sitting and
  Windows startup remain open. See `PROGRESS.md` P24 and
  `docs/03_ACCEPTANCE.md`.
- **City grid remediation (PR #32 review follow-up).** Sidewalks are split
  into 81 segments with a 16 m entrance gap, plazas are 56 m, buildings/HLOD
  are scaled to fit, and the road/marking surfaces are flush. The van can enter
  the city, NPC cars stay on the grid, and the player can step onto the plaza.
  Evidence: `evidence/v14_city/defects/` (before) and `defects-fixed/` (after).
- Screenshots: `evidence/v14_city/720/` and `evidence/v14_city/1080/`.
  Compatibility notes: `docs/CITY_EXPANSION_COMPATIBILITY.md`.

## v1.3.0 scene-based town migration

**The static town is authored directly in Godot scenes.**
`scenes/world/Town.tscn` is the runtime level, assembled from the reusable
scenes in `scenes/world/assets/`; `src/World/WorldSceneAdapter.cs` instantiates
it and populates the `WorldModel` data contract. There is no procedural
`WorldBuilder`, no bake tool and no baked snapshot. Schema stays **3**, all 20
missions, pickups, build sockets, NPC/traffic routes, driving, cosmetics and
save-recovery behaviour are retained.

- 75 gameplay anchors, six building centres/yaws/wall dimensions and all
  NPC/traffic waypoints are checked at load; missing or moved content fails
  loudly.
- Source runtime **1171 assertions** and pure rules **123 tests** pass; every
  packaged Linux runtime phase also passes with zero leaked unsafe references.
- New `--selftest-restore` restores partial M03/M07/M08/M10/M15/M18/M20 worlds;
  `--selftest-release` restores a v1.2.1-shaped 158-coin save with outfits,
  bridge, swings, park and furniture intact.
- Real Radeon 890M: same 3394 draw calls / 66069 primitives and ~16.7 ms mean
  at 720p and 1080p as the v1.2.1 baseline.
- **Not tagged or released.** Visual polish, Windows startup and one continuous
  human M01–M20 sitting remain open. See `PROGRESS.md` P16 and `ACCEPTANCE.md`.

## v1.2.1 published baseline

**Complete v1.2.1 hotfix for the Living Village Update — tagged and
published.** The original ten jobs, optional race, free practice racing, first
celebration, character customisation and save recovery remain. v1.2 adds six
named villagers, two safe NPC drivers, original music/SFX with persisted
settings, a larger continuous rally circuit, and ten new jobs M11–M20 ending in
a separate village-festival finale. v1.2.1
fixes M18 repair-part persistence across save/reload and makes off-island NPC
traffic stop permanently.

- Save schema **3** adds `FestivalSeen`; `CelebrationSeen` keeps its original
  M10 meaning. Schema 1/2 saves migrate with coins, outfits, world state and
  progression intact; a completed v1.1.0 village unlocks M11 immediately.
- Packaged Linux x64 export booted headlessly and passed all runtime phases.
- Windows x64 export is PE32+ with file/product version `1.2.1.0`.
  **Windows startup was not tested** (no Windows runtime or Wine) and was
  explicitly waived for the Linux-first release.
- Real AMD Radeon 890M: 720p mean **16.69 ms** / p95 17.35 ms; 1080p mean
  **16.67 ms** / p95 17.28 ms over 600 frames each. No v1.1.0 real-GPU probe
  exists, so a baseline comparison is **NOT TESTED**.
- Packaged audio reached the real PipeWire sink monitor (nonzero PCM capture)
  and **human listening is PASS** — the product owner verified the packaged
  build on 2026-11-09. v1.2.1 changes no audio code or assets. See
  [docs/RELEASE_NOTES_v1.2.1.md](docs/RELEASE_NOTES_v1.2.1.md).

## Download / install

Published v1.2.1 downloads:
[GitHub release v1.2.1](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.2.1)
(Linux/Windows ZIPs, `README.txt`, `SHA256SUMS`). v1.2.0 remains available as
the previous release.

The same artifacts are also present locally under `dist/`:

```text
dist/windows/PatchworkIsland.exe        Windows x64, single executable (v1.2.1)
dist/linux/PatchworkIsland.x86_64       Linux x86_64, single executable (v1.2.1)
dist/PatchworkIsland-1.2.1-Windows-x86_64.zip
dist/PatchworkIsland-1.2.1-Linux-x86_64.zip
dist/PatchworkIsland-1.1.0-Windows-x86_64.zip   previous release archive
dist/PatchworkIsland-1.1.0-Linux-x86_64.zip     previous release archive
dist/SHA256SUMS
```

The exported builds are self-contained: players do **not** need Godot or a .NET
installation. They are fully offline. Save data is stored per user (see below).
The Windows build is unsigned, so Windows SmartScreen may ask for confirmation.

### Windows export verification

`Godot_v4.7.2-stable_mono_export_templates` produced a valid PE32+ x86-64
executable with file/product version `1.2.1.0`. The embedded pack contains
`data/missions.json`, the P9 customisation code and the .NET assemblies, and
the executable references the self-contained
`Microsoft.NETCore.App.Runtime.win-x64` runtime pack. It was **not launched**
because this machine has no Windows runtime and no Wine. Distinguish this from
the Linux export, which was launched successfully.

## Run from source

Prerequisites:

- Linux (development) or Windows 10/11 x64 (player build)
- **Godot 4.7.2 stable .NET edition** (not the standard build)
- **.NET SDK 8.0** (the project targets `net8.0`)
- For export: matching Godot 4.7.2 .NET export templates

```bash
godot --path . --editor        # import once / open editor
godot --path .                 # play
```

Or open `project.godot` in Godot 4.7.2 .NET and press F5.

## Controls

| Input | Action |
|---|---|
| WASD / arrow keys | Walk (camera-relative) |
| Space | Jump |
| E | Interact with the nearest highlighted object |
| Mouse wheel | Zoom camera |
| Right-drag | Orbit camera |
| J | Open the job journal / board |
| H | Show the current job hint |
| Esc / P | Pause / close menu |
| E near the van | Get in |
| W/S while driving | Accelerate / brake and reverse |
| A/D while driving | Steer; pivot when stopped |
| E while driving slowly | Get out |
| R while driving | Reset the van to its parking spot |
| Left click in build mode | Place a selected piece on a glowing socket/pad |
| R in build mode | Show the rotation hint (bridge/swing/park/furniture snap automatically) |
| F12 | Developer screenshot and state log (only with `--dev` or in the editor) |

## How to play

1. Start a new adventure and choose **Boy** or **Girl**. The choice is visible
   immediately and can be changed later in the town clothes shop.
2. Walk to the yellow **JOBS** board near the town hall (or press **J** anywhere).
3. Choose an available job. Locked jobs show the prerequisite they need.
4. Follow the gold objective marker and the top-centre compass arrow.
5. Press **E** at highlighted people, crates, worksites and counters.
6. Job-specific budgets (M04 groceries and M08 park) are separate from your
   permanent coin wallet. Wrong placements never consume task pieces.
7. Spend earned coins at the **CLOTHES** shop east of the market to buy and
   wear outfits. Wearing an owned outfit is always free.
8. After all ten jobs, the village celebration plays once and free roam,
   practice racing, the playground and your furnished home remain available.

### Character and clothes shop

- The free **Everyday** outfit has a distinct boy and girl starting look.
- **Sunny Day** costs 8 coins, **Meadow Green** 10, **Berry Pink** 12 and
  **Starry Night** 15. All four are shared by both presets.
- Buying deducts the exact price once, marks the outfit owned and wears it
  immediately. Already-owned outfits cannot be bought again; owned outfits can
  be worn free at any time.
- The shop shows your wallet, prices, owned/wearing state, a clearly disabled
  **Not enough coins** state, a paper-doll preview and a Boy/Girl switch.
- The shop building is on the north side of the main road, east of the market
  and beside the village green, with a CLOTHES sign, awning and mannequins.

The ten jobs are:

| Job | Task | Reward |
|---|---|---|
| M01 The Red House Delivery | 3 apples to the RED HOUSE | 10 |
| M02 Workshop Supplies | 5 wood + 3 stone to the WORKSHOP | 12 |
| M03 Build the Bridge | 3 supports + 4 planks, then cross | 20 |
| M04 Market Basket | 2 bread @3 + 1 milk @4 from a 12-coin purse | 12 |
| M05 Village Road Rally | Drive four ordered gates; any pace | 15 (+5 optional) |
| M06 Two-Stop Post | 2 blue parcels to RED HOUSE, 1 orange to WORKSHOP | 15 |
| M07 Fix the Swings | 4 ropes + 2 seats | 18 |
| M08 Park Planner | Bench 6 + flowerbed 4 + lamp 9 from a 20-coin purse | 20 |
| M09 Race the Clock (Bonus) | Replay the course; any pace | 15 (+5 optional) |
| M10 Home Sweet Home | Table 8 + 2 chairs 10 + plant 3 | 20 |

## Build and test

```bash
dotnet build PatchworkIsland.sln
dotnet run --project tests/PatchworkIsland.Tests.csproj
godot --headless --path . --editor --quit
godot --headless --path . --quit-after 120
```

The dependency-free runner currently has **123 tests** covering mission
prerequisites and unlocks, exactly-once rewards, duplicate pickups, mission
switching, hints, bridge/swing socket rules, grocery and park budgets, house
costs and free starter fallback, ordered checkpoints, optional race bonuses
verified against the persisted clock, the full twenty-job graph, JSON
round-trips, save validation, corrupt/backup recovery, newer-save preservation,
deterministic cross-platform write failure/retry, race timer restoration and
saturation, post-completion furniture rearrangement, save-feedback throttling,
settings feedback isolation, persisted/discarded session state,
newer-replacement authorisation, control-mode precedence, mission-catalogue
integrity, clothing purchase/ownership/equip rules, customisation save
round-trips, older-save customisation defaults, a complete-field comparison
between `MISSIONS.json` and `data/missions.json` (including duplicate-property
rejection), and two-file scene-parity checks for all 75 anchors, building
centres/wall sizes, required dynamic roots, rally collision and NPC/traffic
markers.

Godot runtime integration checks (**1171 assertions across eleven phases**)
execute the real scene headlessly:

```bash
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
godot --headless --path . -- --selftest-recovery
godot --headless --path . -- --selftest-living
godot --headless --path . -- --selftest-rally
godot --headless --path . -- --selftest-chapter
godot --headless --path . -- --selftest-hotfix
godot --headless --path . -- --selftest-finale
godot --headless --path . -- --selftest-traversal
godot --headless --path . -- --selftest-release
godot --headless --path . -- --selftest-restore
```

They cover title and modal pause, player/van physics stopping, race timers
stopping, settings/New Game modal returns, mission-completion pause, the
character chooser, opening the real clothes shop, pressing its Buy/Wear
buttons, wallet updates, insufficient funds, owned-outfit protection, visual
appearance changes, New Game reset, autosave/manual save, save/Continue and
driving. The recovery phase injects deterministic write failures and reloads
the real scene to verify failed New Game recovery, discarded unsaved changes,
backup recovery, settings failure feedback, a newer-save replacement retry,
customisation restoration and exact on-disk bytes.

A GitHub Actions workflow (`.github/workflows/build-and-test.yml`) pins Godot
4.7.2 .NET, builds the project, runs the pure and runtime tests, imports and
boots the project, exports a real Linux release, verifies its embedded content
and boots the exported binary. A separate Windows job runs the pure test suite
on Windows.

## Export

Install the matching Godot 4.7.2 .NET export templates into
`~/.local/share/godot/export_templates/4.7.2.stable.mono/`.

```bash
godot --headless --path . --export-release "Windows Desktop" dist/windows/PatchworkIsland.exe
godot --headless --path . --export-release "Linux" dist/linux/PatchworkIsland.x86_64
```

`export_presets.cfg` is committed. The presets use x86_64 templates, embed the
PCK and .NET build outputs, include `data/*.json`, and exclude `reference/`,
`evidence/`, `planning/` and `tests/`.

## Save data

Godot writes `user://save.json` with a `user://save.json.bak` backup:

- Linux: `~/.local/share/godot/app_userdata/Patchwork Island/save.json`
- Windows: `%APPDATA%\Godot\app_userdata\Patchwork Island\save.json`

Writes are atomic (temporary file, flush, move). A structurally invalid or
unreadable main save is renamed `save.json.corrupt-<timestamp>` and the backup
is loaded if possible.

Save schema versions are separate from game release versions. v1.4.0 writes
schema **4** and loads supported older schemas:

- **Schema 1** (v1.0.x) saves migrate in memory through schema 2 and 3 to
  schema 4. Every progression field is preserved; missing character fields
  default to **Boy + Everyday + no purchases**.
- **Schema 2** (v1.1.0) saves keep coins, outfits, cosmetics, bridge/park/
  furniture state, player/van snapshots and the original M10 `CelebrationSeen`.
  The new `FestivalSeen` marker defaults to false, M11 unlocks immediately, and
  the original ten-job celebration does not repeat. The original file is kept
  as the `.bak` backup and is rewritten at schema 4 by the next save.
- **Schema 3** (v1.2.1/v1.3.0) saves keep the complete twenty-job village,
  festival/celebration flags and all world state. The city road starts closed,
  the city board is empty and the city simulation gets a deterministic seed.
- **Schema 5 or newer** is unsupported and is **never** loaded, downgraded,
  discarded or overwritten automatically. The title screen explains the
  incompatibility. An ordinary autosave refuses to touch the file. Only an
  explicitly confirmed New Game may replace it, and a
  `save.json.newer-vN-<timestamp>` copy is preserved first.
- Future releases must keep loading supported older schemas while refusing
  unsupported newer ones. Increment the schema only when the persistence
  contract changes, not for every game release.

Settings, player/van positions, missions, bridge/swing parts, park/furniture
placements, wallet, inventory, character preset, owned outfits, equipped outfit
and the celebration flag are stored. Autosave runs every 20 seconds while
playing and after mission start/completion.

Save failures are explicit: manual saves show success or an inline failure in
the pause menu, autosave failures produce one throttled warning until a later
save succeeds, and settings failures appear inside the settings modal while
retaining a separate throttle. Return-to-title/quit failures offer a
keep-playing or quit-without-saving choice. A failed New Game never marks the
new session as the persisted save. After an unsaved session is discarded,
Continue reloads the scene from the last persisted file rather than resuming
the discarded memory. An explicitly confirmed New Game may replace a newer
save; that permission is session-scoped and survives a failed first write so
Save Now can retry.

After M10 is complete, furniture may be picked up and rearranged freely.
Completion still requires the pieces to be physically placed, but a completed
world remains saveable while a piece is temporarily held or unplaced. Race
timers have no mandatory time limit; elapsed time saturates safely at the
integer-millisecond persistence ceiling instead of invalidating the save.

## Accessibility / settings

Music, sound-effects and reduced-motion toggles are persisted. Reduced motion
stops marker bobbing and spinning. The UI is readable at 1280×720 and scales to
1920×1080. Original music and sound effects are bundled with persisted Music
and SFX settings; the game remains fully playable with sound off.

## Known limitations

- **Windows startup is NOT TESTED.** The PE32+ export compiles and packages,
  but no Windows runtime was available. This was explicitly waived for the
  Linux-first v1.2.1 release.
- **No v1.1.0 real-GPU baseline probe exists**, so the Radeon frame times are
  absolute measurements rather than a before/after comparison.
- **Child usability testing was not possible.** No children participated; the
  age-appropriate design is based on specification, automated checks and adult
  real-input playtests.
- **Text scaling is fixed.** The layout scales, but there is no text-size
  setting.
- Practice-race progress is deliberately not persisted; it is reward-free and
  restarts on reload.

## Verification summary

- `dotnet build PatchworkIsland.sln`: 0 errors, 0 warnings.
- Pure tests: **132 passed, 0 failed**.
- Runtime: **1300 passed, 0 failed** across twelve phases (pause/modal,
  resume, recovery, living village, rally, chapter two, M18 relaunch hotfix,
  finale, traversal, v1.1/v1.2.1 migration, partial-world restoration, city).
- Packaged Linux x64 booted headlessly and passed all twelve phases with exit 0
  and zero leaked unsafe references.
- Real AMD Radeon 890M (Mesa 26.0.8, OpenGL 4.6): the scene-backed build matches
  the v1.2.1 procedural baseline with identical 3394 draw calls / 66069
  primitives; 720p mean **16.68–16.71 ms** / p95 17.21–17.26 ms and 1080p mean
  **16.68 ms** / p95 17.21 ms over 600 frames each (baseline 16.67–16.71 ms).
- Packaged audio reached the real PipeWire sink monitor (nonzero PCM), and the
  product owner verified audible playback on 2026-11-09. Audio is unchanged by
  the v1.3.0 migration.
- v1.3.0 source version and final Linux export are built; Windows was not
  produced or tested for this migration (last Windows artifact: v1.2.1).
- Save schema 3 migration, M05/M09 four-gate timing, the original M10
  celebration and the separate M20 festival are covered by pure and runtime
  tests.
- New in v1.3.0: `--selftest-restore` reloads partial M03/M07/M08/M10/M15/M18/
  M20 worlds; `--selftest-release` restores a v1.2.1-shaped 158-coin save with
  outfits, bridge, swings, park and furniture; the scene adapter validates all
  75 anchors and every building/NPC/traffic coordinate at load.
- 720p and 1080p P14 capture tours are in `evidence/v13_scene_world/`; the known
  v1.2.1 visual glitches remain because parity preceded the optional polish pass.

Detailed evidence and commands are in `PROGRESS.md`, `ACCEPTANCE.md`,
`evidence/p8_*.png`, `evidence/p81_*.png` and `evidence/p9_*.png`.

## Repository map

```text
scenes/Main.tscn          main scene
src/Main.cs               composition root, control/save coordination
src/Core/                 pure rules, mission/save/race/control logic
src/Build/                bridge, swings, park and furniture builders
src/Player/               player and camera
src/Vehicle/              delivery van
src/World/                scene adapter, world data contract and interactables
src/UI/                   HUD, job board, shop, grocery and furniture UI
data/missions.json        authoritative runtime mission content
tests/                    dependency-free .NET test runner
evidence/                 genuine real-input screenshots
dist/                     release artifacts (not committed)
```

Historical handoff and design materials remain available:
`STARTING_PROMPT.md`, `AGENTS.md`, `CLAUDE.md`, `GAME_SPEC.md`,
`IMPLEMENTATION_PLAN.md`, `ART_DIRECTION.md`, `MISSIONS.json`, `planning/` and
`reference/`.
