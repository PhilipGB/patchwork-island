# Art and visual asset direction

## Look and feel

Reference boards:
- `reference/village_style_board.png` — PRIMARY guide to village, player, van, bridge, foliage and world palette.
- `reference/ui_and_scope_board.png` — SECONDARY guide to UI grouping and playable feedback. Text, jobs, numbers and exact colours in this illustrative board may be inaccurate; follow this document and `MISSIONS.json`.

Visual target: cheerful **flat-shaded 3D toy town**, slightly isometric from the chase camera, constructed from clear geometric blocks. Think chunky LEGO-like proportions and simple Minecraft-like readability, but original silhouettes. No realistic textures, dark/grungy shaders, detailed faces or distracting visual noise. Everything important should read from a distance and work at 1280×720.

Use primitive cubes, bevelled boxes where easy, cones, cylinders, triangular roof shapes and low-sided spheres/icospheres. Prefer **untextured flat-colour materials** with roughness ~0.8–1.0 and minimal metallic reflection. One simple directional sun, ambient skylight and light shadowing; avoid post-processing and costly screen-space effects. Procedural materials and meshes are entirely acceptable for the first playable.

## Palette (sRGB hex, recommended)

| Role | Colour | Hex |
|---|---|---|
| Sky | light sky blue | `#75C9FF` |
| Grass | soft spring green | `#86CF55` |
| Dark grass / shrubs | leaf green | `#369B47` |
| Road | slate grey | `#6A7180` |
| Road markings | cream | `#FFF3D6` |
| Water | turquoise | `#24B5E4` |
| Highlight / delivery van | sunny yellow | `#FFD23F` |
| Objective / Red House | bright red | `#F45A54` |
| Shop / interact accent | royal blue | `#2674EC` |
| Reward / special unlock | lavender | `#946FE8` |
| Wood | warm brown | `#B98550` |
| Stone | muted grey | `#9CA3AD` |
| UI panel | cream white | `#FFF8EC` |
| UI primary text | midnight navy | `#27364B` |
| Success | dark green | `#257B47` |

All text on coloured panel backgrounds must meet good practical contrast. For colour cues also write **RED HOUSE**, **SHOP**, etc. Keep interaction text dark on a pale solid panel, not over scenery.

## Scale and geometric recipes (unit = 1m)

**Player:** about 1.6m tall, chibi/tubby silhouette with roundish square head, 1.0m wide, short legs, blue shirt, mustard/yellow rucksack or vest, bright red cap; 2-dot eyes and short smile. A few primitive meshes; minimal idle / walk bob and 2–3 limb rotations. Character must be easy to spot in camera.

**Van:** ~3.2m long × 1.9m wide × 2m high, warm yellow boxy body, blue windscreens, dark grey thick cylinder wheels and a white envelope mark on its side. Use simplified arcade acceleration; wheels can visually rotate. No complex physics suspension needed.

**Houses (five):** 5–8m wide and ~5m tall; ivory/cream box walls, simple red / blue / green triangular roofs, large flat windows and doors; a little porch, sign and decorative planters. Clearly different silhouettes: house with red gable roof, market with striped awning, workshop with wide open door, Town Hall with small clock/bell, player's house with a mailbox.

**Trees and terrain:** cubic foliage clusters on rectangular trunks and/or 6-sided cone fir trees; simple layered grass ground with blocky slopes / islands / boulders; blue water plane with a few large simple foam shapes, not water simulation. Distinct green footprint for build areas.

**Parcels/collectables:** each item large, high-contrast and recognisable. Red apples ~0.4m wide with leaf, wood logs brown cylinders, stones grey polyhedra, packages coloured cubes with white label/envelope, bread tan block with top slices, milk white carton with blue top, planks long brown blocks, supports cube blocks, ropes brown thin rods, swing seats flat blue boxes.

**Bridge:** across narrow river, with seven visible sockets highlighted by translucent ghost meshes: three 1m support blocks below and four 2m planks above. Final path width >= 3m. The bridge must transform from incomplete gap to useful walkable/drivable route. Snap glow green for valid, muted red for wrong slot. No precision clicks.

**Race and obstacle course:** 4 numbered arch checkpoints, easy low ramps, broad road, colourful triangular flags and clear direction arrows. All collision surfaces broad and stable. Time-trial markers must be large enough to read without stopping.

**Playground and furniture:** two swing frames (simple A-shaped supports and 2D-looking rods), four rope parts and two seat parts; park bench, rectangular flower bed, simple lamppost; house table, chairs and potted plant. Reuse common palette and grid snapping.

## HUD, menus and typographic rules

Art direction for UI: playful but clean, softly rounded pale cards with thick coloured borders, simple SVG icons (provided in `assets/icons/`), readable dark text, no tiny labels.

**720p baseline:** mission card upper-left (~280–340px wide), coin counter top-right (~130px), contextual E prompt bottom-centre (~320px), destination indicator and optional tiny compass around edge. Use at least ~24px main prompt type, 20px objective detail, 16–18px secondary type. Standard clickable button target at least 44 × 44 pixels. A major game menu option should usually be 56px high. Scale gracefully to 1080p.

Mission card example:

```
★ CURRENT JOB
Deliver 3 apples to RED HOUSE
[apple icon]  1 / 3
[H] Hint              [J] Jobs
```

Use a real icon and label; don't show a wall of text. HUD mission card changes as tasks progress. Rewards should animate briefly with a positive colour but not flash rapidly.

**Hint states:** initially 'Find three apples at the market'. Next: 'Look for the red apples in the market crate' with a corresponding icon; final: visible glowing target/breadcrumb. Allow repeat click. Distinct first-run pop-ups for `Move`, `E`, `Drive`, `Build`, each dismissible.

## Assets provided in this handoff

`assets/icons/` contains self-authored SVG vector icons for apple, parcel, wood, stone, coin, house, build/hammer, star, van, waypoint, shopping basket and flag. Use these as production source assets as appropriate. Godot may rasterise them on import; verify legibility at target UI sizes. They are simple rather than polished illustrations, which is deliberate.

`reference/` PNGs are concept images generated for this handoff. They are useful for overall shape language and atmosphere **but not** faithful source for task details, UI literal text or reusable seamless textures.

`planning/map.svg` shows only rough spatial organisation. It is a planning asset, not an in-game UI map unless simplified appropriately.

## Build priority for art

1. Character, five recognizable buildings, van, trees, road, water, bridge and marker.
2. Distinct collectable silhouettes and obvious build sockets.
3. Consistent HUD panels, icons, mission board and signs.
4. Race arches, playground and furniture.
5. Simple polish (idle animations, little particle puffs, sound and confetti) after functionality.

## How to judge success

At first glance, a child should be able to identify the player, bright van, red destination house, blue shop, job board and repairable bridge without reading. An adult should be able to read current job and progress from a screenshot at 720p. Colour communicates importance, but written labels and icons make actions understandable without colour perception.
