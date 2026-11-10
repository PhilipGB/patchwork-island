# Patchwork Island v1.2.1 — M18 Persistence Hotfix

A focused maintenance release for the v1.2.0 Living Village Update. There are
no new jobs, no save-schema change and no rendering/audio changes.

## Fixed

- **M18 repair parts no longer respawn after saving and reloading.** The
  mechanic's four physical pickups (`part_tyre_0`, `part_tyre_1`,
  `part_lamp_0`, `part_battery_0`) now record their pickup IDs in the save.
  A repeat collection is refused, and the job cannot hand out the same tyre
  twice.
- **Partial v1.2.0 M18 saves are repaired on load.** Older saves only stored
  the carried/fitted counts. Normalisation now infers exactly the number of
  physical pickups that were acquired, so upgrading cannot respawn a duplicate.
  Already-recorded pickup IDs always win and are never over-marked.
- **Off-island NPC traffic stops permanently.** A vehicle below the island now
  enters a terminal stopped state, silences its engine loops, disables further
  movement processing and logs the failure once instead of re-entering the
  route every physics frame.

## Compatibility

- Save schema **3** is unchanged; v1.2.0 saves load directly.
- A v1.2.0 save with partial M18 progress is normalized in memory. The file is
  rewritten at schema 3 on the next normal save.
- All v1.2.0 content, coins, outfits, world state, M01–M20 IDs/rewards,
  M05/M09 four-gate timing, the original M10 celebration and the separate M20
  festival are unchanged.

## Verification

- Pure logic: **107 passed, 0 failed** (one new M18 persistence test).
- Runtime: pause/modal 161, resume 21, recovery 101, living village 238,
  rally 10, chapter two 217, **hotfix 48**, finale 140, traversal 18,
  v1.1 migration 22 — **976 passed, 0 failed**.
- The new `--selftest-hotfix` phase starts the real M18 job, collects the first
  tyre through its world pickup, saves, reloads the real scene, proves the tyre
  did not respawn, collects the remaining three parts, repairs Pip's car at the
  bench and receives the 24-coin reward exactly once. It also proves an
  off-island traffic car stops once, stays put and does not repeat the error.
- Packaged Linux build passed every phase above with exit 0 and zero leaked
  unsafe references.
- Rally timings remained inside the bonus windows: M05 18.97 s, M09 19.03 s,
  M19 22.32 s (each +5).
- Linux x86-64 export: `ELF 64-bit LSB executable`.
- Windows x86-64 export: `PE32+` GUI executable, file/product version
  `1.2.1.0`.

## Known verification gaps

- Windows startup is **NOT TESTED** (no Windows runtime or Wine); it remains
  explicitly waived for the Linux-first release.
- Audio code and assets are unchanged from v1.2.0. Human listening of the
  packaged v1.2.0 build was **PASS**; v1.2.1 was not separately auditioned.
- Real-Radeon frame times and the P15.1 bridge captures are unchanged from
  v1.2.0; no new rendering work was done.
- Child playtesting and accessibility review remain manual follow-ups.

## Package hashes

```text
610e29c12067e8df90fba576e21c5ce941a41a88bdb1cdeaa31262f5c14ba6c1  linux/PatchworkIsland.x86_64
1421e456ce90dd5dadbb6451d536b34ecc71e2e1649fdee1f1b420c589308aba  windows/PatchworkIsland.exe
cb04e909db41ba19bb74199653c7c6018356e2904d4f6b092f8358aa1408fdf6  PatchworkIsland-1.2.1-Linux-x86_64.zip
ae40a90dfbb640b0cf61941831ce69b83800f2cade768668c4f93c34377d0ac5  PatchworkIsland-1.2.1-Windows-x86_64.zip
```
