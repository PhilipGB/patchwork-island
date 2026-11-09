# v1.2.0 requirements and boundaries

## Audience and controls

Age 5–10, capable five-year-old to older child. Retain keyboard/mouse: WASD/arrows, Space, E, Esc and existing job-board controls. Dialogues should be short and readable; important information has text and icons even if SFX are enabled. No required voice recognition, typing or microphone. No hard timed mission failure; race times are optional bonuses. Keep the existing simple flat-shaded geometry and avoid a skeletal rig rewrite.

## Audio

Four original music loops and 28 original WAV effects are in the ZIP. Integrate menu, free-roam, race and festival music, with smooth volume transitions and no simultaneous track duplication. Separate Master/Music/SFX/UI/Vehicles buses. Respect persisted MusicEnabled/SfxEnabled. Implement missing playback triggers: E interaction, walk/jump/land, collecting, purchases, clothing, building, mission completion, checkpoint, race finish, NPC, vehicle engines. Control repetition and prevent audio spam; background should be quieter than cues. Packaged Linux exports must decode/play resources with an audio device attached. Use a dummy/headless option in CI for structural verification but record real audible verification separately.

## Villagers and drivers

Six named villagers: Milo, Ada, Pip, Fern, Ollie, Ivy. Provide simple geometric models consistent with P9 player design, role-specific clothes/hair, NPC portrait and name/greeting. Wandering is constrained to verified walkable areas. While the player is talking, NPC remains at its interaction spot; conversations do not require precise cursor aim. At least two autonomous vehicles are active in suitable areas, with a third friendly rally rival during M19. Vehicles must never prevent the player reaching stations or progressing missions. No network, complex crowd planner or unsupervised RigidBody physics.

## Visual polish

Capture and fix specific defects (e.g., coplanar roads, seams, floating props, roof/door intersections, hair/cap clipping, UI overflow, decals, NPC intersections). Build a documented before/after list backed by actual screenshots. Preserve current island landmarks, indoor/camera behaviour, driving collisions and P9 clothing silhouettes. Fix root geometry/material/collider issues; do not hide artefacts with broad global effects.

## Larger rally

Existing east-island route is four-gate M05/M09; proposed large loop expands across more of the east island. Render a **continuous smooth road mesh or properly joined segments**, not overlapping rectangles that cause Z-fighting. Minimise sharp hairpins, provide stable wheel contact and wide recoverable shoulders. Use numbered gate arrow markers that align with direction. M19 has six gates. Make NPC competitor nonblocking and optional to beat. Current M05/M09 bonus boundaries remain **90/75 seconds**, respectively; adjust only through a documented gameplay design change if genuinely impossible with longer geometry. New M19 bonus is a proposal, not a promise of an achievable threshold until tested.

## Ten new jobs

M11–M20 are specified in `docs/MISSIONS_M11_M20.md` and `assets/content/missions_M11_M20.extension.json`. M11 unlocks only after all M01–M10 completed; subsequent missions have achievable dependency branches. Use each mission's authored mechanics for hints, content comparison and tests, and ensure the actual game follows them. Give money and visible effects only once. M20 triggers a **second**, independent festival, not a replacement of the original M10 celebration.

## Not in scope

Full NPC dialogue AI/LLM, online races, procedural voice acting, seasonal weather, persistent global traffic simulation, live multiplayer, player avatars beyond P9, landscape overhaul that relocates earlier stations, new platforms or a general-purpose mission scripting engine. Extensions may be proposed later but must not block v1.2.0 delivery.
