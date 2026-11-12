# Driving Academy acceptance record

Status legend: PASS / FAIL / PARTIAL / NOT TESTED. All results below are from the
exact source head recorded in `evidence/v15_academy/source/head.txt`; the full
logs, screenshots and the exported binary hash are in `evidence/v15_academy/`.

Verification summary: pure **145/145**, source runtime **1373/1373** across 13
phases, packaged runtime **1373/1373**, source and packaged graphical quality
**22/22** each, packaged graphical academy **62/62**.

## A. Asset and scene contract

| ID | Result | Evidence |
|---|---|---|
| A01 | PASS | `godot --headless --path . --editor --quit` exits 0 with all 11 academy scenes and 4 SVG icons imported; `DrivingAcademyPreview` and the course load headlessly. |
| A02 | PASS | 27 declared markers are present and unique in `GameplayMarkers`; `DrivingAcademyController.CacheMarkers` throws a named error for a missing or duplicate marker. Pure scene-contract test checks each declared name. |
| A03 | PASS | Automated probes place the learner car on both outer lanes, both parking pads and the driveway joins and assert it settles on the authored surface; L02/L06/TEST01 drive the full route with real input. Markings are authored at road level with shadows off. A human lane-by-lane visual review was not performed. |
| A04 | PASS | Office/yard are instanced at `(32, 48)`, scaled `0.75`, rotated to face the village. Pure tests assert the footprint clears every building, the island edge, the rally line and critical stations; the screenshot shows the door, sign and cones on solid ground. |
| A05 | PASS | The venue mounts at world `(2000, 0, 0)`, is freed on return, and 30 enter/exit cycles leave no `AcademyVenue` orphan and no town-compass leak. No procedural scenery or fallback anchors exist. |

## B. Gameplay and UX

| ID | Result | Evidence |
|---|---|---|
| B01 | PASS | School and numbered cones are on-foot stations; enrolment UI is reachable before payment; W/S/A/D/E/R/Esc keep their meanings. |
| B02 | PASS | 15-coin enrolment is a single atomic deduction; repeat clicks and reloads cannot recharge it (service + runtime + persistence tests). |
| B03 | PASS | Five physical cone rounds pay 3 coins each; a wrong cone resets the round for free; helper step and round count persist across saves. |
| B04 | PASS | L01 requires ≥2 m/s of travel and 1.5 s below 0.3 m/s in the stop zone. |
| B05 | PASS | L02 requires the three gates in order; skipping is impossible. |
| B06 | PASS | L03 requires a 1.5 s stop before the line and then a safe release past it. |
| B07 | PASS | L04 stops on red/red+amber, enters only on green and raises a hazard when the pedestrian is on the crossing. |
| B08 | PASS | L05 measures ≥4 m of reverse and a 1.5 s stationary hold inside the bay within 3.2 m and 25° of the bay axis (either heading). |
| B09 | PASS | L06 requires four ordered checkpoints and warns above 20 mph (8.9408 m/s). |
| B10 | PASS | TEST01 combines stop line, signal, crossing and parking; three minor notes are allowed and any major error fails; retry and R-reset are free. A red-light offence is decided only when the car crosses the line, so legally clearing on green is not penalised later. |
| B11 | PASS | A pass issues the licence and one 5-coin gift; practice and reload cannot duplicate either (runtime persistence assertion). |
| B12 | PASS | 16 screenshots (8 categories × 720p/1080p) show readable school, HUD, lesson, road rules, parking, exam and licence; camera-latch regressions stay green. |
| B13 | PASS | Return is safe before enrolment, mid-lesson, after failure and after success; a mid-lesson exit saves `AcademyActiveStage` and the school offers Resume. |

## C. Persistence and architecture

| ID | Result | Evidence |
|---|---|---|
| C01 | PASS | Schema 5; v1–v4 fixtures migrate to 5 with the legacy permit. Newer-save protection, backups and corrupt-file recovery remain green. |
| C02 | PASS | v1–v4 receive `LegacyVanPermit` only; a fresh v5 save starts with neither permit nor licence and the regular van is gated until the licence is earned. |
| C03 | PARTIAL | All 24 missions and 3 city jobs pass their dedicated runtime phases (base, chapter, finale, city) with the academy code present. The fresh-save graphical walkthrough earned the licence and completed M01–M15, then stopped at M16 on a pre-existing walkthrough NPC-approach/route-planner limitation. Not a human playthrough. |
| C04 | PASS | Mid-lesson save/exit and Resume restore the physical stage; no academy coordinates enter town/city snapshots. |
| C05 | PASS | `GameSession.ReplaceData` rebinds village, city and academy to the same `GameData` (unit assertion), so Continue and New Game cannot leave a stale service. |
| C06 | PASS | Pure tests cover stop duration, parking distance/heading, mph conversion, every light transition, red-line edge cases, grading boundaries, retries and reward idempotency. |
| C07 | PASS | Dedicated `DrivingAcademyController`, `DrivingAcademyService`, `DrivingAcademyRules`, `DrivingAcademyCatalog`, `DrivingAcademyUi`, `DrivingAcademyHud`; `Main` only composes. |

## D. Mandatory P3 fixes

| ID | Result | Evidence |
|---|---|---|
| P3-A1 | PASS | CI runs the graphical quality gate on the source build under Xvfb with disposable XDG dirs, and on the exported binary under Xvfb. |
| P3-A2 | PASS | The exported Linux binary passes the packaged graphical academy gate (62 assertions: enrolment UI, learner car, rendered lesson completion, clean return). |
| P3-A3 | PASS | Every packaged run uses disposable XDG data/config/cache; `packaged/hash.txt` records the SHA-256 and ELF details of the exported binary. |
| P3-B1 | PASS | C02 crate availability requires an active C02, an open road, an unfinished C02 and an unconsumed crate. |
| P3-B2 | PASS | Consumed, switched-away and completed crates lose E focus; restoring C02 regains it. Covered by runtime and graphical quality checks. |

## E. Quality and release

| ID | Result | Evidence |
|---|---|---|
| E01 | PASS | Pure, 13 source phases, export, 13 packaged phases, source graphical quality and packaged graphical quality/academy all run at this head and exit 0. |
| E02 | PASS | Camera latch, 60 shop open/close cycles, city kerbs, roadworks, city cars and recovery remain green in the 22-check graphical gate. |
| E03 | PARTIAL | Academy isolation is proven by the 30-cycle venue test and the runtime academy phase. The full 24-mission graphical walkthrough is incomplete at M16 (see C03); this is reported, not claimed. |
| E04 | PASS | Same-hardware 720p/1080p p50/p95/max, draw calls and primitives are recorded in `performance.log`. The 230 ms academy-entry maximum is the one-time course load and is reported as-is; software Xvfb numbers are labelled separately. |
| E05 | NOT TESTED | Windows build/pure tests run in CI; Windows gameplay startup could not be tested here. |
| E06 | PASS | 16 screenshots in `evidence/v15_academy/screenshots/`, captured from the frozen head at both resolutions. |
| E07 | PASS | PR opened against the verified base; the exact head, CI state and any CI discrepancy are recorded in `PROGRESS.md`. No merge, tag or release. |
