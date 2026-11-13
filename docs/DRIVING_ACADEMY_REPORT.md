# Driving Academy implementation report

Status: implementation and exact-head verification complete on
`feature/v1.5.0-driving-academy` (PR against #34's branch). No merge, tag or
release was performed.

## Implemented

- Authored school office and cone yard placed at `(32, 48)` in `Town.tscn`,
  scaled `0.75`, with scene-owned enrolment/approach markers and three physical
  numbered cones three metres apart.
- Six lessons and `TEST01`, driven by velocity/position/time predicates on the
  authored course markers; no teleport-to-marker completion in production.
- One-time 15-coin enrolment, free retries, 3-coin repeatable cone job and one
  one-time 5-coin graduation gift.
- Schema 5 with v1–v4 migration, `LegacyVanPermit`, strict academy validation and
  `GameSession`-rebound academy service.
- New-save licence gate for the regular delivery van; legacy campaigns keep van
  access but never receive a false licence.
- Isolated venue mount/unmount at world `(2000, 0, 0)`; no academy coordinates in
  town/city snapshots; the town job compass is hidden in the venue; safe return
  and Resume.
- Dedicated `DrivingAcademyController`, `DrivingAcademyService`,
  `DrivingAcademyRules`, `DrivingAcademyCatalog`, `DrivingAcademyUi` and
  `DrivingAcademyHud`.
- C02 crate focus now requires active C02, open road, unfinished job and an
  unconsumed crate.

## Historical pre-review performance (AMD Radeon 890M, vsync on)

| Stage | 720p p50/p95/max | 1080p p50/p95/max | Draw calls |
|---|---|---:|---:|
| town-only | 16.52 / 33.34 / 47.75 ms | 16.59 / 26.15 / 35.30 ms | 2324 |
| academy course | 16.56 / 26.93 / 230.40 ms | 16.58 / 26.92 / 135.80 ms | 224 |
| town return | 16.59 / 26.64 / 39.34 ms | 16.72 / 26.79 / 34.23 ms | 2141 |

The academy-entry maximum is a one-time course-load transition, not steady
state. Xvfb/llvmpipe numbers (p50 39–86 ms) are software rendering and are
recorded separately in `evidence/v15_academy/performance.log`; they are not used
as GPU evidence.
These retained figures have not been remeasured after the safety fixes.

## Verification (tested code commit `c6cb6b1`)

| Gate | Result |
|---|---|
| `dotnet build PatchworkIsland.sln` | 0 warnings, 0 errors |
| Pure tests | **146 passed, 0 failed** |
| Source runtime, 13 phases | **1448 passed, 0 failed** (base 286, resume 21, recovery 101, living 238, rally 10, chapter 217, hotfix 48, finale 140, traversal 18, release 40, restore 54, city 138, academy 137) |
| Source graphical quality (Xvfb) | **22 passed, 0 failed** |
| Packaged Linux boot + 13 phases | exit 0; **1448 passed, 0 failed** |
| Packaged graphical quality (Xvfb) | **22 passed, 0 failed** |
| Packaged graphical academy (Xvfb) | **137 passed, 0 failed** |
| Export hash | `evidence/v15_academy/packaged/hash.txt` (SHA-256 of the exact binary run) |
| Fresh-save graphical walkthrough | historical pre-review run only; not repeated with corrected rules |
| Exact-head CI | see the PR checks and `PROGRESS.md` |

The refreshed runs replace the logs in `evidence/v15_academy/source/` and
`evidence/v15_academy/packaged/`; each `summary.txt` records exit codes.

### Graphical walkthrough

The retained pre-review `--dev --playthrough` under Xvfb earned the licence on a fresh v5 save (five cone
rounds, enrolment, L01–L06, TEST01 with 0 major / 0 minor), then completes
M01–M15 and stops at the deliberate M15 save/reload checkpoint. The continuation
completes M15 and fails at M16 when the automated walker cannot reach a moving
NPC's talk station; a retry hit the walker's own route planner ("no walking
route"). Both are properties of the test driver's navigation, not of the academy,
and the 24 missions remain green in their dedicated runtime phases. These logs
were captured immediately before the final UI-only change that hides the town
compass in the venue; the exact-head screenshots show that fix. This is
recorded as **PARTIAL** in the acceptance record rather than claimed as a
full-campaign pass. It also predates the pedestrian/signal/crossing fixes and is
not evidence that a real-input walkthrough passes the corrected rules.

## Review corrections (2026-11-13)

- Pedestrian setup follows stage selection, including first entry from school;
  stage changes remove stale actors and resets restore the waiting actor.
  Movement is across the authored lane, not along it.
- Signal entry is recorded once. Red/red+amber and avoidable amber are major
  errors and cannot unlock the final crossing step. Amber is permitted only
  if stopping safely was impossible at its onset (one second reaction travel
  plus braking at 11 m/s²). Later green cannot erase an unlawful entry.
- The final crossing requires forward entry and clearance within scene-authored
  bounds; detours, reverse traversals and side exits cannot unlock parking.
- Follow-up: signal entry now also requires directed traversal of an authored
  11 m stop-line segment from its bounded approach. Lateral green detours beyond
  either endpoint, downstream side re-entry and backwards traversal cannot
  satisfy it. Final stop hold and amber checks use the signal's local frame.
- Follow-up: R and the pause-menu restart action now reuse the full stage
  initialiser. Major/minor notes, pending speed warnings, timers and signal/
  crossing/checkpoint history are cleared. Runtime checks generate real offences,
  send R input, verify progression/economy are preserved, then complete cleanly
  without leaving the venue.
- Runtime regressions exercise actual pedestrian presence, visibility, movement,
  stopped yielding, movement offences (including reversing), reset and stage
  cleanup; both
  relevant stages enter through the school UI. Negative signal attempts cannot
  issue a licence; a clean green/crossing/parking attempt still earns it.
- `evidence/v15_academy/PROVENANCE.md` corrects the old base-commit attribution.
  Fresh logs name the committed tested code before execution; documentation
  heads are distinct. Historical performance and real-input walkthrough logs
  are explicitly labelled, not silently attributed to the corrected code.

## Real-input playthrough review (2026-11-13)

Six fresh/continue real-input playthroughs of the complete academy (cone job →
enrolment → L01–L06 → TEST01 → licence) were driven with keyboard, E-interact
and menu input only, under Xvfb 1280×720 with disposable XDG directories, and
52 screenshots captured per full run. Findings and in-scope fixes:

- **Coin chip staleness**: the HUD chip did not refresh after paid cone rounds
  and enrolment until an unrelated later path updated it. The chip is now set
  immediately when wallet changes succeed.
- **L04 HUD step counter** froze at "Step 1 of 3" even while the controller
  tracked stop/green internally. The counter now advances on a held stop at
  the line and on a lawful green entry.
- **Wayfinding**: L02 gates and L06 checkpoints carry no on-scene numbering and
  the HUD named no target; the L06 hint referenced arrows that do not exist.
  The lesson HUD now shows a per-stage "Next marker: N m direction" line and
  the hint was rewritten.
- **Signal-head occlusion**: the top-centre lesson panel covered the traffic
  light's head from the learner's stop-line view (projection ≈ screen
  (426, 203)); only a pole sliver showed. The panel now sits top-right and the
  lit lens is fully visible at the decision point. A past-line close-up
  confirms the lens materials and shadow-casting behave correctly.
- **Mirrored Label3D text**: STOP/GIVE WAY/speed/L-plate/yard text rendered
  double-sided and read mirrored from behind; all academy labels are now
  single-sided.
- **Driver-timing findings** (harness, not game rules): an idle after green
  crossed on amber (green is 8 s and the amber rule is fair), and crossing the
  zebra inside the pedestrian's first-3.5-s green window is a major offence.
  The walkthrough now starts promptly on green, skips stale greens, and
  crosses the zebra during the red phase with a mid-approach yield guard.
- Final state: a fresh save passes the whole academy on the first TEST01
  attempt ("Perfect drive"), the licence and 5-coin gift are granted, and the
  post-licence school board lists all six lessons as replayable.

Logs and captures: `evidence/v15_academy/graphical/walkthrough-fresh-save.log`
and `evidence/v15_academy/graphical/walkthrough/` (52 screenshots). The runtime
academy suite grew to 144 checks (L02/L06 guidance text, L04 step progression).

## Fresh verification (tested code commit `5a0abda`)

- Build 0 warnings/errors; pure **146/146**; headless import and source/packaged
  boot exit 0. All 13 source phases **1455/1455** and all 13 packaged phases
  **1455/1455** (academy phase now 144). Source graphical quality **22/22**,
  packaged graphical quality **22/22**, packaged graphical academy **144/144**
  under Xvfb with disposable XDG directories.
- The real-input fresh-save walkthrough above completed with exit 0, licence
  earned on the first TEST01 attempt; all 52 of its screenshots are committed.
- `evidence/v15_academy/source/head.txt` and `packaged/head.txt` name
  `5a0abda…` before execution; export SHA-256 refreshed in `packaged/hash.txt`;
  `SHA256SUMS` regenerated over all 119 evidence files and `sha256sum -c` passes.

## Evidence

- `evidence/v15_academy/source/` — import, pure, 13 source phases, quality and
  capture logs with `summary.txt` exit codes.
- `evidence/v15_academy/packaged/` — export log, binary hash, boot, 13 packaged
  phases, packaged graphical quality/academy.
- `evidence/v15_academy/graphical/` — the fresh-save real-input walkthrough log
  plus `walkthrough/` with all 52 playthrough screenshots of the reviewed run.
- `evidence/v15_academy/screenshots/` — 9 categories × 720p/1080p, including the
  coloured pedestrian; developer state setup is used for captures.
- `evidence/v15_academy/performance.log` — frame-interval probe.
- `evidence/v15_academy/README.md` — method, environment and limits.
- `docs/DRIVING_ACADEMY.md` — design and code map.
- `docs/SAVE_COMPATIBILITY.md` — schema 5 migration contract.

## Known limitations

- Windows gameplay startup is **NOT TESTED**; the Windows build and pure suite
  run in CI only.
- No human child-usability study, accessibility audit or independent visual
  sign-off was performed.
- The automated full-campaign graphical walkthrough is incomplete at M16 (above).
- Performance numbers are from the available Linux GPU only; the prior native
  audio-shutdown warning and unrelated hitch investigations remain open where
  they are not directly measured here.
