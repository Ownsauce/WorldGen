# Temple and Subway asset readiness

Reviewed 2026-10-01 against the current WorldGen asset folders, portable catalog/integration, and ProjectMayhem's existing Temple kit and procedural presentation code.

This is a file/contract inventory, not a visual approval. Triangle counts and static GLB readability were checked with the portable GLB reader. Scale, seams, lighting, collision, and appearance still need client review. Reference images and asset folder names do not establish that models tile or connect correctly.

## Gathering checklist for tomorrow — 2026-10-02

Start with the basic room kit. One usable asset per role is enough before gathering more variants.

### Subway

- [ ] Finish the floor asset from `Assets/subway/architecture/floors/1/reference.png`, or prepare a repeatable floor material for procedural geometry.
- [ ] Gather a platform edge/safety strip model or material treatment.
- [ ] Review the existing step, straight handrail, and post together; gather any missing landing, sloped railing, end, or corner pieces identified by that assembly.
- [ ] Gather one station light fixture and one sign treatment.
- [ ] Check existing wall/ceiling/doorway joins before gathering extra corner or trim models.

### Temple

- [ ] Finish one portable wall model from the existing references.
- [ ] Finish one floor and one ceiling treatment (GLB or repeatable procedural material).
- [ ] Gather one portable pillar and one beam/lintel using the existing references.
- [ ] Finish one skull torch or other light fixture.
- [ ] Gather a stair/landing treatment only if the first assembled sample needs elevation changes.
- [ ] Prepare a portable altar after the basic shell is ready.

### Keep with each model

- [ ] Reference image and untouched source model.
- [ ] Reviewed compressed export saved as `game.glb` in its asset folder.
- [ ] Intended role, measured dimensions in meters, pivot, and connection/clearance notes.
- [ ] Any external textures needed by the source FBX.

Do not overwrite originals. Compression metadata JSON is optional and is not WorldGen placement metadata.

### Follow-up integration work

- [ ] Configure and validate placement metadata for the existing Subway doorway.
- [ ] Extend catalog/client placement support for the selected floors, walls, ceilings, stairs, and fixtures.
- [ ] Assemble one small test room/corridor per theme, including a corner, doorway, and stairs where relevant.
- [ ] Check scale, seams, openings, collisions, railing connections, and lighting in the client.

**Deferred:** treasure, additional cosmetic variants, and optional complex doorway/pyramid/glass-room assets. Earlier seed testing is already complete; focus on the assets and their assembly.

## What is present

### Temple

- One `game.glb`: `architecture/doorways/basic-01`, 3,102 triangles, approximately 1.13 MiB. It has `asset.json` and is the only asset in the current portable catalog.
- Other doorway originals and compressed candidates are retained beside it.
- References exist for five wall variants, six floor tiles, two ceilings, pillars, beams, a complex doorway, an altar, skull torches, a plaque, pyramid, and glass room. Those categories do not currently contain portable `game.glb` models.
- ProjectMayhem retains 17 Temple Unity prefabs: six floors, five walls, two ceilings, a pillar, a beam, an altar, and their mesh/material resources. These remain useful in that client, but are not a portable GLB kit supplied by the WorldGen asset folders.

### Subway

Eleven `game.glb` models are present, all readable as static GLBs:

| Existing model | Triangles | Approx. MiB |
| --- | ---: | ---: |
| Wall 1 | 2,500 | 0.77 |
| Ceiling 1 | 2,500 | 0.90 |
| Ceiling 2 | 5,686 | 1.20 |
| Doorway 1 | 2,552 | 0.99 |
| Pillar 1 | 2,784 | 1.07 |
| Pillar 2 | 2,502 | 0.83 |
| Stair step (`stairs/stationary`) | 2,498 | 0.87 |
| Straight handrail | 2,498 | 0.71 |
| Handrail post | 2,498 | 0.73 |
| Straight train rail | 2,500 | 0.94 |
| Train | 12,498 | 1.37 |

The floor folder has a reference image but no model. None of these eleven models has `asset.json`, and none is in the current catalog. Their presence means there are candidates to integrate, not that the procedural client already uses them.

## Major missing pieces and work

### Temple priorities

1. **Portable room shell:** one usable wall, floor, and ceiling treatment. Generate/export reviewed GLBs from the references, or retain simple procedural surfaces with a cohesive material/texture set. A plain plane or box does not need an AI-generated high-detail model. The existing Unity kit can continue serving ProjectMayhem while portable replacements are prepared.
2. **Structural accents:** one pillar and one beam/lintel, with verified placement pivots and repeat spacing. Existing references cover these, but portable models are absent.
3. **Lighting fixture:** finish at least one skull torch or other fixture. A fixture GLB still needs client light placement and any flame/emission effects; the model alone does not light the room.
4. **Stair/landing treatment if the chosen layouts change elevation:** a reusable tread or suitable procedural stair material, top/bottom joins, and any needed edge treatment. No Temple stair GLBs are present.
5. **Room focal piece:** a portable altar is a useful first addition after the shell. Plaques and further room variants can follow. Treasure remains on hold.

A second complex doorway, pyramid, and glass room are optional variety. They do not need to precede a consistent basic room kit.

### Subway priorities

1. **Walkable floor and platform edge:** complete the floor treatment and add a platform edge/safety strip or procedural equivalent. These cover large visible areas and distinguish the station floor from the track bed.
2. **Modular joins:** test wall corners/end faces, floor-to-wall seams, doorway surrounds, and ceiling ends in a room. Add dedicated corner, cap, or trim pieces only where the existing modules cannot make clean joins.
3. **Stair completion:** test the existing step, rail, and post as an assembled flight. Add landing/top/bottom transitions, sloped rail coverage, and rail ends/corners as needed. A folder named `straight-hand-rail` does not prove it fits the stair slope or step spacing.
4. **Lighting and navigation:** one modular station light and one sign treatment, with client lights/text. These are absent as game GLBs, though the current procedural client builds placeholder lighting/sign details.
5. **Track transitions where the generated layout needs them:** the straight rail exists; junctions, bends, and rail/track-bed end transitions need either suitable modules or procedural geometry using consistent materials. Do not create a curved rail unless the chosen layout actually uses curved routes.
6. **A small station prop set:** a bench plus a service cabinet/bin are useful once the shell is integrated. Further decoration is lower priority than floors, joins, lighting, and traversal.

The train, pillars, doorframe, wall, and ceilings already provide a solid starting set. Producing more variants of those is lower priority than proving the existing pieces fit together.

## Integration is a separate gap

The current catalog and Unity importer are a doorway-frame pilot. Simply adding `game.glb` to a folder does not automatically place walls, stairs, or props in procedural rooms.

- Configure and validate the existing Subway doorway first using measured dimensions, opening clearance, collision proxies, and sockets.
- Extend the portable placement contracts and client adapter for additional asset roles before expecting full-library imports to work. Do not invent doorway metadata for a floor or stair just to pass the validator.
- Keep procedural layout and authoritative collision intact; authored models supply their visual treatment through explicit placement rules.
- Prepare a small assembled test: two connected rooms, a corner, a doorway, and a stair flight where relevant. Check gaps, repeat seams, collisions, opening clearance, rail connections, and lighting in the client.

## Suggested next work order

1. Subway floor/platform treatment and one portable Temple wall/floor/ceiling set.
2. Integrate the existing Subway doorway and extend placement support for the chosen shell pieces.
3. Assemble and review a sample room/corridor for each theme; finish joins and stair transitions discovered there.
4. Add one light fixture per theme, Temple pillar/beam, and the altar or Subway bench.
5. Expand variants once the first kit has proven scale, clearance, pivots, and placement behavior.

Seed testing is already complete for the earlier work; this review does not propose rerunning it. New placement contracts or collision behavior should receive focused checks when implemented.
