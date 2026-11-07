# First playable acceptance contract

**Rule:** Do not mark an item PASS unless exercised with a real game build or an appropriate automated test. Distinguish `PASS`, `FAIL`, `NOT TESTED (environment)` and `NOT IMPLEMENTED` in `PROGRESS.md`. Screenshot reference images are NOT in-game evidence.

## A. Boot / controls / visual

- [ ] Fresh clone / unpack, exact engine and .NET prerequisites documented; `dotnet build` passes.
- [ ] Godot editor imports and main scene launches without missing-script, missing-resource or node-path errors.
- [ ] 3D village shows five distinguishable buildings, roads, river/bridge, trees, player and van (not an empty floor).
- [ ] Avatar walks with WASD and arrows; Space jumps; camera auto-follows; mouse orbit works when used.
- [ ] E picks up, interacts and enters/exits van in context. Clear target highlight and prompt; no precise crosshair aiming.
- [ ] Esc pause/cancel returns control correctly, cursor available in menus; keyboard focus and buttons work.
- [ ] Van can drive, steer, reverse, stop, exit and recover if stuck; no loss of access to missions.
- [ ] Accessible at 1280×720: readable mission text, coin count, interaction prompts, signs and shop prices; UI scales to 1080p.

## B. Complete game: clean-save walkthrough

- [ ] M01: collect exactly 3 apples, deliver to labelled RED HOUSE, receive 10 coins and complete state.
- [ ] M02: collect 5 wood and 3 stone and submit at workshop; total/count feedback; receive 12 coins.
- [ ] M03: after M02, correctly place 3 supports and 4 planks; incomplete bridge blocks crossing and completed bridge permits actual crossing; receive 20 coins.
- [ ] M04: buy 2 bread @3 and 1 milk @4 with dedicated 12-coin budget; UI shows total 10 and change 2; deliver to town hall; receive 12 coins.
- [ ] M05: drive through four ordered checkpoints, complete even if slowly; receive 15 coins.
- [ ] M06: deliver two blue parcels to Red House and one orange parcel to Workshop; correct destinations verified; receive 15 coins.
- [ ] M07: assemble 2 swing seats and 4 ropes and visibly repair playground; receive 18 coins.
- [ ] M08: place bench (6) + flower bed (4) + lamp (9) within 20-coin task budget; running total 19, 1 left, objects visible; receive 20 coins.
- [ ] M09: complete replay course regardless of optional 75-second time target; optional bonus only if earned; receive 15 coins.
- [ ] M10: decorate house with table (8) + 2 chairs (5 each) + plant (3), total 21; visibly furnished home; receive 20 coins.
- [ ] Every mission unlocks only when prerequisites are met. When all 10 complete, village celebration and free-roam/replay remain available.
- [ ] Completion reward cannot be claimed twice by talking to station again, reloading, or revisiting job board.

## C. Education / error handling

- [ ] No mandatory maths text entry or long dialogue; learning proved by actual actions and quantities.
- [ ] Short readable instructions, icons and labels; correct answer never required to be spoken or typed.
- [ ] Each mission has layered hints and an active location marker, without a vague 'figure it out' softlock.
- [ ] Unwanted/wrong purchases or placements can be cancelled/undone without irreversibly exhausting a task budget.
- [ ] Fallen player and stuck car recover to safe position. Lost quest resource can be recollected.
- [ ] No combat, death, forced timed fail, money penalties or login needed.

## D. Persistence and data integrity

- [ ] New Game / Continue works and doesn't destroy save without confirmation.
- [ ] Save during M03 (partly built bridge), reload, and observe the same slots filled and quest progress intact.
- [ ] Complete M03, restart and drive/walk across intact bridge. Persist the *collider*, not only visible material.
- [ ] Save during M08 and M10, reload with chosen objects still correctly placed.
- [ ] Inventory, player/vehicle position, coins, unlocked cosmetics, completed and active missions persist.
- [ ] A missing, invalid or old-version save is handled safely with appropriate user feedback and a recoverable start.

## E. Delivery and testing evidence

- [ ] Pure state/logic tests cover: mission prerequisites; duplicate claim prevention; grocery cost and change; park budget; house subtotal; ordered checkpoints; snap-slot validation; serialisation round trip; corrupted save.
- [ ] Headless project import and startup smoke verification recorded with exact commands and exit codes.
- [ ] Real graphical gameplay walkthrough performed, or explicitly marked NOT TESTED with exact limitation.
- [ ] README_RUN.md provides setup, running, controls, testing, export, save path, and known issues.
- [ ] Windows x64 export built and launched where the environment allows, otherwise documented unambiguously.
- [ ] Source and working assets present; no missing proprietary downloads or runtime remote resources.

## Release severity

**BLOCKER:** game cannot boot; main controls broken; one of ten missions cannot complete; bridge doesn't open; save corrupts play; players can get irreversibly stuck; references mistakenly substituted for real UI/3D game.

**SHOULD FIX:** unreadable UI, confusing wayfinding, van hard to control, educational step cannot be understood without adult instruction, invalid physics, severe frame-rate issues.

**OPTIONAL:** fancy VFX, animated residents, musical soundtrack, beautiful interiors, decorative harbour, additional races.

## Manual test scenario

Fresh save → M01 apples → M02 resources → M03 bridge → M04 groceries → M05 checkpoint drive → M06 two-stop delivery → M07 swings → M08 park spending → M09 optional trial → M10 home furniture → restart → verify structures/missions/coins → enter free roam. At least once intentionally trigger: bad purchase, wrong build slot, fall in water, van reset and pause during job. Record actual observations.
