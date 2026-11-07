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

## Headless capture harness

```bash
godot --path . -- --capture        # gameplay screenshots to evidence/
godot --path . -- --capture-title  # title screen screenshot
```

## Save data

Save location is documented once persistence lands (P7).
