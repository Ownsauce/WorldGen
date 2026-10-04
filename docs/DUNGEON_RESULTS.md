# Shared dungeon generation results

Implemented October 4, 2026. `DungeonGenerationResult` is an additive, engine-neutral
view of the modular generator and the existing-room generator. It builds on the
existing `DungeonLayout`; it does not introduce another layout algorithm or change
the accepted generation versions, manifests, recipes or hash formats.

## Entry points

```csharp
// The existing modular generator, exposed through the common result.
DungeonGenerationResult modular = DungeonGenerationResults.GenerateModular(manifest, parameters);

// Adapt a modular layout that was already generated and validated by the host.
DungeonGenerationResult modularView = DungeonGenerationResults.FromModular(layout);

// The host chooses provider/catalog identities and revisions from its content settings.
var presentation = new DungeonResourceCatalog(presentationProvider, catalogId, visualRevision);
var collision = new DungeonResourceCatalog(collisionProvider, catalogId, collisionRevision);
DungeonGenerationResult rooms = DungeonGenerationResults.GenerateRooms(
    roomManifest, roomCatalog, count, pool, presentation, collision);

// Adapt an already resolved or restored room recipe; never regenerate a saved recipe here.
DungeonGenerationResult roomView = DungeonGenerationResults.FromRooms(
    roomManifest, recipe, presentation, collision);
```

`GenerateRooms` uses the manifest seed and the current source-room generator version.
The two generators still have different inputs and algorithms. Once they finish,
both return the same result type. Existing APIs remain available.

## Common information

| Member | Meaning |
| --- | --- |
| `Layout` | Existing room bounds, role/name records, connections, corridors, portals and spawns |
| `LayoutHash`, `Manifest` | Existing authoritative version/seed/content identity and generator-specific layout hash |
| `Rooms` | Logical room plus source kind, transform and separate presentation/collision references |
| `Connections`, `Corridors`, `SpawnPoints`, `DoorPortals` | Views of the existing layout records |
| `Openings` | Connected threshold positions, cardinal outward directions, widths/heights and owning room/connection IDs |
| `Closures` | Static closures for unused source-room sockets, preserving their original placement and appearance |

Coordinates, bounds, widths/heights and transform anchors are integer millimetres.
`DungeonSourceTransform.TransformPoint` accepts floating-point source vertices in
metres and returns metres without quantizing vertices. Quarter turns rotate +Z
toward +X. Openings use floor thresholds, not vertically centered points.

The old native transform entry point delegates to this same transform. Presentation
and collision must use this operation rather than infer their own pivot offsets.

For modular output, source data is already in world coordinates, so the room source
transform is the identity. References use the published
`worldgen.modular.presentation` and `worldgen.modular.collision` provider keys,
with world ID, layout hash and room ID. Those keys address the generated room
instructions in `Layout`; they do not select a single finished GLB. A renderer
continues to resolve its surfaces and fixtures through its content kit, and a
physics adapter continues to use the shared collision baker and corridor data.
Interactive door state remains separate.

For catalog-room output, references use host-supplied provider/catalog/revision
keys and invariant numeric source-room IDs. The provider resolves those keys to
the prepared catalog and original room resources. Presentation and collision may
come from different providers. PF127 is not hardcoded into the common contract.
AO hosts now resolve registered PF127/PF1931 sources and explicit retained
revisions through the [local catalog store](NATIVE_ROOM_CATALOGS.md).

The native adapter validates its catalog, restores a defensive placement snapshot,
validates walking connections, and rejects inconsistent used-socket state. It
does not invent modular collision, corridors, interactive doors or boss roles
for source rooms. Closure instructions retain the existing seal geometry.

## Integration boundary

This contract contains no Unity object, database connection, network transport,
download mechanism or server instance allocation. The host registers providers
and validates their actual resource revisions. A reference alone does not prove
that the provider's installed data matches its declared revision.

The result is an in-process contract. Existing manifest/recipe serialization and
network paths remain in use; no new wire format or general provider resolver is
installed by this change. Current gameplay renderers have not migrated to the
common result. A consumer can adopt it incrementally without regenerating worlds
or replacing its instance lifecycle.

New placement/opening/closure collections are read-only and copied from source
recipe state. Existing `Layout`/`Manifest` contracts are retained; hosts must treat
them as finalized after result creation, because manifest parameter maps remain
mutable under their existing API.

## Validation

The focused result check in ProjectMayhem exercises Facility/Subway/Temple outputs
and the prepared existing subway room catalog. It verifies old hashes/spawns,
source references, opposite openings at the same threshold/elevation, closures,
all four rotations and bit-exact legacy collision arithmetic. It rejects mismatched
connections, missing used sockets, invalid rotations and the wrong source adapter.

```bash
dotnet run --project tests/WorldGen.Result.Tests.csproj -- \
  AO.Unity/Assets/StreamingAssets/NativeCopies/pf127.rooms.json
```

Run from ProjectMayhem2.0. The check references WorldGen.Core only and starts no
client or server. Shared Core/Geometry build and the focused check passed; no
deployment, gameplay acceptance or client/server migration is claimed.

## Host adoption update — October 4, 2026

ProjectMayhem Unity manifest verification and AORebirth native registration now use
`FromRooms` with source-scoped presentation/collision references from published
catalogs. Rendering and collision assembly retain the established recipe paths.
Full lifecycle adapter extraction remains future work. See [catalog publication](NATIVE_ROOM_CATALOGS.md).
