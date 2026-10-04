# Unity and server integration

This guide describes the current shared-library boundary and the working
ProjectMayhem/AORebirth integration. A different client/server must provide its own
host adapters. These are local C# calls; WorldGen does not require an HTTP service.

## Required pieces

| Responsibility | Shared code | Host responsibility |
| --- | --- | --- |
| Layout and room transforms | `WorldGen.Core`, `DungeonGenerationResults` | Choose source, seed, settings and supported generator version |
| Geometry utilities | `WorldGen.Geometry` | Convert buffers into engine meshes/materials as needed |
| AO resources | Room/source references and catalog store | Read the user's AO installation, decode resources and prepare matching collision |
| Unity presentation | Static GLB importer in `Integrations/Unity` | Dungeon rendering, visibility, lights, doors, loading/cancellation and cleanup |
| Server physics | Modular collision utilities; prepared AO collision packages | Register/remove collision with the server's physics engine |
| Entry and persistence | Versioned manifests, result contracts and room recipes | Instance IDs, transport, hash checks, saved worlds, supported spawns and player transfer |

AO decoding is in ProjectMayhem's `AO.Assets`; AO presentation is in its
`AO.Unity/World/NativeDungeons` module. AORebirth's `NativeRoomPackage`,
`NativeRoomArchive` and `ProceduralInstanceService` provide the existing server
integration. These host implementations are not all packaged inside WorldGen.
Full reusable Unity/server lifecycle adapters are still planned.

## Local checkout and package references

The current ProjectMayhem configuration expects sibling directories:

```text
Coding/
  ProjectMayhem2.0/
    AO.Unity/
  WorldGen/
    WorldGen.Core/
    WorldGen.Geometry/
  AORebirth/                 # server checkout for the existing host integration
```

ProjectMayhem's `AO.Unity/Packages/manifest.json` includes:

```json
"com.worldgen.core": "file:../../../WorldGen/WorldGen.Core",
"com.worldgen.geometry": "file:../../../WorldGen/WorldGen.Geometry"
```

Use the Unity version in ProjectMayhem's `AO.Unity/ProjectSettings/ProjectVersion.txt`.
Another Unity project can use **Add package from disk** with each library's
`package.json`, then adapt its assembly references. The GLB importer additionally
needs glTFast and the `Integrations/Unity` code; do not copy duplicate adapters into
ProjectMayhem, which already has them.

A .NET server references `WorldGen.Core.csproj` from its own project, for example:

```xml
<ItemGroup>
  <ProjectReference Include="../WorldGen/WorldGen.Core/WorldGen.Core.csproj" />
</ItemGroup>
```

Adjust that relative path to the server project directory. Core targets
`netstandard2.0`; host tools have their own SDK/dependency requirements. Start with
[`DungeonGenerationResults.GenerateModular` / `GenerateRooms`](DUNGEON_RESULTS.md)
and preserve resource revisions, transforms and hashes. For AO rooms, both hosts
must resolve the same publication and collision policy. The game still owns entry,
physics registration and supported spawn confirmation.

## Custom assets / GLB path

1. Prepare an optimized `game.glb` and authored placement metadata in `asset.json`.
2. Build/validate a catalog with `tools/catalog.py`.
3. Import it through **Tools > WorldGen > Import GLB Catalog**; provide the matching
   custom asset folder. The current portable contract is a static doorway-frame pilot.
4. Bind additional room surfaces/fixtures through the host's kit/renderer. ProjectMayhem's
   Subway authoring tool and existing Temple kit are separate host integrations.
5. Generate through the modular path, register shared collision on the server and
   render the verified result on the client.

See [portable GLB integration](INTEGRATION.md) for the actual importer scope,
validation and packaging commands. Custom assets are distributed with the game;
this path does not resolve them from the AO database.

The integrated ProjectMayhem custom Subway command is:

```text
.worldgen 90602 30 custom-subway-01 subway subway procedural
.worldenter custom-subway-01
```

Its editor is **Tools > WorldGen > Dungeon Authoring**. The existing custom Temple
reference workflow is documented separately in
[ProjectMayhem Temple design](https://github.com/Ownsauce/ProjectMayhem2.0/blob/projectmayhem-2.0/docs/temple-procedural-design.md).
That reference layout is distinct from rearranging PF1931 database rooms.

## AO database room path

This uses the **Unity client reading the local AO installation**; it does not require
running the original AO client or adding an extension to it.

Prerequisites for the existing host integration:

- A compatible local AO installation selected in ProjectMayhem, including its database
  and the verified native reader build. Current native extraction supports Windows
  and Linux with Wine; consult the reader notes for build compatibility.
- Local runtime reader executables built from ProjectMayhem source. On the current
  Linux toolchain, run `./tools/build-indoor-runtime-helpers.sh` from ProjectMayhem;
  it requires `i686-w64-mingw32-g++`.
- Validated local indoor surface caches (`127_v2.aois` and `1931_v2.aois`) obtained
  through the existing indoor extraction path. A fresh clone does not include them.
- Prepared and published source catalogs/collision for both client and server.
  Source definitions live in ProjectMayhem's `tools/WorldGen.NativeRoomCatalog/`.
- The integrated AORebirth server built and installed locally; GM command access.

Preparation/publication tools live in **ProjectMayhem**, not this shared repository.
For an existing validated surface cache, these examples run from ProjectMayhem:

```bash
dotnet run --project tools/WorldGen.NativeRoomCatalog -- \
  '/path/to/Anarchy Online' '/path/to/127_v2.aois' '/tmp/prepared-subway' \
  - tools/WorldGen.NativeRoomCatalog/pf127.source.json

dotnet run --project tools/WorldGen.PublishRoomCatalog -- \
  tools/WorldGen.NativeRoomCatalog/pf127.source.json /tmp/prepared-subway \
  AO.Unity/Assets/StreamingAssets/NativeCopies \
  ../AORebirth/AORebirth/Server/ZoneEngine_New/NativeCopies
```

For Temple, use `1931_v2.aois`, `pf1931.source.json` and a separate output directory
in both commands. Replace `-` with the matching saved authoring settings file when
applying reviewed tags/exclusions. Both sources must be prepared/published before
their corresponding entry commands will work. Do not overwrite retained revisions
referenced by saved worlds.

After initial preparation, **Tools > WorldGen > Native Dungeon Authoring** can
select a registered source, inspect rooms, edit settings and validate/publish changes.
Publication updates local files; it does not rebuild, install or restart the server.
Build/install the AORebirth host, stop Play, refresh Unity and reconnect. Existing
local service deployment scripts are specific to this project's configured Linux
installation; another server must use its own deployment workflow.

Use in-game GM chat:

```text
.worldgen native-rooms ao-subway 90602 24 subway-run-01 mixed
.worldenter subway-run-01

.worldgen native-rooms ao-temple 90603 24 temple-run-01 mixed
.worldenter temple-run-01
```

- `ao-subway` selects PF127 resources; `ao-temple` selects PF1931 resources.
- `90602`/`90603` are seeds; `24` is the room count, currently limited to 4–24.
- Use a new world name for another generated instance. Source pools are catalog data;
  `mixed` selects across the source's available pools.
- The server allocates the runtime playfield ID. It is separate from the source PF ID.
- `.worldenter` restores a saved instance with its recorded source/catalog revision.
  A source-room editor preview does not create a server instance.

Rendering meshes, textures and static models come from each player's local AO
installation, with runtime caching and temporary processing. Prepared catalog and
collision packages are local generated data and Git-ignored. No exported AO GLB/PNG
pack is needed. User-created custom GLBs remain the separate pipeline above.

See [catalog identities and publication](NATIVE_ROOM_CATALOGS.md),
[ProjectMayhem AO setup](https://github.com/Ownsauce/ProjectMayhem2.0/blob/projectmayhem-2.0/DATA_SETUP.md),
[native authoring and entry](https://github.com/Ownsauce/ProjectMayhem2.0/blob/projectmayhem-2.0/docs/native-dungeon-authoring.md),
and [room data/DAO design](https://github.com/Ownsauce/ProjectMayhem2.0/blob/projectmayhem-2.0/docs/dungeon-authoring-data.md).
Normal generation remains available; encounter/arena progression is planned rather
than implemented. Cross-source room assembly and complete portable host adapters
remain future work.
