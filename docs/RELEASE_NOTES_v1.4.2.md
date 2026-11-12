# Patchwork Island v1.4.2 — City jobs load hotfix (test build)

This is a **pre-release test build** fixing a city progression bug in v1.4.1.
The v1.4.0 and v1.4.1 tags and releases are unchanged.

## Fixed

After loading a save where all twenty town jobs were complete, M21 Roadworks
Rescue was finished and the city road was visibly open, the CITY JOBS board
opened but starting a city job failed with the closed-road/prerequisite message.

The load path replaced the game data and the town mission service, but left the
city mission service bound to the pre-load data. The board therefore saw the
loaded open road while the city service still saw a closed one. The same stale
object also affected city progress, turn-in and completed-job checks after a
load or backup recovery.

Loading now rebinds the town and city services to the same loaded `GameData`.
No save schema changed: existing v1.4.x saves load normally.

## Verification

- 132 pure tests pass.
- 1,300 source runtime assertions pass across all 12 phases.
- 129 city runtime assertions pass, including a real save load that opens the
  road and starts C01.
- The packaged Linux build boots and passes all 12 phases with 1,300
  assertions.
- The new regression fails twice if the city-service rebind is removed.
- Windows builds and passes CI, but **Windows startup was not tested** here.

## Known gaps

- No continuous human M01–M21 → C01–C03 playthrough and no human visual sign-off.
- Windows gameplay startup has not been independently verified.
- City directional shadows are coarse at this shadow distance.
- Transition-hitch measurements and a same-method v1.3.0 baseline were not
  captured.

## Controls

WASD/arrows move, Space jump, E interact, J jobs, H hint, Esc/P pause. Near the
van press E to drive; R resets it. The city unlocks after the 20 village jobs and
M21 Roadworks Rescue.

## SHA-256

```text
d52d0f6768cbccde383671f1ddf792949b52bc963c8686f94337d72f3d7f5aa2  linux/PatchworkIsland.x86_64
64ee41e60ee26195b8bbe78f0115b4bf87c8089047be69c2a7728b404446a2ed  windows/PatchworkIsland.exe
5ed02c478945632e77790f8806bd75e64c0d2e9f8e419baa8d4bcf80838feadb  PatchworkIsland-1.4.2-Linux-x86_64.zip
74cbd6ff74d0547c7e6748ef5b3a83fd28e8f383e7760db98b18fb6ce6b5e254  PatchworkIsland-1.4.2-Windows-x86_64.zip
```
