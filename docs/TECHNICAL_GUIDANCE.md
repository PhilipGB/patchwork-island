# Technical guidance — inspect live code before implementing

## Existing architecture to preserve

- `src/Main.cs` orchestrates scene transitions, saves, gameplay stations and modal controls.
- `src/World/WorldSceneAdapter.cs` instantiates the authored `scenes/world/Town.tscn`; `src/World/WorldModel.cs` is the runtime data contract. There is no procedural world builder or bake step.
- `src/Player/PlayerController.cs` owns player movement and model geometry.
- `src/Core/MissionCatalog.cs`, `MissionService.cs`, `MissionIntegrity.cs`, `SaveValidation.cs` own authored missions and deterministic state/rules.
- `src/UI/JobBoardUi.cs` renders the existing 10-job list.
- `src/Core/SaveService.cs` handles schema 2 with migrations, atomic backups and newer-save protection.
- `src/Testing/RuntimeSelfTest.cs` + `tests/Program.cs` provide runtime and pure regression coverage.
- `GameData.Settings.MusicEnabled` and `.SfxEnabled` already exist, but as of v1.1.0 do not trigger any audio playback.
- Content exists in `MISSIONS.json` **and** `data/missions.json`; a strict compare test rejects drift.

## Audio architecture

Use Godot `AudioStreamPlayer` nodes for menus/music, `AudioStreamPlayer3D` for spatial NPC/engine cues if appropriate, and restrained global `AudioStreamPlayer` voices for UI/SFX. Set up `Master`, `Music`, `SFX`, `UI` and `Vehicles` buses, with the Music toggle muting Music and the SFX toggle muting SFX/UI/Vehicles. On startup, saved settings must be applied even when loading a v1.1.0 save. Set OGG import **Loop=true** and offset=0; test seam quality by ear and inspect headroom. WAV SFX should remain nonlooping except engine idle/drive, which must loop in the import settings or stream configuration. Avoid constructing a new audio player every tick; use pooled/existing players and bounded simultaneous voices. Tie footsteps to physical movement, surface and grounded state; use engine gain/pitch filtered by actual vehicle speed, not raw key presses.

The audio cue manifest in `assets/content/audio_cues.json` is a *mapping*. Code should centralise cue IDs, avoid silent missing streams, and verify all referenced files can load from packaged `res://`. No internet dependency at runtime.

## NPC scene lifecycle

Procedural NPC `CharacterBody3D` or imported unrigged GLB NPC `Node3D` + verified movement/collision, using stable names, clothes, faces and simple shared idle/walk animation. Do not duplicate the full player controller just to get an NPC. Prefer `NpcDefinition` (pure data), `NpcActor` (Godot visual/physics), and `NpcService` (talk/task state) or similarly small components. NPC routes in the handoff are provisional. Agent must inspect actual colliders; project waypoints to navigable ground and use clearance checks or author verified route anchors. Add near-player relevance updates and optional coarse simulation for distant traffic. Ensure `ProcessMode.Pausable`, deterministic spawn and safe reset/reload. NPC talk prompts should not compete with job-builder stations at the same position.

## Driver safety

Use scripted spline/waypoint tracking with a forward collision/obstacle sensor and capped acceleration rather than unconstrained rigidbody cars. Stop or yield at the player/van, roads, sharp bends and populated intersections. Collision layers should avoid blocking M05/M09 gates and critical job interactions. At most two free-roam cars plus one M19 competitor. An off-screen reset is allowed only if it is genuinely not visible; never pop into view. Run long-duration no-softlock tests. NPC traffic should not consume the player van's mission counters, races or save state.

## Rally geometry

Current `BuildRaceMeadow` uses several overlapping `AddRoadSegment` rectangles and four gates. Replace the circuit representation with a closed centreline curve, sampled at fixed arc lengths into a coherent **single road mesh** (or seam-safe joined strips), with normal/winding and consistent road elevation. Keep a clear separation from underlying grass to prevent Z-fighting. Construct road surface collision following the visual mesh. Maintain physical margins to east-island bounds; collisions may require landscape adjustment. Add road shoulder/edge markers and predictable start, split, gate/finish colliders. The packaged `rally_course.proposal.json` points and schematic are *illustrative*. M05/M09 four gates retain original IDs and saved checkpoint order even when repositioned; M19 six gates use distinct state and validation.

## Visual QA targets

Record actual defects with screenshots before changing geometry: coplanar/z-fighting roads, curb and shoulder gaps, floating signs/pickups, van clipping, building roof/window/awning intersections, player hair/hat/dress clipping, props intruding into the road, turn/bridge alignment, NPC overlap, UI overflow at 720p, readability at 1080p. Fix one root cause at a time, re-run visual captures after each. GPU/desktop-driver warning messages alone are not in-game geometry defects.

## Interaction and accessibility

Prefer existing `Interactable.Create` and Hud modals; maintain `E` interaction range and one prompt at a time. NPC dialogue may use supplied portraits, a readable name, 1–3 short sentences and up to 3 choices. Add labels so no gameplay requires audio. Job board should use tabs or section filters for original chapter vs New Adventures, not a single 20-item wall. Preserve older player's completed jobs and show new available chapter cleanly.

## Do not overbuild

Avoid general NPC dialogue AI, enormous pathfinding frameworks, ECS replacement, fancy shaders, full skeleton animation, elaborate cutscenes or a new mission scripting interpreter. Runtime rules will need explicit implementation in the existing MissionRules/MissionService boundaries, with small helpers as necessary. Keep C# pure logic testable without launching Godot.
