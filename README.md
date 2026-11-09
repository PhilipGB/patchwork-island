# Patchwork Island — v1.1.0 character update

A happy little 3D village adventure for players aged roughly 5–10. A storm has
scattered the village's supplies. Deliver parcels, repair the bridge, drive the
post van, rebuild the playground, decorate the village green and furnish your
own home. Every completed job visibly changes the island.

This repository contains the complete Godot 4.7.2 .NET / C# source, automated
tests, real-input GUI evidence and release build instructions.

## Current status

**First playable — complete, now at v1.1.0.** All ten missions, the optional
bonus race, free practice racing, the all-jobs celebration, save/load, settings,
onboarding, stability hardening and character personalisation are implemented.
The v1.0.1 reliability update fixed save invalidation after
furniture rearrangement and long races, truthful save feedback, New Game
persistence flags and modal cancellation. v1.0.2 makes Continue reload the last
persisted village after an unsaved session is discarded, allows retry of an
authorised newer-save replacement, shows settings failures inside their modal,
rejects duplicate mission JSON properties, and verifies race bonuses against
the persisted clock.

**v1.1.0 (P9) character personalisation** adds a Boy/Girl choice when starting
a new game, four purchasable outfits plus the free Everyday starting outfit, a
clothes shop in town, coin spending, an in-shop changing room, and save/load for
the chosen preset, owned outfits and worn outfit. Save schema **2** introduces
those fields; schema 1 saves migrate with all progression intact and default to
Boy + the Everyday outfit. Unsupported newer schemas are never loaded or
overwritten automatically. See [Save data](#save-data). v1.1.0 Windows x64 and
Linux x86_64 exports were produced from this source; the Linux export passed all
three runtime phases headlessly.

- v1.1.0 Linux export: launched headlessly, exit 0, and passed **103 + 19 + 101**
  runtime assertions from the packaged executable.
- v1.1.0 Windows export: PE32+ x86-64, file/product version `1.1.0.0`.
  **Windows startup was not tested** because no Windows runtime or Wine is
  available here. See [Windows export verification](#windows-export-verification).

## Download / install

Prebuilt v1.1.0 artifacts are under `dist/`:

```text
dist/windows/PatchworkIsland.exe        Windows x64, single executable (v1.1.0)
dist/linux/PatchworkIsland.x86_64       Linux x86_64, single executable (v1.1.0)
dist/PatchworkIsland-1.1.0-Windows-x86_64.zip
dist/PatchworkIsland-1.1.0-Linux-x86_64.zip
dist/PatchworkIsland-1.0.2-Windows-x86_64.zip   previous release archive
dist/PatchworkIsland-1.0.2-Linux-x86_64.zip     previous release archive
dist/SHA256SUMS
```

The exported builds are self-contained: players do **not** need Godot or a .NET
installation. They are fully offline. Save data is stored per user (see below).
The Windows build is unsigned, so Windows SmartScreen may ask for confirmation.

### Windows export verification

`Godot_v4.7.2-stable_mono_export_templates` produced a valid PE32+ x86-64
executable with file/product version `1.1.0.0`. The embedded pack contains
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

The dependency-free runner currently has **77 tests** covering mission
prerequisites and unlocks, exactly-once rewards, duplicate pickups, mission
switching, hints, bridge/swing socket rules, grocery and park budgets, house
costs and free starter fallback, ordered checkpoints, optional race bonuses
verified against the persisted clock, the full ten-job graph, JSON round-trips,
save validation, corrupt/backup recovery, newer-save preservation, deterministic
cross-platform write failure/retry, race timer restoration and saturation,
post-completion furniture rearrangement, save-feedback throttling, settings
feedback isolation, persisted/discarded session state, newer-replacement
authorisation, control-mode precedence, mission-catalogue integrity, clothing
purchase/ownership/equip rules, customisation save round-trips, older-save
customisation defaults and a complete-field comparison between `MISSIONS.json`
and `data/missions.json`, including duplicate-property rejection.

Godot runtime integration checks (190 assertions across three phases) execute
the real scene headlessly:

```bash
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
godot --headless --path . -- --selftest-recovery
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

Save schema versions are separate from game release versions. v1.1.0 writes
schema **2** and loads supported older schemas:

- **Schema 1** (v1.0.x) saves migrate in memory to schema 2. Every progression
  field is preserved; missing character fields default to **Boy + Everyday + no
  purchases**. The file is rewritten as schema 2 by the next save, and the
  original schema 1 copy is kept as the `.bak` backup.
- **Schema 3 or newer** is unsupported and is **never** loaded, downgraded,
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
- Automated tests: **78 passed, 0 failed**.
- Godot runtime self-tests: **103 + 19 + 101 assertions passed, 0 failed**.
- P9 screenshots: character chooser, girl/boy world models, clothes shop,
  purchase feedback and outfit change (`evidence/p9_*.png`).
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
- v1.0.2 recovery scenarios: a failed New Game followed by Return to title →
  Return anyway → Continue reloaded the nine-mission/130-coin village; a
  discarded movement change reloaded the persisted player position; a schema-99
  newer save survived a failed replacement and was then replaced successfully
  by Save Now with an exact preserved copy.
- v1.0.2 settings: a failed settings write was shown inside the modal, the
  following autosave failure still produced its first warning, and a successful
  retry replaced the error message.
- Linux v1.0.1 export: launched and played; mission catalogue and final save
  loaded; the packaged binary also passed its runtime assertions headlessly.
- v1.0.2 exports: Linux `62,221,666` bytes and Windows `72,595,458` bytes
  (ZIPs), with version `1.0.2`. The Linux build launched under Xvfb, loaded the
  existing nine-job/130-coin save and the job board, and passed **42 + 10 + 98**
  runtime assertions from the packaged executable. The Windows executable is
  PE32+ x86-64 with version `1.0.2.0`, embedded mission content, assemblies and
  the self-contained .NET 8 runtime.
- P9 v1.1.0: Boy/Girl choice, four purchasable outfits, clothes-shop building,
  real Buy/Wear runtime tests, save/Continue persistence and new-game reset.
  Project and export metadata report version `1.1.0`.
- P9 review corrections: schema 1→2 migration with schema 3/99 refusal and
  untouched-file checks; all outfits previewable through enabled Preview
  controls; actual Boy/Girl/Cancel buttons exercised in runtime tests; a
  successful clothes-shop retry clears the previous save error.
- v1.1.0 Linux export: headless boot exit 0 and **103 + 19 + 101** runtime
  assertions passed from the packaged binary.
- v1.1.0 Windows export: PE32+ x86-64, file/product version `1.1.0.0`.
- F12 is inactive in the normal export (screenshot count unchanged).
- Windows startup: **NOT TESTED** (no Windows runtime or Wine).
- SHA-256 checksums are recorded in `dist/SHA256SUMS`; the Linux ZIP is
  `c6f4b7e6bdcc53df6167b9632266266b873c119fb4837cf4e2f13849c27defad` and the
  Windows ZIP is
  `5bebe3aaadb5ef7737af718d4cadbb074a0a1bd9a25ef52173051a4c183d0e8e`.

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
