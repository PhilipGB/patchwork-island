# Patchwork Island — run and test guide

> Status: P8.1 (v1.0.1) reliability release complete. All ten missions,
> practice racing, the all-jobs celebration, save/load, settings, onboarding,
> stability hardening and release packaging are in place. P8.1 fixes
> post-completion furniture and long-race save invalidation, makes every save
> failure visible, protects New Game persistence flags, verifies pause
> behaviour at runtime and fixes pause-confirmation cancellation. See
> `ACCEPTANCE.md` and `PROGRESS.md` for the final pass/fail matrix and evidence.

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

The runner currently has **60 tests** and covers mission catalog, prerequisites,
unlocks, duplicate pickups, exactly-once rewards, mission switching, hints,
bridge/swing snapping (including rejected unknown part types), grocery and park
budgets, furniture costs/starter fallback/purchase caps, ordered checkpoints,
optional race-bonus boundaries and invalid bonus rejection, the full ten-job
graph, JSON round-trips, structural save validation, corrupt/backup recovery,
newer-save preservation, a real cross-platform failed-write/retry, race-time
restoration and saturation, post-completion furniture rearrangement, save
feedback throttling, New Game persistence flags, control-mode precedence,
mission-catalogue integrity and a complete-field comparison of
`MISSIONS.json`/`data/missions.json`.

## Godot runtime integration tests

```bash
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
```

These use an isolated `user://selftest-save.json` and run 52 assertions across
two processes: title/modal pause, pausable player/van/world processing, stopped
physics and race timers, Pause→Settings→Pause, Title→Settings→Title, New Game
confirmation cancellation, mission-completion pause, New Game reset, manual and
automatic saves, and save/relaunch while driving.

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
`--capture` or scenario argument. F12 writes to `user://screenshots/`, never to
an arbitrary working directory, and reports file errors instead of throwing an
unobserved task exception.

## Current gameplay loop

1. Esc/Enter on the title screen starts the game.
2. Walk to the yellow JOBS board by the town hall, or press **J** anywhere.
3. Choose an available job. Prerequisites are shown on locked cards.
4. Follow the gold marker and top-centre arrow, press **E** on highlighted
   objects, and return to the labelled destination or build site.

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

## Release packaging

Export templates for Godot 4.7.2 .NET must be installed under
`~/.local/share/godot/export_templates/4.7.2.stable.mono/`.

```bash
godot --headless --path . --export-release "Windows Desktop" dist/windows/PatchworkIsland.exe
godot --headless --path . --export-release "Linux" dist/linux/PatchworkIsland.x86_64
```

Both exports are single-file, self-contained builds (no Godot or .NET install
required on the player machine). `export_presets.cfg` is committed. The Linux
export was launched and played. The Windows x64 export was produced and
inspected but **startup was not tested** because this environment has no Windows
runtime or Wine. See `dist/SHA256SUMS` and `dist/README.txt`.

## Final verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj -> 60 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
godot --headless --path . -- --selftest                  -> 42 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 10 passed, 0 failed
Linux exported binary --headless --quit-after 120        -> exit 0
Linux exported binary (Xvfb)                             -> title, Continue and job board verified
```

Performance on this machine was measured only with the Mesa llvmpipe software
renderer at 1280×720: 11–13 FPS. GPU performance and child usability testing
were not available. Audio is not bundled; all feedback is visual.

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
```

They do not alter unlock tests or normal play.

## Save data

Godot writes `user://save.json` and keeps `user://save.json.bak`:

- Linux: `~/.local/share/godot/app_userdata/Patchwork Island/save.json`
- Windows: `%APPDATA%\Godot\app_userdata\Patchwork Island\save.json`

Writes are atomic (temporary file, flush, move). An unreadable or structurally
invalid save is renamed `save.json.corrupt-<timestamp>` and the backup is loaded
if possible. A save from a newer game version is **never** renamed or
overwritten automatically: the title screen explains the incompatibility, and an
explicitly confirmed New Game keeps a `save.json.newer-vN-<timestamp>` copy
first. Settings, player/van positions, missions, bridge/swing parts, park/furniture
placements, wallet, inventory and the celebration flag are all stored. Autosave
runs every 20 seconds while playing and after mission start/completion.

Manual save success/failure is shown inside the pause modal. Automatic failures
produce one visible warning until a later save succeeds. Return-to-title and
quit failures offer a safe choice instead of implying success. A failed New
Game leaves the previous persisted save and Continue state untouched.

Completed M10 saves validate the owned furniture set rather than requiring all
pieces to be placed, so rearrangement is always saveable. Completion itself
still requires the physical placements. Race elapsed time has no gameplay cap
and saturates safely at the integer-millisecond representation limit.

## Accessibility / settings

The pause menu and title screen both open Settings. Music, sound-effects and
reduced-motion toggles are persisted. Reduced motion stops marker bobbing.
There is no bundled audio yet, so visual confirmations are always shown; the
sound toggles are ready for a future audio pass. The HUD, title, job board and
furniture catalogue are tuned for 1280×720 and scale to larger windows.
