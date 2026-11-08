# Patchwork Island — First Playable v1.0.1

A happy little 3D village adventure for players aged roughly 5–10. A storm has
scattered the village's supplies. Deliver parcels, repair the bridge, drive the
post van, rebuild the playground, decorate the village green and furnish your
own home. Every completed job visibly changes the island.

This repository contains the complete Godot 4.7.2 .NET / C# source, automated
tests, real-input GUI evidence and release build instructions.

## Current status

**First playable — complete, with the v1.0.1 reliability update.** All ten
missions, the optional bonus race, free practice racing, the all-jobs
celebration, save/load, settings, onboarding, stability hardening and final
release verification are implemented. v1.0.1 fixes save invalidation after
furniture rearrangement and long races, makes every save failure visible,
protects New Game persistence flags, verifies Godot pause behaviour at runtime,
and returns modal cancellations to the correct screen.

- Windows x64 and Linux x86_64 standalone exports were produced.
- The Linux export was launched and played in this environment.
- The Windows export was produced and inspected, but **Windows startup was not
  tested** because no Windows runtime or Wine is available here. See
  [Windows export verification](#windows-export-verification).

## Download / install

Prebuilt artifacts are under `dist/` after an export:

```text
dist/windows/PatchworkIsland.exe        Windows x64, single executable
dist/linux/PatchworkIsland.x86_64       Linux x86_64, single executable
dist/PatchworkIsland-1.0.1-Windows-x86_64.zip
dist/PatchworkIsland-1.0.1-Linux-x86_64.zip
dist/SHA256SUMS
```

The exported builds are self-contained: players do **not** need Godot or a .NET
installation. They are fully offline. Save data is stored per user (see below).
The Windows build is unsigned, so Windows SmartScreen may ask for confirmation.

### Windows export verification

`Godot_v4.7.2-stable_mono_export_templates` produced a valid PE32+ x86-64
executable. The embedded pack contains `data/missions.json` and the .NET
assemblies, and the executable references the self-contained
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

1. Start a new adventure, then walk to the yellow **JOBS** board near the town
   hall (or press **J** anywhere).
2. Choose an available job. Locked jobs show the prerequisite they need.
3. Follow the gold objective marker and the top-centre compass arrow.
4. Press **E** at highlighted people, crates, worksites and counters.
5. Job-specific budgets (M04 groceries and M08 park) are separate from your
   permanent coin wallet. Wrong placements never consume task pieces.
6. After all ten jobs, the village celebration plays once and free roam,
   practice racing, the playground and your furnished home remain available.

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

The dependency-free runner currently has **60 tests** covering mission
prerequisites and unlocks, exactly-once rewards, duplicate pickups, mission
switching, hints, bridge/swing socket rules, grocery and park budgets, house
costs and free starter fallback, ordered checkpoints, optional race bonuses,
the full ten-job graph, JSON round-trips, save validation, corrupt/backup
recovery, newer-save preservation, cross-platform write failure/retry, race
timer restoration and saturation, post-completion furniture rearrangement,
save-feedback throttling, New Game persistence flags, control-mode precedence,
mission-catalogue integrity and a complete-field comparison between
`MISSIONS.json` and `data/missions.json`.

Godot runtime integration checks (52 assertions across two phases) execute the
real scene headlessly:

```bash
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
```

They cover title and modal pause, player/van physics stopping, race timers
stopping, settings/New Game modal returns, mission-completion pause, New Game
reset, autosave/manual save, and a driving save/continue cycle.

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
is loaded if possible. A save from a newer game version is **never** renamed or
overwritten automatically: the title screen explains the incompatibility, and
an explicitly confirmed New Game preserves a
`save.json.newer-vN-<timestamp>` copy first. Settings, player/van positions,
missions, bridge/swing parts, park/furniture placements, wallet, inventory and
the celebration flag are stored. Autosave runs every 20 seconds while playing
and after mission start/completion.

Save failures are explicit: manual saves show success or an inline failure in
the pause menu, autosave failures produce one throttled warning until a later
save succeeds, and return-to-title/quit failures offer a keep-playing or
quit-without-saving choice. A failed New Game never marks the new session as
the persisted save; Continue keeps loading the last valid file.

After M10 is complete, furniture may be picked up and rearranged freely.
Completion still requires the pieces to be physically placed, but a completed
world remains saveable while a piece is temporarily held or unplaced. Race
timers have no mandatory time limit; elapsed time saturates safely at the
integer-millisecond persistence ceiling instead of invalidating the save.

## Accessibility / settings

Music, sound-effects and reduced-motion toggles are persisted. Reduced motion
stops marker bobbing and spinning. The UI is readable at 1280×720 and scales to
1920×1080. There is currently no bundled audio, so all feedback is visual and
the game is fully playable with sound off.

## Known limitations

- **No audio assets are bundled.** Music/SFX toggles persist for a future audio
  pass; visual feedback is complete.
- **Text scaling is fixed.** The layout scales, but there is no text-size
  setting.
- **Windows startup not tested** in this environment (export succeeded).
- **Performance is measured only on software rendering** (Mesa llvmpipe,
  1280×720, observed 11–13 FPS). GPU performance was not measurable here.
- **Child usability testing was not possible.** No children participated; the
  age-appropriate design is based on specification and adult real-input checks.
- Practice-race progress is deliberately not persisted; it is reward-free and
  restarts on reload.

## Verification summary

- `dotnet build PatchworkIsland.sln`: 0 errors, 0 warnings.
- Automated tests: **60 passed, 0 failed**.
- Godot runtime self-tests: **42 + 10 assertions passed, 0 failed**.
- Headless editor import and main-scene boot: exit 0.
- Missing mission content: explicit FATAL and exit 1.
- Full fresh-save walkthrough (v1.0.0): M01–M10 completed with real
  keyboard/mouse input, final wallet 136, celebration once, practice race
  rewarded nothing, save/quit/relaunch preserved the world.
- v1.0.1 save scenarios: completed M10 → picked up a chair → manual save →
  quit → Continue; the chair remained owned and unplaced, then was moved to a
  new pad and persisted. A 66-minute race state (`elapsedMs=3,960,000`) was
  completed, M06 was then played normally, saved, quit and reloaded with both
  completions and the correct wallet.
- v1.0.1 failure UI: write-denied manual saves showed **Save failed**, left the
  previous file intact, and a retry succeeded. A failed New Game did not claim
  persistence, and quitting without saving preserved the old save.
- Linux export: launched and played; mission catalogue loaded from the embedded
  pack; final save loaded.
- Windows x64 export: produced and inspected; startup **NOT TESTED**.

Detailed evidence and commands are in `PROGRESS.md`, `ACCEPTANCE.md`,
`evidence/p8_*.png` and `evidence/p81_*.png`.

## Repository map

```text
scenes/Main.tscn          main scene
src/Main.cs               composition root, control/save coordination
src/Core/                 pure rules, mission/save/race/control logic
src/Build/                bridge, swings, park and furniture builders
src/Player/               player and camera
src/Vehicle/              delivery van
src/World/                procedural village and interactables
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
