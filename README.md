# WorldGen

Shared procedural dungeon generation and custom GLB content tools. ProjectMayhem
uses these libraries with a Unity client and AORebirth's ZoneEngine_New server.
Core generation is engine-independent; rendering, asset access, physics registration,
networking and player transfers belong to host adapters.

## Start here

- [Connect a Unity client and server; choose an asset path](docs/CLIENT_SERVER_SETUP.md)
- [Common generation result and local C# entry points](docs/DUNGEON_RESULTS.md)
- [AO room sources, publication and saved catalog revisions](docs/NATIVE_ROOM_CATALOGS.md)
- [Custom GLB catalog preparation and Unity importing](docs/INTEGRATION.md)
- [GLBCompression user guide](GLBCompression/USER_GUIDE.md)

## Two asset paths

| Path | Generation input | Client presentation | Current integration |
| --- | --- | --- | --- |
| Custom assets / GLB | Modular dungeon settings and authored content metadata | Imported project-authored models, materials and Unity kits | Shared modular generator, ProjectMayhem kits and a reusable static GLB catalog importer |
| Existing AO rooms | Prepared room catalog, seed, source and catalog revision | Rooms, textures and static models read at runtime from the user's local AO installation | Registered `ao-subway` (PF127) and `ao-temple` (PF1931) in ProjectMayhem/AORebirth |

Both paths expose `DungeonGenerationResult`. Their geometry preparation and
renderers remain separate. One AO source is selected per dungeon; arbitrary
cross-source/custom-room joins are not implemented. Current normal AO generation
does not use authored encounter/arena tags for pacing; that optional profile is
planned.

AO database resources and generated catalog/collision packages are not stored in
this repository. Runtime loading uses local caches/temporary processing as needed;
it does not require a downloaded GLB/texture pack. Custom optimized `game.glb`
assets and their metadata remain repository content. Original working models,
conversion variants, build output and distribution archives are Git-ignored.

## Unity and server basics

For the existing integration, clone WorldGen beside ProjectMayhem2.0 so its Unity
manifest and .NET project references resolve. Add Core and Geometry as local Unity
packages; a .NET server references Core. The client resolves presentation resources,
while the server owns collision and gameplay authority. Exchange versioned generation
data and verify source/catalog identity and layout hashes before entry.

No separately hosted API is required to call these libraries. Complete reusable
Unity/server lifecycle adapters remain future work; adding the packages alone does
not provide a renderer, AO database reader, login protocol or teleport workflow.
See [the integration guide](docs/CLIENT_SERVER_SETUP.md) for what exists today.

Build the shared libraries from this directory:

```bash
dotnet build WorldGen.slnx -c Release
```

## Run AO-room dungeons in ProjectMayhem

After preparing/publishing both sources and installing the integrated local server,
select your AO installation in the Unity client and use GM chat:

```text
.worldgen native-rooms ao-subway 90602 24 subway-run-01 mixed
.worldenter subway-run-01

.worldgen native-rooms ao-temple 90603 24 temple-run-01 mixed
.worldenter temple-run-01
```

The number after the source ID is the seed; the next number is the room count
(currently 4–24). Use a new world name when generating. These commands belong to
AORebirth's integration, not to the shared library or the original AO client.
Fresh clones need locally prepared packages before these commands can work.
See [setup and authoring details](docs/CLIENT_SERVER_SETUP.md#ao-database-room-path).

## Custom GLB workflow

Create or obtain a project-authored model, reduce it with GLBCompression, and check
scale, openings, pivots and appearance before importing it into Unity. Keep its
optimized model as `Assets/<theme>/<category>/<asset>/game.glb`; placement metadata
lives in `asset.json`. Source originals may remain locally beside it but are ignored.

Run `python3 GLBCompression/server.py` and open http://127.0.0.1:8765. Rebuild the
viewer with `npm ci` then `npm run build` inside GLBCompression. The browser bundle
is generated locally. See [portable integration](docs/INTEGRATION.md) for validation,
Unity importing and content packaging. The portable importer currently covers static
doorway frames; it is not a complete portable room-kit renderer.

## Repository layout

- `WorldGen.Core`: deterministic layouts, room catalogs, contracts and collision utilities.
- `WorldGen.Geometry`: engine-neutral mesh evaluation and geometry buffers.
- `Integrations/Unity`: reusable static GLB catalog/prefab adapter.
- `GLBCompression`: mesh simplification and texture reduction tools.
- `Assets`: custom optimized models, metadata and reference images.
- `docs`, `tests`, `tools`: integration notes, checks and preparation utilities.

Preserve generator versions and recorded catalog revisions when restoring saved
worlds. The earlier image-to-code/Option A pipelines are retired; existing examples
remain, and a new standalone client/server example is not planned.
