# Patchwork Island v1.2.0 — Living Village Update

A cheerful 3D village adventure for ages roughly 5–10. This release keeps the
original ten jobs and adds a second ten-job chapter, six named villagers, live
audio and a longer continuous rally circuit.

## What's new

- **Six named villagers** — Milo, Ada, Pip, Fern, Ollie and Ivy have portraits,
  name labels, patrols, dialogue and mission roles.
- **Two safe NPC drivers** — a post scooter and a delivery car follow authored
  road-bound routes, yield to the player and never block mission stations.
- **Music and sound** — four looping tracks and 28 effects on Music/SFX/UI/Vehicles
  buses, with persisted Music and SFX settings.
- **M11–M15** — welcome cards, bakery counting, lost library books, reversible
  road signs and the RED/BLUE community garden.
- **M16–M20** — the delivery round, a separate 20-coin market budget, Pip's
  staged car repair, a six-gate grand rally with a friendly rival, and the
  invite → tables → lanterns village festival.
- **Larger rally circuit** — one closed ~206 m road mesh with a continuous
  shoulder and collision, preserving the original four-gate M05/M09 semantics
  and the 90 s / 75 s optional bonuses.
- **Visual cleanup** — no moving cloud shadows, clean road joins, readable gate
  numbers/arrows, correctly sized mission icons, readable book covers, and the
  delivery/festival props placed clear of walls, trees and traffic.

## Saves

Save schema **3** adds `FestivalSeen` for the second finale. Schema 1 and 2
saves migrate in memory; the original file is kept as the `.bak` backup. A
completed v1.1.0 village (M01–M10) keeps its coins, outfits, cosmetics, bridge,
park, furniture, player/van position and `CelebrationSeen`; M11 unlocks
immediately and the original M10 celebration does not repeat. Schema 4 or newer
is never loaded or overwritten automatically.

## Controls

WASD/arrows walk, Space jump, E interact/enter-exit van, mouse wheel zoom,
right-drag orbit, J job board, H hint, Esc/P pause, R reset van, left click
place build pieces.

## Verification

- Pure logic: **106 passed, 0 failed**.
- Runtime: pause/modal 161, resume 21, recovery 101, living village 238,
  rally 10, chapter two 217, finale 140, traversal 12, v1.1 migration 22.
- Packaged Linux build passed the same selftest, rally, finale, traversal and
  v1.1-migration phases, with every process exit code 0.
- Real AMD Radeon 890M (Mesa 26.0.8, OpenGL 4.6): 1280×720 mean 16.69 ms /
  p95 17.35 ms; 1920×1080 mean 16.67 ms / p95 17.28 ms over 600 frames.
- Real-device audio: packaged Linux ran with PipeWire/PulseAudio and a 20-second
  monitor capture contained nonzero PCM from the title music/UI cues
  (peak 19777, RMS 944.8). **Human listening was not possible in this
  environment.**
- Linux x86-64 export: `ELF 64-bit LSB executable`, booted headlessly and
  passed every packaged runtime phase.
- Windows x86-64 export: `PE32+` GUI executable, file/product version
  `1.2.0.0`. **Windows startup was not tested** (no Windows runtime or Wine).

## Known verification gaps

- Subjective audible playback by a person is **NOT TESTED**; physical output to
  the real sink monitor was captured.
- Windows startup is **NOT TESTED**.
- Real-Radeon frames are measured; no v1.1.0 real-GPU baseline probe exists.
- Child playtesting and accessibility review remain manual follow-ups.

## Assets

All shipped audio, PNG and GLB assets in the handoff were generated for this
project; no external samples or third-party songs are embedded. The supplied
PNG portraits, mission icons and audio are used at runtime. The supplied GLB
villager/vehicle bodies were not used because their vertices share world space
and lack clean pivots; v1.2.0 uses procedural low-poly bodies matching the
existing style. See `docs/ASSET_USAGE.md`.
