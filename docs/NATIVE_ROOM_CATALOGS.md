# Native room sources and local publication

`NativeRoomCatalogStore` provides an in-process, engine-independent catalog store.
It does not open the AO database, host an endpoint, choose runtime instance IDs or
replace the editable authoring DAO. Hosts supply `INativeRoomCatalogCodec` JSON
serialization and the local root. Current provider is `ao-install`; custom assets
remain a separate preparation/provider path.

```text
NativeCopies/
  sources.json
  RoomCatalogs/<source-id>/<catalog-sha256>/
    catalog.json
    collision.bin
    publication.json
```

The registry records source ID, display name, provider, source playfield and active
revision; its default is authored data. Room identity combines source ID and room
index. `Load(sourceId, revision)` resolves an explicit retained revision;
`LoadPlayfield(playfield, revision)` supports older manifests without source IDs.
A resolved publication binds catalog and collision hashes plus source/surface
fingerprints and the preparation's authoring revision. It rejects missing/corrupt
files, mismatched source identities and invalid collision blocks. Verified catalog
snapshots are cached for the process lifetime; published files must remain immutable.

`Publish` validates the input catalog/collision, takes cooperating file locks,
stages every destination's immutable revision, then selects it by atomically
replacing each registry. A handled activation failure restores already changed
registries. This is local multi-folder publication, not a distributed transaction.
A crash between registry replacements can leave different defaults. Explicit-revision
lookup works for sources already registered on both hosts because all packages were
staged first; a newly added source may remain unregistered on one host and entry
will be blocked. Repeat publication to repair the selection. Failed/unselected packages
can remain, and revisions are not automatically deleted. Later reference-aware
retention must protect saved worlds and exported recipes.

Collision stays in the existing AONR v1 format: playfield plus source-room triangle
blocks in source metres. Structural validation requires each catalog room's collision
block, finite coordinates and no trailing bytes. Geometric spawn/doorway/traversal
validation remains the preparation tool's responsibility. Host preparation checks
must pass before publication; structural consistency alone does not establish
walking safety. Rendering geometry/images still come from the user's installation,
not from the publication package. Prepared local collision is stored in both local
host packages for matching revision checks; nothing is uploaded or redistributed.

ProjectMayhem's preparation definitions register `ao-subway`/PF127 and
`ao-temple`/PF1931. Its publisher CLI validates 4/12/24-room recipes and stages the
client/server folders together. Unity's authoring window runs preparation followed
by publication asynchronously. AORebirth archives record source identity and catalog
hash, retaining v1 PF127 save compatibility. Neither host resolves an old save against
a newer active revision. Original local source/surface fingerprint matching remains
a prerequisite for client entry.

Full Unity/server lifecycle adapters, mixed-source assembly, catalog cleanup and
SQL/team authoring storage remain future work.

## Collision policy — doorway repair

New prepared catalogs use Version4 / CollisionGeometryVersion1: each source-room
collision mesh is clipped to its declared rectangular footprint. The same AO.Assets
clipper prepares server triangles and Unity source collision. This removes source
ramps/cages overhanging into a rearranged neighboring room. Legacy Version3 catalogs
with absent/zero policy preserve their original collision for saved-revision identity.
Version4 requires policy1; unknown policies are rejected. The native generation
algorithm/version and recipe hashes remain unchanged for retained input snapshots.
Fresh instances select the active corrected catalog; old saves keep their recorded
revision. Client contact projection and server N3Lite sweep/slide are host movement
responsibilities and do not change deterministic room placement.
