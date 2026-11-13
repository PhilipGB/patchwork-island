# Patchwork Island — run and test guide

> Current published baseline: **v1.4.2 City jobs load hotfix**, a
> [pre-release test build](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.2).
> See `docs/RELEASE_NOTES_v1.4.2.md` for the load fix and platform verification gaps. The static town is `scenes/world/Town.tscn`; the city is authored
> in `scenes/city/` and loaded by `CityWorldAdapter`. There is no bake step and
> no procedural `WorldBuilder` or city generator. All v1.2.1/v1.3.0 gameplay,
> M01–M20 semantics and schema-3 saves are retained; schema 4 adds city state.
> See `PROGRESS.md` and `docs/03_ACCEPTANCE.md` for the gate and remaining gaps.
> The v1.2.1 release notes remain under `docs/`.

## Current source verification

This branch is **1.5.0-dev**, the Driving Academy release. The academy
report, acceptance record and remaining gaps are in
[docs/DRIVING_ACADEMY_REPORT.md](docs/DRIVING_ACADEMY_REPORT.md) and
[docs/DRIVING_ACADEMY_ACCEPTANCE.md](docs/DRIVING_ACADEMY_ACCEPTANCE.md);
the 1.4.3 Linux quality pass remains in
[docs/LINUX_QUALITY_PASS.md](docs/LINUX_QUALITY_PASS.md); architecture is in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).
The v1.4.0 table later in this guide is historical.

Graphical input regressions need a real display or Xvfb; headless captured-mouse
semantics do not exercise physical orbit correctly:

```sh
XDG_DATA_HOME=/tmp/patchwork-quality godot --path . -- --selftest-quality
```

For an input-driven progression run, use a fresh disposable directory. Rerun the
same command at its three save/exit checkpoints to Continue. The driver walks,
steers, presses E and clicks controls/sockets; it does not seed progress or
teleport. It reads scene positions to navigate, so this is functional coverage,
not a human usability study.

```sh
XDG_DATA_HOME=/tmp/patchwork-walkthrough godot --path . -- --dev --playthrough
```

`--disable-render-loop --fixed-fps 60 -- --dev --playthrough --no-captures`
can accelerate a functional replay on a graphical backend. It supplies no
visual or performance evidence. Never use a player's user-data directory.

## Prerequisites

- Linux (development) or Windows 10/11 x64 (player build)
- **Godot 4.7.2 stable .NET edition** (not the standard build)
- **.NET SDK 8.0** (the project targets `net8.0`)
- Optional for export: matching Godot 4.7.2 .NET export templates

## Run from source

```bash
godot --path . --editor        # import once / open editor
godot --path .                 # play
```

Or open `project.godot` with Godot 4.7.2 .NET and press F5.

The editable static town is `scenes/world/Town.tscn`, assembled from the
reusable scenes under `scenes/world/assets/`. Edit those scenes directly in the
Godot editor; `WorldSceneAdapter` loads `Town.tscn` at runtime. There is no bake
or regeneration step.

## Build check

```bash
dotnet build PatchworkIsland.sln
```

## Headless smoke checks

```bash
godot --headless --path . --editor --quit
godot --headless --path . --quit-after 120
```

## Controls

| Input | Action |
|---|---|
| WASD / arrows | Move (camera-relative) |
| Space | Jump |
| E | Interact / use nearest highlighted thing |
| Mouse wheel | Zoom camera |
| Right-drag | Orbit camera |
| J | Open the job journal |
| H | Current job hint |
| Esc / P | Pause / close menu |
| E (near van) | Get in |
| W/S, A/D (driving) | Accelerate/brake-reverse, steer; A/D pivots at a stop |
| E (driving) | Get out when slow |
| R (driving) | Reset van to parking |
| F12 | Developer screenshot/state log — only in the editor or with `--dev`; writes `user://screenshots/` |

## Automated tests

```bash
dotnet run --project tests/PatchworkIsland.Tests.csproj
```

The runner currently has **132 tests** and covers mission catalog, prerequisites,
unlocks, duplicate pickups, exactly-once rewards, mission switching, hints,
bridge/swing snapping (including rejected unknown part types), grocery and park
budgets, furniture costs/starter fallback/purchase caps, ordered checkpoints,
optional race-bonus boundaries, persisted-clock bonus verification and invalid
bonus rejection, the full twenty-job graph, M21 roadworks quantities/order,
city job exactly-once rewards, city save validation, deterministic city
simulation, scene contracts, JSON round-trips, structural save validation,
corrupt/backup recovery, newer-save preservation, deterministic in-process
failed-write/retry, race-time restoration and saturation, post-completion
furniture rearrangement, save-feedback throttling, settings feedback isolation,
persisted/discarded session state, newer-replacement permission, control-mode
precedence, clothing defaults, purchase and ownership rules, equip
restrictions, clothing save round-trips, older-save defaults, mission-catalogue
integrity, a complete-field comparison of `MISSIONS.json`/`data/missions.json`
that rejects duplicate properties at any depth, and scene-parity checks for all
75 anchors, building centres/wall sizes, required dynamic roots, rally collision
and NPC/traffic markers.

## Godot runtime integration tests

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
godot --headless --path . -- --selftest-city
godot --headless --path . -- --selftest-academy
```

These use isolated `user://selftest-save.json` and
`user://selftest-recovery-save.json` files and total **1300 assertions across
twelve phases** on the v1.4.0 tree. The city phase loads the real authored
district, proves the closed road gating, C01–C03 state flow, physical gate
open/close, logical-while-hidden simulation, LOD/actor cleanup, a mid-drive save
and a return trip. Run `--selftest` before `--selftest-resume`: the fresh phase
creates the driving save that the resume phase continues.

The first two phases cover title/modal pause, pausable player/van/world
processing, stopped physics and race timers, Pause→Settings→Pause,
Title→Settings→Title, New Game confirmation cancellation, the character
chooser, real clothes-shop Buy/Wear buttons, wallet updates, insufficient
funds, owned-outfit protection, visible appearance changes, mission-completion
pause, New Game reset, manual/automatic saves, and save/relaunch while driving.
The recovery phase injects deterministic write failures and reloads the real
scene to verify failed New Game recovery, discarded movement, backup recovery,
the no-save case, settings failure feedback, autosave warning independence,
newer-save replacement retries, preserved newest-version copies, restored
character/outfit state and unchanged on-disk bytes. `--selftest-restore`
additionally reloads seven partial M03/M07/M08/M10/M15/M18/M20 fixtures and
proves their bridge, swing, park, furniture, garden, pickup and festival visuals
come back. `--selftest-release` now also restores a v1.2.1-shaped 158-coin save
with outfits and all constructed world state.

If a concurrently running Godot instance makes `dotnet run` wait on the project
DLL, build once and execute the test assembly directly:

```bash
dotnet build tests/PatchworkIsland.Tests.csproj -nodeReuse:false -p:UseSharedCompilation=false
dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
```

## Headless capture harness

```bash
godot --path . -- --dev --capture        # gameplay screenshots to user://dev-captures/
godot --path . -- --dev --capture-title  # title screen screenshot
```

Developer screenshots are gated behind the editor or an explicit `--dev`,
`--capture` or scenario argument. The capture tour also writes P9 evidence for
the character chooser, the Girl/Boy world models, the clothes-shop building and
the buy/wear feedback inside the shop. F12 writes to `user://screenshots/`,
never to an arbitrary working directory, and reports file errors instead of
throwing an unobserved task exception.

## Current gameplay loop

1. Esc/Enter on the title screen opens a Boy/Girl character choice, then starts
   the game.
2. Walk to the yellow JOBS board by the town hall, or press **J** anywhere.
3. Choose an available job. Prerequisites are shown on locked cards.
4. Follow the gold marker and top-centre arrow, press **E** on highlighted
   objects, and return to the labelled destination or build site.
5. Press **E** at the **CLOTHES** shop east of the market to buy/wear outfits or
   switch between Boy and Girl. Shop purchases use the earned coin wallet.

All ten missions are fully playable (M01 apples, M02 supplies, M03 bridge,
M04 groceries, M05 rally, M06 parcels, M07 swings, M08 park, M09 optional
time-trial, M10 home). The bridge opens the east meadow; the yellow van waits by
the house; the playground and village green persist in the world; the house
interior has a furniture catalogue stand. After M09 the job board offers a
reward-free practice race. Finishing all ten shows the village celebration and
leaves free roam available. Progress autosaves, can be saved from the pause
menu, and is restored by **Continue adventure**.

The P8 fresh-save walkthrough completed all ten jobs with real keyboard/mouse
input, finishing with 136 coins (157 rewards minus 21 furniture), the
celebration shown once, a reward-free practice lap, and a save/relaunch that
preserved the bridge collider, furniture, coins and completed job board.

The P8.1 regression scenarios, with real input, verified:

- Completed M10 → picked up one chair → manual save → quit → Continue. The
  chair stayed owned but unplaced; it was then moved to a new pad and persisted.
- A controlled `M05` state with `elapsedMs=3,960,000` (66 minutes) was completed
  and saved; M06 was then played and completed normally. Both completions,
  the long counter and the 130-coin wallet survived quit/relaunch.
- A write-denied manual save showed **Save failed**, left the previous file
  byte-for-byte intact, and a retry after permissions were restored succeeded.
- A failed New Game kept the old on-disk save and did not advertise the new
  session as persisted. Quit-without-saving then restored the old save.

The P8.2 Godot recovery phase adds deterministic failure and reload coverage:

- Failed New Game → Return to title → Return anyway → Continue restored the
  nine-mission/130-coin save and its active M10 job from disk.
- A discarded movement change did not survive Continue; the persisted player
  position and wallet were restored.
- A schema-99 newer save survived cancellation and failed replacement writes;
  after explicit New Game confirmation and an I/O recovery, Save Now replaced
  it and preserved an exact `newer-v99` copy.
- A settings write failure was displayed inside the settings modal, and the
  next autosave failure still produced its first warning. A successful settings
  retry replaced the error state.

## Release packaging

Export templates for Godot 4.7.2 .NET must be installed under
`~/.local/share/godot/export_templates/4.7.2.stable.mono/`.

```bash
godot --headless --path . --export-release "Windows Desktop" dist/windows/PatchworkIsland.exe
godot --headless --path . --export-release "Linux" dist/linux/PatchworkIsland.x86_64
```

Both exports are single-file, self-contained builds (no Godot or .NET install
required on the player machine). `export_presets.cfg` is committed. The v1.4.0
test build is published as a GitHub pre-release at
<https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.0>.
The packaged Linux binary was launched, booted and passed all twelve runtime
phases from the exported ZIP. The Windows x64 export is a PE32+ 1.4.0.0
executable, but **startup was not tested** because this environment has no
Windows runtime or Wine.

## Historical verification — v1.4.0 source tree

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj -> 132 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
godot --headless --path . -- --selftest                  -> 284 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
godot --headless --path . -- --selftest-chapter          -> 217 passed, 0 failed
godot --headless --path . -- --selftest-hotfix           -> 48 passed, 0 failed
godot --headless --path . -- --selftest-finale           -> 140 passed, 0 failed
godot --headless --path . -- --selftest-traversal        -> 18 passed, 0 failed
godot --headless --path . -- --selftest-release          -> 40 passed, 0 failed
godot --headless --path . -- --selftest-restore          -> 54 passed, 0 failed
godot --headless --path . -- --selftest-city             -> 129 passed, 0 failed
godot --headless --path . --export-release "Linux" ...    -> exit 0
packaged Linux v1.4.0 (all runtime phases)                -> 1300 passed, 0 failed
packaged Linux --headless --quit-after 120                -> exit 0
```

The new city phase covers the authored district, M21 gate/collider, C01–C03
state flow, LOD/actor cleanup and a mid-drive save/continue. `--selftest-restore`
still reloads seven partial construction/finale worlds. The v1.4.0 packaged
Linux executable passed the same phases from a temp export, every process exit
code 0.

Real AMD Radeon 890M measurements for the city-aware world are in
`docs/05_CITY_PERFORMANCE.md`. The v1.3.0 scene-backed numbers remain recorded in
`PROGRESS.md` for historical comparison. Packaged audio is unchanged from
v1.2.1; human listening remains **PASS** from the v1.2.0 verification and was
not separately re-auditioned. Child usability testing remains a manual
follow-up.

The v1.3.0 migration keeps the v1.2.1 hotfix, legacy-save pickup normalisation,
terminal off-island traffic stop and P15.1 bridge work. Traversal still runs 18
checks (fast/slow centre, both road edges, westbound, no vertical step, both
gate-1 walk/jump checks and a from-rest road-lip walk), plus seam/junction/cap
captures at exact 720p and true 1080p in `evidence/v13_scene_world/`.

## Developer QA scenarios

For faster visual QA only, these launch straight into a prepared state:

```bash
godot --path . -- --scenario-m03   # M01+M02 complete, bridge job active
godot --path . -- --scenario-m04   # M01 complete, Market Basket active
godot --path . -- --scenario-m05   # M01–M04 complete + bridge, rally active
godot --path . -- --scenario-m06   # M01–M04 complete, post job active
godot --path . -- --scenario-m07   # M01–M04 + bridge, swings active
godot --path . -- --scenario-m08   # M07 + playground done, park active
godot --path . -- --scenario-m09   # M05 done, bonus race active in the meadow
godot --path . -- --scenario-m10   # M08 + park done, furniture job active
godot --path . -- --scenario-final # nine jobs done, M10 active; finish for celebration
godot --path . -- --scenario-city  # roadworks done, road open, city plaza; CITY JOBS behind you
godot --path . -- --capture-city   # ten city screenshots; add --resolution 1920x1080
godot --path . -- --city-perf      # city stage metrics; add --perf-1080 for 1080p
```

They do not alter unlock tests or normal play.

## Save data

Godot writes `user://save.json` and keeps `user://save.json.bak`:

- Linux: `~/.local/share/godot/app_userdata/Patchwork Island/save.json`
- Windows: `%APPDATA%\Godot\app_userdata\Patchwork Island\save.json`

Writes are atomic (temporary file, flush, move). An unreadable or structurally
invalid save is renamed `save.json.corrupt-<timestamp>` and the backup is loaded
if possible.

Save schema versions are separate from game release versions. v1.4.0 writes
schema **4**. Schema 1 migrates through schema 2 and 3; missing character fields
default to **Boy + Everyday**. Schema 2 (v1.1.0) keeps coins, outfits,
cosmetics, bridge/park/furniture state, player/van snapshots and
`CelebrationSeen`; the new `FestivalSeen` starts false and M11 unlocks
immediately. Schema 3 (v1.2.1/v1.3.0) keeps all village progress and adds a
closed city road, an empty city board and a deterministic city seed. The next
save rewrites at schema 4 and keeps the original as `.bak`. A schema 5 or newer
save is **never** loaded, downgraded, discarded or overwritten automatically.
The title screen explains the incompatibility, and an ordinary autosave refuses
to touch it. Only an explicitly confirmed New Game may replace it, and a
`save.json.newer-vN-<timestamp>` copy is kept first.

Settings, player/van positions, missions, bridge/swing parts, park/furniture
placements, wallet, inventory, character preset, owned outfits, equipped outfit
the original celebration flag and the separate festival flag are all stored. Autosave runs every 20 seconds while
playing and after mission start/completion.

Manual save success/failure is shown inside the pause modal. Automatic failures
produce one visible warning until a later save succeeds. Settings failures are
shown inside the settings modal with their own throttle, so they cannot hide a
later autosave warning. Return-to-title and quit failures offer a safe choice
instead of implying success. A failed New Game leaves the previous persisted
save and Continue state untouched. If the unsaved session is then discarded,
Continue reloads the scene from the last successfully saved file. An explicitly
confirmed New Game may replace a newer save; that permission is session-scoped
and survives a failed write for a Save Now retry.

Completed M10 saves validate the owned furniture set rather than requiring all
pieces to be placed, so rearrangement is always saveable. Completion itself
still requires the physical placements. Race elapsed time has no gameplay cap
and saturates safely at the integer-millisecond representation limit.

## Accessibility / settings

The pause menu and title screen both open Settings. Music, sound-effects and
reduced-motion toggles are persisted. Reduced motion stops marker bobbing.
Bundled music, UI/world effects and vehicle loops are routed through separate
audio buses. Visual confirmations also support play with sound disabled. The HUD, title, job board and
furniture catalogue are tuned for 1280×720 and scale to larger windows.

## v1.5.0 Driving Academy verification

```text
dotnet run --project tests/PatchworkIsland.Tests.csproj -> 145 passed, 0 failed
godot --headless --path . -- --selftest                  -> 286 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 21 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
godot --headless --path . -- --selftest-living           -> 238 passed, 0 failed
godot --headless --path . -- --selftest-rally            -> 10 passed, 0 failed
godot --headless --path . -- --selftest-chapter          -> 217 passed, 0 failed
godot --headless --path . -- --selftest-hotfix           -> 48 passed, 0 failed
godot --headless --path . -- --selftest-finale           -> 140 passed, 0 failed
godot --headless --path . -- --selftest-traversal        -> 18 passed, 0 failed
godot --headless --path . -- --selftest-release          -> 40 passed, 0 failed
godot --headless --path . -- --selftest-restore          -> 54 passed, 0 failed
godot --headless --path . -- --selftest-city             -> 138 passed, 0 failed
godot --headless --path . -- --selftest-academy          -> 62 passed, 0 failed
xvfb-run ... godot --path . -- --selftest-quality        -> 22 passed, 0 failed
```

Schema 5 adds academy state and `LegacyVanPermit`; v1–v4 saves migrate
automatically. The licence gate applies to new v5 saves only.

The exported Linux binary was booted and passed the same 13 phases, plus the
packaged graphical quality gate (**22/22**) and packaged graphical academy
(**62/62**), under Xvfb with disposable XDG directories. The exact binary hash
and logs are in `evidence/v15_academy/packaged/`.

The fresh-save `--dev --playthrough` run earns the licence with real input, then
completes M01–M15; it stops at M16 on a pre-existing walkthrough
NPC-approach/route-planner limitation. That limitation is recorded in
`docs/DRIVING_ACADEMY_REPORT.md` and is not claimed as a passing full campaign.
