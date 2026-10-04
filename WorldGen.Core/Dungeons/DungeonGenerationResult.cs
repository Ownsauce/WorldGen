using System;
using System.Collections.Generic;
using WorldGen.Contracts;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    /// <summary>A host-selected catalog. Provider keys are data, not file paths or engine types.</summary>
    public sealed class DungeonResourceCatalog
    {
        public DungeonResourceCatalog(string providerId, string catalogId, string revision)
        {
            ProviderId = Required(providerId, nameof(providerId));
            CatalogId = Required(catalogId, nameof(catalogId));
            Revision = Required(revision, nameof(revision));
        }

        public string ProviderId { get; }
        public string CatalogId { get; }
        public string Revision { get; }
        public DungeonResourceReference Reference(string resourceId) =>
            new DungeonResourceReference(this, resourceId);

        internal static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value;
        }
    }

    /// <summary>A reference to source data. It contains no mesh, texture, database connection or download URL.</summary>
    public sealed class DungeonResourceReference
    {
        public DungeonResourceReference(DungeonResourceCatalog catalog, string resourceId)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            ResourceId = DungeonResourceCatalog.Required(resourceId, nameof(resourceId));
        }

        public DungeonResourceCatalog Catalog { get; }
        public string ResourceId { get; }
    }

    public enum DungeonRoomSourceKind { GeneratedLayout, CatalogRoom }

    /// <summary>Quantized millimetre anchors; source vertices stay floating-point metres.
    /// Quarter turns rotate +Z toward +X, matching the existing room-placement convention.</summary>
    public readonly struct DungeonSourceTransform
    {
        public DungeonSourceTransform(WorldVector3 sourceOrigin, WorldVector3 position, int quarterTurns)
        {
            if (quarterTurns < 0 || quarterTurns > 3) throw new ArgumentOutOfRangeException(nameof(quarterTurns));
            SourceOrigin = sourceOrigin;
            Position = position;
            QuarterTurns = quarterTurns;
        }

        public WorldVector3 SourceOrigin { get; }
        public WorldVector3 Position { get; }
        public int QuarterTurns { get; }

        public void TransformPoint(float x, float y, float z, out float worldX, out float worldY, out float worldZ)
        {
            x -= SourceOrigin.X * .001f;
            z -= SourceOrigin.Z * .001f;
            float rx = x, rz = z;
            switch (QuarterTurns)
            {
                case 1: rx = z; rz = -x; break;
                case 2: rx = -x; rz = -z; break;
                case 3: rx = -z; rz = x; break;
            }
            worldX = Position.X * .001f + rx;
            worldY = Position.Y * .001f + y - SourceOrigin.Y * .001f;
            worldZ = Position.Z * .001f + rz;
        }
    }

    public sealed class DungeonResolvedRoom
    {
        internal DungeonResolvedRoom(DungeonRoom room, DungeonRoomSourceKind sourceKind,
            DungeonSourceTransform transform, DungeonResourceReference presentation, DungeonResourceReference collision)
        {
            Room = room;
            SourceKind = sourceKind;
            Transform = transform;
            Presentation = presentation;
            Collision = collision;
        }

        public DungeonRoom Room { get; }
        public DungeonRoomSourceKind SourceKind { get; }
        public DungeonSourceTransform Transform { get; }
        public DungeonResourceReference Presentation { get; }
        public DungeonResourceReference Collision { get; }
    }

    /// <summary>A connected room opening. Position is the floor threshold in millimetres;
    /// outward is a cardinal vector with length 1000. An opening does not imply a door leaf.</summary>
    public sealed class DungeonConnectionOpening
    {
        internal DungeonConnectionOpening(string id, string connectionId, string roomId,
            WorldVector3 position, WorldVector3 outward, int width, int height)
        {
            Id = id; ConnectionId = connectionId; RoomId = roomId;
            Position = position; Outward = outward; Width = width; Height = height;
        }

        public string Id { get; }
        public string ConnectionId { get; }
        public string RoomId { get; }
        public WorldVector3 Position { get; }
        public WorldVector3 Outward { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>A static closure for an unused source-room socket, separate from interactive doors.</summary>
    public sealed class DungeonPassageClosure
    {
        internal DungeonPassageClosure(string id, string roomId, WorldVector3 center, WorldVector3 size,
            int quarterTurns, string appearanceKey)
        {
            Id = id; RoomId = roomId; Center = center; Size = size;
            QuarterTurns = quarterTurns; AppearanceKey = appearanceKey;
        }

        public string Id { get; }
        public string RoomId { get; }
        public WorldVector3 Center { get; }
        public WorldVector3 Size { get; }
        public int QuarterTurns { get; }
        public string AppearanceKey { get; }
    }

    /// <summary>An additive common view of either generator. Layout and its existing hash remain authoritative.
    /// Provider resolution, rendering, physics registration, serialization and instance lifecycle belong to hosts.</summary>
    public sealed class DungeonGenerationResult
    {
        internal DungeonGenerationResult(DungeonLayout layout, string layoutHash,
            List<DungeonResolvedRoom> rooms, List<DungeonConnectionOpening> openings,
            List<DungeonPassageClosure> closures)
        {
            Layout = layout; LayoutHash = layoutHash;
            Rooms = rooms.AsReadOnly(); Openings = openings.AsReadOnly(); Closures = closures.AsReadOnly();
        }

        public const int ContractVersion = 1;
        public DungeonLayout Layout { get; }
        public GenerationManifest Manifest => Layout.Manifest;
        public string LayoutHash { get; }
        public IReadOnlyList<DungeonResolvedRoom> Rooms { get; }
        public IReadOnlyList<DungeonConnection> Connections => Layout.Connections;
        public IReadOnlyList<DungeonCorridor> Corridors => Layout.Corridors;
        public IReadOnlyList<DungeonSpawnPoint> SpawnPoints => Layout.SpawnPoints;
        public IReadOnlyList<DungeonDoorPortal> DoorPortals => Layout.DoorPortals;
        public IReadOnlyList<DungeonConnectionOpening> Openings { get; }
        public IReadOnlyList<DungeonPassageClosure> Closures { get; }
    }
}
