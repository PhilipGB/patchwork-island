# Linux quality and architecture pass — 1.4.3-dev

Base: `a429791` (published v1.4.2). Branch:
`fix/linux-quality-and-architecture`. This is an unreleased source/PR quality
pass, not a new release. The current scope is 21 village missions plus three
city jobs; the ten-job original handoff remains historical.

## Changes

- Camera orbit requires the currently held right mouse button. Releasing it
  during pause cannot leave orbit latched after resuming.
- Grocery and furniture controllers are ordinary C# objects. Their widgets
  belong to the HUD scene tree; reopening shops no longer creates orphan Nodes.
- The HUD owns the E keycap once. Locked job pickups are hidden and inactive;
  available active-job pickups win focus over nearby incidental interactions.
  Consumed city crates cannot retain focus.
- A swept up/forward/down step permits walking onto the city's 0.29–0.33 m
  curbs. It checks headroom, landing height and walkable normal. One-metre walls
  and blocked headroom remain impassable.
- Six town wall materials suppress the distracting jagged eave shadows.
  Shadows remain on the ground and other objects. Increasing the global atlas
  and reducing its range did not resolve the artifact sufficiently; that
  experiment was reverted.
- Camera ray-query state and destination-arrow storage are reused; interaction
  distance comparisons use squared distance. HUD row caching updates icon and
  text state together. Hidden pickups stop processing.
- Audio shutdown cancels pending music fades. Assets and mix are unchanged;
  the product owner has accepted the sound.
- Live session facade plus composition controllers/presenter replace the
  monolithic gameplay orchestration. Main falls from 6,062 to 2,163 lines;
  MissionService from 1,128 to 503; CityActorPool from 661 to 227. See
  [architecture](ARCHITECTURE.md) for ownership, SOLID review and intentional
  retained boundaries.
- A pure contract checks authored mission quantities/prices/bonuses against
  executable rules for every village mission. CI now runs graphical input and
  orphan-lifetime regressions under Xvfb.

## Progression and graphical evidence

The graphical input driver completed **M01–M21 and C01–C03** from fresh user
state, using movement, steering, E, menu buttons and build-socket clicks. It
reads scene positions and progress for navigation/assertions, but does not
teleport, seed counters, grant money or call completion methods. Failed harness
attempts resumed the same normal save; this is not an uninterrupted human run.

Normal Save → exit → Continue checkpoints preserved the M15 garden pattern,
M18 partial repair parts and M20 invitations. The completed save contains all
24 mission IDs, **441 coins**, both celebrations, seven bridge pieces, six swing
pieces, three park pieces and four furniture pieces. A second post-refactor
replay using a graphical backend with rendering disabled also completed all
24 jobs and 441 coins. It is functional evidence only.

Graphical regression checks passed **17/0 at 1280×720 and 1920×1080**, including
four grounded curb approaches, tall-wall/headroom rejection, camera input and
60 shop opens/closes without orphan growth. Screenshots at both resolutions
and the full graphical progression are retained in
[the evidence directory](../evidence/linux_quality_2026-11-12/).

The first city-curb regression failed on the old movement code, reproducing a
player blocked at x=348.38 from the east. Other retained failures distinguish
harness problems (cursor coordinates, route/marker selection and aiming a van
through finished park furniture) from product defects. Headless orbit and
headless title-click attempts are not counted as graphical passes.

## Build and runtime verification

Environment: Linux x86_64, Godot `4.7.2.stable.mono.official.ed1daf0bf`, .NET SDK
10.0.115 building `net8.0`. Graphical input checks use Xvfb/Mesa llvmpipe; real
GPU measurements use the host display separately. All saves/configuration are
isolated from player state.

- Source build: zero warnings/errors after a forced restore with writable
  NuGet HTTP cache. An earlier cached NU1900 warning was environmental.
- Pure C# tests: **134 passed / 0 failed**.
- Graphical quality regressions: **17 passed / 0 failed**, each resolution.
- Full source runtime suite: **1300 passed / 0 failed** across twelve phases.
- Self-contained Linux release executable: boot **PASS**, all twelve runtime
  phases **1300 passed / 0 failed**, graphical quality **17 passed / 0 failed**
  at 720p. Export completed without logged errors/warnings. Package SHA-256 is
  retained in `evidence/linux_quality_2026-11-12/package-sha256.txt`; the local
  review build is `/tmp/patchwork-quality-export/PatchworkIsland.x86_64`.
  This executable contains the gameplay source at `b3295e8`; later commits
  consolidate documentation/evidence.

The initial export attempts failed inside .NET publish despite Godot returning
0: read-only home build logs, then .NET mutex storage under `/run/user/1000`.
Writable XDG data/config/cache/runtime directories corrected the environment.
The failed logs are retained; only the subsequently launched package is counted.

## Performance comparison

The sandbox has software rendering only. Approved host runs expose **AMD
Radeon 890M Graphics**, Mesa 26.0.8, OpenGL 4.6 Compatibility. This answers the
GPU-access question: physical GPU verification is possible through host runs.

Both revisions used the same completed-save fixture, engine, Debug C# build,
Dummy audio driver, disabled vsync and measurement code. Baseline is `a429791`
with only the retained benchmark instrumentation patch. Four stationary stages
warm for 1.4 seconds and sample 240 frame intervals each. Steady allocation is
`GC.GetAllocatedBytesForCurrentThread` delta per sampled frame, including the
measurement awaiter overhead. Draw/physics/actor counts remain in raw JSONL.
These are full game frame intervals using hardware rendering, **not GPU timestamp
queries**, and this is a shared desktop rather than a controlled benchmark rig.

The table uses the final sequential static-only set, after builds/exports and
other game tests had stopped. The earlier baseline 720p static sample overlapped
an export; it is retained but excluded here. Both sets are retained to make
variation visible; there is no claim of a uniform frame-rate improvement.

| Resolution | Stage | Base p50 / p95 ms | Current p50 / p95 ms | Base → current managed B/frame |
| --- | --- | ---: | ---: | ---: |
| 1280x720 | town-only | 3.32 / 7.61 | 3.30 / 9.81 | 836 → 308 |
| 1280x720 | city-approach | 4.21 / 12.70 | 3.74 / 14.28 | 1600 → 1050 |
| 1280x720 | city-street | 5.27 / 14.03 | 5.14 / 16.59 | 2644 → 2126 |
| 1280x720 | return-to-town | 3.11 / 7.35 | 3.70 / 13.98 | 817 → 325 |
| 1920x1080 | town-only | 4.39 / 9.41 | 5.55 / 19.08 | 871 → 428 |
| 1920x1080 | city-approach | 4.28 / 11.10 | 4.54 / 14.35 | 1584 → 1216 |
| 1920x1080 | city-street | 4.49 / 13.10 | 4.84 / 10.73 | 2532 → 2128 |
| 1920x1080 | return-to-town | 4.42 / 15.55 | 3.80 / 5.93 | 879 → 285 |

Steady measured allocation falls in every stage (roughly 368–594 B/frame in this
set). Frame-time results vary: the current 1080p town p95 is **19.08 ms**, and
its median is **1.15 ms above** baseline in this set; an earlier sequential run
was 3.46 vs 3.31 ms median. Therefore a consistent 60 fps/p95 target and the
+1 ms town regression budget are **not established**, rather than marked PASS.
No performance gain is inferred solely from source changes.

At 720p, a sustained physical van route from the east approach through multiple
city cells and back completed in both versions. It uses the existing diagnostic
autopilot, unlike the input-only mission walkthrough:

| Metric | Baseline | Current |
| --- | ---: | ---: |
| Duration | 87.56 s | 87.72 s |
| Samples | 16,619 | 19,146 |
| p50 / p95 | 3.98 / 14.79 ms | 3.52 / 11.93 ms |
| Frames >33 ms | 24 | 7 |
| Maximum frame | 94.44 ms | **241.74 ms** |
| Maximum in tracked LOD-change windows | 31.57 ms | 23.58 ms |

A LOD-change window is the frame where visible district/detail/HLOD counts change
plus the following three frames. This is useful correlation, not proof of
causation or coverage of every actor-pool event. The 242 ms outlier was outside
those windows and remains unattributed. Cold stationary transitions before
warmup also include large stalls: approximately 2–3 s at startup and up to
0.53 s on first city approach. They are retained in `transition_frame_ms_max`,
not hidden by reporting only warmed percentiles. **The game is not verified
hitch-free.** CPU/render-thread profiling of cold activation and rare outliers
is the next performance task before a release claim.

The hidden 30-minute logical-city advance costs **0.002–0.003 ms** in these
runs. The first attempted drive stopped on the diagnostic autopilot's river
safety guard at x=48.71; that failed log is retained. The corrected profiling
route stays east of the bridge. Normal input-driven bridge crossing and the
separate runtime traversal tests passed.

Raw device logs, JSONL, baseline instrumentation and matching hardware captures
are under [GPU evidence](../evidence/linux_quality_2026-11-12/gpu/).

## Remaining limits and next work

- Windows gameplay is outside this Linux pass. Existing Windows build/pure-test
  CI is retained, and does not imply a Windows playthrough.
- Human city composition/usability sign-off is not inferred from the driver.
  Product direction and sound acceptance have already been supplied by the
  owner; no repeated feedback request is a gate for this work.
- Immediate process exit after gameplay can still report native WAV/OGG audio
  playback resources at Godot teardown. Title boot/quit and the explicit
  stop/fade regression are clean. Raw verbose warnings are preserved; this
  report does not claim every gameplay shutdown is warning-free.
- Keep the current save/reward contracts and activity boundaries while adding
  features. Prioritise any measured transition hitch before expanding content.
  Main remains a sizeable coordinator; extract further responsibilities when
  behaviour requires it, instead of adding generic frameworks or splitting
  cohesive validation tables solely by line count.
