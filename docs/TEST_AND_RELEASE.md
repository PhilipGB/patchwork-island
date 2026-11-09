# Test, gameplay and release acceptance

## Baseline and exact-head checks

Record actual baseline on the live repository. v1.1.0 reported 78 pure tests, 103 initial + 19 resume + 101 recovery runtime assertions. Preserve them and add tests for every new core rule. Use the repository's exact CI CLI phases; commands below are examples subject to existing build scripts:

```bash
dotnet build PatchworkIsland.sln
dotnet run --project tests/PatchworkIsland.Tests.csproj
godot --headless --path . --editor --quit
godot --headless --path . --quit-after 120
godot --headless --path . -- --selftest
godot --headless --path . -- --selftest-resume
godot --headless --path . -- --selftest-recovery
```

## Automated tests that must be added

**Audio:** each cue resolves to a supported, included and non-empty asset, audio buses present, settings affect correct bus, music transitions, 3D attenuation/spam limit, exported binary contains assets. CI may structurally verify audio using dummy output; it cannot prove the music is audible.

**NPC/traffic:** correct stable IDs and count, route points project to walkable areas, state machine pauses with game, conversations don't double-complete missions, cars slow/yield before player/critical stations, drivers never update player race counters, traffic never enters inaccessible terrain. A full time-forward stability run should record no collisions/hangs.

**Rally:** continuous mesh bounds and collision, non-overlapping road surface, track gates in order, restart/reload checkpoint progress, original M05/M09 optional bonus times and no hard limits, no progress corruption when new layout loads an old partially-completed M05/M09. M19 six unique gates, optional bonus authoritative time, rival cannot obstruct.

**M11–M20:** each has positive and negative domain tests, wrong action no item loss, objective counts cannot overflow, duplicate completion fails, rewards exactly once, switching jobs and save/reload retain progress, no dependence on player's personal wallet for mission supplies. Test complete progression M11, M12/M13 in either order, M14/M15 branches, M16–M20 sequence.

**Upgrade:** schema1→current, schema2→current, unsupported-newer safe refusal, 10/10-completed with previous celebration still valid and M11 available, P9 character/outfit purchases preserved.

## Manual/graphical acceptance (do not claim from headless alone)

- In-game Linux audio check with speakers/headphones: hear all four tracks in their appropriate contexts, individual UI/SFX, NPC reactions, motor loops. Music/SFX settings toggle reliably and persist across restart. No loud clipping or endlessly stacked player instances.
- At 1280×720 and 1920×1080: title, job board, villagers and NPC dialogue, clothing/shop modal, rally road joins/edges, checkpoint sightlines, festival finale. Capture actual before/after screenshots for glitch fixes; no clipping or text overflow.
- Walk and drive through central village and round expanded rally; complete M05 and M09 with forgiving handling and new geometry; complete M19 alongside driver; verify NPCs do not trap car or player.
- Complete a **fresh** M01–M20 game, not just seeded counters, with save/reload during three new jobs; finish both celebrations exactly once.
- Load v1.1.0 valid completed village, verify prior possessions and achievements, then complete at least M11 and a full M11–M20 chapter in a disposable save.
- Test on actual GPU (not only Mesa llvmpipe). Record hardware, resolution, rendering backend, frame median and p95, and compare to v1.1.0. Do not claim specific FPS without measurements.

## Export/release

- Build/import Linux and Windows from pinned Godot 4.7.2 .NET and .NET 8.
- Run the actual packaged Linux executable, not only editor. Verify included and playing audio with a real output device, imported PNG portraits/icons, and actual runtime tests.
- Windows build/export + pure tests passing is not Windows startup verification. Run Windows executable if available, otherwise label `Windows startup: NOT TESTED`.
- Update game version metadata to 1.2.0, acceptance/progress/readme/mission docs and release notes.
- Release ZIPs should include self-contained runtimes, correct Godot content, no development-only debug screenshot shortcuts, SHA-256 checksums and licensing notes for any new external assets.
- Deliver PR and tag URLs, exact commit, test counts and runs, visual/audio evidence, known limitations and clear platform statuses.
