# WorldGen

Shared procedural world generation and GLB content tools.

- `WorldGen.Core`: deterministic layouts, portals, contracts, and authoritative collision.
- `WorldGen.Geometry`: reusable procedural geometry and mesh utilities.
- `GLBCompression`: standalone GLB mesh simplification and texture reduction viewer.
- `Assets`: images and models grouped by theme and intended use.
- `docs`: procedural review notes and asset migration inventory.

## GLB workflow

See [GLBCompression user guide](GLBCompression/USER_GUIDE.md) for model preparation and [Temple/Subway asset readiness](docs/ASSET_READINESS.md) for the current kit gaps.

Create a reference image, generate a GLB with Hunyuan or another model tool, reduce it with GLBCompression, and check scale, openings, pivot, and appearance before importing it into Unity. Original and reduced models stay together in the asset folder.

Run `python3 GLBCompression/server.py` and open http://127.0.0.1:8765. To rebuild the viewer: run `npm ci` then `npm run build` inside GLBCompression.

## Procedural libraries

Build `WorldGen.slnx`. ProjectMayhem consumes Core and Geometry as local Unity packages and .NET project references. Preserve generator versions for reproducible saved worlds.

The image-to-code authoring and Option A pipelines have been retired. Existing Temple Unity meshes, materials, prefabs, and the procedural dungeon generator remain in the client. `Tools > WorldGen > GLB Preview` reviews GLB candidates.

## Portable content integration

See [docs/INTEGRATION.md](docs/INTEGRATION.md) for catalog building, Unity importing, and versioned distribution. `python3 tools/catalog.py build Assets catalog.json --version temple-pilot-1 --only temple/architecture/doorways/basic-01` selects only `game.glb` files and validates their explicit placement metadata.
