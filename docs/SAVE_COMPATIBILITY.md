# Save compatibility and mission expansion

## v1.2.0 contract

- `GameData.SchemaVersion` defaults to `SaveService.CurrentVersion == 3`.
- Schema 1 migrates in memory through schema 2 to schema 3. Schema 2 adds no
  required JSON fields for old saves; `FestivalSeen` defaults to false.
- Unsupported newer schemas are detected by `JsonDocument` before `GameData`
  deserialisation. Ordinary saves refuse to overwrite them. Explicit New Game
  replacement creates a preserved `newer-vN` copy first.
- `CompletedMissions`, `MissionProgress`, player/van snapshots, bridge/swing/
  park/furniture state, wallet, inventory, clothes ownership, equipped outfit,
  `CelebrationSeen` and `FestivalSeen` are persisted.

## Two completion milestones

- `MissionRules.OriginalChapterComplete(data)` = M01–M10.
  `CelebrationSeen` belongs only to this original finale.
- `MissionCompletionResult.AllTwentyComplete` /
  `FinaleRules.M20Complete(data)` = M01–M20. `FestivalSeen` belongs only to the
  M20 village festival.
- `Main.AllMissionsComplete()` intentionally means the original ten, so the M10
  celebration and celebration decorations keep their old meaning.
- The job board shows chapter one and chapter two; M11 unlocks only after all
  ten original jobs are complete. M12/M13 branch from M11, M14 from M12, M15
  from M13, M16 from both M14 and M15, then M17→M18→M19→M20.

## Migrations

- **1 → 2:** character customisation fields are supplied by `Normalise`
  (Boy + Everyday + no purchases); no progression field is touched.
- **2 → 3:** `FestivalSeen` is set false. The original file is kept as `.bak`
  and is rewritten at schema 3 by the next save.
- A completed v1.1.0 village with `CelebrationSeen=true`, all ten jobs, bridge,
  swing, park, furniture, outfits and coins loads successfully; M10 stays
  complete, M11 is available, and the original celebration does not repeat.
- Schema 4+ remains untouched by ordinary loads/saves and is never quarantined
  as corrupt.

## Validator coverage

Every new persisted counter or pickup is bounded and allow-listed:

- M11 card stock / deliveries, M12 orders, M13 books/shelves, M14 signs,
  M15 six pads.
- M16 loaded/carrying/delivered parcels, M17 basket/finalised, M18 parts and
  repair stages, M19 six-checkpoint clock, M20 invitation/table/lantern stock.
- M18 part pickup IDs are in the pickup allow-list.
- Completed missions must match their physical objective state (M11–M15,
  M16–M20, M03/M07/M08/M10).
- `CelebrationSeen` requires M01–M10; `FestivalSeen` requires M20.

## Acceptance fixtures

1. Fresh save: v1.2 starts at M01 and the job board shows both chapters.
2. Schema 2 M01–M10-completed save with celebration, clothes, coins,
   rearranged furniture and bridge: loads, M11 available, no repeat celebration.
3. Schema 1 historical save with missions in progress: migrates through 2→3.
4. Schema 4/99 incompatible files: refuse to load, are not quarantined, and
   ordinary saves leave every byte unchanged.
5. Explicit confirmed New Game replacement: newer-vN copy retained and retry
   after write failure.
6. In-progress M11/M16/M19 save and reload: counters, inventory, driving state,
   race time and NPC visuals remain coherent.
7. M20 complete and festival seen: one-time festival does not reopen after
   quit/relaunch; M01–M10 remain complete.
