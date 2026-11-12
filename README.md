# Patchwork Island

A small 3D village adventure for players aged roughly 5–10, built with
Godot 4.7.2 .NET and C#. Collect supplies, deliver parcels, build a bridge,
drive the post van, help the villagers and complete three jobs in Patchwork City.

The current game has **21 village missions (M01–M21) and three city jobs
(C01–C03)**, two celebrations, optional race bonuses, free practice racing,
character customisation and persistent progress. The original ten-mission
handoff is the historical first-playable specification.

## Current status

This branch is the **1.4.3-dev Linux quality and architecture pass**, unreleased.
It fixes camera orbit after pause, shop-controller lifetimes, interaction
clarity, city curb traversal and collected-crate focus. Gameplay controllers,
mission activities and presentation are separated behind a live session facade.

- [Linux verification, evidence and remaining limits](docs/LINUX_QUALITY_PASS.md)
- [Architecture and responsibility boundaries](docs/ARCHITECTURE.md)
- [Run, controls, QA scenarios and export guide](README_RUN.md)
- [Durable progress and historical release results](PROGRESS.md)

The latest published baseline is
[v1.4.2](https://github.com/PhilipGB/patchwork-island/releases/tag/v1.4.2).
The changes on this branch have not been published as a release. Older release
notes and performance measurements in `docs/` describe their named revisions;
they are not verification of the current source.

## Run from source

Install **Godot 4.7.2 stable .NET edition** and **.NET SDK 8.0** (the target is
`net8.0`). The standard Godot edition cannot run the C# game.

```sh
dotnet build PatchworkIsland.sln
godot --path . --editor       # import assets once
godot --path .
```

The authored town is `scenes/world/Town.tscn`; the city is
`scenes/city/CityDistrict.tscn`. Edit these scenes in Godot. There is no bake
or procedural world-generation step.

## Controls and progression

| Input | Action |
| --- | --- |
| WASD / arrows | Move relative to the camera |
| Space | Jump |
| E | Use the highlighted target / enter or leave the van |
| Right-drag / mouse wheel | Orbit / zoom |
| J / H | Jobs / hint |
| Esc / P | Pause or close a menu |
| W/S, A/D in van | Accelerate, brake/reverse and steer |
| R in van | Recover the van |

Start a job at the yellow JOBS board, follow the mission card and destination
arrow, then interact with the highlighted supplies or station. Build mode uses
large clickable sockets. Race time bonuses are optional. Completing M01–M20
unlocks Roadworks Rescue (M21), which opens the road to the independent city
board. City and village jobs share the same wallet.

## Verification and Linux export

```sh
dotnet run --project tests/PatchworkIsland.Tests.csproj
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-city
# Requires a graphical display, or xvfb-run:
godot --path . -- --selftest-quality
```

The full twelve-phase runtime suite, graphical regression checks and input
walkthrough commands are in [the run guide](README_RUN.md). Tests and the
walkthrough must use disposable user data; do not point them at a player save.

Matching Godot .NET export templates are required:

```sh
mkdir -p dist/linux
godot --headless --path . --export-release Linux dist/linux/PatchworkIsland.x86_64
```

The self-contained Linux executable needs neither Godot nor .NET installed on
the player's machine. Check the export log and launch the executable: Godot
can return exit code 0 even if its .NET publish step failed.

## Saves and settings

Linux saves live in
`~/.local/share/godot/app_userdata/Patchwork Island/`. Save schema 4 retains
migration from older saves, backup recovery and protection against overwriting
newer schemas. Copy the whole save directory before moving between builds.
Music, sound, reduced motion, contrast and text settings are available in game.
The product owner has accepted the sound; this pass changes no audio assets or
mix levels.

## Repository map

```text
src/Main.cs              session/input composition and coordination
src/Core/                pure C# state, rules, missions, save validation/recovery
src/Gameplay/            activity, population, pickup and race controllers
src/Player/, Vehicle/    movement, physics and camera
src/World/, Build/       authored-scene adapters, actors and building systems
src/UI/                  views and mission HUD presenter
src/Testing/             opt-in runtime fixtures and input walkthrough
scenes/, assets/         authored world and original game assets
data/                    runtime mission content
tests/                   dependency-free pure C# tests
docs/, evidence/         versioned decisions and executed verification
```

[ACCEPTANCE.md](ACCEPTANCE.md) records the original playable contract;
[city acceptance](docs/03_ACCEPTANCE.md) extends it. Current verification is
recorded separately in [the Linux quality report](docs/LINUX_QUALITY_PASS.md).
Current hardware verification covers Linux on one Radeon GPU; platform and
usability limits are recorded in the Linux report.
