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

## Software-renderer measurements (not a desktop-GPU claim)

| Resolution | Mean frame | p95 frame | Max frame | Nodes | Draw calls | Primitives |
|---|---:|---:|---:|---:|---:|---:|
| 1280×720 | 86.25 ms | 97.15 ms | 107.45 ms | 2581 | 2997 | 60,647 |
| 1920×1080 | 97.34 ms | 106.84 ms | 121.95 ms | 2581 | 2997 | 60,647 |

The v1.1.0 baseline did not include this probe, so a baseline frame-time
comparison is **NOT TESTED**. The relative scene cost is recorded for later
optimisation. Real Radeon 890M profiling remains **NOT TESTED**.

## Remaining visual gaps

- The clothes-shop awning and mannequin silhouettes are close together at very
  low camera angles; no clipping was visible in the fixed captures.
- The large direction-post sign cluster is intentionally whimsical and can look
  edge-on from some angles, but back faces are now readable.
- Roof gable wedges are part of the flat-shaded toy style, not z-fighting.
- Real-GPU anti-aliasing, Windows rendering and player-eye comfort remain
  **NOT TESTED** in this environment.
