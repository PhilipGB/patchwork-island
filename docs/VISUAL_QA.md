# Visual QA — v1.2.0

All captures in this document are real Godot viewport captures, not mockups.
The environment is Godot 4.7.2 .NET under Xvfb with Mesa llvmpipe; a real Radeon
890M desktop GPU was **not available** in this environment. Performance numbers
below are software-renderer numbers and must not be read as desktop FPS claims.

## Capture commands

```bash
# 1280x720
xvfb-run -a -s "-screen 0 1280x720x24" \
  godot --path . --resolution 1280x720 -- --dev --capture-p12

# 1920x1080 true viewport
xvfb-run -a -s "-screen 0 1920x1080x24" \
  godot --path . --resolution 1920x1080 -- --dev --capture-p12 --capture-1080

# chapter-two content, 720p and true 1080p
xvfb-run -a -s "-screen 0 1280x720x24" \
  godot --path . --resolution 1280x720 -- --capture-p13
xvfb-run -a -s "-screen 0 1920x1080x24" \
  godot --path . --resolution 1920x1080 -- --capture-p13 --capture-1080

# finale content, 720p and true 1080p
xvfb-run -a -s "-screen 0 1280x720x24" \
  godot --path . --resolution 1280x720 -- --capture-p14
xvfb-run -a -s "-screen 0 1920x1080x24" \
  godot --path . --resolution 1920x1080 -- --capture-p14 --capture-1080

# software-renderer frame probe
xvfb-run -a -s "-screen 0 1280x720x24" godot --path . --resolution 1280x720 -- --dev --perf-probe
xvfb-run -a -s "-screen 0 1920x1080x24" godot --path . --resolution 1920x1080 -- --dev --perf-probe --perf-1080
```

## Reproducible defect log

| ID | Defect | Reproduction | Root cause | Fix | Before evidence | After evidence |
|---|---|---|---|---|---|---|
| V01 | Large dark triangular patches and moving cloud-shaped smudges across roads/grass | Load title/free-roam at 720p | Decorative cloud meshes cast real-time directional shadows | Cloud puffs and the ocean skirt no longer cast shadows | `evidence/p2_01_start.png`, `evidence/p2_02_village_overview.png`, `evidence/p9_05_girl_sunny_outfit_world.png` | `evidence/p12_720_00_village_overview.png`, `evidence/p12_720_08_town_traffic.png` |
| V02 | Rally track made from overlapping grey rectangles, hard seams, sign lying across tarmac, paddock fence through the course | Start `--scenario-m09` or inspect meadow | Five independent `AddRoadSegment` boxes plus unrelated fence/sign placement | One closed Catmull-Rom road mesh with welded shoulder, collision mesh, relocated signage/grandstand, removed paddock fence | `evidence/p12_before_old_rally.png` (baseline `p1_07_race_meadow.png`) | `evidence/p12_720_01_rally_overview.png`, `evidence/p12_720_02_start_line.png`, `evidence/p12_720_05_road_join.png` |
| V03 | Comb/ladder stripes and thin grass cracks along the rally shoulder (intermediate P12 pass) | Fly over first continuous-mesh version | Every segment computed its own tangent, so shared-edge vertices did not coincide | Shared miter normal per centreline sample; single-colour shoulder | intermediate capture observed during P12, not committed | `evidence/p12_720_01_rally_overview.png`, `evidence/p12_1080_01_rally_overview.png` |
| V04 | Village road intersections show coplanar Z-fighting and dirty seams | Overview from high camera | All road slabs and markings shared one top height | Sub-millimetre slab offsets; markings raised to a common proud plane; clothes path raised | `evidence/p12_before_village_roads.png` (baseline `p2_02_village_overview.png`) | `evidence/p12_720_00_village_overview.png`, `evidence/p12_720_08_town_traffic.png` |
| V05 | Checkpoint number signs read blank or edge-on; road arrows looked like a Y | Drive to gate 1/gate 2 | Number labels were single static faces; arrow was three crossing boxes | Billboard number label with no-depth-test over both faces; proper triangular arrowhead | first P12 pass observed during capture, not committed | `evidence/p12_720_03_gate1.png`, `evidence/p12_720_04_gate4.png`, `evidence/p12_1080_03_gate1.png` |
| V06 | Large blank sign backs floated over roads | Look back along main road | Sign boards had text only on the front face | Every authored sign now has a correctly rotated back-face label | first P12 pass observed during capture, not committed | `evidence/p12_720_08_town_traffic.png`, `evidence/p12_1080_08_town_traffic.png` |
| V07 | New chapter-two mission icons floated as 3–4 m blobs over villagers, books and signs | Inspect bakery/library/world marker at 720p | `Sprite3D.PixelSize` assumed the original 48 px SVG set; the supplied mission PNGs are 128 px | `Geo.SignBoard`, `Geo.IconSprite` and `WorldMarker.ShowAt` normalise world icon size by texture width | first P13 capture pass observed during capture, not committed | `evidence/p13_720_01_bakery_stall.png`, `evidence/p13_720_02_library_shelf.png`, `evidence/p13_1080_01_bakery_stall.png` |
| V08 | Generic library icon covered the large MOON/SUN book cover word | Walk to the MOON book pickup | Book pickups rendered both a 3D cover word and a generic library icon at the same height | Books keep their readable cover word and skip the redundant generic pickup icon | first P13 capture pass observed during capture, not committed | `evidence/p13_720_05_book_pickup.png`, `evidence/p13_1080_05_book_pickup.png` |
| V09 | Van could catch at the bridge/road seam; player could catch at the shoulder edge near gate 1 | Slow bridge crossing; walk from grass to the number 1 arch and jump | Rally shoulder/connector collision was a raised flat edge with no approach slope | Added sloped collision skirts around the rally shoulders and a ramp at the bridge-to-connector seam; traversal harness starts from rest at the lip and tests low-speed van crossing plus jumping at both gate-1 arches | playtest report; pre-fix character-body catch was not captured on video | `evidence/p14_720_13_bridge_approach.png`, `evidence/p14_720_14_gate_one.png`, `evidence/p14_1080_13_bridge_approach.png` |
| V10 | Delivery board and post station were placed inside the town-hall shell | Complete M16 and look for the delivery board | Board/station offsets used the post counter's +Z side, which is inside the building | Moved the post station and delivery board to the open south-east approach; capture now shows the board from the road | first P14 capture pass observed during capture, not committed | `evidence/p14_720_02_delivery_board.png`, `evidence/p14_1080_02_delivery_board.png` |
| V11 | Hand-placed tree canopy covered Pip's broken car | Stand at the workshop repair bay | Existing tree at `(31,0,-9)` was not covered by the new keep-clear rectangle | Moved the tree to `(33,0,-28)`; broken/repaired car silhouettes are readable | first P14 capture pass observed during capture, not committed | `evidence/p14_720_06_broken_pip_car.png`, `evidence/p14_720_07_repaired_pip_car.png` |
| V12 | Market checkout's Pay button sat below the fold at 720p | Open Market Day Maths at 1280×720 | Three two-line item rows plus footer exceeded the modal body | Compacted item rows to one line each and removed the duplicate footer; Pay and Step away are visible | first P14 capture pass observed during capture, not committed | `evidence/p14_720_03_market_maths_ui.png`, `evidence/p14_1080_03_market_maths_ui.png` |
| V13 | Grey slab and a raised lip at the bridge-to-road seam; bank boulder sat on the deck | Drive east across the M03 bridge | East road box began at x=49, 1.45 m inside the bridge deck, and the rally connector ramp started at x=48.1; both painted over the wooden planks. A bank rock was authored at (40.1, 0.55, 0) on the deck. | East road now starts at x=50.5; connector starts at x=50.4 with its ramp removed and collision/visuals nearly flush; entrance discs removed; rocks kept ≥6 m from the bridge; side rails pulled back to the water span; dark underlay closes plank gaps. | user playtest screenshots 2026-11-09 23:20 | `evidence/p14_720_15_bridge_seam.png`, `evidence/p14_720_16_bridge_junction.png`, `evidence/p14_1080_15_bridge_seam.png`, `evidence/p14_1080_16_bridge_junction.png` |
| V14 | Rally connector read as a floating grey wedge at the bridge branch | Look east from the bridge exit | Connector had a road surface but no shoulder strips, unlike the rally loop | Added 0.8 m sandy shoulders on both connector edges and kept the branch clear of the rail ends | same P15.1 capture pass | `evidence/p14_720_16_bridge_junction.png`, `evidence/p14_1080_16_bridge_junction.png` |
| V15 | Blue cap appeared to hover above the long-hair block | View the girl/Berry character from behind | Cap cylinder sat above a 1 cm exposed strip of head skin; long-hair back stopped below the cap underside | Raised the long-hair back to meet the cap, lowered the cap/brim and shortened the top hair box | user close-up screenshot 2026-11-09 15:24 | `evidence/p14_720_17_player_cap.png`, `evidence/p14_1080_17_player_cap.png` |

## Continuous-road and driving verification

- `RallyCourse` is a closed Catmull-Rom centreline with 13 control points and a
  sampled length of about 206 m. Pure test
  `rally centreline does not self-overlap its own road width` rejects any
  non-local approach closer than the 8 m road width.
- `RallyRoadBuilder` samples 28 steps per span (364 samples) into one road mesh,
  two joined shoulder strips, edge lines, centre dashes, a connected bridge spur
  and one concave collision shape. No overlapping road rectangles are emitted.
- Runtime collision probes hit the raised road plane on 51 of 52 exact
  centreline samples; the one low reading is a physics ray exactly on a
  Catmull-Rom knot edge. A continuous autopilot drive of the real van physics
  completes both four-gate races, so the surface is traversable end to end.
- `--selftest-rally` drove M05 in **18.98 s** and M09 in **19.03 s** with the
  genuine van controller and collision, reaching all four gates in order and
  earning the existing optional bonuses. No hard time limit was introduced.
- Gates 1–4 and their saved checkpoint semantics are unchanged; only their
  world positions moved. A part-completed M05 save (`checkpoint=2`) round-trips
  with its active mission and timer intact.

## UI captures

`evidence/p12_720_09_job_board.png` and
`evidence/p12_1080_09_job_board.png` show the 10-job board before the v1.2
chapter tabs are added.

`evidence/p13_720_06_job_board_chapter_two.png` and the 1080 counterpart show
the real twenty-job board with `Village jobs 1–10` / `New adventures 11–20`
tabs, locked rows and the current job. The P13 UI set also proves the real Ada
card dialogue, bakery order steppers, library shelf matcher, road-sign
install/remove controls and garden colour correction. P13's runtime
`--selftest-chapter` presses those same named controls; it does not call a
hidden completion shortcut.

## Chapter-two world captures

`evidence/p13_720_00_village_chapter_two.png` through
`evidence/p13_720_05_book_pickup.png` (and the 1080 set) show the new bakery
stall, library shelf, three sign sockets, six-pad community garden and labelled
book pickups. `evidence/p13_720_12_repaired_signs.png` and
`evidence/p13_720_13_garden_complete.png` show the persistent completed-world
visuals. P13 intentionally captures M16–M20 as **COMING SOON**; their world
content and evidence belong to P14.

## P14 finale captures

`evidence/p14_720_*.png` and `evidence/p14_1080_*.png` show the M16 delivery
dialogue/post counter/board, M17 checkout and market stock, M18 repair bench
with broken and repaired Pip car, the six-gate grand rally, M20 invitations,
tables/lanterns, completed festival decoration, the all-20 job board and the
second `VILLAGE FESTIVAL!` finale. `p14_*_13_bridge_approach.png` records the
bridge/connector approach used by the traversal regression, and
`p14_*_14_gate_one.png` shows the number 1 arch where the playtest reported a
stuck player. The gate's only nearby static body is the camera-only blocker on
layer 16; the player capsule uses layers 1 and 3, so the fix targets the road
shoulder collision rather than the arch visual.

P15.1 re-captured the whole set after the bridge hotfix. The additional
`p14_*_15_bridge_seam.png` and `p14_*_16_bridge_junction.png` show the road
meeting the planks without a grey overlay, the connector with sandy shoulders,
the rails ending before the road, and no boulder on the deck.
`p14_*_17_player_cap.png` shows the cap seated on the long hair from behind.

## Software-renderer measurements (not a desktop-GPU claim)

| Resolution | Mean frame | p95 frame | Max frame | Nodes | Draw calls | Primitives |
|---|---:|---:|---:|---:|---:|---:|
| 1280×720 | 86.25 ms | 97.15 ms | 107.45 ms | 2581 | 2997 | 60,647 |
| 1920×1080 | 97.34 ms | 106.84 ms | 121.95 ms | 2581 | 2997 | 60,647 |

The v1.1.0 baseline did not include this probe, so a baseline frame-time
comparison is **NOT TESTED**. The relative scene cost is recorded for later
optimisation. Real Radeon 890M profiling is recorded in `ACCEPTANCE.md` and
`evidence/p15_verification.txt`: 720p mean 16.69 ms / p95 17.35 ms and 1080p
mean 16.67 ms / p95 17.28 ms over 600 frames. Windows rendering remains
**NOT TESTED**.

## Remaining visual gaps

- The clothes-shop awning and mannequin silhouettes are close together at very
  low camera angles; no clipping was visible in the fixed captures.
- The large direction-post sign cluster is intentionally whimsical and can look
  edge-on from some angles, but back faces are now readable.
- Roof gable wedges are part of the flat-shaded toy style, not z-fighting.
- Real-Radeon captures are recorded; Windows rendering and player-eye comfort
  remain **NOT TESTED** in this environment.

## v1.3.0 canonical visual audit (P18)

The canonical scene was audited from 39 fixed district cameras at 1280×720,
with every static `Label3D` rendered from both sides, plus the 18-shot P14,
10-shot P12 and 14-shot P13 tours at true 1920×1080.

| ID | Defect | Root cause | Fix | Evidence |
|---|---|---|---|---|
| V16 | Freestanding sign text mirrored from the viewer side (bakery, library, jobs, playground, community garden, stone patch, race meadow, bridge works, sign sockets, direction post). | Labels were left facing away from the intended viewer; several were on the wrong side of their panel. | Added a correctly rotated front label and back label to each sign, made each label single-sided, and moved the stone-patch label to the town-facing side. | `evidence/v13_canonical_visual/07_bakery_front.png`, `09_library_front.png`, `11_job_board_front.png`, `05_playground_sign_front.png`, `20_community_garden.png`, `19_stone_patch.png`, `21_rally_signs.png`, `24_bridge_site.png`, `37_market_sign_socket.png` |
| V17 | Sign panels floated without posts (playground, community garden, stone patch, race meadow, bridge works). | Authored panels had no support geometry. | Added two wooden posts behind each wide panel and one post behind the bridge-works panel. | same set plus `13_direction_post_front.png` |
| V18 | Two overlapping BROKEN labels per swing frame; rear view mirrored. | One fixed and one billboard label occupied the same spot. | Removed the fixed duplicate; one billboard label remains per frame. | `33_swing_front.png`, `34_swing_back.png` |
| V19 | Duplicate BRIDGE WORKS sign and cones overlapped the canonical bridge site. | Legacy worksite instance remained beside the canonical `BridgeSite`. | Removed the legacy instance; kept the canonical anchor/sign/crate. | `24_bridge_site.png` |
| V20 | Lamps inside the town hall, playground sandpit, road, and across sign sightlines. | Legacy lamp coordinates were not adjusted for the authored scene collision. | Moved lamps to verges, moved the playground lamp outside the sand, moved the town-hall lamp to its front verge, removed the redundant direction-post lamp. | `31_west_top_down.png`, `32_east_top_down.png`, `13_direction_post_front.png` |
| V21 | Playground sign, direction post and bench stood in the main road. | Source coordinates came from the older, narrower road layout. | Moved the sign to the playground entrance, moved the post/bench off the carriageway, removed the redundant lamp. | `05_playground_sign_front.png`, `13_direction_post_front.png`, `30_top_down.png` |
| V22 | Bench on the rally start road. | Legacy decoration placement. | Removed the misplaced bench. | `p12_1080_02_start_line.png` |
| V23 | Gate number panels blank/edge-on from the approach. | Single small number face was on the far side of the panel. | Added larger numbers on both faces of all six gate panels. | `22_gate_one_front.png`, `23_gate_one_back.png`, `35_gate_two_front.png`, `36_gate_two_back.png` |
| V24 | `SUPPORT`/`ROPE`/`SEAT` ghost labels visible during normal play. | Build ghosts stayed visible when their build mode was closed. | `BuildSocket` now hides ghost mesh/icon/label outside build mode; runtime regression added. | `p12_1080_02_start_line.png` |

## v1.3.0 manual defect pass (V25–V35)

This pass used ordinary gameplay-height cameras and distances rather than only
fixed inspection cameras. Each item was re-rendered from the same viewpoint
after correction. Evidence is in the ignored
`evidence/v13_canonical_visual/v13.1_recheck/` directory.

| ID | Defect | Root cause | Fix | Evidence |
|---|---|---|---|---|
| V25 | `BRIDGE WORKS` text embedded in the sign panel; a second tiny fragment above it. | One billboard label was anchored behind the panel, so it clipped through both faces. | Replaced it with two single-sided labels just outside each panel face and moved the support posts behind the sign. | `V25_bridge_works_close.png` |
| V26 | Red House (and the other gabled buildings) had open gable ends with sky visible under the roof. | The reusable buildings had roof slabs and walls but no triangular end walls. | Added a wall-coloured `PrismMesh` gable to both ends of RedHouse, PlayerHouse, Workshop, Market, ClothesShop and TownHall, sized to the roof ridge and width. | `V26_red_house_corner.png`, `V26_player_house_corner.png`, `V26_workshop_corner.png`, `V26_market_corner.png`, `V26_clothes_corner.png`, `V26_townhall_north.png` |
| V27 | Playground swings, sign and yellow interaction marker encroached on the carriageway. | The playground sand overlapped the road and the interaction station was at the old road edge. | Moved the playground centre to z=-9.5, shortened the sand to 12.4 m, moved the swings to z=-11.5, moved the station to z=-5.0 on the sand, and moved the fix sign and bench with it. | `V27_playground_road.png`, `V27_playground_marker.png` |
| V28 | Playground sign intersected/obscured the nearest swing frame. | The sign sat between the swing frames at the old centre. | Centred the sign at the playground entrance (local 0,2.1,6.0), 8 m clear of the swings, with new posts. | `V27_playground_road.png` |
| V29 | Rally gate number was partly behind the crossbeam. | The number panel was centred at y=5.45 while the beam top is y=5.15. | Raised and shortened the panel to 2.4×1.45 m at y=5.95, with matching numbers on both faces of all six gates. | `V29_gate4_approach.png` |
| V30 | Delivery-van cargo roof showed black/yellow moiré, and `PATCHWORK POST` floated above the body. | The authored vehicle GLBs have no normal data; their flat panels self-shadowed. The rear label sat inside/above the cargo box. | Disabled self-shadow casting for the van and all NPC/rival cars, added a soft contact shadow, and placed the postal label flush on the rear door. | `V30_van_isolated_rear_high.png`, `p12_1080_02_start_line.png`, `p14_1080_01_post_counter.png` |
| V31 | Pip's broken-car label floated against the workshop wall. | The billboard label was high above the roofline and low contrast. | Lowered it to 1.82 m, raised the font to 40 with a cream outline, and applied the same treatment to the repaired-car label. | `V31_pip_car.png`, `p14_1080_06_broken_pip_car.png` |
| V32 | Jobs Board junction showed overlapping rectangular road patches, dark joins and lane lines crossing side-road mouths. | Road slabs overlapped at different heights, three stray patches did not connect to anything, and centre/edge dashes ran across junctions. | Removed the three stray patches, trimmed side roads to butt against the main carriageway at z=±3.2, unified every slab top at y=0.0175, and removed centre/edge dashes inside junction mouths. | `V32_jobs_overhead.png`, `V32_road_west_top.png` |
| V33 | Crossing stripes extended past the side-road boundary. | The crosswalk was 4.2 m wide over a 3.6 m mouth. | Narrowed the crossing to 3.2 m, centred in the mouth. | `V33_crosswalk_top.png`, `p14_1080_01_post_counter.png` |
| V34 | Lamps stood in the carriageway or against an awning near the Jobs Board. | Legacy lamp coordinates were not checked against the authored road rectangles. | Moved the in-road lamp to the verge between the two north roads and moved the Market lamp to the south verge, clear of the awning. | `V32_jobs_overhead.png`, `V34_lamp_jobs.png`, `V26_market_corner.png` |
| V35 | `BROKEN`, car-status and `START / FINISH` labels were hard to read at gameplay distance. | Small font sizes and billboard labels that could clip into their backing panels. | Raised the swing label to 46 px with outline, raised the car label to 40 px with outline, enlarged the start flag to 2.8×1.5 m with two 54 px single-sided faces, and moved the postal label to a readable rear position. | `V35_swing_labels.png`, `V35_start_flag.png`, `V35_start_flag_back.png`, `p12_1080_02_start_line.png` |

The blue traffic car in `p12_1080_02_start_line.png` showed the same missing-normal
roof moiré found on the van. The fix was applied to every `TrafficCar` visual
(including the rally rival), not just the player van.
