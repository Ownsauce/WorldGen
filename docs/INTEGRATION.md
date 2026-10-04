# Portable GLB integration

WorldGen remains separate from game code. The server uses Core for deterministic layout and collision; the client maps content IDs to prepared assets. Players receive compiled libraries and prepared client content, not the authoring repository. This first integration covers static doorway frames; other placement contracts can extend the same catalog later.

## Asset folders

```text
Assets/temple/architecture/doorways/basic-01/
  reference.png
  original.glb
  game.glb
  asset.json
```

Only `game.glb` is selected. Folder names establish identity (`temple/architecture/doorways/basic-01`), theme, and category. `asset.json` is the small placement specification; it does not contain geometry. It supplies purpose, dimensions, opening clearance, sockets, collision boxes, and quarter-turn rotations. The builder calculates SHA-256, source bounds, and triangle count. Identical filenames in different folders cannot collide.

Originals and reference images remain developer files. Treat a changed file as a new content revision while retaining the asset ID. Change the content version for a changed catalog and ship compatible content to clients before the server selects those assets. V1 does not implement client downloads, network content negotiation, or historical content retention. Pin a particular catalog/package when replaying saved visual selections; changing the catalog can change selected frame variants even though layout RNG is unchanged.

## Build and validate

From the WorldGen root:

```bash
python3 tools/catalog.py init-doorway Assets/temple/architecture/doorways/001 --dimensions 3.3 3.5 0.38 --opening 2.2 2.8
python3 tools/catalog.py build Assets catalog.json --version temple-pilot-1 --only temple/architecture/doorways/basic-01
python3 tools/catalog.py validate catalog.json --assets Assets
```

The pilot uses an explicit `--only` selection so other unconfigured `game.glb` files are reported by full-library builds without blocking this first asset. Omit `--only` to build the entire library.

Use `init-doorway` only on a new asset folder; edit existing metadata explicitly. Dimensions are normalized outer bounds in meters. The opening must remain clear after normalization; the builder tests triangles against the complete clearance box, not just vertex locations. Collision boxes are separate simplified proxies and must not enter the opening. This geometric check does not establish final appearance or in-game traversal approval.

The pilot selects the compressed `templ-doorway-2-0.05-tex0.1.glb` as `game.glb` without changing its bytes. The reference/original remain under their existing names. Its 3,102 triangles require 3.30 x 3.50 x 0.38 m outer bounds to preserve a 2.20 x 2.80 m centered opening. Sockets are at the bottom center of front/back faces. Raw Hunyuan scale is normalized by the client importer; do not stretch the model to placeholder-frame dimensions without checking its opening.

GLBs must be self-contained glTF 2.0 with a default scene, static triangle meshes, embedded textures, float positions, and unsigned indices. The portable inspector rejects animation, skins, morphs, sparse geometry, required extensions, and compressed primitives. Use the uncompressed reduced GLB produced by GLBCompression. `catalog.json` is schema version 1, right-handed +Y-up (glTF), with X width/Y height/Z depth and bottom-center pivot. Adapters must account for their engine's coordinate conventions. Unity/glTFast converts glTF handedness; v1 socket positions/facings lie on Y/Z and require no X reflection.

## Unity

ProjectMayhem already contains the adapter. Another Unity project can:

1. Add Core/Geometry as local Unity packages from the extracted distribution, or distribute their compiled assemblies with the required Unity references. No sibling checkout is needed.
2. Install glTFast with its GLB editor importer enabled.
3. Copy `Integrations/Unity` beneath the client's Assets directory. Do not also copy the same adapter classes if the client already has them.
4. Use **Tools > WorldGen > Import GLB Catalog** and choose `catalog.json`. The importer locates the adjacent `Assets` folder, or lets you choose it.

The importer verifies every file hash, imports persistent GLB subassets through glTFast, normalizes vertices to declared dimensions and bottom-center pivot, checks the opening again, and builds static collision proxies and socket markers. The output is under `Assets/WorldGen/Models`, `Assets/WorldGen/Prefabs`, and `Assets/Resources/WorldGen/WorldGenPrefabCatalog.asset`. Full ID/content hashes isolate assets and revisions. The active catalog changes only after all entries succeed. Previous imported revisions are retained until references can be reviewed and cleaned up.

The catalog references prepared prefabs, so normal Unity builds include meshes, textures, and materials without loading GLBs from the developer repository at runtime. ProjectMayhem loads this catalog and places compatible frames at Temple/Subway pedestrian doorways; placeholders remain for unmatched sizes/themes. Moving door leaves and authoritative server collision remain controlled by the existing generator. Prototype frame variants use a stable placement-ID hash without consuming layout RNG. Exact server-selected visual asset IDs should be resolved with `catalog.Resolve(assetId)`.

```csharp
var catalog = Resources.Load<WorldGenPrefabCatalog>("WorldGen/WorldGenPrefabCatalog");
var entry = catalog.Resolve("temple/architecture/doorways/basic-01");
var frame = Object.Instantiate(entry.Prefab);
```

Each new client needs to implement rendering, materials, coordinate conversion, and collision from the portable contract. This adapter is reusable Unity code; ProjectMayhem's door-placement hook is client-specific.

## Distribution and .NET example

```bash
dotnet build WorldGen.slnx -c Release -p:NuGetAudit=false
python3 tools/catalog.py package catalog.json dist/worldgen-temple-pilot-1.zip
dotnet run --project examples/CatalogClient -- catalog.json Assets
```

The ZIP contains versioned catalog and selected game GLBs, compiled netstandard2.0 libraries, source Unity packages, the standalone Unity adapter, the .NET example, validators, and integration instructions. It excludes originals, images, compression dependencies, and build caches. Extract anywhere; package/project references are relative inside the distribution. The .NET example parses and verifies the catalog without Unity or ProjectMayhem. A non-.NET server can consume generated layouts through its own service/serialization boundary; the package does not provide a port to other languages.

The ZIP is an SDK/content handoff for developers. Build the game client through its normal pipeline; do not ship the full ZIP to players.

## Verification

```bash
python3 -m unittest discover -s tests -v
python3 tools/catalog.py validate catalog.json --assets Assets
```

Core/Geometry regression suites remain in ProjectMayhem. Imported appearance, lighting, door animation, and traversal should be checked in the Unity client before production use.

The Temple pilot is already installed in ProjectMayhem as a persistent prefab/catalog. Its importer was exercised in an isolated Unity project with persistent mesh/material, collision, compatible-slot, and repeat-import checks. The extracted distribution example also runs without a sibling checkout. Appearance and in-game traversal remain for client review. See `integration-verification.json` for recorded checks.
