# PR and milestone plan

## P10 — Music and sound effects

**Branch:** `p10-original-audio` | **PR:** `P10 — Original audio and real sound settings`

- Import all four OGG loops and 28 WAV SFX; include only assets actually used in production and account for the rest.
- Add AudioManager/AudioBus bindings using existing Godot nodes; retain existing Settings UI and persistent MusicEnabled/SfxEnabled. Separate Music/SFX/UI/Vehicles.
- Trigger appropriate sounds on movement, pickups, coin/shop/equip, mission, NPC (later milestone) and race/van. Use `assets/content/audio_cues.json` as guidance, not magic string lookups spread through the code.
- Ensure music transitions on title → roam → race → roam and festival. Prevent duplicate players, unpleasant clipping and loud overlapping effects.
- Pure tests: settings routing, cue IDs and asset manifest completeness; runtime: modal settings mute/re-enable, transitions, resource load. Real GUI/audio output test on Linux; do not claim audible success from dummy CI.
- Evidence: audio manifest, screenshot of audio buses, recording/testing notes, new exact-head green CI.

## P11 — Named villagers and NPC traffic

**Branch:** `p11-living-village` | **PR:** `P11 — Villagers and safe NPC traffic`

- Implement procedural low-poly NPC visual variants; import six supplied PNG portraits as dialog art and `npcs.proposal.json` as source of role/cue ideas.
- NPC: Idle → Walk verified patrol → Stop/greet/talk → Resume. Use deterministic fixed IDs and explicit `ProcessMode.Pausable`. Do not pass through static buildings. Interaction E and large name labels.
- Driver: bounded waypoints projected to verified roads, capped acceleration/turn rate, slow/yield near player, safety on intersections and no critical-station blocking. Create two actual active vehicles and a third rally rival spawned for M19 only.
- NPC driver AI can be simple scripted path-following; a general 3D navmesh is optional and should not be introduced if stable authored routes suffice.
- Separate free-roam driver movement from mission logic. No NPC can take player inventory or auto-complete jobs.
- Tests: presence/count, names, route bounds, pause, yield, no collision with gates/player, NPC talk transitions; visible screenshot and long-duration stability check.

## P12 — Visual QA and larger continuous rally

**Branch:** `p12-rally-polish` | **PR:** `P12 — World visual cleanup and expanded rally`

- Record reproducible visual defects. Compare 720p/1080p screenshots across scenes, low and high camera angle, clothing variants, vehicle driving and race checkpoints.
- Replace uneven or overlapping rally-road quads with stable joined/smoothed geometry, clear margins, collision and waypoints. Refer to `design/rally_track_schematic.png` and `assets/content/rally_course.proposal.json` **as proposals only**.
- Keep four-gate M05/M09 checkpoint IDs and elapsed progress stable, position/order visually obvious and timers still completable. Validate both races with representative low-skilled driving, not only teleport-based tests.
- Ensure NPC routes and dynamic vehicles align with the new road layout. No bridge obstructions; no visible teleport at route turnover.
- Performance budget: profile real Radeon 890M at 720p/1080p after changes, capturing mean/p95 frame time and scene/node counts, with baseline comparison. No fixed FPS claims on llvmpipe.
- Evidence: actual before/after images, rally screen recording or ordered screenshots, no Z-fighting, CI/runtime checks.

## P13 — Chapter 2, jobs M11–M15

**Branch:** `p13-next-chapter-1` | **PR:** `P13 — First five follow-on jobs`

- Expand mission catalogue from 10 → 20 IDs but only ship as implemented when active; do **not** show non-playable M16–M20 as available.
- Add authored content to **both** source/reference JSON files, preserving strict consistency checks and full mission integrity.
- Fix completion concepts: `OriginalChapterComplete` for M01–M10; `AllTwentyComplete` for M01–M20. Preserve `CelebrationSeen` meaning original first finale and add separate post-M20 marker when needed.
- Add job-board chapter tabs/grouping; original chapter retains its completed status. M11 unlocks after all original ten complete. M12/M13 then branch, etc.
- Implement genuine M11 NPC matching, M12 bakery order counting, M13 book shelving, M14 sign placement and M15 six-flower pattern. Add real stations, pickups, world feedback, hints, resets, and economy-neutral job supplies.
- Tests: fail-first domain guards, exact once rewards, wrong-item no loss, mission switching, seeded old-save upgrade, all five missions end-to-end, inactive chapter lock.
- Keep the existing completed-save upgrade functioning before proceeding.

## P14 — Jobs M16–M20 and festival

**Branch:** `p14-next-chapter-finale` | **PR:** `P14 — Delivery, mechanic, rally and village festival`

- M16 delivery using player van and three named NPCs; M17 separate 20-job-coin market budget; M18 vehicle repair staged sequence; M19 longer six-gate NPC-assisted rally; M20 invitations, tables and lanterns in correct order.
- Ensure M19 works with the revised circuit, does not require beating Pip, and cannot be obstructed by AI vehicles. Medal bonus based on authoritative elapsedMs and optional threshold verified by actual drive.
- M20 adds a distinct second celebration, audio cue/music, decorations, and an independent persistent `FestivalSeen` (or equivalent) without disturbing original `CelebrationSeen`.
- Job board handles a completed 20-job world and free roam. First M10 celebration must never repeat on an already-completed v1.1.0 save.
- Tests: sequential full chapter, alternate order M12/M13, save/reload between stages, money safety after zero-wallet clothing purchases, final exactly-once rewards and festival.

## P15 — v1.2.0 release verification

**Branch:** `p15-v1.2-release` | **PR:** `P15 — v1.2.0 release and evidence`

- Full clean-save M01–M20 walkthrough and v1.1.0 ten-completed-save migration acceptance. Verify M05/M09 after track redesign.
- Verify packaged music/SFX with a real audio device; failure to hear music is a blocker for this specific release.
- Real Godot GUI/real-GPU visual QA including NPCs, 3D road mesh, UI readability and collision.
- Run pure suite, all pre-existing/new runtime phases, editor import/headless boot, Linux export/boot, Windows compilation/export. Windows startup is NOT TESTED unless actually launched.
- Include build versions, licensed-source audit for any newly introduced external assets, SHA-256 archives, docs and release notes.
- Tag/publish v1.2.0 only at green, audited exact head, consistent with repository policies.
