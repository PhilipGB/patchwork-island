# M11–M20: second chapter specification

## Core rules

- Exactly ten NEW missions with IDs **M11–M20**; keep M01–M10 unchanged and completed on existing saves.
- **M11 unlock:** all of M01–M10 must be completed, not merely M10 (M05/M06/M09 can complete out of order in existing data). M12 and M13 unlock after M11; M14 after M12; M15 after M13; M16 after both M14 and M15; M17→M18→M19→M20 form a final progression.
- Missions may be switched, but their progress remains stable. Only active mission interactions advance counters. Wrong answers never consume irreplaceable items or lose personal coins. Completion pays a one-time **personal wallet reward**, while mission shopping/ingredients use separately supplied stock or job budget.
- A player with zero coins (spent in P9 clothes shop) must be able to finish every new job. Big labels, limited steps and three accessible hints per job. Provide visual indicators in addition to sounds.
- New content is **not** solved by a one-click `Complete` interaction; complete real gameplay actions and objective verification.
- `assets/content/missions_M11_M20.extension.json` supplies machine-readable metadata and mechanics descriptions. Adapt runtime rules and ensure authored JSON and code are consistent. Do not pretend the JSON mechanics execute themselves.

| ID | Title | Reward | Prerequisites | Learning | Gameplay |
|---|---|---:|---|---|---|
| M11 | Meet the Neighbours | 12 coins | All M01–M10 | Read names, matching, count to 3 | Talk to Ada, Ollie and Fern; hand each correct named greeting card |
| M12 | Bakery Busy Day | 14 | M11 | Count and add orders | Fill three customer bread/cake orders with correct quantities |
| M13 | Lost Library Books | 14 | M11 | Read labels, categorise | Collect four labelled books; match SUN/MOON/TREE/STAR shelves |
| M14 | Road Sign Rescue | 16 | M12 | Direction and position | Install MARKET, WORKSHOP and GREEN signs in correct sockets/orientations |
| M15 | Garden Patterns | 16 | M13 | Sequences, pairs | Plant six flowers in alternating RED/BLUE order |
| M16 | The Delivery Round | 20 | M14, M15 | Read destinations, plan route | Drive parcels to Ada, Pip and Ivy; wrong recipient does not consume parcel |
| M17 | Market Day Maths | 22 | M16 | 6+4+4, change from 20 | Use a 20-job-coin purse to buy 2 bread (3), 1 milk (4), 2 apples (2): cost 14, change 6 |
| M18 | Mechanic’s Toolbox | 24 | M17 | 2+1+1, order | Inspect Pip’s vehicle, fit 2 tyres, 1 lamp, then battery |
| M19 | The Grand Rally | 25 | M18 | Number order, route, time | Complete 6-gate long course alongside Pip; no hard time limit; optional bonus only |
| M20 | Village Festival | 30 | M19 | Multi-step plan, 3+2+3 | Invite 3 villagers, place 2 tables, hang 3 lanterns; trigger second celebration |

## Implementation per mission

### M11: Meet the Neighbours

Start at job board; receive three labelled cards. Use E at the three named NPCs, with visible names/portraits and readable card names. A wrong card gets a hint without consuming it. When 3/3 correct handovers, award once. Record recipient IDs rather than a single increment-only counter (or validate a bounded unique mask). Completion unlocks the bakery/library branch.

### M12: Bakery Busy Day

Ada presents three order cards. Each contains small numbers of bread and cakes, with large pictograms. Player selects quantities using plus/minus buttons, can correct errors. Three completed correct orders trigger a permanently filled bakery display. Items come from mission stock, not wallet. Track orders by ID; prevent submission twice.

### M13: Lost Library Books

Four physical books in reachable public areas; each carries a word on the cover. Bring to Ollie’s shelf and match exactly. Incorrect shelf offers a hint, no loss. Completion fills the library shelf visual. Do not reuse apple pickup logic with cross-mission IDs.

### M14: Road Sign Rescue

Three signs with large text and arrows. Place at dedicated road sign sockets in intended orientation. Wrong sign/orientation is reversible. Ensure visual markers never physically block NPC cars or van. World-visible repaired signs persist independently of active mission.

### M15: Garden Patterns

Six garden pads (separate from original M08 park pads) using `RED BLUE RED BLUE RED BLUE`. Player sees first two examples, fills rest with unlimited mission seed stock. Wrong colour reversible. Distinct from M08 so completed original park placements remain valid. Persist unique pad selections.

### M16: The Delivery Round

Three addressed parcels: Ada, Pip and Ivy. Player uses the van; recipient interaction within reasonable radius and after stopping safely. Prompt and job board show remaining recipients. No time limit. Wrong destination retains parcel. NPC traffic must not trap the van or player. Record delivered recipient set and parcel custody.

### M17: Market Day Maths

A **separate** budget of 20 coins, not the player’s wallet. Shopping list: bread 2×3=6, milk 1×4=4, apple 2×2=4 → total 14, change 6. Use normal visible checkout and allow mistake correction/refunds before finalising. Player’s personal coins never decline; they earn 22 on valid completion.

### M18: Mechanic’s Toolbox

Inspect the stalled NPC vehicle; find four visible parts in a workshop tool area. Apply two tyres, one lamp, then battery. Wrong stage gives hint without spending the item. Once repaired, switch Pip’s car from disabled to working. Define a stable scripted vehicle state that survives reload and does not block driving routes.

### M19: The Grand Rally

A separate **six-checkpoint** race on the larger east-island loop. Proper lap start/finish, gate order, robust duplicate-gate prevention, pause-safe timekeeping and save/reload. Pip’s NPC is a friendly competitor. Player need only finish; beating Pip is an optional medal. Suggested bonus <=155 seconds **requires actual driving balance verification**. The original M05 (<=90) and M09 (<=75) rules and their existing four-gate progress remain intact. Preserve long-race saturation and do not award unearned speed bonuses.

### M20: Village Festival

Three-stage preparation: send three named invites, place two tables, hang three lanterns. Use mission supply inventory and dedicated festival pads; all operations must be repeatable/undoable until accepted. Completion awards once, turns on second-chapter decorations, starts festival music if music enabled, and records a **new** finale marker. Preserve existing `CelebrationSeen` marker for the M10 finale. If returning to free roam after completion, the festival remains visible but must not constantly reopen a modal.

## Learning and inclusivity

A five-year-old can complete each by reading short labels and observing pictograms with optional hints; older children benefit from planning, reading multi-part orders and more complex routes. Avoid deliberately difficult prose and use forgiving distances/checkpoints. Labels and colours must not be the only differentiators: combine text, shape and icon for colour-vision accessibility.
