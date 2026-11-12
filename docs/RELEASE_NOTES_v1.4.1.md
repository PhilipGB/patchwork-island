# Patchwork Island v1.4.1 — City Expansion fixes (test build)

This is a **pre-release test build** of the city expansion, based on the merged
asset-driven town (`main` at `318c74f`). The previously published **v1.4.0** tag
and release are unchanged.

## Included since v1.4.0

- **M21 Roadworks Rescue delivery fix** — at 6/6 supplies, the objective moves
  to the gate and E delivers them; the job completes exactly once.
- **Drivable city grid** — split sidewalk segments, a 16 m entrance gap, 56 m
  plazas, scaled buildings/HLOD and flush road markings. Buildings and plazas no
  longer intrude into the carriageway.
- **Full traversal regression** — sustained-throttle autopilot enters the city,
  reaches the first intersection and central block, then returns to the approach.
- **Cooperative city traffic** — NPC cars collide with static geometry only and
  yield to the player/van, so traffic cannot trap a child. A controlled yield
  and stream test covers the policy.
- **Bounded traffic reconciliation** — near visual cars stay within 18 m of the
  logical schedule, never snap forward through a yielding player, and reuse the
  bounded lag after streaming. A five-second hold test exercises the cap.
- **Scoped city collision** — the district stays visible at 335 m for
  streaming/HLOD, while physical ground collision uses the authored square
  footprint (local ±180 m). All outer carriageway corners are covered; the town
  centre remains outside the footprint.
- **Outer-loop regression** — a real 1 km drive around the outer lane verifies
  ground collision stays active and all four outer corners are visited.

## Verification

- 132 pure tests pass.
- 1,297 source runtime assertions pass across all 12 phases.
- 126 city runtime assertions pass, including full traversal, traffic policy,
  long-hold reconciliation, collision scope and the outer loop.
- The packaged Linux build boots and passes all 12 phases with 1,297 assertions.
- Windows builds and passes the pure/runtime CI job, but **Windows startup was
  not tested** in this environment.

## Known gaps

- No continuous human M01–M21 → C01–C03 playthrough and no human visual sign-off.
- Windows gameplay startup has not been independently verified.
- City directional shadows are coarse at this shadow distance.
- Transition-hitch measurements and a same-method v1.3.0 baseline were not
  captured.

## Controls

WASD/arrows move, Space jump, E interact, J jobs, H hint, Esc/P pause. Near the
van press E to drive; R resets it. The city unlocks after the 20 village jobs and
M21 Roadworks Rescue.

## SHA-256

```text
7305b2d29f93c6d20e3c7ba4478dbab466842166f0fc7e461a668703cf5a0f54  linux/PatchworkIsland.x86_64
a05d172897c11a35ee70a83f7efa72df2019efed68dcf12a98cd42d21c3e2a33  windows/PatchworkIsland.exe
297050bda3254bc3af772d864410161d9cdf2fa6e11f8a6848214738d417b467  PatchworkIsland-1.4.1-Linux-x86_64.zip
5f33f816214ade69136e0611a689c61fc1f4d11a02f07861e8d0118134391aae  PatchworkIsland-1.4.1-Windows-x86_64.zip
```
