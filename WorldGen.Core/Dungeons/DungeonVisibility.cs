using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public enum DungeonLocationKind { Room, Corridor }

    public sealed class DungeonLocation
    {
        public DungeonLocation(DungeonLocationKind kind, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Location ID is required.", nameof(id));
            Kind = kind;
            Id = id;
        }

        public DungeonLocationKind Kind { get; }
        public string Id { get; }
    }

    public sealed class DungeonVisibilitySet
    {
        public DungeonVisibilitySet(IReadOnlyCollection<string> visibleRoomIds,
            IReadOnlyCollection<string> prefetchedRoomIds,
            IReadOnlyCollection<string> visibleConnectionIds,
            IReadOnlyCollection<string> prefetchedConnectionIds)
        {
            VisibleRoomIds = visibleRoomIds ?? throw new ArgumentNullException(nameof(visibleRoomIds));
            PrefetchedRoomIds = prefetchedRoomIds ?? throw new ArgumentNullException(nameof(prefetchedRoomIds));
            VisibleConnectionIds = visibleConnectionIds ?? throw new ArgumentNullException(nameof(visibleConnectionIds));
            PrefetchedConnectionIds = prefetchedConnectionIds ?? throw new ArgumentNullException(nameof(prefetchedConnectionIds));
        }

        public IReadOnlyCollection<string> VisibleRoomIds { get; }
        public IReadOnlyCollection<string> PrefetchedRoomIds { get; }
        public IReadOnlyCollection<string> VisibleConnectionIds { get; }
        public IReadOnlyCollection<string> PrefetchedConnectionIds { get; }
    }

    public static class DungeonVisibilityResolver
    {
        public static DungeonRoom FindContainingRoom(DungeonLayout layout, WorldVector3 position)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                IReadOnlyList<WorldBounds> sections = DungeonRoomGeometry.FloorSections(layout.Rooms[i]);
                for (int section = 0; section < sections.Count; section++)
                    if (sections[section].Contains(position)) return layout.Rooms[i];
            }
            return null;
        }

        public static DungeonLocation FindContainingLocation(DungeonLayout layout, WorldVector3 position)
        {
            DungeonRoom room = FindContainingRoom(layout, position);
            if (room != null) return new DungeonLocation(DungeonLocationKind.Room, room.Id);
            for (int i = 0; i < layout.Corridors.Count; i++)
                if (layout.Corridors[i].WalkableBounds.Contains(position))
                    return new DungeonLocation(DungeonLocationKind.Corridor, layout.Corridors[i].ConnectionId);
            return null;
        }

        public static DungeonVisibilitySet Resolve(DungeonLayout layout, DungeonLocation origin,
            IReadOnlyDictionary<string, DungeonDoorStateSnapshot> doorStates)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (origin == null) throw new ArgumentNullException(nameof(origin));

            var roomPortals = new Dictionary<string, List<DungeonDoorPortal>>(StringComparer.Ordinal);
            var corridorPortals = new Dictionary<string, List<DungeonDoorPortal>>(StringComparer.Ordinal);
            for (int i = 0; i < layout.Rooms.Count; i++) roomPortals[layout.Rooms[i].Id] = new List<DungeonDoorPortal>();
            for (int i = 0; i < layout.Corridors.Count; i++) corridorPortals[layout.Corridors[i].ConnectionId] = new List<DungeonDoorPortal>();
            for (int i = 0; i < layout.DoorPortals.Count; i++)
            {
                DungeonDoorPortal portal = layout.DoorPortals[i];
                roomPortals[portal.RoomId].Add(portal);
                corridorPortals[portal.ConnectionId].Add(portal);
            }

            var visibleRooms = new HashSet<string>(StringComparer.Ordinal);
            var visibleCorridors = new HashSet<string>(StringComparer.Ordinal);
            var queue = new Queue<DungeonLocation>();
            Add(origin);
            while (queue.Count > 0)
            {
                DungeonLocation location = queue.Dequeue();
                List<DungeonDoorPortal> portals = location.Kind == DungeonLocationKind.Room
                    ? roomPortals[location.Id] : corridorPortals[location.Id];
                for (int i = 0; i < portals.Count; i++)
                {
                    DungeonDoorPortal portal = portals[i];
                    if (!IsOpen(portal.Id, doorStates)) continue;
                    Add(location.Kind == DungeonLocationKind.Room
                        ? new DungeonLocation(DungeonLocationKind.Corridor, portal.ConnectionId)
                        : new DungeonLocation(DungeonLocationKind.Room, portal.RoomId));
                }
            }

            var prefetchedRooms = new HashSet<string>(StringComparer.Ordinal);
            var prefetchedCorridors = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < layout.DoorPortals.Count; i++)
            {
                DungeonDoorPortal portal = layout.DoorPortals[i];
                if (visibleRooms.Contains(portal.RoomId) && !visibleCorridors.Contains(portal.ConnectionId))
                    prefetchedCorridors.Add(portal.ConnectionId);
                if (visibleCorridors.Contains(portal.ConnectionId) && !visibleRooms.Contains(portal.RoomId))
                    prefetchedRooms.Add(portal.RoomId);
            }

            return new DungeonVisibilitySet(Sorted(visibleRooms), Sorted(prefetchedRooms),
                Sorted(visibleCorridors), Sorted(prefetchedCorridors));

            void Add(DungeonLocation location)
            {
                bool added = location.Kind == DungeonLocationKind.Room
                    ? visibleRooms.Add(location.Id) : visibleCorridors.Add(location.Id);
                if (added) queue.Enqueue(location);
            }
        }

        private static bool IsOpen(string doorId,
            IReadOnlyDictionary<string, DungeonDoorStateSnapshot> doorStates) =>
            doorStates == null || !doorStates.TryGetValue(doorId, out DungeonDoorStateSnapshot state)
            || state == null || state.State == DungeonDoorState.Open;

        private static IReadOnlyCollection<string> Sorted(HashSet<string> values)
        {
            var result = new List<string>(values);
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }
    }
}
