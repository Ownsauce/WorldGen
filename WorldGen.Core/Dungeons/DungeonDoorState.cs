using System;
using System.Collections.Generic;

namespace WorldGen.Dungeons
{
    public enum DungeonDoorState
    {
        Open = 0,
        Closed = 1,
        Locked = 2,
        Sealed = 3
    }

    public sealed class DungeonDoorStateSnapshot
    {
        public DungeonDoorStateSnapshot(string doorId, DungeonDoorState state,
            long revision, string requiredKeyId = "")
        {
            if (string.IsNullOrWhiteSpace(doorId))
                throw new ArgumentException("Door ID is required.", nameof(doorId));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            DoorId = doorId;
            State = state;
            Revision = revision;
            RequiredKeyId = requiredKeyId ?? string.Empty;
        }

        public string DoorId { get; }
        public DungeonDoorState State { get; }
        public long Revision { get; }
        public string RequiredKeyId { get; }
        public bool BlocksPassage => State != DungeonDoorState.Open;
        public bool CanToggleWithoutKey => State == DungeonDoorState.Open
                                           || State == DungeonDoorState.Closed;
    }

    /// <summary>
    /// One authoritative door controlling one room/corridor portal. A corridor
    /// has two independent doors, one at each endpoint.
    /// </summary>
    public sealed class DungeonDoor
    {
        public DungeonDoor(string id, string connectionId, DungeonDoorPortal portal)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Door ID is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(connectionId))
                throw new ArgumentException("Connection ID is required.", nameof(connectionId));
            Portal = portal ?? throw new ArgumentNullException(nameof(portal));
            if (portal.ConnectionId != connectionId)
                throw new ArgumentException("Portal must belong to the door connection.");
            Id = id;
            ConnectionId = connectionId;
        }

        public string Id { get; }
        public string ConnectionId { get; }
        public DungeonDoorPortal Portal { get; }
    }

    public static class DungeonDoorFactory
    {
        public static IReadOnlyList<DungeonDoor> Create(DungeonLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            // Cave passages use open rock sockets rather than mechanical doors.
            // Portal metadata remains in the layout for topology, visibility,
            // orientation, and transition construction.
            if (DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                == DungeonLayoutProfile.CaveDungeon)
                return Array.Empty<DungeonDoor>();
            var portals = new List<DungeonDoorPortal>(layout.DoorPortals);
            portals.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
            var doors = new List<DungeonDoor>(portals.Count);
            for (int i = 0; i < portals.Count; i++)
            {
                if (IsOpenJoin(layout, portals[i].ConnectionId)) continue;
                doors.Add(new DungeonDoor(portals[i].Id, portals[i].ConnectionId, portals[i]));
            }
            return doors.AsReadOnly();
        }

        private static bool IsOpenJoin(DungeonLayout layout, string connectionId)
        {
            DungeonConnection connection = null;
            for (int i = 0; i < layout.Connections.Count; i++)
                if (layout.Connections[i].Id == connectionId)
                {
                    connection = layout.Connections[i];
                    break;
                }
            if (connection == null) return false;
            DungeonRoom from = null;
            DungeonRoom to = null;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Id == connection.FromRoomId) from = layout.Rooms[i];
                if (layout.Rooms[i].Id == connection.ToRoomId) to = layout.Rooms[i];
            }
            if (from == null || to == null) return false;
            if (DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                    == DungeonLayoutProfile.Subway
                && (from.ModuleKind == DungeonModuleKind.Entrance
                    || to.ModuleKind == DungeonModuleKind.Entrance)) return true;
            bool fromRail = from.ModuleKind == DungeonModuleKind.TrainChamber
                || from.ModuleKind == DungeonModuleKind.TrackTunnel
                || from.ModuleKind == DungeonModuleKind.TrackJunction;
            bool toRail = to.ModuleKind == DungeonModuleKind.TrainChamber
                || to.ModuleKind == DungeonModuleKind.TrackTunnel
                || to.ModuleKind == DungeonModuleKind.TrackJunction;
            return fromRail && toRail;
        }
    }
}
