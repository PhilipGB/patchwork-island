# City expansion performance — measured v1.4.0

Measured on the development machine, not a synthetic estimate. Raw data:
`evidence/city_perf_1280x720.jsonl` and
`evidence/city_perf_1920x1080.jsonl`.

## Environment and method

- CPU/GPU: AMD Radeon 890M Graphics (radeonsi, Mesa 26.0.8, OpenGL 4.6),
  Linux x86_64.
- Godot 4.7.2 stable mono; project Compatibility renderer.
- `--city-perf` disables vsync, warms each stage for 1.4 s, then measures 240
  consecutive `ProcessFrame` deltas. `p50`, `p95`, draw calls, primitives,
  active 3D physics bodies and object nodes are recorded per stage.
- `--perf-1080` switches the window to 1920×1080. The probe ran on the real
  display/GPU, not under llvmpipe.
- Because vsync is disabled, these values are **not** directly comparable with
  the earlier 60 fps-capped ~16.7 ms release probes. They are comparable between
  the four stages in each run.

## Results

| Resolution | Stage | p50 ms | p95 ms | Draw calls | Primitives | Active physics | City detail | HLOD | Actors (near/mid) |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|
| 1280×720 | town-only | 1.752 | 2.909 | 1343 | 27,774 | 10 | 0 | 0 | 0 / 0 |
| 1280×720 | city approach | 2.132 | 3.246 | 611 | 8,900 | 10 | 0 | 16 | 0 / 20 |
| 1280×720 | city street | 2.875 | 4.509 | 1255 | 18,292 | 19 | 20 | 16 | 19 / 17 |
| 1280×720 | return to town | 1.750 | 3.354 | 1333 | 27,048 | 10 | 0 | 0 | 0 / 0 |
| 1920×1080 | town-only | 2.978 | 4.864 | 1343 | 27,798 | 10 | 0 | 0 | 0 / 0 |
| 1920×1080 | city approach | 2.474 | 4.397 | 611 | 8,704 | 10 | 0 | 16 | 0 / 21 |
| 1920×1080 | city street | 3.360 | 5.601 | 1255 | 18,438 | 24 | 20 | 16 | 19 / 17 |
| 1920×1080 | return to town | 3.059 | 4.944 | 1333 | 27,102 | 10 | 0 | 0 | 0 / 0 |

Stage positions:

- **town-only** — player at the player house (`-38, 0.3, 9.5`) facing north;
  city hidden.
- **city approach** — player at world `(120, 0.4, 0)` facing east; city approach
  visible, 16 HLOD buildings and 20–21 mid-tier actors.
- **city street** — city-local `(0, 18)` facing north; 20 detailed buildings,
  16 HLOD buildings, 36 actors (19 near, 17 mid).
- **return to town** — the same player-house view after leaving the city.

## Logical city while hidden

The logical population is 24 vehicles and 42 pedestrians. It is a deterministic
function of `(seed, logical time, authored route)`, so a hidden city does not
tick agent nodes. A fresh simulation advanced by 1,800 seconds in **0.002–0.003 ms**
(`logical_catchup_30min_ms`; 0.003 ms in the 720p run, 0.002 ms in 1080p) with zero active actor nodes. There is no
per-agent catch-up loop and no frame hitch proportional to absence time.

## Tier behaviour observed

- Town-only and return: city district invisible, all nine cells at hidden tier,
  zero city actors and zero city physics bodies.
- Approach: 16 HLOD proxies, no detailed buildings, 20–21 mid-tier visual actors.
- Street: 20 detailed buildings and 16 HLOD proxies; 19 near/17 mid actors out
  of 66 logical agents. Far actors stay logical-only.
- The tier selector uses an 18 m hysteresis margin per cell, so a camera hovering
  on a tier boundary does not rebuild meshes every frame.

## Targets

| Target | Result |
|---|---|
| 60 fps / 16.7 ms on Radeon 890M at 720p | **PASS** in all measured stages (worst p95 4.51 ms, vsync off) |
| ≤ 0.5 ms median CPU per distant logical tick | **PASS**: 30-minute advance measured at 0.002–0.003 ms |
| No > 33 ms hitch during normal city cell transitions | No hitch was observed during capture/probe runs; not separately instrumented |
| Town cost within +1.0 ms of the unchanged baseline when city is hidden | **NOT INDEPENDENTLY BASELINE-COMPARED**: the town-only stage has zero city nodes/actors/colliders, but a same-method probe was not run on the v1.3.0 build |
| Publish p50/p95, draw calls, primitives, tier counts, physics | **PASS** (table above) |

## Caveats

- Real integrated GPU, but a single Linux/Mesa machine and a short 240-frame
  sample. Windows and other GPUs are **NOT TESTED**.
- Primitives are reported by Godot's frame monitor; the table uses the probe's
  recorded values, not a triangle recount from the meshes.
- Run-to-run variation is visible between the first and final probes on this
  machine. The final numbers above are from the final tree, with the same probe
  and warm-up.
- The city street is intentionally heavier than the approach: the tier system
  trades detail for distance, and the measured p95 stays below 5.7 ms at both
  resolutions.
