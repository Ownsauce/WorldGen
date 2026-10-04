using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Dungeons
{
    public sealed class DungeonValidationResult
    {
        internal DungeonValidationResult(List<string> errors) { Errors = errors.AsReadOnly(); }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }

    public static class DungeonValidator
    {
        public static DungeonValidationResult Validate(DungeonLayout layout)
        {
            var errors = new List<string>();
            if (layout == null) { errors.Add("Layout is null."); return new DungeonValidationResult(errors); }
            var rooms = layout.Rooms ?? Array.Empty<DungeonRoom>();
            var connections = layout.Connections ?? Array.Empty<DungeonConnection>();
            var corridors = layout.Corridors ?? Array.Empty<DungeonCorridor>();
            var portals = layout.DoorPortals ?? Array.Empty<DungeonDoorPortal>();
            var spawns = layout.SpawnPoints ?? Array.Empty<DungeonSpawnPoint>();
            if (rooms.Count < 3) errors.Add("A dungeon requires at least three rooms.");
            if (rooms.Count(r => r.Role == DungeonRoomRole.Entrance) != 1) errors.Add("Exactly one entrance is required.");
            if (rooms.Count(r => r.Role == DungeonRoomRole.Boss) != 1) errors.Add("Exactly one boss room is required.");

            var byId = new Dictionary<string, DungeonRoom>(StringComparer.Ordinal);
            foreach (DungeonRoom room in rooms)
            {
                if (room == null || string.IsNullOrWhiteSpace(room.Id)) { errors.Add("Room has no ID."); continue; }
                if (byId.ContainsKey(room.Id)) errors.Add("Duplicate room ID: " + room.Id);
                else byId.Add(room.Id, room);
            }

            var adjacency = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string id in byId.Keys) adjacency[id] = new List<string>();
            var connectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (DungeonConnection connection in connections)
            {
                if (connection == null) { errors.Add("Null connection."); continue; }
                if (!connectionIds.Add(connection.Id)) errors.Add("Duplicate connection ID: " + connection.Id);
                if (!byId.ContainsKey(connection.FromRoomId) || !byId.ContainsKey(connection.ToRoomId))
                { errors.Add("Connection references an unknown room: " + connection.Id); continue; }
                if (connection.FromRoomId == connection.ToRoomId) { errors.Add("Self connection: " + connection.Id); continue; }
                adjacency[connection.FromRoomId].Add(connection.ToRoomId);
                adjacency[connection.ToRoomId].Add(connection.FromRoomId);
            }

            var knownConnections = new HashSet<string>(connections.Where(c => c != null).Select(c => c.Id),
                StringComparer.Ordinal);
            var corridorConnections = new HashSet<string>(StringComparer.Ordinal);
            foreach (DungeonCorridor corridor in corridors)
            {
                if (corridor == null) { errors.Add("Null corridor."); continue; }
                if (!knownConnections.Contains(corridor.ConnectionId))
                    errors.Add("Corridor references an unknown connection: " + corridor.Id);
                if (!corridorConnections.Add(corridor.ConnectionId))
                    errors.Add("Multiple corridors reference connection: " + corridor.ConnectionId);
            }
            if (corridorConnections.Count != knownConnections.Count)
                errors.Add("Every connection requires exactly one corridor.");

            var portalCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (DungeonDoorPortal portal in portals)
            {
                if (portal == null) { errors.Add("Null door portal."); continue; }
                if (!knownConnections.Contains(portal.ConnectionId))
                    errors.Add("Door portal references an unknown connection: " + portal.Id);
                if (!byId.ContainsKey(portal.RoomId))
                    errors.Add("Door portal references an unknown room: " + portal.Id);
                portalCounts.TryGetValue(portal.ConnectionId, out int count);
                portalCounts[portal.ConnectionId] = count + 1;
            }
            foreach (string connectionId in knownConnections)
                if (!portalCounts.TryGetValue(connectionId, out int count) || count != 2)
                    errors.Add("Connection requires two door portals: " + connectionId);

            if (spawns.Count(s => s != null && s.Role == DungeonSpawnRole.PlayerEntrance) != 1)
                errors.Add("Exactly one player entrance spawn is required.");
            if (spawns.Count(s => s != null && s.Role == DungeonSpawnRole.PlayerExit) != 1)
                errors.Add("Exactly one player exit spawn is required.");
            if (spawns.Count(s => s != null && s.Role == DungeonSpawnRole.Boss) != 1)
                errors.Add("Exactly one boss spawn is required.");
            foreach (DungeonSpawnPoint spawn in spawns)
            {
                if (spawn == null) { errors.Add("Null spawn point."); continue; }
                if (!byId.TryGetValue(spawn.RoomId, out DungeonRoom spawnRoom))
                    errors.Add("Spawn references an unknown room: " + spawn.Id);
                else if (!spawnRoom.Bounds.Contains(spawn.Position))
                    errors.Add("Spawn lies outside its room: " + spawn.Id);
            }

            DungeonRoom entrance = rooms.FirstOrDefault(r => r != null && r.Role == DungeonRoomRole.Entrance);
            if (entrance != null && byId.ContainsKey(entrance.Id))
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { entrance.Id };
                var pending = new Queue<string>(); pending.Enqueue(entrance.Id);
                while (pending.Count > 0)
                    foreach (string next in adjacency[pending.Dequeue()]) if (visited.Add(next)) pending.Enqueue(next);
                if (visited.Count != byId.Count) errors.Add("Dungeon graph is not fully connected.");
            }

            for (int a = 0; a < rooms.Count; a++)
                for (int b = a + 1; b < rooms.Count; b++)
                    if (rooms[a] != null && rooms[b] != null && Overlaps(rooms[a], rooms[b]))
                        errors.Add("Rooms overlap: " + rooms[a].Id + " and " + rooms[b].Id);
            return new DungeonValidationResult(errors);
        }

        private static bool Overlaps(DungeonRoom a, DungeonRoom b) =>
            a.Bounds.Minimum.X < b.Bounds.Maximum.X && a.Bounds.Maximum.X > b.Bounds.Minimum.X &&
            a.Bounds.Minimum.Y < b.Bounds.Maximum.Y && a.Bounds.Maximum.Y > b.Bounds.Minimum.Y &&
            a.Bounds.Minimum.Z < b.Bounds.Maximum.Z && a.Bounds.Maximum.Z > b.Bounds.Minimum.Z;
    }
}
