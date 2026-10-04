# WorldGen assets

Organize content as `theme/category/type/asset` where useful. Keep reference images, original GLBs, reduced GLBs, and optional Blender files together. Do not create more folders unless needed.

Preserve original model filenames. Existing `-0.05-tex0.1.glb` names identify mesh and texture reduction settings; they are candidates, not automatic approvals or LOD assignments. Never overwrite the original with a reduced model. New assets may use `reference.png`, `original.glb`, and `game.glb`.

`experimental/` contains retained GLBs from the retired image-to-code experiments. These are unapproved diagnostic models.

Before client use, check dimensions, opening clearance, pivot, materials, and appearance in Unity. GLBs use meters; Temple pedestrian openings are 2.2 m wide by 2.8 m high; Subway standard openings are 2.6 m by 2.8 m, and widened optional concourse openings are 3.6 m by 2.8 m.

See `../docs/asset-migration.tsv` for original paths and content hashes. Unity prefabs, materials, and colliders belong to the client project.
