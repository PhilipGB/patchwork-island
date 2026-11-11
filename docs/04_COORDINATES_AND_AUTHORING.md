# Editor asset coordinate guide — v1.4.0

The city is authored in `scenes/city/` and loaded by `CityWorldAdapter`. There is
no runtime city generator. All coordinates in the city scenes are **local to
`CityDistrict`** unless noted; the district is placed at world
`(320, 0, 0)` by `CityLayout.OriginX`.

## District

- Local footprint: `-180..+180` on X and Z (360 × 360 m stylised scale).
- Road axes: `-120`, `-40`, `+40`, `+120` on each axis; 24 m main carriageway,
  14 m joint lanes.
- Nine 68 m plaza cells centred at `(-80, -80)` … `(+80, +80)`.
- Four building plots per cell at local offsets `(-18, -18)`, `(+18, -18)`,
  `(-18, +18)`, `(+18, +18)`.
- 36 building instances (12 archetypes, three variants each) and 36 matching
  HLOD proxies, one per building.
- `CityDistrict.tscn` owns ground, sidewalk and building collision, the district
  roads, the city job board and all gameplay markers.
- `CityJobs.tscn` owns the 15 city-job markers and their authored signs/props.
- `CityLife.tscn` is the authored city-life visual set; traffic and pedestrians
  are simulated by `CitySimulation` and drawn by `CityActorPool`.

## Town/city join

- Approach scene: `scenes/city/TownCityApproach.tscn`, parented under the
  district root so its local coordinates become world coordinates.
- World span: roughly `x = 48..188`; the approach road's surface is flush with
  the island/rally ground. The bridge deck is the 50 m span at world
  `x ≈ 94..144`; the east approach reaches the city carriageway at `x = 188`.
- The west 47 m of the approach is a road crossing the rally meadow. It is
  deliberately flush (top ≈ 0.01 m) and below the rally surface (top ≈ 0.022 m)
  so the rally track stays visually and physically continuous. It must never be
  raised into a slab again.
- `CityArrival` local `(-159, 0.4, 0)` → world `(161, 0.4, 0)`.
- `CityBridgeApproach` local `(-160, 0.4, 0)` → world `(160, 0.4, 0)`.
- `CityReturnRoad` local `(-175, 0.4, 0)` → world `(145, 0.4, 0)`.
- City west carriageway edge: world `x = 188`.

## Roadworks gate

- Authored scene: `scenes/city/TownCityRoadworks.tscn`, placed at world
  `(96, 0, 0)` by `TownCityRoadworks.WorldX`. The gate sits on the bridge apron
  just east of the island boundary (`x = 94.5`), clear of the rally circuit's
  eastern edge.
- Physical barrier: `GateCollision` with a 16.6 m shape spanning the 16 m
  terrain opening; the extra width prevents walking or driving around the posts.
- Visual barrier and both fences hide when the road opens; the collider layer is
  cleared and all its shapes are disabled in the same idempotent call.
- `RoadworksInteraction` is 2.6 m west of the gate at local `(-2.6, 0.2, 4.8)`
  (world `93.4, 0.2, 4.8`). `TownSideStop` / `CitySideStop` are at -6 m and
  +14 m.
- The notice board stands on the south verge at local `(-2.5, 2.55, 8.5)`,
  rotated `-1.45` rad so drivers approaching from town read `CITY ROAD CLOSED`
  or `ROAD OPEN` head-on. The crew works on the same south verge at
  `z ≈ 8.9..10`.
- Approach road surfaces and their collision are flush with the island/rally
  ground (top ≈ 0.02 m). The rally surface therefore wins the overlap and the
  city road cannot introduce a step across the racing line.

## City job board and markers

- `CityJobBoard` instance: local `(0, 0.4, 22)`, rotated 180° about Y so its
  notice face points at the plaza (`-Z` world).
- `CityJobBoardAnchor` gameplay marker: local `(0, 0.4, 19)` (one metre in front
  of the interaction point).
- Board interaction marker inside the authored board scene: local `(0, 0, 2)`,
  which becomes world/local `(0, 0.4, 20)` after the board's 180° rotation.
- C01 depot: local `(-78, 0.2, -58)`. Stops 1/2/3: `(-80, 0.2, -80)`,
  `(0, 0.2, -80)`, `(80, 0.2, -80)`.
- C02 crates: local `(79..81, 0.6, 76..82)`; stall at `(80, 0.2, 84.6)`.
- C03 cabinets: local `(0, 0.2, 0)`, `(0, 0.2, 80)` and `(80, 0.2, -80)`.

## Routes

- Four authored vehicle routes and eleven pedestrian routes are read from
  `CityLife.tscn` by `CitySimulation`. They are final runtime routes, not
  placeholders.
- Each route is a closed loop of `Marker3D` waypoints. Changing a waypoint in
  the editor changes the deterministic logical paths and the visible actors; the
  scene-contract test fails if a route has fewer than three points or leaves the
  district.
- Vehicles use the road axes; pedestrians use sidewalks and crossings. The
  simulation does not perform pathfinding at runtime.

## Authoring rules

- Edit the `.tscn` files, not C# coordinate constants. The adapter validates the
  required markers and reports a named error if one is missing or out of bounds.
- Keep the gameplay id in `metadata/gameplay_id` when moving or renaming a
  marker. City job and gate lookups use these ids.
- Do not add a procedural fallback: missing city content is a hard failure, not
  a silently generated replacement.
- Preview roads/crosswalks overlap at intersections by design. Collision is
  authored as merged ground/sidewalk shapes, not per-visual-slab boxes.
- HLOD proxies must remain one per building and keep the building footprint.
  `CityWorldAdapter` toggles authored detail/HLOD roots; it does not build them.
