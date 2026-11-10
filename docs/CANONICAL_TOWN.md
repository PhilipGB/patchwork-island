# Canonical editor-authoritative town (v1.3.0)

This document records the second v1.3.0 migration: the runtime no longer uses
the baked snapshot or runtime procedural scenery. `scenes/world/Town.tscn` is
the editor-authoritative level, assembled from the supplied reusable scenes in
`scenes/world/assets/`.

## Runtime source of truth

- `WorldSceneAdapter` instantiates `res://scenes/world/Town.tscn`.
- `WorldBuilder.Build()` is not called during gameplay and `SceneBakeTool` is not
  rerun for the canonical scenery.
- Gameplay positions come from `GameplayAnchors`, building `DoorMarker` children,
  scene gate nodes and the authored `NpcPatrolMarkers` / `TrafficRouteMarkers`
  groups. No position-equality assertions remain in the runtime adapter.
- Save schema remains **3**; all M01–M20 mission content, pickups, build states,
  cosmetics and recovery behaviour are unchanged.
- The previous `Town_Baked.tscn` and `Town_Assets.tscn` remain in the repository
  as reference material. Neither is loaded at runtime.

## Scene additions

`Town.tscn` is a copy of the supplied `Town_Assets.tscn` with only the additions
needed by existing stable gameplay IDs:

| Node | Reason |
|---|---|
| `SignSocket_market`, `SignSocket_workshop`, `SignSocket_green` | M14 needs the three authored sign locations that were absent from the candidate scene. |
| `RaceStart` marker at `(56, 0, -6)` | The adapter exposes the race start from a scene node instead of a C# constant. |
| `BakeryStall/BakeryOrder_*` | `BakeryOrderSlot.tscn` instances provide the stable M12 display slots. |
| `LibraryShelf/ShelfBook_*` | `ShelfBookSlot.tscn` instances provide the stable M13 display slots. |

`SignSocket.tscn`, `BakeryOrderSlot.tscn` and `ShelfBookSlot.tscn` are authored
reusable scenes, not runtime-generated meshes.

## Collision contract

- Physical ground, roads and rally surfaces are on layer 1.
- Camera-only blockers remain on layer 16.
- The authored town-road visual slabs sit 2–3.4 cm above the terrain. Their
  collision boxes were originally offset to the visual top as well, creating a
  vertical lip at every junction and at the built bridge deck. A van crossing
  the east bridge seam stopped with its bumper at `x≈50.5`.
- `TownRoads_Collidable.tscn` now places the road collision roadbed at `y=0`
  (top flush with the terrain and bridge deck) while leaving the visuals
  unchanged. This removes the lips without changing the road layout, render
  surfaces or layer.
- Traversal now passes fast, slow, both lateral offsets, and both bridge
  directions. The bridge seam has no measurable vertical step.

## Dynamic visual templates

Player, six NPCs, van, both town traffic cars, the rally rival and pickups remain
dynamic actors with their original controllers, collision, camera rig, save
restoration and logic. Only their visual subtrees use the supplied GLB-backed
scenes.

- The GLB meshes are unrigged and store part geometry in model space. Their
  palette is in `COLOR_0`, so `VisualTemplate` enables
  `VertexColorUseAsAlbedo`.
- `VisualTemplate.BuildCharacterRig` reparents head, arm and leg parts under
  pivots while preserving their global transforms. Existing walk, wave, jump,
  head-turn and body-bob animation therefore still works.
- `VisualTemplate.BuildWheelPivots` does the same for the four authored vehicle
  wheels, so wheel rotation remains functional.
- Player outfit colours are applied to the clothing parts only; skin, hair and
  facial parts keep their authored colours. The M01 cap reward remains a small
  overlay on the boy silhouette because the authored boy model has no cap.
- Pickups preserve their authored materials. Books and parcels receive their
  per-mission cover colour and word label so M06/M13 matching stays readable.

Template mapping is recorded in `docs/scene_bindings.json` and enforced at
runtime: every player/NPC/van/traffic/pickup visual reports the template path it
loaded, and the runtime self-test asserts those paths.

## Asset deviations

- No scooter GLB was supplied. The `town_scooter` route ID, driver, speed,
  waypoints and audio are unchanged; its visual uses
  `TownCar_Green_Visual.tscn`, and `delivery_car` uses
  `TownCar_Blue_Visual.tscn`.
- The supplied swing frames contain four `BROKEN` labels (two per frame). The
  restore check now verifies that every scene-authored label stays visible
  rather than requiring exactly two.
- The supplied frontend/landmark scenes do not include a scooter or procedural
  sign posts. The sign posts and display slots are the authored additions
  listed above.

## Editor-move proof

The proof is intentionally scene-only and was run without editing any C#
coordinate constant:

| State | ClothesShop centre | DoorMarker | Shop prompt | Purchase |
|---|---|---|---|---|
| Original `Town.tscn` | `(24, 0, 7)` | `(24, 0, 3.5)` | PASS | Sunny Day owned/worn |
| Shop instance moved `+5 m` on Z | `(24, 0, 12)` | `(24, 0, 8.5)` | PASS | Sunny Day owned/worn |

The same moved scene also passed `--selftest-resume` (21 assertions) and
`--selftest-restore` (54 assertions), proving an existing v1.2.1 save loads
without C# changes. The scene was then restored to `(24, 0, 7)` and the base
suite was re-run (279 passed).

## Verification summary

- Pure tests: **111 passed, 0 failed**
- Source runtime: **1166 passed, 0 failed** across 11 Godot phases
- Packaged Linux runtime: **1166 passed, 0 failed**, boot exit 0
- Canonical scene: 75 anchors, 159 top-level roots, 6 NPC routes, 2 free-roam
  traffic routes, 364 rally samples, 206.17 m circuit
- Visual captures: 18-shot P14 tour at 1280×720 and 1920×1080, plus a 10-shot
  P12 tour at 1280×720 on the real GPU
- Frame time (Radeon 880M/890M, 600 frames): canonical 16.68–17.08 ms vs v1.2.1
  baseline 16.70–16.71 ms; draw calls fall from 3394 to 2403 and primitives from
  66069 to 49142
- GitHub CI
  [run 42850939120](https://github.com/PhilipGB/patchwork-island/actions/runs/42850939120)
  passed the Linux job (including packaged `--selftest-restore`) and Windows
  build/pure-test job

Captured images and generated logs are kept outside version control under the
existing ignored `evidence/` and `references/` paths.

## Visual audit corrections (P18)

A full two-sided audit of every static sign and fixed district views found and
corrected mirrored sign text, unsupported floating sign panels, duplicate
BROKEN/BRIDGE WORKS assets, lamps inside buildings or sightlines, signs and a
bench standing in roads, blank rally-gate numbers, and build ghosts leaking into
normal play. The full before/after defect log is in `docs/VISUAL_QA.md`; the
ignored `evidence/v13_canonical_visual/` directory holds the 39 audit views and
the 1080p P12/P13/P14 tours. Source runtime after the correction: **1168
passed, 0 failed**.

### Follow-up at gameplay distance (V25–V35)

A second audit used ordinary player-height cameras and found defects that the
fixed inspection cameras had missed: bridge-sign clipping, open gable ends,
playground/road overlap, rally-gate numbers behind the beam, vehicle roof
self-shadow moiré, a floating Pip-car label, stray road patches and
junction-crossing markings, an over-wide crossing, misplaced lamps and
undersized world labels. All were corrected in the authored scenes; the full
table is in `docs/VISUAL_QA.md`. Vehicle visuals now use a contact shadow
because the supplied GLBs carry no face normals.
