# Patchwork Island — Scene-based town migration asset kit

**Source baseline:** `PhilipGB/patchwork-island` `v1.2.1` at `2d6647b09b3494b263b62d90c05ba3edfacbe6f6`.
**Intended milestone:** v1.3.0, editor-editable world architecture, initially visually and functionally equivalent to v1.2.1.

## Migration status in this checkout — implemented

Steps 1–3 have been completed locally:

- The exact bake was executed with Godot 4.7.2 .NET and exited 0. It produced
  `scenes/world/baked/Town_Baked.tscn`, 32 reusable `parts/*.tscn` scenes and
  488 top-level source roots.
- `src/World/WorldSceneAdapter.cs` instantiates the baked town at runtime,
  validates all 75 gameplay anchors and the six building centres/yaws/wall
  dimensions, resolves the legacy `WorldBuilder` handles and fails loudly if
  anything is missing or moved.
- `Main.cs` now uses the scene adapter. `WorldBuilder.Build()` is retained only
  as the deterministic source consumed by `SceneBakeTool`.
- Schema stays **3**, all M01–M20 content and save recovery paths are intact.
- New `--selftest-restore` proves partial M03/M07/M08/M10/M15/M18/M20 world
  visual restoration; `--selftest-release` now also restores a v1.2.1-shaped
  158-coin save with bridge, swings, park, furniture and outfits.
- Source runtime **1124 assertions**, pure rules **111 tests** and every packaged
  Linux runtime phase pass. See `PROGRESS.md` and `ACCEPTANCE.md` for the exact
  commands and remaining visual-polish gaps.

The original asset-kit instructions below are kept for provenance. The first
run verification warning in Step 2 is historical; the bake has now succeeded.

This ZIP contains Godot 4 `.tscn` scenes, editable primitive building/landmark assets,
positioned town preview, complete coordinate manifest, native rally preview mesh,
NPC/traffic waypoints, exact-bake tool, source tour screenshots and a migration handoff.

## Important distinction

- `scenes/world/Town_Preview.tscn` is **immediately inspectable in the Godot 3D editor**.
  It uses the same building and town-road coordinates as v1.2.1 but has some intentionally
  approximate ornament and editor-only props. It has **no gameplay wiring**.
- `tools/SceneBakeTool.cs` is a **one-shot exact-geometry converter**. Running it *inside the
  v1.2.1 source checkout* calls the existing `WorldBuilder.Build()` and serialises
  its actual meshes, collisions and deterministic foliage into packed Godot scenes.
  Output: `scenes/world/baked/Town_Baked.tscn` and reusable `parts/*.tscn`.
- `tools/WorldSceneAnchors.cs` supplies fail-fast lookup of named anchor nodes for the migration adapter.
- Neither scene is injected into `scenes/Main.tscn` automatically. Doing so while the
  original `WorldBuilder` is still active would duplicate the town and collisions.
- The game is **not fully migrated** merely by importing these files. The coding agent
  must build the gameplay adapter to reconnect the saved-world state and test parity.

## Step 1: Import into the real game checkout

Copy the *contents* of this ZIP into the Git checkout root, preserving file paths.
It is an additive overlay. It does not overwrite `Main.cs`, `WorldBuilder.cs`, `GameData.cs`,
or existing saves.

Open `res://scenes/world/Town_Preview.tscn` in Godot 4.7.2 .NET to inspect the
initial editable layout.

## Step 2: Generate the exact v1.2.1 scene snapshot

From the original game source root, after `dotnet build` and Godot import:

```bash
dotnet build PatchworkIsland.sln
godot --headless --path . --editor --quit
godot --headless --path . res://scenes/tools/SceneBakeTool.tscn
```

The bake tool should exit 0 and produce `Town_Baked.tscn` and `parts/`.
The bake tool has not been executed in this delivery environment (Godot is not installed here),
so the first run in the actual repository is an explicit verification step.
If it fails, examine the Godot output and address the source-version-specific
pack/ownership issue. Do not fall back to a visually similar but unverified
geometry substitute in the actual release.

## Step 3: Agent migration

Read `docs/MIGRATION_AGENT_HANDOFF.md` before changing runtime code.
The first merged version must retain schema **3**, mission M01–M20 state,
pickup IDs, build sockets, NPC/traffic locations, driving, cosmetics and the
original save recovery behaviours. Compare the source world and new scene
with the screenshots in `references/` and the numeric data in
`docs/layout_manifest_v1.2.1.json`. The canonical v1.3.0 town is described by
`docs/layout_manifest_v1.3.0.json`; the v1.2.1 file is retained as the
immutable historical source baseline.

## Scene folder contents

- `assets/buildings/*.tscn` — six reusable source-sized building shells.
- `assets/landmarks/*.tscn` — reusable job board, bakery, playground, etc.
- `assets/props/*.tscn` — trees, firs, flowers, benches/road props.
- `assets/environment/*.tscn` — terrain, roads, smooth rally-preview mesh.
- `Town_Preview.tscn` — placed editor preview of the known town layout.
- `GameplayAnchors.tscn` — stable positions and IDs for locations/mission pads.
- `NpcPatrolMarkers.tscn` / `TrafficRouteMarkers.tscn` — route data markers.
- `baked/` — generated exact packed scenes after running the tool.

## Guarantees and limitations

- All primary buildings use the verified source centre coordinates and dimensions.
- The preview's decorative foliage, gables, curved-road collision, and some signs
  are not pixel- or collider-identical to the source. **The exact bake is the
  authoritative path to visual parity.**
- Scene assets use `MeshInstance3D`, `BoxMesh`/`CylinderMesh`, `StaticBody3D`,
  `CollisionShape3D`, `Label3D` and `Marker3D` — genuinely editable Godot nodes.
- `RallyRoad.obj` is a **render-only preview** using the source Catmull–Rom points;
  it needs the original collision mesh or a collision bake before runtime use.
- No existing game file or user save is modified by unzipping this asset pack.
