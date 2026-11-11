# Agent handoff — v1.3.0 scene-based world migration

Implement a genuine Godot editor-editable town without changing gameplay or
schema-3 save semantics. Baseline tag: v1.2.1. Do not merge or release solely
because the static town preview looks acceptable.

## Architecture

Current `Main.cs` constructs `WorldBuilder`, calls `Build()` and reads many
mutable properties: `BridgeBarrier`, `FinishedBridge`, `CelebrationDecor`,
`FestivalDecor`, `RallyPodium`, `PipCarBroken/Fixed`, `SwingFrames`, `ParkPads`,
`HousePads`, `GardenPads`, `BookSpawnPoints`, `RepairPartSpawnPoints`,
`RaceGates`, `M19RaceGates`, `BakeryDisplaySlots`, `LibraryShelfSlots`,
`SignSocketRoots`, `FestivalTableVisuals`/`FestivalLanternVisuals`, etc.

Keep these contracts while migrating. Recommended pattern:

1. Create `WorldSceneAdapter` / scene-backed `WorldLayout` which instantiates
   `Town_Baked.tscn`, resolves stable node paths and typed marker groups, and
   exposes the existing gameplay handles/arrays. Fail visibly if required
   nodes/sockets are missing; never silently substitute zero coordinates.
2. Move persistent objects to reusable scenes with stable gameplay IDs.
   Object visual content is reusable, while scene instances own their position.
3. Preserve the code for mission rules, player/van controls, NPC movement,
   traffic and the rally timer. Reconnect existing scripts to scene roots;
   do not port their logic into static assets.
4. Preserve `BuildSystem` ownership of dynamic bridge/swing/furniture/park
   construction (or recreate the identical objects by saved IDs). Never bake
   completed or unfinished dynamic state as a permanent, duplicate static mesh.
5. Move remaining root-level props into suitable nearby landmark subscenes
   after the initial exact bake. Keep all world transforms unchanged.
6. Retire procedural static construction only after the new world passes parity.

## Save compatibility — non-negotiable

- Leave `SaveService.CurrentVersion == 3` and the schema-3 save model unchanged.
- Load historical schema 1/2/3 saves, including completed v1.2.1 villages.
- Keep newer-save protection, atomic saves, backup recovery and new-game retry.
- Preserve all 20 missions, exact reward/wallet state, and the two distinct finales.
- Maintain collected pickup IDs including v1.2.1 M18 repair parts.
- Keep player and van transforms and driving state exactly correct after Continue.
- Preserve bridge pieces, 2 swings, park/furniture ownership and placements.
- Preserve original boy/girl presentation and all owned/equipped clothing.
- Keep waypoint targets, door interaction radii, collision layers, NPC/traffic routes.

## Required deterministic parity checks

1. `Town_Baked` imports and opens in Godot 4.7.2 .NET with no missing resources.
2. Source-instance placement, yaw, wall dimensions and doors match the
   `layout_manifest_v1.2.1.json` values, with at most 0.01 m tolerance.
   The canonical v1.3.0 scene is checked against
   `layout_manifest_v1.3.0.json`; v1.2.1 remains the historical source
   baseline.
3. All critical world anchors in `GameplayAnchors.tscn` resolve once.
4. Doors, road/bridge approaches, river, rally gates and other reachable
   geometry have equivalent collision; camera-only blockers remain layer 16.
5. World object/scene IDs match historical saves; no saved object is renamed.
6. All 20 missions complete through actual stations/interactions.
7. Partially completed M03/M07/M08/M10/M15/M18/M20 saves restore world visuals.
8. A v1.2.1 save with 158 coins and existing placements, outfits and bridge
   restores exactly; do not use the screenshot's wallet as a forced fixture.
9. NPC patrols and both free-roam traffic routes stay in their source locations.
10. Real Linux exported-game boot, source CI and packaged runtime tests pass.
11. Record a comparable GPU FPS/frame-time run; avoid a large performance
    regression from hundreds of separate scene MeshInstance3D nodes.
12. Verify visually at both 1280x720 and 1920x1080 using the reference tour.

## Acceptable visual improvements

Screenshots identify view occlusion by large tree canopies, awkward camera
interior clipping, visible straight-edge joins at perpendicular roads/rally,
sign stacking/occlusion and large distracting foreground objects.

Fix these carefully **after** snapshot parity. For each improvement, capture
before/after screenshots; keep player access, collider and route coordinates
unchanged unless a change is justified and regression-tested. Suggested fixes:

- Make camera obstruction fade/hide nearby tree canopies or resolve camera boom
  collision, without deleting trees or collision geometry.
- Make interiors readable at close camera distances; validate doorway camera
  blocker on physics layer 16.
- Use one nonoverlapping meshed surface for each road junction rather than
  overlapping coplanar slabs; preserve road height and vehicle paths.
- Keep decorative rally crossings clear of raised collision lips; use the
  v1.2.1 continuous rally source and preserve both gate sets.
- Move/scale sign visuals independently from interaction markers so labels are
  visible without changing mission triggers.

## PR breakdown

- PR 1 — Add the exact baked snapshot, reusable assets and numeric anchor tests.
- PR 2 — Adapter: static town scene instantiation in place of procedural static
  world, retaining dynamic objects and save mapping. Compare old/new side by side.
- PR 3 — Move remaining decorations, roads, collision and NPC/mission markers
  to appropriate scene instances; remove old static code only when unused.
- PR 4 — Targeted geometry/camera polish, regression tests and export checks.

Do not automatically tag or release until verified by human gameplay/visual
checks. Never claim the preview alone is a fully migrated playable game.
