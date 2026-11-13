# Gameplay architecture

This is a scene-based Godot game with a pure C# domain. It uses a **Facade** at
the session boundary and composition-based activity controllers and presenters.
There is no service locator, dependency-injection container, ECS or mission DSL.

## Boundaries and ownership

- `Main` is the composition root and session/control-mode coordinator. It creates
  the authored world, actors, builders and collaborators; handles input modes,
  save feedback, title/pause transitions and cross-module completion effects.
- `GameSession` is a stable facade over the current `GameData`, village, city
  and driving-academy services. `ReplaceData` constructs the complete replacement
  service graph before publishing it. Controllers retain the facade, not a mission
  service or data instance that can become stale after Continue/New Game.
- `MissionService` retains prerequisites, mission switching and exactly-once
  reward authority. It delegates community and finale activities to
  `CommunityMissionTasks` and `FinaleMissionTasks`. Compatibility methods retain
  the previous API, while bakery/library/garden/sign/repair/shop views depend on
  their narrower activity handler. Only the central completion path pays coins.
- `CityGameplayController`, `VillageStationController`, `PickupController`,
  `VillagePopulationController`, `RaceController` and
  `DrivingAcademyController` own distinct interaction, presentation and activity
  lifecycles. Their small `IGameplayHost` port requests
  persistence, objective updates or completion effects from the coordinator.
  These controllers do not reference `Main` or use its private members.
- `DrivingAcademyController` owns the isolated authored venue, learner-car
  lifecycle, lesson predicates, traffic-light/crossing state and safe return.
  `DrivingAcademyService` and `DrivingAcademyRules` are pure and persist through
  `GameSession`; `DrivingAcademyUi`/`DrivingAcademyHud` only present state.
- `MissionHudPresenter` converts current model/world state into mission rows and
  a destination. It reads the live session and receives the few changing
  physical-state providers it needs. Godot `Hud` owns the actual controls.
- `WorldSceneAdapter`/`CityWorldAdapter` own authored geometry and visibility;
  `CityActorPool` owns bounded actor selection. Vehicle/pedestrian visuals and
  the palette now live in their own files, not inside the actor-pool file.
- Godot nodes remain owned by the scene tree. The two shop UI controllers are
  ordinary C# objects whose controls belong to the HUD modal. Camera/step-query
  helpers explicitly release their native references on tree exit.

## Why this pattern

The expensive reliability bug to avoid is a partly rebound session: the HUD can
show an open road while an old service still sees a closed road. A session
facade makes the replacement operation explicit and gives every activity one
current source of state. Small activity controllers also allow mission UI,
world presentation and persistence policy to change independently.

Composition keeps Godot physics/input classes concrete and straightforward.
Interfaces are used at the actual orchestration boundary, rather than added to
every class. The existing pure-rule tests and real-scene integration tests
remain the contracts for the extracted code.

The main SOLID improvement is single responsibility: session replacement,
activity rules, world interactions and HUD presentation have distinct owners.
The host interface inverts the controllers' dependency on the coordinator, and
activity-specific UI depends on narrower handlers. Mission dispatch still uses
explicit known IDs; adding a mission requires code and content changes. A
plug-in mission framework would add complexity without a current requirement.

## Length and SOLID review

- `Main.cs`: approximately 2,160 lines, down from 6,062. Activity logic, race
  state, mission HUD presentation, captures and test fixtures are separate
  collaborators. The remaining coordinator still handles several session
  transitions; further extraction should follow new behaviour, not an arbitrary
  line limit.
- `MissionService.cs`: approximately 500 lines, down from 1,128, with focused
  activity handlers of roughly 200 and 340 lines. Mission metadata/results and
  objective rules are separate domain files.
- `CityActorPool.cs`: approximately 230 lines, down from 661. Its previous four
  independent types are separate files.
- `SaveValidation` remains relatively long because it is the cohesive schema
  validation boundary, with declarative counter bounds and ordered structural
  checks. Splitting those tables into unrelated services would add indirection
  without reducing policy coupling. Save compatibility and write protection are
  preserved.
- `Hud` remains a concrete view composing status, modal and build controls.
  Mission decisions have moved to presenters/controllers. Its children are
  scene-owned; modal callbacks retain the existing pause/focus contract.
- The 4,200-line pure test runner and 2,600-line runtime fixture are organised by
  feature/CLI phase. Diagnostic partial files are intentionally adapters around
  one fixture, not the production decomposition pattern. Aggregate results,
  ordering and cross-reload phase state remain unchanged.

## Diagnostics

`Main.RuntimeProbe` is an internal adapter for deterministic integration
fixtures. It can set up states and teleport actors; production controllers never
call it. `DeveloperTools` contains opt-in capture/profiling scenarios.

`GameplayWalkthrough` is different: it reads positions and progress through the
adapter, but changes gameplay only via movement actions, E, menu buttons and
world clicks. It never seeds mission counters, grants money or teleports. Its
save/exit checkpoints deliberately exercise normal Continue. Use a disposable
XDG data directory. Fixed-rate/no-draw replays are functional checks and are not
visual or performance measurements.
