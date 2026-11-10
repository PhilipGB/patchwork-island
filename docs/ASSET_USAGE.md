# Supplied assets — usage and provenance

## Audio supplied and ready for Godot import

**4 original OGG Vorbis tracks** in `assets/audio/music/`:

- `music_title.ogg` — 40.00 seconds; title and main menu.
- `music_village_day.ogg` — 34.29 seconds; roaming around village.
- `music_rally.ogg` — 26.67 seconds; M05, M09 and M19 race events.
- `music_festival.ogg` — 30.48 seconds; M20 finale/festival.

**28 original PCM WAV effects** in `assets/audio/sfx/`:

`ui_confirm`, `ui_cancel`, `ui_error`, `ui_hover`, `coin_collect`, `coin_spend`, `shop_buy`, `outfit_equip`, `mission_complete`, `chapter_unlock`, `race_checkpoint`, `race_finish`, `npc_greet_01`, `npc_greet_02`, `book_collect`, `parcel_deliver`, `garden_plant`, `build_place`, `jump`, `land`, `footstep_grass_01`, `footstep_grass_02`, `footstep_road_01`, `footstep_road_02`, `npc_horn`, `engine_idle_loop`, `engine_drive_loop`, `festival_pop`.

Import OGG music as loops; test beginning/end transitions audibly. WAV engine effects should loop; short UI effects should not. Both formats are supported by Godot. Map events using `assets/content/audio_cues.json`. The recordings are **synthetic original starter assets** generated for this package, not licensed/commercial music and not copies of an existing soundtrack. They're designed for early integration and can be polished after in-game listening. No external sound recordings, bought samples or third-party compositions were used.

**Important:** A Godot CI `AudioServer` dummy driver does not prove sound actually plays. The agent must open an exported build with a working audio device and listen.

## Low-poly 3D starter models

- `assets/models/npcs/*.glb`: **six original NPC models** (Milo, Ada, Pip, Fern, Ollie, Ivy), simple coloured faces/heads/hair/clothes and separate named meshes. Unrigged, no skeleton or animations. The agent must add any collision/animation/script and verify actual Godot import; these are mesh assets, not fully working NPC scenes.
- `assets/models/vehicles/*.glb`: **three original vehicle models** — NPC scooter, compact car and rally car. Unrigged; the agent must implement vehicle controller, collision shapes, road-following, tyre rotation and safe behaviour.
- Model coordinates are Y-up, facing towards -Z; nominal NPC height ≈2 m. Inspect transforms in Godot and adjust scale to fit existing player/van. The models are optional if procedural meshes prove cleaner, but use one consistent style rather than mixing incompatible art.

## UI PNG assets

- Six `192×192` transparent NPC portraits in `assets/ui/npc_portraits/` (Milo, Ada, Pip, Fern, Ollie and Ivy). Artwork is original, flat shaded, and fits the current P9 paper-doll style. Portraits are intended for simple NPC dialogue/name cards.
- Ten `128×128` transparent icons in `assets/ui/mission_icons/` (neighbours, bakery, library, road signs, garden, delivery, market maths, mechanic, grand rally, festival). Map them to M11–M20 respectively. Replace or tune sizes as needed for readable HUD buttons.
- `design/npc_portrait_contact_sheet.png` gives an art-style reference; it is not a required runtime asset.
- `design/rally_track_schematic.png` shows a **proposed** enlarged loop; it is NOT a screenshot or a verified driveable track.

## Structured design data

- `assets/content/missions_M11_M20.extension.json` — complete authored mission specs in the current mission-definition field names. **Append and integrate**, not drop-in replace. Must update both source and reference JSON plus all code rules/tests.
- `assets/content/npcs.proposal.json` — six NPC roles, greetings and **suggested** world patrol points.
- `assets/content/traffic_routes.proposal.json` — two free-roam driver ideas + one M19 rival; routes **not validated**.
- `assets/content/rally_course.proposal.json` — enlarged east-island centreline, four original-race and six M19 gate location proposals; **not verified against world geometry**.
- `assets/content/audio_cues.json` — intended cue, bus, context and settings mapping.

Only original audio/UI files are ready-made runtime assets. Proposal JSON should be validated and modified in code before use.

## Reproducibility

The ZIP contains `tools/generate_assets.py`, `tools/generate_models.py`, `tools/generate_content.py`, and `tools/draw_rally_map.py`. Generating the GLB models requires Python `trimesh` and `numpy`. Generating the provided audio requires Python `numpy`, `soundfile` and `ffmpeg` (Vorbis encoder); PNGs require `Pillow`. **The delivered files are already generated and can be used without these tools.** Re-running generation deterministically recreates the intended asset set, though compressed OGG bytes can differ by encoder version.

### Usage rights

All audio, PNG and GLB assets in this handoff were generated specifically for this project with custom code; no external samples or existing songs are embedded. They may be copied, edited and distributed as part of Patchwork Island without attribution to external providers. `docs/ASSET_USAGE.md` provides provenance. For any externally sourced assets added later, record licence and attribution separately.


## v1.2.0 implementation note: 3D starter models

The supplied NPC and vehicle GLBs were imported and inspected. Each named part
is a separate mesh but its vertices are authored in shared world space with no
pivot transform, so rotating an arm, leg or wheel about its node would rotate
the part around the world origin. Rather than shipping a broken animation rig,
v1.2.0 reproduces the intended low-poly silhouettes procedurally in the same
flat-shaded style as the P9 player and existing yellow van, with correct limb
and wheel pivots. The supplied PNG portraits, mission icons and all audio are
used as runtime assets. This is an intentional consistency decision, not a
silent omission; replacement provenance remains the original handoff generator.

## v1.3.0 implementation note: GLB template integration

The canonical-town migration supersedes the v1.2.0 presentation choice. The
supplied GLB characters, vehicles and pickup meshes are now the runtime visual
templates, while all controllers, collision, camera and save logic remain in
C#. Because the GLBs are unrigged and authored in shared model space, the
integration reparents body parts under head/arm/leg pivots and the four vehicle
wheels under their own centre pivots, preserving each part's transform before
animation. `COLOR_0` is enabled as albedo so the authored palettes render
correctly. Details and the full template mapping are in
`docs/CANONICAL_TOWN.md` and `docs/scene_bindings.json`.
