# Patchwork Island — run and test guide

> Status: P0 skeleton. This document is expanded as milestones are completed.

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
| F12 | Developer screenshot (writes `evidence/manual_*.png`) |

## Automated tests

```bash
dotnet run --project tests/PatchworkIsland.Tests.csproj
```

The runner currently covers mission catalog, prerequisites, unlocks, duplicate
pickups, exactly-once rewards, mission switching, hints and JSON round-trips.

## Headless capture harness

```bash
godot --path . -- --capture        # gameplay screenshots to evidence/
godot --path . -- --capture-title  # title screen screenshot
```

## Current gameplay loop

1. Esc/Enter on the title screen starts the game.
2. Walk to the yellow JOBS board by the town hall, or press **J** anywhere.
3. Choose The Red House Delivery or Workshop Supplies.
4. Follow the gold marker and top-centre arrow, press **E** on highlighted
   objects, and return to the labelled destination.

Four missions are fully playable in this milestone (M01 apples, M02 supplies,
M03 bridge, M04 groceries). The bridge opens the east meadow. There is no save
yet (P7).

## Developer QA scenarios

For faster visual QA only, these launch straight into a prepared state:

```bash
godot --path . -- --scenario-m03   # M01+M02 complete, bridge job active
godot --path . -- --scenario-m04   # M01 complete, Market Basket active
```

They do not alter unlock tests or normal play.

## Save data

Save location is documented once persistence lands (P7).
