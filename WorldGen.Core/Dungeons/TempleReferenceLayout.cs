using System;
using System.Collections.Generic;
using WorldGen.Contracts;
using WorldGen.Identity;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    /// <summary>
    /// Reviewable reconstruction of PF 1931's room identities and connectivity.
    /// Geometry is deliberately made from the shared procedural kit: this is the
    /// bridge used to approve rooms before promoting them into seeded Temple layouts.
    /// </summary>
    internal static class TempleReferenceLayout
    {
        private static readonly string[] Names =
        {
            "Present Connector", "Future West Hub", "Future East Hub",
            "Future Inner Hub", "Future Connector", "Past Connector",
            "Present Temple", "Entry Hall", "East Upper Hallway",
            "East Lower Hallway", "West Lower Hallway", "West Upper Hallway",
            "East Small Hub", "West Small Hub", "Past Antechamber",
            "Present Chambers", "Crypt Chamber", "Great Hall",
            "Present Concourse", "Future Guardian Hall", "Future Chambers II",
            "Future Chambers III", "Future East Concourse",
            "Future West Concourse", "Future Inner Concourse",
            "Future Chambers I", "Future Temple", "Crypt Hall",
            "Wing of the Past", "Entrance Hub"
        };

        // Authored review coordinates preserve the recognizable entrance, paired
        // approach routes, Great Hall, and three deep wings without copying RDB mesh.
        private static readonly int[] X =
        {
             0,  4,  8,  6,  2, -2,  0,  0,  4,  2,
            -2, -4,  4, -4, -2,  0, -6,  0,  0,  6,
             4,  2,  8,  4,  6,  6,  6, -8, -4,  0
        };

        private static readonly int[] Z =
        {
             6, 14, 14, 12,  4,  4, 12,  0,  4,  2,
             2,  4,  2,  2,  6, 10,  6,  4,  8,  6,
             6,  6,  8,  8, 10,  8, 14,  6,  6,  2
        };

        private static readonly int[,] Edges =
        {
            { 7, 29 }, { 29, 9 }, { 9, 12 }, { 12, 8 }, { 8, 4 }, { 4, 17 },
            { 29, 10 }, { 10, 13 }, { 13, 11 }, { 11, 5 }, { 5, 17 },
            // Each upper hallway now terminates at its connector room. The wing
            // exits north from that room while the inward door reaches Great Hall;
            // no corridor shell continues through and walls off the branch socket.
            { 17, 0 }, { 0, 18 }, { 18, 15 }, { 15, 6 },
            { 5, 14 }, { 14, 28 }, { 28, 16 }, { 16, 27 },
            { 4, 21 }, { 21, 20 }, { 20, 19 }, { 19, 25 },
            { 25, 23 }, { 23, 1 }, { 1, 26 },
            { 25, 22 }, { 22, 2 }, { 2, 26 },
            { 25, 24 }, { 24, 3 }, { 3, 26 }
        };

        public static DungeonLayout Generate(GenerationManifest manifest,
            DungeonGenerationParameters parameters)
        {
            if (parameters.RoomCount != 30)
                throw new ArgumentException(
                    "The TempleReference profile requires exactly 30 rooms.",
                    nameof(parameters));

            const int spacing = 32000;
            var rooms = new List<DungeonRoom>(30);
            for (int i = 0; i < 30; i++)
            {
                DungeonRoomRole role = i == 7 ? DungeonRoomRole.Entrance
                    : i == 6 ? DungeonRoomRole.Boss
                    : i == 19 ? DungeonRoomRole.Objective
                    : i == 16 || i == 27 ? DungeonRoomRole.Treasure
                    : i == 17 || i == 29 ? DungeonRoomRole.Normal
                    : DungeonRoomRole.Branch;
                DungeonModuleKind module = ResolveModule(i);
                DungeonRoomShape shape = ResolveShape(i);
                ResolveSize(i, out int width, out int depth);
                int height = i == 17 || i == 6 || i == 26 ? 10000
                    : i == 27 || i == 16 ? 5500 : 7500;
                var center = new WorldVector3(X[i] * spacing, 0, Z[i] * spacing);
                var size = new WorldVector3(width, height, depth);
                var bounds = new WorldBounds(
                    new WorldVector3(center.X - width / 2, 0, center.Z - depth / 2),
                    new WorldVector3(center.X + (width - width / 2), height,
                        center.Z + (depth - depth / 2)));
                rooms.Add(new DungeonRoom(
                    StableWorldId.Create(manifest.WorldId, "reference-room-" + i.ToString("D2")),
                    i, role, center, size, bounds, shape, module, Names[i]));
            }

            var connections = new List<DungeonConnection>(Edges.GetLength(0));
            for (int i = 0; i < Edges.GetLength(0); i++)
            {
                int from = Edges[i, 0], to = Edges[i, 1];
                connections.Add(new DungeonConnection(
                    StableWorldId.Create(manifest.WorldId,
                        "reference-connection-" + i.ToString("D2")),
                    rooms[from].Id, rooms[to].Id, i < 13));
            }
            var corridors = new List<DungeonCorridor>(connections.Count);
            var portals = new List<DungeonDoorPortal>(connections.Count * 2);
            for (int i = 0; i < connections.Count; i++)
                DungeonGenerator.AddPassage(manifest, parameters, rooms,
                    connections[i], i, corridors, portals);

            var spawns = new List<DungeonSpawnPoint>
            {
                DungeonGenerator.CreateSpawn(manifest, rooms[7],
                    DungeonSpawnRole.PlayerEntrance, "entrance", 0),
                DungeonGenerator.CreateSpawn(manifest, rooms[7],
                    DungeonSpawnRole.PlayerExit, "exit", 180),
                DungeonGenerator.CreateSpawn(manifest, rooms[6],
                    DungeonSpawnRole.Boss, "boss", 180)
            };
            var result = new DungeonLayout(manifest, rooms, connections, corridors,
                portals, spawns);
            DungeonValidationResult validation = DungeonValidator.Validate(result);
            if (!validation.IsValid)
                throw new InvalidOperationException(
                    "Temple reference layout is invalid: "
                    + string.Join("; ", validation.Errors));
            return result;
        }

        private static DungeonModuleKind ResolveModule(int index)
        {
            if (index == 7) return DungeonModuleKind.PilgrimEntry;
            if (index == 29) return DungeonModuleKind.NaveHub;
            if (index == 17) return DungeonModuleKind.GrandHall;
            if (index == 6) return DungeonModuleKind.ProfaneSanctum;
            if (index == 19) return DungeonModuleKind.InnerGate;
            if (index == 16 || index == 27) return DungeonModuleKind.SunkenCrypt;
            if (index == 26) return DungeonModuleKind.HighSanctuary;
            if (index == 14 || index == 15 || index == 20 || index == 21
                || index == 25) return DungeonModuleKind.RitualChapel;
            if (index == 0 || index == 4 || index == 5)
                return DungeonModuleKind.CollapsedCloister;
            return DungeonModuleKind.ProcessionalHall;
        }

        private static DungeonRoomShape ResolveShape(int index)
        {
            if (index == 17 || index == 6 || index == 19 || index == 26)
                return DungeonRoomShape.GrandChamber;
            if (index == 7 || index == 0 || index == 4 || index == 5
                || (index >= 8 && index <= 11))
                return DungeonRoomShape.LongHall;
            // The reference pass must remain completely traversable. These rooms
            // can regain authored crypt alcoves after their socket-safe module is
            // reviewed; an L-notch currently puts an internal wall across the exit.
            if (index == 16 || index == 27) return DungeonRoomShape.Rectangle;
            return DungeonRoomShape.Rectangle;
        }

        private static void ResolveSize(int index, out int width, out int depth)
        {
            width = 12000;
            depth = 12000;
            if (index == 7) { width = 14000; depth = 30000; return; }
            if (index == 17) { width = 30000; depth = 26000; return; }
            if (index == 6 || index == 19 || index == 26)
            { width = 26000; depth = 26000; return; }
            if (index == 0 || index == 4 || index == 5)
            { width = 10000; depth = 15000; return; }
            if (index >= 8 && index <= 11)
            { width = index == 8 || index == 11 ? 10000 : 15000;
              depth = index == 8 || index == 11 ? 15000 : 10000; return; }
            if (index == 15 || index == 18 || (index >= 20 && index <= 25))
            { width = 18000; depth = 16000; }
        }
    }
}
