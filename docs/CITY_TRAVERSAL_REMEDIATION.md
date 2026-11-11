# City traversal remediation — v1.4.0 follow-up

## Reported defects

- The delivery van could not drive into the city; a pavement acted as a wall.
- Continuous paved sidewalk/plaza slabs crossed the city grid and blocked the
  road network.
- NPC city cars got stuck on those raised paved areas.

## Evidence captured in the running scene

Before the fix, `--capture-city-defects` recorded the real physics result:

```text
CITY_DEFECT van_entry x=182.16 y=0.05 z=0.00 speed=0.00
```

The van stopped against the west sidewalk band at world x≈184–188. The same run
recorded city cars visually stopped on the raised blocks.

- `evidence/v14_city/defects/p24_01_city_entry_side.png`
- `evidence/v14_city/defects/p24_02_city_entry_overhead.png`
- `evidence/v14_city/defects/p24_03_van_stopped_at_entry.png`
- `evidence/v14_city/defects/p24_04_intersection_overhead.png`
- `evidence/v14_city/defects/p24_05_sidewalk_crossing_cars.png`
- `evidence/v14_city/defects/p24_06_stuck_city_car_close.png`
- `evidence/v14_city/defects/p24_07_city_grid_overview.png`
- `evidence/v14_city/defects/city-defects-before.log`

## Root causes

1. **Sidewalk strips were continuous for 350 m.** Every `NSSidewalk` and
   `EWSidewalk` mesh/collision box crossed all perpendicular roads at a height
   of 0.33 m. This made every intersection a curb wall and sealed the city
   entrance.
2. **Plazas were 68 m square on an 80 m road grid.** With 24 m roads, each plaza
   overlapped the carriageway by 6 m on every side at a height of 0.29 m.
3. **Building footprints were wider than the available block.** Four buildings
   at ±18 m with up to 30 m widths protruded up to 5 m into the roads.
4. **Lane dashes and crosswalks floated above the road.** They sat at y=0.25–0.3
   over a road surface at 0.08 m, casting detached shadows.
5. **The city road/approach seam had a 7 cm lip.** The approach was flush at
   0.01 m while the city base collision was at 0.08 m.

## Remediation implemented

### Geometry and collision

- Rebuilt every sidewalk into authored segments between road crossings. Each
  strip now has five segments (two 43 m end pieces and three 56 m middle
  pieces); the westmost strip has a 16 m gap at `z=0` for the city entrance.
  81 segment meshes and 81 matching collision shapes.
- Shrank all nine `BlockPlaza` meshes and the shared plaza collision shape from
  68 m to 56 m, so they stop at the road edge instead of covering the
  carriageway.
- Scaled all 36 building instances to `0.62` and copied that scale to their
  HLOD proxies, so no building footprint enters a road.
- Lowered road surfaces and the city base collision to 0.01–0.02 m to match the
  approach; lane dashes and crosswalk meshes now sit at 0.04–0.045 m.
- Moved benches, trees and the CITY JOBS board onto the 0.29 m plaza surface;
  raised city-job marker roots to the same surface.
- Restored traffic-signal props to the 0.3 m sidewalk level after the road
  lowering pass.

### Simulation

- City cars keep their authored routes and collision, but the intersection
  curbs that blocked them are gone. They now cross the full grid.
- Player, van and cars no longer have to climb a raised slab at the entrance.

## Verification

- New pure contract checks:
  - every sidewalk segment lies outside every perpendicular 24 m road interval;
  - no plaza overlaps a road;
  - at least 80 authored sidewalk segments exist.
- New runtime checks:
  - the van crosses the former west-sidewalk barrier (`x > 188`);
  - every near city car is on the road grid;
  - at least two near cars keep moving through intersections;
  - the player walks from the road onto the central plaza.
- `--selftest-city` now has **111 passed, 0 failed**.
- Pure tests: **132 passed, 0 failed**.

## After-fix evidence

- `evidence/v14_city/defects-fixed/p24_01_city_entry_side.png`
- `evidence/v14_city/defects-fixed/p24_02_city_entry_overhead.png`
- `evidence/v14_city/defects-fixed/p24_03_van_stopped_at_entry.png`
- `evidence/v14_city/defects-fixed/p24_04_intersection_overhead.png`
- `evidence/v14_city/defects-fixed/p24_05_sidewalk_crossing_cars.png`
- `evidence/v14_city/defects-fixed/p24_06_stuck_city_car_close.png`
- `evidence/v14_city/defects-fixed/p24_07_city_grid_overview.png`
- `evidence/v14_city/defects-fixed/city-defects-after.log`

The after-fix van log reaches `x=189.35` on the city road and the car log shows
cars progressing along the inner and outer routes.

## Review findings addressed

### Full city traversal

`CityRuntimeSelfTest.CityTraversalChecks` now drives the van with a real
sustained-throttle autopilot from the approach into the city, through the first
intersection, around the central block and back to the approach. It asserts
entrance, first intersection, central block and return separately, rather than
only passing the old barrier.

### Traffic policy

City NPC traffic is intentionally **cooperative/decorative**, matching the
town's traffic design. Cars collide with static geometry but not with the player
or van, and they yield when the player/van is directly ahead. This avoids
trapping a child in an intersection. The policy is explicit in
`CityVehicleVisual` and covered by static yield tests plus a controlled
stop/stream test.

### Visual/logical reconciliation

Near vehicles keep a bounded visual lag (18 m maximum) relative to the compact
logical schedule. A yielding car is never snapped forward through the player;
the lag is stored and reused when the actor streams out and back in. Mid-tier
actors follow the logical pose directly, and near actors gain a small bounded
catch-up speed when they resume. Tests hold a car, stream it out to town, return
it, and verify the bounded lag and repeat yield.

### Collision activation distance

Distant city visibility is still enabled at 335 m for HLOD/streaming, but
physical city-ground collision now activates only within 185 m of the city
centre. At the town centre the district can remain visible while
`ActiveDistrictCollisionBodies == 0`, so town movement cannot interact with
unreachable city paving.

### Traffic test count

The movement assertion now requires **at least two near cars present** and
**at least two moving**; it can no longer pass vacuously with zero cars.

## Remaining visual follow-ups (not blockers)

- Directional shadows in the city are still coarse at this shadow distance;
  the low-poly style makes the bands visible. This is a rendering-quality
  follow-up, not a collision issue.
- Mid-tier car proxies use a fixed 0.35 m visual height; near-tier cars use
  physics. A future pass can align the proxy height to the road surface.
- The city still uses the supplied placeholder architecture; the remediation
  only makes the authored grid traversable and visually consistent.

## Release status

The existing GitHub **v1.4.0** pre-release/tag points to commit `21b226e`
(before this remediation). Do not move a published tag. The grid fix and review follow-up are on the
`v1.4.0-city-expansion` branch in PR #32; a new **v1.4.1** tag and pre-release
should be used if this is published.
