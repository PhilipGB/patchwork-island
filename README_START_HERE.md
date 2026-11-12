# Patchwork Island — Scene-based town migration asset kit

**Source baseline:** `PhilipGB/patchwork-island` `v1.2.1` at `2d6647b09b3494b263b62d90c05ba3edfacbe6f6`.
**Intended milestone:** v1.3.0, editor-editable world architecture, initially visually and functionally equivalent to v1.2.1.

## Current status — asset-driven, bake removed

The canonical town is `scenes/world/Town.tscn`, assembled from the reusable
scenes under `scenes/world/assets/`. `WorldSceneAdapter` instantiates it and
populates the `WorldModel` data contract. `WorldBuilder`, `SceneBakeTool` and
`scenes/world/baked/` have been removed; future development edits the authored
scenes directly.

- Schema stays **3**, all M01–M20 content and save recovery paths are intact.
- All 75 anchors, six building centres/yaws/wall dimensions and the NPC/traffic
  routes are validated at load.
- Source runtime **1171 assertions**, pure tests **123** and every packaged
  Linux runtime phase pass.

The asset-kit notes below are retained for provenance only. The bake
instructions are historical and no longer apply.

This ZIP contains Godot 4 `.tscn` scenes, editable primitive building/landmark assets,
positioned town preview, complete coordinate manifest, native rally preview mesh,
NPC/traffic waypoints, source tour screenshots and a migration handoff. The
historical exact-bake tool is no longer part of the repository.

## Important distinction

- `scenes/world/Town_Preview.tscn` is **immediately inspectable in the Godot 3D editor**.
  It uses the same building and town-road coordinates as v1.2.1 but has some intentionally
  approximate ornament and editor-only props. It has **no gameplay wiring**.
- There is no bake tool or baked snapshot. Edit `scenes/world/Town.tscn` and
  the reusable asset scenes directly. `WorldSceneAdapter` fails loudly if a
  required anchor, door or route marker is missing.
- `Town_Preview.tscn` remains as an editor preview only; it is not loaded at runtime.

## Step 1: Import into the real game checkout

Copy the *contents* of this ZIP into the Git checkout root, preserving file paths.
It is an additive overlay. It does not overwrite `Main.cs`, `GameData.cs`, or
existing saves.

Open `res://scenes/world/Town_Preview.tscn` in Godot 4.7.2 .NET to inspect the
initial editable layout.

## Step 2 (historical): exact bake — retired

The bake tool and `scenes/world/baked/` snapshot have been removed. Do not
regenerate scenery; edit `scenes/world/Town.tscn` and `scenes/world/assets/`
directly and run the normal build/tests.

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
- `Town_Preview.tscn` — editor-only preview; not loaded at runtime.
- `GameplayAnchors.tscn` — stable positions and IDs for locations/mission pads.
- `NpcPatrolMarkers.tscn` / `TrafficRouteMarkers.tscn` — route data markers.

## Guarantees and limitations

- All primary buildings use the verified source centre coordinates and dimensions.
- The preview's decorative foliage, gables, curved-road collision, and some signs
  are not pixel- or collider-identical to the source. **The authored scenes are
  the authoritative path; there is no bake step.**
- Scene assets use `MeshInstance3D`, `BoxMesh`/`CylinderMesh`, `StaticBody3D`,
  `CollisionShape3D`, `Label3D` and `Marker3D` — genuinely editable Godot nodes.
- `RallyRoad.obj` is a **render-only preview** using the source Catmull–Rom points;
  runtime collision comes from `RallyRoad_Collidable.tscn`.
- No existing game file or user save is modified by unzipping this asset pack.
