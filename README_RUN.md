# Patchwork Island — run and test guide

> Status: v1.1.0 — P9 character update. All ten missions,
> practice racing, the all-jobs celebration, save/load, settings, onboarding,
> stability hardening and release packaging remain in place. P9 adds a
> Boy/Girl starting choice, a town clothes shop, four purchasable outfits, an
> in-shop changing room and persisted character/outfit ownership. P8.2's
> session recovery, newer-save protection, inline settings failures, duplicate
> mission-property rejection and persisted-clock race bonuses are retained. See
> `ACCEPTANCE.md` and `PROGRESS.md` for the pass/fail matrix and evidence.

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

The runner currently has **77 tests** and covers mission catalog, prerequisites,
unlocks, duplicate pickups, exactly-once rewards, mission switching, hints,
bridge/swing snapping (including rejected unknown part types), grocery and park
budgets, furniture costs/starter fallback/purchase caps, ordered checkpoints,
optional race-bonus boundaries, persisted-clock bonus verification and invalid
bonus rejection, the full ten-job graph, JSON round-trips, structural save
validation, corrupt/backup recovery, newer-save preservation, deterministic
in-process failed-write/retry, race-time restoration and saturation,
post-completion furniture rearrangement, save-feedback throttling, settings
feedback isolation, persisted/discarded session state, newer-replacement
permission, control-mode precedence, clothing defaults, purchase and ownership
rules, equip restrictions, clothing save round-trips, older-save defaults,
mission-catalogue integrity and a complete-field comparison of
`MISSIONS.json`/`data/missions.json` that rejects duplicate properties at any
depth.

## Godot runtime integration tests

```bash
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
godot --headless --path . -- --selftest-recovery
```

These use isolated `user://selftest-save.json` and
`user://selftest-recovery-save.json` files and run **190 assertions across
three phases**. The first two cover title/modal pause, pausable player/van/world
processing, stopped physics and race timers, Pause→Settings→Pause,
Title→Settings→Title, New Game confirmation cancellation, the character
chooser, real clothes-shop Buy/Wear buttons, wallet updates, insufficient
funds, owned-outfit protection, visible appearance changes, mission-completion
pause, New Game reset, manual/automatic saves, and save/relaunch while driving.
The recovery phase injects deterministic write failures and reloads the real
scene to verify failed New Game recovery, discarded movement, backup recovery,
the no-save case, settings failure feedback, autosave warning independence,
newer-save replacement retries, preserved newest-version copies, restored
character/outfit state and unchanged on-disk bytes.

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
required on the player machine). `export_presets.cfg` is committed. The Linux
export was launched and played. The Windows x64 export was produced and
inspected but **startup was not tested** because this environment has no Windows
runtime or Wine. See `dist/SHA256SUMS` and `dist/README.txt`.

## Final verification

```text
dotnet build PatchworkIsland.sln                         -> 0 warnings, 0 errors
dotnet run --project tests/PatchworkIsland.Tests.csproj -> 77 passed, 0 failed
godot --headless --path . --editor --quit                -> exit 0
godot --headless --path . --quit-after 120               -> exit 0
godot --headless --path . -- --selftest                  -> 70 passed, 0 failed
godot --headless --path . -- --selftest-resume           -> 19 passed, 0 failed
godot --headless --path . -- --selftest-recovery         -> 101 passed, 0 failed
xvfb-run godot --path . -- --dev --capture               -> P9 visual evidence written
godot --headless --path . --export-release "Linux" ...    -> exit 0
godot --headless --path . --export-release "Windows..."   -> exit 0
dist/linux/PatchworkIsland.x86_64 --headless --quit-after 120 -> exit 0
dist/linux/PatchworkIsland.x86_64 (three runtime phases)  -> 70 + 19 + 101 passed, 0 failed
```

The P9 runtime phase opens the real character chooser and clothes shop, presses
the actual Buy/Wear buttons, checks wallet and ownership rules, and verifies
that the saved appearance survives Continue. The recovery phase restores a
saved girl/outfit seed after a real scene reload.

The v1.1.0 Linux export was produced and passed all three runtime phases from
the packaged executable. The v1.1.0 Windows executable is PE32+ x86-64 with
file/product version `1.1.0.0`; Windows startup remains untested. The previous
v1.0.2 release verification remains recorded in `ACCEPTANCE.md`.

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
first. Settings, player/van positions, missions, bridge/swing parts,
park/furniture placements, wallet, inventory, character preset, owned outfits,
equipped outfit and the celebration flag are all stored. Autosave runs every 20
seconds while playing and after mission start/completion. The three
customisation fields are optional additions inside schema version 1: saves from
v1.0.0–v1.0.2 load as **Boy + Everyday** with no purchased outfits.

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
There is no bundled audio yet, so visual confirmations are always shown; the
sound toggles are ready for a future audio pass. The HUD, title, job board and
furniture catalogue are tuned for 1280×720 and scale to larger windows.
