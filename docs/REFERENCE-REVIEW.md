# Procedural world review

GLB assets are the active content workflow. Seed testing is complete for the current checkpoint; treasure placement remains on hold. Next: Temple visual/traversal review, surface and transition fixes, doorway integration, and runtime measurements. The retained historical checkpoint below predates completion of seed testing.

### Procedural dungeon checkpoint (September 30, 2026)

The shared `WorldGen.Core` generator now feeds both the AORebirth server and the Unity
client. Procedural character movement and world collision use N3Lite in the current
AORebirth path. Layout changes remain versioned so saved manifests reproduce their
original rooms and connections.

- **Subway 5.12.0 is the current test version.** The 30-room, seed-1 playthrough
  exposed a rail-end connection from a Track Tunnel to a pedestrian room and a
  narrow space beside the stopped train. Pedestrian rooms now connect only to
  the sides of rail rooms. The Train Chamber and track bed are wider, giving at
  least 2.2 m between each car and the platform wall. The yellow platform safety
  trim now follows the widened edge. The player reported that both the train-side
  movement and room connections feel much better after the geometry changes.
- **The earlier Subway fixes remain included.** Version 5.11.0 removed the optional
  Track Junction reached by an unstepped rail-height jump and widened the reported
  Concourse 8–11 connector. Version 5.10.0 recovered occupied optional branch cells.
  The seed-sweep notes and in-game commands are in
  [ProjectMayhem's WorldGen seed sweep](../../../ProjectMayhem2.0/docs/review/worldgen-seed-sweep.md).
- **Automated checks are green.** The 5.12.0 Core regression generated seeds 1–1000
  at both 24 and 30 rooms, validated each layout, and checked rail-side connections
  and train-side clearance. WorldGen Core and Geometry checks pass; AORebirth's
  `ZoneEngine_New` project builds. These checks do not replace in-game traversal
  across other seeds or Unity performance measurements.
- **Treasure placement is on hold.** Graph-based Treasure placement would improve
  rewards but is not needed for the current procedural traversal and presentation
  checkpoint. Resume it when gameplay rewards become a priority.

### Next procedural steps

1. **Review more playable Subway layouts.** Walk several 24- and 30-room seeds,
   including both rail orientations. Check train aisles, platform stairs, side-room
   doors, floor-height changes, and dead ends in the Unity client. Record the seed,
   room indices, and screenshot for any collision or visual issue before changing
   geometry again.
2. **Finish the Temple prototype visual and traversal review.** Build the revised
   floor, ceiling, and accent-row kit in Unity as described under “Next environment
   work” above. Check entrances, doors, corners, collisions, and repeatability over
   several seeds before promoting any intake module.
3. **Record runtime cost and representative baselines.** Keep canonical hashes and
   layout metrics per generator version. Measure Unity frame time, draw calls,
   object count, and mesh/texture memory for representative Subway and Temple
   layouts. Use those results to choose presentation optimizations.
4. **Improve placement resilience and spatial variety.** Add named room-level random
   streams and bounded deterministic retries where placement still fails. Then test
   portal offsets and selected nonstraight passages with door clearance and
   traversability checks. Version any output change and compare saved seeds.
5. **Add loops only when their full passage is valid.** Build and test second
   corridors, portals, collision, and navigation together. Add mission locks and
   keys after shortcuts work reliably and objectives remain reachable.
6. **Apply measured quality ranges and refactor covered behavior.** Set path,
   branching, and loop targets from seed-sweep distributions. Then consolidate
   duplicate branch placement and version-feature parsing behind tests. Seed cave
   detail variation separately from authoritative collision when cave presentation
   becomes the active focus.

**On hold:** Graph-based Treasure placement on a distant branch dead end. Preserve
boss and objective roles on the intended critical path when that work resumes.

