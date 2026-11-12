# Verification / acceptance checklist — city expansion v1.4.0

## Asset smoke checks
- [x] Import project with Godot 4.7.2 .NET and no missing scene resources.
- [x] Confirm `CityDistrict.tscn` contains nine city blocks, four north/south +
  four east/west road axes, 36 visible building instances and an
  interactive-board visual (scene-contract test + `--selftest-city`).
- [x] Confirm all 12 building archetypes have a visible front door and a
  `DoorMarker` with a stable binding ID; 36 instances use those archetypes.
- [x] Confirm 36 separate HLOD proxies are present (one per building instance);
  runtime tier swapping is implemented and covered by `--selftest-city`.
- [x] Confirm the roadworks barrier has a clear visual state and a working
  physical collider; opening hides it and disables collision atomically.

## Functional acceptance
- [x] Current mandatory town mission, celebration and bonus-racing semantics
  unchanged (M01–M20 graph, `CelebrationSeen`, `FestivalSeen`, practice race).
- [x] Roadworks final job hidden/locked until every required town mission is
  complete (M21 prerequisites are M01–M20).
- [x] Road closed by a physical collider and readable signage until mission
  completion.
- [x] Roadworks supplies (3 cones, 2 panels, 1 toolbox) plus the 3 + 2 + 1
  counting/order task are required, and the 25-coin reward is credited once.
- [x] Barrier visually removed and collider disabled atomically; persists after
  save/reload and is idempotent across repeated loads.
- [x] City reachable and returnable by driving the van; the full-speed runtime
  test crosses the gate in both directions, and the approach has no seam trap
  or van-loss path.
- [x] Sidewalk segments never cross a carriageway, plazas/buildings stay out of
  the road grid, and the van and NPC cars traverse the city grid.
- [x] The player can step from the road onto the central plaza to reach CITY
  JOBS.
- [x] Independent CityJobs board with C01, C02 and C03 playable end-to-end.
- [x] City mission progress, rewards and completed world changes survive
  save/reload.
- [x] Simulated pedestrians and traffic advance while the city scene is not
  loaded (logical clock, deterministic pose function).
- [x] 30-minute offscreen advance is bounded and deterministic; measured
  at 0.002–0.003 ms with no per-agent catch-up loop.
- [x] No agents duplicate or teleport visibly when city cells activate; actor
  pools phase in near/mid actors at their logical poses.
- [x] Walking pedestrians use sidewalks/crossings; vehicles use authored lanes
  and stops and yield to the player (layer 4, player mask includes it).
- [x] Near/mid/far LOD tiers are observable; cell hysteresis prevents rapid
  thrashing.
- [x] No hidden offscreen per-frame Godot physics, transforms or character
  animation for the 66 logical city agents; only the logical clock advances
  while the city is hidden.
- [x] Frame metrics captured for town-only, city approach, city street and
  return at 720p and 1080p (`evidence/city_perf_*.jsonl`,
  `docs/05_CITY_PERFORMANCE.md`).
- [x] Existing save migrations, newer-schema protection and manual save failure
  feedback still verified (132 pure tests + all runtime phases).
- [x] Existing .NET suite, runtime phases and packaged Linux boot pass locally
  (132 pure; 1300 runtime assertions across 12 source phases and 1300 in a
  packaged build). Exact-head CI for the current PR head passes
  `build-test` and `windows-tests` (see the
  [PR checks](https://github.com/PhilipGB/patchwork-island/pull/32/checks)).
  See `PROGRESS.md` for exact counts.

## Performance targets
- [x] 60 fps / 16.7 ms on Radeon 890M at 720p: measured p95 ≤ 5.7 ms with vsync
  disabled in all four stages. **PASS**.
- [ ] No more than +1.0 ms median frame time in town compared with the unchanged
  baseline when the distant city is logical-only: the town-only stage has zero
  city nodes, actors and colliders, but a same-method v1.3.0 baseline probe was
  not run. **NOT INDEPENDENTLY BASELINE-COMPARED**.
- [x] ≤ 0.5 ms median CPU per distant logical tick: 30-minute advance measured
  at 0.002–0.003 ms. **PASS**.
- [ ] Avoid > 33 ms hitch during normal cell streaming/HLOD transitions: no
  hitch was observed in captures or the runtime probe, but transition timing is
  not separately instrumented. **NOT MEASURED**.
- [x] Publish p50/p95 frame times, draw calls, primitives, visible tier counts
  and active physics bodies. **PASS**.

## Explicitly not tested
- One continuous human M01–M21 → C01–C03 sitting.
- Human visual/composition sign-off of the city and its screenshots.
- Windows startup.
- Real-GPU performance outside this single Linux/Mesa Radeon 890M machine.
