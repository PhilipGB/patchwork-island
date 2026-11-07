# Patchwork Island — run and test guide

> Status: P6 complete. All ten missions, practice racing and the all-jobs
> celebration are playable; save/load, onboarding polish and final packaging are
> the remaining milestones.

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
| F12 | Developer screenshot (writes `evidence/manual_*.png`) |

## Automated tests

```bash
dotnet run --project tests/PatchworkIsland.Tests.csproj
```

The runner currently covers mission catalog, prerequisites, unlocks, duplicate
pickups, exactly-once rewards, mission switching, hints, bridge/swing snapping,
grocery and park budgets, furniture costs/starter fallback, ordered checkpoints,
optional race-bonus boundaries, the full ten-job graph and JSON round-trips.

If a concurrently running Godot instance makes `dotnet run` wait on the project
DLL, build once and execute the test assembly directly:

```bash
dotnet build tests/PatchworkIsland.Tests.csproj -nodeReuse:false -p:UseSharedCompilation=false
dotnet tests/bin/Debug/net8.0/PatchworkIsland.Tests.dll
```

## Headless capture harness

```bash
godot --path . -- --capture        # gameplay screenshots to evidence/
godot --path . -- --capture-title  # title screen screenshot
```

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
leaves free roam available. Save/load and final packaging arrive in P7–P8.

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

Save location is documented once persistence lands (P7).
