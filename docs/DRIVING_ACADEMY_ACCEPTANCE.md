# Driving Academy acceptance record

Status legend: PASS / FAIL / PARTIAL / NOT TESTED. Automated gates and captures
identify the tested code in `evidence/v15_academy/source/head.txt`; historical
walkthrough/performance evidence is explicitly distinguished below. See
`evidence/v15_academy/PROVENANCE.md` for the original base-commit correction.

Verification at code commit `c6cb6b1`: pure **146/146**, source runtime
**1448/1448** across 13 phases, packaged runtime **1448/1448**, source and
packaged graphical quality **22/22** each, packaged graphical academy **137/137**.

## A. Asset and scene contract

| ID | Result | Evidence |
|---|---|---|
| A01 | PASS | `godot --headless --path . --editor --quit` exits 0 with all 11 academy scenes and 4 SVG icons imported; `DrivingAcademyPreview` and the course load headlessly. |
| A02 | PASS | 27 declared markers are present and unique in `GameplayMarkers`; `DrivingAcademyController.CacheMarkers` throws a named error for a missing/duplicate marker or missing signal/crossing bounds. Pure scene-contract tests check each declared name and finite directional signal dimensions. |
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
| B07 | PASS | L04 configures a visible pedestrian on school entry; runtime checks actual crossing movement, safe stationary yielding and a once-only major error for movement through the occupied crossing (including reverse). Red/red+amber prohibit entry; amber requires a safe stop unless stopping was impossible at onset. Green detours outside either signal endpoint and downstream side re-entry cannot pass the lesson. After R clears the offence, the clean L04 attempt completes without leaving the venue. |
| B08 | PASS | L05 measures ≥4 m of reverse and a 1.5 s stationary hold inside the bay within 3.2 m and 25° of the bay axis (either heading). |
| B09 | PASS | L06 requires four ordered checkpoints and warns above 20 mph (8.9408 m/s). |
| B10 | PASS | TEST01 requires lawful entry through the finite, directed authored signal segment and bounded forward zebra-crossing traversal before parking. Green signal detours on both sides (including just beyond the endpoints), downstream side re-entry and reverse traversal cannot latch signal entry or earn a licence after subsequent crossing/parking. Red/red+amber and avoidable amber remain blocked; lawful green/unavoidable amber are accepted. R clears major/minor errors, pending speeding warnings, timers and checkpoint history without changing enrolment, wallet, completed lessons or test-attempt count. Real R input is tested after offences; the fresh attempt can earn the licence without exiting. Three minor notes and zero major errors are allowed. |
| B11 | PASS | A pass issues the licence and one 5-coin gift; practice and reload cannot duplicate either (runtime persistence assertion). |
| B12 | PASS | 18 screenshots (9 categories × 720p/1080p) show school, HUD, lesson, road rules, coloured crossing pedestrian, parking, exam and licence; camera-latch regressions stay green. Captures use developer state setup, not independent gameplay completion. |
| B13 | PASS | Return is safe before enrolment, mid-lesson, after failure and after success; a mid-lesson exit saves `AcademyActiveStage` and the school offers Resume. |

## C. Persistence and architecture

| ID | Result | Evidence |
|---|---|---|
| C01 | PASS | Schema 5; v1–v4 fixtures migrate to 5 with the legacy permit. Newer-save protection, backups and corrupt-file recovery remain green. |
| C02 | PASS | v1–v4 receive `LegacyVanPermit` only; a fresh v5 save starts with neither permit nor licence and the regular van is gated until the licence is earned. |
| C03 | PARTIAL | All 24 missions and 3 city jobs pass their dedicated runtime phases (base, chapter, finale, city) with the academy code present. The historical pre-review graphical walkthrough earned the licence under the old defective rules, completed M01–M15, then stopped at M16 on a walkthrough NPC-approach/route-planner limitation. A corrected-code real-input academy/campaign walkthrough has not been repeated. Not a human playthrough. |
| C04 | PASS | Mid-lesson save/exit and Resume restore the physical stage; no academy coordinates enter town/city snapshots. |
| C05 | PASS | `GameSession.ReplaceData` rebinds village, city and academy to the same `GameData` (unit assertion), so Continue and New Game cannot leave a stale service. |
| C06 | PASS | Pure tests cover stop duration, parking distance/heading, mph conversion, every light transition, red-line edge cases, grading boundaries, retries and reward idempotency. |
| C07 | PASS | Dedicated `DrivingAcademyController`, `DrivingAcademyService`, `DrivingAcademyRules`, `DrivingAcademyCatalog`, `DrivingAcademyUi`, `DrivingAcademyHud`; `Main` only composes. |

## D. Mandatory P3 fixes

| ID | Result | Evidence |
|---|---|---|
| P3-A1 | PASS | CI runs the graphical quality gate on the source build under Xvfb with disposable XDG dirs, and on the exported binary under Xvfb. |
| P3-A2 | PASS | The exported Linux binary passes the packaged graphical academy gate (137 assertions including pedestrian checks, lateral signal bypasses, directional entry, full R restart and zebra-crossing bypass regressions). |
| P3-A3 | PASS | Every packaged run uses disposable XDG data/config/cache; `packaged/hash.txt` records the SHA-256 of the exported binary; `packaged/head.txt` identifies the tested code. |
| P3-B1 | PASS | C02 crate availability requires an active C02, an open road, an unfinished C02 and an unconsumed crate. |
| P3-B2 | PASS | Consumed, switched-away and completed crates lose E focus; restoring C02 regains it. Covered by runtime and graphical quality checks. |

## E. Quality and release

| ID | Result | Evidence |
|---|---|---|
| E01 | PASS | Pure, 13 source phases, export, 13 packaged phases, source graphical quality and packaged graphical quality/academy all run at this head and exit 0. |
| E02 | PASS | Camera latch, 60 shop open/close cycles, city kerbs, roadworks, city cars and recovery remain green in the 22-check graphical gate. |
| E03 | PARTIAL | Academy isolation is proven by the 30-cycle venue test and the runtime academy phase. The full 24-mission graphical walkthrough is incomplete at M16 (see C03); this is reported, not claimed. |
| E04 | PARTIAL | Historical pre-review same-hardware 720p/1080p p50/p95/max, draw calls and primitives remain in `performance.log`. The 230 ms academy-entry maximum is a one-time course load, reported as-is; software Xvfb numbers are labelled separately. Performance has not been remeasured after these safety fixes. |
| E05 | NOT TESTED | Windows build/pure tests run in CI; Windows gameplay startup could not be tested here. |
| E06 | PASS | 18 screenshots in `evidence/v15_academy/screenshots/`, captured from code commit `c6cb6b1` at both resolutions. |
| E07 | PASS | PR #35 opened against the verified base; the code head, CI state and any discrepancy are recorded in `PROGRESS.md` and on the PR. No merge, tag or release. |
