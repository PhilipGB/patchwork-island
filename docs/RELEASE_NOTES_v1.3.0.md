# Patchwork Island v1.3.0 — Canonical Town

A world-architecture and visual-quality release. The town is now a genuine
Godot-editor-authoritative level assembled from reusable authored scenes
instead of geometry constructed at runtime. There are no new missions, no
save-schema change and no control changes.

## Highlights

- **The town is an editable level.** Buildings, roads, landmarks, props, rally
  gates, NPC paths and traffic routes are authored scenes and markers. Moving a
  building in the editor moves its doorway, job, shop and build sockets with it.
- **Gameplay follows scene markers.** Doors, jobs, shops, build sockets, bakery
  and library display slots and race checkpoints resolve from `Town.tscn` and
  its marker resources. The runtime never rebuilds the static world and never
  falls back to hard-coded coordinates.
- **Supplied low-poly visuals.** The player, six villagers, the delivery van,
  both town traffic cars, the rally rival and all pickups use the supplied GLB
  templates while keeping the existing controllers, collision, animation,
  audio and save behaviour.
- **Flush road junctions.** Town-road collision now sits at the same height as
  the bridge and terrain, removing the 2–3.4 cm lips that could catch the van.
  The road surface classification matches the visible slabs exactly.
- **Visual audit corrections.** Three player-height review rounds corrected
  sign faces and arrows, gable placement, the celebration and festival
  banners, rally-gate numbers, GOLD LAP and podium labels, NPC nameplates and
  portraits, vehicle labels, lamp and prop placement, crossing alignment,
  road-junction seams and bunting ground shadows.
- **Traffic separation.** The two free-roam vehicles share a one-way village
  loop with a deterministic following rule, so the faster scooter can no
  longer drive through the delivery car. The rule is deadlock-free: only the
  car behind yields.

## Compatibility

- Save schema **3** is unchanged. v1.2.0 and v1.2.1 saves load directly, and
  schema 1 and 2 saves still migrate with the original kept as a `.bak`.
- Coins, outfits, mission progress, the bridge, swings, park, furniture and
  both finale states are preserved.
- All 20 jobs, the M05/M09 four-gate races and the separate M19/M20 finale
  content are unchanged.
- `docs/layout_manifest_v1.2.1.json` is retained unchanged as the historical
  source baseline; the canonical town is described by
  `docs/layout_manifest_v1.3.0.json`.

## Verification

- Build: `dotnet build PatchworkIsland.sln` — 0 warnings, 0 errors.
- Pure logic tests: **123 passed, 0 failed** (`dotnet run --project
  tests/PatchworkIsland.Tests.csproj`).
- Source runtime self-tests: **1171 passed, 0 failed** across all 11 phases,
  each exiting 0:
  `284 / 21 / 101 / 238 / 10 / 217 / 48 / 140 / 18 / 40 / 54`.
- Headless import and boot (`godot --headless --path . --editor --quit` and
  `--quit-after 120`): exit 0.
- Packaged Linux build (`dist/linux/PatchworkIsland.x86_64`): boot PASS; all
  11 phases 1171/1171; `--selftest` repeated 3/3 green. The binary extracted
  from the published ZIP booted and passed `--selftest` (284/284).
- Linux export: `ELF 64-bit LSB executable`.
- Windows export: `PE32+` GUI executable, file/product version `1.3.0.0`.

## Known verification gaps

- Human visual sign-off and one continuous M01–M20 playthrough remain
  **NOT TESTED**; the automated tours and self-tests are the evidence for this
  release.
- Windows startup is **NOT TESTED** (no Windows runtime or Wine in the build
  environment); it remains explicitly waived for this Linux-first release.
- Real-GPU frame-time comparison and the traffic-gap simulation are unchanged
  or idealised as documented in `PROGRESS.md`. The 7 m following gap is larger
  than the worst braking distance, and the runtime living-village phase still
  passes.
- Child playtesting and accessibility review remain manual follow-ups.

## Package hashes

```text
b0ab55eba61619bde6e59ab9e8674862b99f87523c7af61449a6b90244a663fc  linux/PatchworkIsland.x86_64
01e8e88cae3e6b91fd5466bebd09046a1ae6b065f9290df98380ae51143e8cf1  windows/PatchworkIsland.exe
37c7330bdb76ae323aad56cd5aa542d5a94cb289375f3256e5c51cd3e9ae4dff  PatchworkIsland-1.3.0-Linux-x86_64.zip
63724b9197ef0536c6bcfebd2adb55ee13709bf48b24fd073bc3aa6ea4d2147d  PatchworkIsland-1.3.0-Windows-x86_64.zip
```
