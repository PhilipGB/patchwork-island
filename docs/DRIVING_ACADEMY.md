# Driving Academy (v1.5.0)

## Player flow

1. The driving school stands at world `(32, 48)` on the open south lawn of the
   authored west island, scaled to `0.75` so its footprint clears the island
   edge and every existing prop. Its door and `EntranceApproach` face north
   toward the village. The three numbered cones sit in an authored yard west of
   the office, three metres apart so E focus is unambiguous.
2. Enrolment costs **15 coins once**. Lessons, practice and retries are free.
3. A player with fewer coins sorts the physical cones in order 1 → 2 → 3.
   Each completed round pays **3 coins** and is saved. A wrong cone resets the
   round without charge.
4. Six lessons use the isolated authored course. The learner car is the supplied
   `TrainingCar_Visual.tscn` under the existing `VanController` physics. While a
   stage is active, a lesson HUD in the screen's top-right shows the stage
   title, step counter, plain-language hint, a "Next marker: N m direction"
   wayfinding line to the current target marker, live speed and the signal
   state. It is placed there so the traffic-signal head stays visible from the
   learner's stop-line view.
5. The final assessment allows **three minor notes and zero major errors**.
   Passing issues the official licence and a one-time **5-coin** graduation
   gift. The school stays open for free practice.

## Licence rule

- **New v5 saves** require an earned `DrivingLicenceIssued` before the regular
  delivery van takes E focus. The learner car is available after enrolment.
- **v1–v4 saves** migrate with `LegacyVanPermit = true`. This keeps existing
  campaigns playable but is *not* an official licence and does not qualify for
  future professional licence-gated work.
- The licence card explicitly says it is a game certificate, not a real-world
  driving qualification.

## Course and rules

- `scenes/academy/DrivingAcademyCourse.tscn` is a 224 × 196 m standalone venue.
  It is mounted at world `(2000, 0, 0)` only while training and freed on return.
- The 27 marker contract is declared in `data/driving_academy_lessons.json`.
  Missing markers are a hard, named failure.
- Rules use spatial/velocity predicates, not keypress completion:
  - L01: reach 2 m/s, then hold below 0.3 m/s for 1.5 s in the stop zone.
  - L02: visit three gates in order.
  - L03: stop before the line for 1.5 s, then release.
   - L04: obey red/red+amber, stop on amber when safe, and yield to the zebra crossing.
  - L05: reverse at least 4 m and hold inside the bay within 25° of its axis.
  - L06: four checkpoints in order; the 20 mph sign is monitored.
  - TEST01: stop line, signal, crossing and parking with minor/major scoring.
- 20 mph = 8.9408 m/s. Warnings never gate licence on speed or time.
- A red-light offence is not raised when the car was already past the line when
  the signal turned red.
- Amber entry is lawful only when safe stopping was impossible at amber onset:
  one second of reaction travel plus braking at the learner car's 11 m/s².
  Accelerating towards the line after amber starts cannot create an exception.
- The pedestrian is configured after selecting L04/TEST01, restored on reset,
  and moves across the lane in crossing-marker local space during the first
   3.5 s of every green phase, waiting at the kerb otherwise.
- TEST01 requires a lawful signal entry and forward traversal of the authored
  crossing rectangle (marker-local half-width 6 m, half-length 3 m). A side
  detour, reverse traversal or side exit cannot satisfy the crossing checkpoint.
- Signal markers author an 11 m stop-line segment and a 16 m approach in their
  local frame. Entry requires movement from the bounded approach through the
  segment towards marker-local -Z; a lateral detour or downstream side re-entry
  cannot record a lawful signal entry. Amber stopping distance uses that same
  directed frame.
- R and the pause menu's **Restart attempt** action start a fresh attempt in the
  current lesson, clearing major/minor errors, pending speed warnings, signal and
  crossing history, timers and checkpoints. Enrolment, wallet, completed lessons
  and licence ownership are unchanged; restart is free.

## Persistence

Schema **5** stores enrolment, completed lessons, active stage, test/licence
state, the one-time graduation reward, helper progress/rounds, test attempts and
`LegacyVanPermit`. The academy is bound through `GameSession`, so Continue, New
Game and recovery cannot leave a stale service. Academy-local actor positions
are never written into the town/city player or van snapshots; Continue returns
to the school and offers Resume.

## Code map

| Area | File |
|---|---|
| Lesson contract | `src/Core/DrivingAcademyCatalog.cs` |
| Deterministic rules | `src/Core/DrivingAcademyRules.cs` |
| Economy/progression | `src/Core/DrivingAcademyService.cs` |
| Venue, car, rules runtime | `src/Gameplay/DrivingAcademyController.cs` |
| School/licence UI | `src/UI/DrivingAcademyUi.cs` |
| Lesson HUD | `src/UI/DrivingAcademyHud.cs` |
| Authored venue | `scenes/academy/` |
| Icons | `ui/academy/icons/` plus `assets/icons/` mirrors |
| Lesson data | `data/driving_academy_lessons.json` |
