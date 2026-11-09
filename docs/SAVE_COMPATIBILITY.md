# Save compatibility and mission expansion (critical)

## Verified v1.1.0 starting contract

- `GameData.SchemaVersion` defaults to `SaveService.CurrentVersion == 2`.
- A schema 1 save migrates in memory to schema 2; previous save backup remains on rewrite.
- Unsupported newer schemas are detected by `JsonDocument` before `GameData` deserialisation, and ordinary saves refuse to overwrite them. Explicit New Game replacement creates a preserved newer-vN copy.
- `GameData` includes `CompletedMissions`, `MissionProgress`, player/van snapshots, furniture, park/swing/bridge placements, coin wallet, clothes ownership and equipped outfit, `CelebrationSeen`.

## Key compatibility hazard in v1.2

The current completed-save logic assumes **exactly 10 missions**:

- `MissionIntegrity.RequiredMissionIds` enumerates M01–M10 and validates exact catalogue count.
- `MissionRules.IsObjectiveComplete` has M01–M10 switch cases and returns false for new IDs.
- `MissionService.CompleteActive` computes `AllComplete` using `Catalog.Missions.Count`.
- `Main.AllMissionsComplete()` drives original world decorations and M10 celebration.
- `SaveValidation.ValidateMissions` rejects `CelebrationSeen=true` unless **all catalogue missions** completed. If the catalogue suddenly grows to 20, previously valid 10/10 completed saves with celebration would become invalid.
- Some HUD and runtime tests assume completion means M10, ten jobs or fixed progress rows. Inspect all before changing.

**Required fix:** Define two separate milestones, e.g., `OriginalChapterComplete(data)` = all M01–M10, and `SecondChapterComplete(data)` = all M01–M20. Continue to interpret persisted `CelebrationSeen` as the original M10 celebration. Do NOT rewrite old saves or clear that flag. Add a distinct persistent `FestivalSeen` / `ChapterTwoCelebrated` only for M20 if required.

## Schema strategy

Use schema **2** if no new persisted fields are necessary and existing dicts/sets can safely store new mission counters. If new state is necessary (festival seen, vehicle repairs, NPC progression, placement data), upgrade to **schema 3** with explicit 2→3 migration, preserving 1→2→3. The old save backup should remain valid. Version check must remain before type deserialisation so schema 4+ is untouched even when its JSON fields are incompatible with the current typed DTO.

Every new persisted list/counter requires a save validator: bounded counts, known IDs, no duplicates, mission prerequisite integrity, exactly-once reward, finite transforms, no physical state conflicts. Existing legitimate complete saves must remain loadable. A saved v1.1.0 village with all ten jobs done should show the original celebration as already complete and M11 ready to start; the remaining ten should be unstarted with no missing required fields.

## Acceptance fixtures (create locally, never commit a real child's save)

1. Fresh schema-2 save: v1.2 loads and plays original M01.
2. Schema-2 M01–M10-completed save with `CelebrationSeen=true`, bridge, swing, park, rearranged furniture, clothes purchases and coins: v1.2 loads; M11 available; M10 remains complete; no repeated original celebration; player appearance correct.
3. Schema-1 historical save with missions in progress: migrates through supported versions without losing counters.
4. Schema 4 and 99 saves with intentionally incompatible typed fields: refuse to load; do not quarantine; ordinary autosave refuses; exact bytes unchanged.
5. Explicit confirmed New Game replacement of newer save: backup newer-vN retained and retries work after write failure.
6. In-progress M11/M16/M19 save and reload: objective counters, inventory, driving state, race time and NPC visuals remain coherent.
7. M20 complete and festival seen: stays complete and one-time after quit and relaunch; M01–M10 still completed.

Do not increase the required mission list to 20 until all authored content is present and code/tests support every added mission. Intermediate PRs may use a deliberate feature flag or milestones grouping but must never make existing valid saves unloadable.
