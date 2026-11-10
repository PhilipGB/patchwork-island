# Visual triage from the supplied 18-frame town tour

These are candidate improvements rather than proven gameplay bugs.

| Issue | Observed in tour | Fix approach | Regression concern |
|---|---|---|---|
| Camera enters/approaches walls and roof in the player's home | Interior screenshot with oversized avatar foreground | Camera distance/near-clip tuning, camera-only blockers, close-wall transparency | Door collision, player controls |
| Broadleaf trees fill most of the foreground | Several images with enormous block-green nearby canopy | Camera obstruction fade (not removal), smaller canopy only after review | Collision, landmark silhouette |
| Cross-road slabs remain visibly rectangular | Road/rally junction images | Junction mesh / height continuity | Van and NPC traversal |
| Close-up shop/job signs overlap other props | Central village screenshots | Tweak sign visual offsets, keep station marker anchored | NPC talk and E prompt |
| Bridge/rally approach surface layering | Bridge-to-rally screenshots | Preserve flush collision, inspect seam from both directions | M05/M09/M19 racing and walking |
| Long left HUD panel obscures much of scene | Every screenshot | Optional compact HUD after the scene migration, not part of layout bake | Accessibility for younger players |

Keep every correction as a separate, small, before/after-verifiable change.

## v1.3.0 migration status

All items above are **still present** in the scene-backed parity build. They were
deliberately not changed in the migration milestone: the first target was exact
v1.2.1 geometry/function parity. Before/after evidence exists in
`evidence/v13_scene_world/` for baseline capture, but no polish change has been
made yet. Each fix should be a separate small change with the regression checks
listed above.
