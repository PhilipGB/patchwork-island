# City Expansion v1.4.0 — compatibility notes

Release version: **1.4.0**. Save schema: **4**.

The city expansion is additive. The village remains the same game: every
M01–M20 mission, the M10 celebration, the M20 festival, practice racing, town
traffic, NPCs, bridge/swing/park/furniture state and the shared coin wallet keep
their existing behaviour. The city is a second, independent play space reached
only after the new M21 gate job.

## Town progression

- M21 **Roadworks Rescue** is appended to the existing catalogue with all twenty
  village jobs as prerequisites. It does not replace, reorder or renumber
  M01–M20.
- `MISSIONS.json`, `data/missions.json` and the runtime catalogue contain the
  same 21 records. Mission integrity now requires exactly 21.
- `CelebrationSeen` still means the original M01–M10 finale and `FestivalSeen`
  still means the M16–M20 finale. M21 has its own completion fields and does not
  re-trigger either modal.
- The gate is a physical collider at the east end of the island. It opens only
  after M21 completion, and the open state is persisted. A save that claims an
  open road before M21 is rejected.
- The city roadworks reward is recorded separately from M21 progress and is
  credited exactly once.

## City jobs

- C01, C02 and C03 live in `data/city_missions.json` with their own ids,
  prerequisites, progress counters and completion list. They are not mixed into
  `CompletedMissions` or the village unlock graph.
- The city board is a separate interaction in the central plaza. The village
  job board does not list city jobs, and the city board does not list village
  jobs.
- City rewards use the same `Coins` wallet. Rewards are credited exactly once
  and are validated against persisted progress.
- City job progress is typed and bounded. Unknown ids, unknown counters,
  impossible counts, and jobs marked complete without their objective progress
  make the save invalid instead of silently loading.

## Save schema 4

Schema 3 saves migrate forward without changing town progression:

| Field | Default for a schema-3 save |
|---|---|
| `ActiveCityMission` | empty |
| `CompletedCityMissions` | empty list |
| `CityMissionProgress` | empty map |
| `CityRoadworksCompleted` | false |
| `CityRoadworksRewardClaimed` | false |
| `CityRoadOpen` | false |
| `CityVisited` | false |
| `CityLogicalSeconds` | 0 |
| `CitySeed` | `20261111` when absent/zero |

The migrated save is only rewritten at schema 4 when the game next saves.
Loading does not modify the file on disk.

The player and van position bounds now include the whole city approach and the
city district (`CityLayout`), in addition to the village. Positions outside both
areas, non-finite positions, and a driving save without both snapshots remain
rejected.

Schema 5 and newer are still unsupported-newer: they are left untouched,
Continue is unavailable, and an explicit New Game first preserves a
`save.json.newer-vN-*` copy. Corrupt main saves still recover the backup, and a
bad main save never replaces a good backup.

## Saves that remain valid

- A completed M01–M20 village with no city fields (schema 3) loads with a closed
  road, an empty city board and M21 available.
- A save with M21 active, partial supplies or a partially solved crew task
  restores those counters.
- A completed M21 with the road open reloads with the collider disabled and the
  barrier hidden. The operation is idempotent, so repeated loads cannot award
  the roadworks reward twice.
- A save in the city restores the player or van in the city and resumes the
  city's logical clock from `CityLogicalSeconds`.
- A mid-drive save on the approach or in the city restores driving state and
  both player/van snapshots.

## Compatibility evidence

- Pure tests: schema 3 → 4 migration, town progression preservation, city job
  exactly-once rewards, invalid city state rejection, city simulation
  determinism, scene contracts and mid-drive save/reload.
- Runtime `--selftest-city`: real scene reloads for roadworks completion,
  gate/collider closure, city job flow, return trips, mid-drive saves and LOD /
  actor cleanup.
- Existing `--selftest-*` phases and the packaged Linux boot remain part of the
  release gate; their results are recorded in `PROGRESS.md`.

## Known non-goals

- City jobs are English-language reading/counting tasks like the village jobs;
  no new localisation layer was added.
- The city uses authored placeholder buildings, not final hand-modelled art.
- A city job board entry is available as soon as the road is open; individual
  city jobs do not require an additional village mission beyond M21.

## City traffic policy

City NPC vehicles are intentionally cooperative/decorative. They collide with
authored static geometry but not with the player or van, and they yield when the
player is directly ahead. This mirrors the town traffic rule and avoids trapping
a child in a city intersection. The pure and runtime suite covers the yield
rule and the near/streamed actor reconciliation.

## City collision activation

City visibility and physical collision are separate. The district and its HLOD
proxies become visible within 335 m of the centre, but shared city-ground
collision follows the authored square footprint (local ±180 m), not a radius.
This keeps the whole outer carriageway — including corners ~187 m from the
centre — supported, while the town centre remains outside the footprint and
cannot interact with unreachable city paving.
