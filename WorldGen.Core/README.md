# WorldGen.Core

Pure .NET deterministic world-generation contracts for .NET and Unity consumers.

The library targets `netstandard2.0` so it can be consumed by Unity, modern
ZoneEngine builds, and the legacy .NET Framework 4.8 server during migration.

This package has no Unity or AO/RDB dependency. Consumers use the same source:

- Server projects reference `WorldGen.Core.csproj`.
- Unity imports `com.worldgen.core` as a local package.

Current foundation:

- PCG-based deterministic random streams
- Named sub-seed derivation
- Versioned generation manifests
- Canonical manifest serialization and SHA-256 hashing
- Stable logical world-object IDs
- Integer-quantized vectors and bounds
- Deterministic connected dungeon graphs and spatial room embedding
- Walkable corridor bounds and oriented door portals
- Closed-door authoritative blocking bounds
- Entrance, exit, and boss spawn points
- Structural validation and canonical full-layout hashing
- Engine-neutral surface, fixture, and light presentation records
- Semantic material roles and authoritative fixture collision intent
- Constructive geometry recipes included in the canonical layout hash

`WorldGen.Core` describes what must exist; it does not create Unity, Godot, or Unreal
objects. The sibling [`WorldGen.Geometry`](../WorldGen.Geometry) library evaluates Core's
constructive recipes into engine-neutral vertices, normals, UVs, and triangle indices.
Engine adapters upload those buffers and resolve semantic material roles to native shaders.
Core must never reference `WorldGen.Geometry`; Geometry references Core.

Run the contract tests with:

```bash
dotnet run --project ../../ProjectMayhem2.0/tests/WorldGen.Core.Tests.csproj
```

The ProjectMayhem Unity client includes an opt-in `ProceduralDungeonDebugView` component for
previewing floor, wall, corridor, door, and spawn geometry. On AORebirth's
`ZoneEngine_New`, `.worldgen <seed> <rooms> [instance-id]` safely generates and
caches a validated layout without replacing a live playfield.

The development vertical slice now carries a versioned, transport-neutral
manifest envelope through the existing AO chat response path. The client
independently regenerates the layout and blocks presentation unless its hash
matches the authoritative server hash. Explicit capability negotiation and a
dedicated production transport remain future work; do not assign an arbitrary
AO/N3 opcode for them.


## Common dungeon results

`DungeonGenerationResults.GenerateModular` and `GenerateRooms` return the same
engine-neutral `DungeonGenerationResult`. Existing outputs can also be adapted
with `FromModular`/`FromRooms`. The additive contract includes source references,
placement transforms, connected openings and source-room closures while preserving
existing layout hashes, spawns and algorithms. Provider resolution and engine/host
integration remain separate. See [the contract and examples](../docs/DUNGEON_RESULTS.md).
