using System;
using System.Collections.Generic;
using WorldGen.Contracts;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public enum DungeonRoomRole
    {
        Entrance = 0,
        Normal = 1,
        Branch = 2,
        Treasure = 3,
        Objective = 4,
        Boss = 5
    }

    public enum DungeonRoomShape
    {
        Rectangle = 0,
        WideHall = 1,
        LongHall = 2,
        LShaped = 3,
        GrandChamber = 4,
        Cavern = 5
    }

    public enum DungeonModuleKind
    {
        Entrance = 0,
        CombatRoom = 1,
        Junction = 2,
        ServiceRoom = 3,
        TreasureRoom = 4,
        ObjectiveRoom = 5,
        BossArena = 6,
        StationPlatform = 7,
        StationConcourse = 8,
        TrainChamber = 9,
        Terminal = 10,
        TrackTunnel = 11,
        TrackJunction = 12,
        GrandHall = 13,
        Restroom = 14,
        PilgrimEntry = 15,
        ProcessionalHall = 16,
        NaveHub = 17,
        InnerGate = 18,
        ExarchGallery = 19,
        HighSanctuary = 20,
        ProfaneSanctum = 21,
        Reliquary = 22,
        SunkenCrypt = 23,
        Scriptorium = 24,
        RitualChapel = 25,
        Vestry = 26,
        CollapsedCloister = 27
    }

    public sealed class DungeonRoom
    {
        public DungeonRoom(string id, int index, DungeonRoomRole role,
            WorldVector3 center, WorldVector3 size, WorldBounds bounds,
            DungeonRoomShape shape = DungeonRoomShape.Rectangle,
            DungeonModuleKind moduleKind = DungeonModuleKind.CombatRoom,
            string displayName = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Index = index;
            Role = role;
            Center = center;
            Size = size;
            Bounds = bounds;
            Shape = shape;
            ModuleKind = moduleKind;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }

        public string Id { get; }
        public int Index { get; }
        public DungeonRoomRole Role { get; }
        public WorldVector3 Center { get; }
        public WorldVector3 Size { get; }
        public WorldBounds Bounds { get; }
        public DungeonRoomShape Shape { get; }
        public DungeonModuleKind ModuleKind { get; }
        public string DisplayName { get; }
    }

    public sealed class DungeonConnection
    {
        public DungeonConnection(string id, string fromRoomId, string toRoomId, bool criticalPath)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            FromRoomId = fromRoomId ?? throw new ArgumentNullException(nameof(fromRoomId));
            ToRoomId = toRoomId ?? throw new ArgumentNullException(nameof(toRoomId));
            IsCriticalPath = criticalPath;
        }

        public string Id { get; }
        public string FromRoomId { get; }
        public string ToRoomId { get; }
        public bool IsCriticalPath { get; }
    }

    public enum DungeonPortalFacing
    {
        NegativeX = 0,
        PositiveX = 1,
        NegativeZ = 2,
        PositiveZ = 3
    }

    public sealed class DungeonCorridor
    {
        public DungeonCorridor(string id, string connectionId, WorldBounds walkableBounds,
            int fromFloorY = 0, int toFloorY = 0)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            WalkableBounds = walkableBounds;
            FromFloorY = fromFloorY;
            ToFloorY = toFloorY;
        }

        public string Id { get; }
        public string ConnectionId { get; }
        public WorldBounds WalkableBounds { get; }
        public int FromFloorY { get; }
        public int ToFloorY { get; }
        public bool ChangesElevation => FromFloorY != ToFloorY;
        public DungeonTransitionKind TransitionKind => ChangesElevation
            ? DungeonTransitionKind.Stairs : DungeonTransitionKind.LevelPassage;
    }

    public enum DungeonTransitionKind
    {
        LevelPassage = 0,
        Stairs = 1
    }

    public sealed class DungeonDoorPortal
    {
        public DungeonDoorPortal(string id, string connectionId, string roomId, DungeonPortalFacing facing,
            WorldVector3 center, WorldBounds closedBlockingBounds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ConnectionId = connectionId ?? throw new ArgumentNullException(nameof(connectionId));
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            Facing = facing;
            Center = center;
            ClosedBlockingBounds = closedBlockingBounds;
        }

        public string Id { get; }
        public string ConnectionId { get; }
        public string RoomId { get; }
        public DungeonPortalFacing Facing { get; }
        public WorldVector3 Center { get; }
        public WorldBounds ClosedBlockingBounds { get; }
    }

    public enum DungeonSpawnRole
    {
        PlayerEntrance = 0,
        PlayerExit = 1,
        Boss = 2
    }

    public sealed class DungeonSpawnPoint
    {
        public DungeonSpawnPoint(string id, string roomId, DungeonSpawnRole role,
            WorldVector3 position, int yawDegrees)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            RoomId = roomId ?? throw new ArgumentNullException(nameof(roomId));
            Role = role;
            Position = position;
            YawDegrees = yawDegrees;
        }

        public string Id { get; }
        public string RoomId { get; }
        public DungeonSpawnRole Role { get; }
        public WorldVector3 Position { get; }
        public int YawDegrees { get; }
    }

    public sealed class DungeonLayout
    {
        public DungeonLayout(GenerationManifest manifest,
            IReadOnlyList<DungeonRoom> rooms, IReadOnlyList<DungeonConnection> connections,
            IReadOnlyList<DungeonCorridor> corridors, IReadOnlyList<DungeonDoorPortal> doorPortals,
            IReadOnlyList<DungeonSpawnPoint> spawnPoints)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            Rooms = rooms ?? throw new ArgumentNullException(nameof(rooms));
            Connections = connections ?? throw new ArgumentNullException(nameof(connections));
            Corridors = corridors ?? throw new ArgumentNullException(nameof(corridors));
            DoorPortals = doorPortals ?? throw new ArgumentNullException(nameof(doorPortals));
            SpawnPoints = spawnPoints ?? throw new ArgumentNullException(nameof(spawnPoints));
            Presentation = DungeonPresentationBuilder.Build(this);
            ConstructiveRecipes = DungeonConstructiveRecipeBuilder.Build(this);
        }

        public GenerationManifest Manifest { get; }
        public IReadOnlyList<DungeonRoom> Rooms { get; }
        public IReadOnlyList<DungeonConnection> Connections { get; }
        public IReadOnlyList<DungeonCorridor> Corridors { get; }
        public IReadOnlyList<DungeonDoorPortal> DoorPortals { get; }
        public IReadOnlyList<DungeonSpawnPoint> SpawnPoints { get; }
        public IReadOnlyList<DungeonPresentationElement> Presentation { get; }
        public IReadOnlyList<DungeonPrimitiveRecipe> ConstructiveRecipes { get; }
    }

    public sealed class DungeonRoomWallSection
    {
        public DungeonRoomWallSection(DungeonPortalFacing facing, int fixedAxis,
            int start, int end, bool acceptsPortals)
        {
            Facing = facing; FixedAxis = fixedAxis; Start = start; End = end;
            AcceptsPortals = acceptsPortals;
        }
        public DungeonPortalFacing Facing { get; }
        public int FixedAxis { get; }
        public int Start { get; }
        public int End { get; }
        public bool AcceptsPortals { get; }
    }

    public static class DungeonRoomGeometry
    {
        public const int SubwayTrackWidth = 5000;

        // The larger 5.12 train chamber reserves a full walking aisle beside
        // each car. Legacy room dimensions retain their original collision mesh.
        public static int SubwayTrackWidthForRoom(DungeonRoom room) =>
            room.ModuleKind == DungeonModuleKind.TrainChamber
            && Math.Min(room.Size.X, room.Size.Z) >= 42000 ? 7000 : SubwayTrackWidth;

        public static IReadOnlyList<WorldBounds> FloorSections(DungeonRoom room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (IsSubwayTrackRoom(room)) return SubwayTrackFloorSections(room);
            if (IsAuthoredSubwayEntrance(room)) return SubwayEntranceFloorSections(room);
            if (room.Shape == DungeonRoomShape.Cavern) return CavernFloorSections(room);
            if (room.Shape != DungeonRoomShape.LShaped) return new[] { room.Bounds };
            ResolveNotch(room, out int signX, out int signZ, out int cutX, out int cutZ);
            WorldBounds b = room.Bounds;
            int keptXMin = signX > 0 ? b.Minimum.X : cutX;
            int keptXMax = signX > 0 ? cutX : b.Maximum.X;
            int otherXMin = signX > 0 ? cutX : b.Minimum.X;
            int otherXMax = signX > 0 ? b.Maximum.X : cutX;
            int keptZMin = signZ > 0 ? b.Minimum.Z : cutZ;
            int keptZMax = signZ > 0 ? cutZ : b.Maximum.Z;
            return new[]
            {
                Bounds(keptXMin, b.Minimum.Y, b.Minimum.Z, keptXMax, b.Maximum.Y, b.Maximum.Z),
                Bounds(otherXMin, b.Minimum.Y, keptZMin, otherXMax, b.Maximum.Y, keptZMax)
            };
        }

        public static bool IsSubwayTrackRoom(DungeonRoom room) => room != null
            && (room.ModuleKind == DungeonModuleKind.TrainChamber
                || room.ModuleKind == DungeonModuleKind.TrackTunnel);

        private static bool IsAuthoredSubwayEntrance(DungeonRoom room) => room != null
            && room.ModuleKind == DungeonModuleKind.Entrance
            && room.Shape == DungeonRoomShape.LShaped
            && room.Size.X == 36000 && room.Size.Z == 36000;

        private static IReadOnlyList<WorldBounds> SubwayEntranceFloorSections(
            DungeonRoom room)
        {
            const int halfWidth = 6000;
            WorldBounds b = room.Bounds;
            // Two equal 8 m-wide legs meet at the bend. Their terminal end caps
            // are centered at (maxX, centerZ) and (centerX, maxZ).
            return new[]
            {
                Bounds(room.Center.X - halfWidth, b.Minimum.Y,
                    room.Center.Z - halfWidth, b.Maximum.X, b.Maximum.Y,
                    room.Center.Z + halfWidth),
                Bounds(room.Center.X - halfWidth, b.Minimum.Y,
                    room.Center.Z - halfWidth, room.Center.X + halfWidth,
                    b.Maximum.Y, b.Maximum.Z)
            };
        }

        private static void CavernCuts(DungeonRoom room, out int x0, out int x1,
            out int z0, out int z1)
        {
            WorldBounds b = room.Bounds;
            // Different shoulders on each side make an asymmetric but connected
            // cross-shaped chamber. All four door-bearing wall centers remain full-width.
            x0 = b.Minimum.X + room.Size.X * (25 + room.Index % 4 * 3) / 100;
            x1 = b.Maximum.X - room.Size.X * (27 + (room.Index * 3) % 4 * 3) / 100;
            z0 = b.Minimum.Z + room.Size.Z * (26 + (room.Index * 5) % 4 * 3) / 100;
            z1 = b.Maximum.Z - room.Size.Z * (24 + (room.Index * 7) % 4 * 3) / 100;
            // Cave doors are 4.2 m wide. Preserve the entire opening plus room for
            // the player capsule at all four arm tips, including asymmetric cuts.
            x0 = Math.Min(x0, room.Center.X - 2800);
            x1 = Math.Max(x1, room.Center.X + 2800);
            z0 = Math.Min(z0, room.Center.Z - 2800);
            z1 = Math.Max(z1, room.Center.Z + 2800);
        }

        private static IReadOnlyList<WorldBounds> CavernFloorSections(DungeonRoom room)
        {
            WorldBounds b = room.Bounds;
            CavernCuts(room, out int x0, out int x1, out int z0, out int z1);
            return new[]
            {
                Bounds(x0, b.Minimum.Y, z0, x1, b.Maximum.Y, z1),
                Bounds(b.Minimum.X, b.Minimum.Y, z0, x0, b.Maximum.Y, z1),
                Bounds(x1, b.Minimum.Y, z0, b.Maximum.X, b.Maximum.Y, z1),
                Bounds(x0, b.Minimum.Y, b.Minimum.Z, x1, b.Maximum.Y, z0),
                Bounds(x0, b.Minimum.Y, z1, x1, b.Maximum.Y, b.Maximum.Z)
            };
        }

        public static int SubwayTrackFloorY(DungeonRoom room) =>
            room.Bounds.Minimum.Y - 1200;

        // A continuous structural slab under the complete station footprint prevents
        // high-speed falls from finding a seam between the stepped platform/track
        // sections. Presentation and server collision both bake this same contract.
        public static int SubwayTrackFoundationDepth => 2000;

        public static WorldBounds SubwayTrackFoundationBounds(DungeonRoom room)
        {
            if (!IsSubwayTrackRoom(room))
                throw new ArgumentException("Room has no subway track.", nameof(room));
            WorldBounds b = room.Bounds;
            int top = SubwayTrackFloorY(room);
            return Bounds(b.Minimum.X, top - SubwayTrackFoundationDepth, b.Minimum.Z,
                b.Maximum.X, top, b.Maximum.Z);
        }

        // Physical platform lip along the trench. This keeps the recessed rail bed
        // visible while preventing a player from accidentally stepping down into it.
        // It is shared by client presentation and authoritative server collision.
        public static WorldBounds SubwayPlatformEdgeBounds(DungeonRoom room)
            => SubwayPlatformEdgeBounds(room, false);

        public static WorldBounds SubwayPlatformEdgeBounds(DungeonRoom room, bool outerEdge)
        {
            if (!IsSubwayTrackRoom(room))
                throw new ArgumentException("Room has no subway track.", nameof(room));
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int crossCenter = alongX ? room.Center.Z : room.Center.X;
            int edge = crossCenter + (outerEdge ? 1 : -1) * SubwayTrackWidthForRoom(room) / 2;
            return alongX
                ? Bounds(b.Minimum.X + 4000, b.Minimum.Y - 1200, edge - 120,
                    b.Maximum.X - 4000, b.Minimum.Y - 180, edge + 120)
                : Bounds(edge - 120, b.Minimum.Y - 1200, b.Minimum.Z + 4000,
                    edge + 120, b.Minimum.Y - 180, b.Maximum.Z - 4000);
        }

        public static WorldBounds SubwayPlatformEdgeSegmentBounds(DungeonRoom room,
            bool outerEdge, bool positiveAlong)
        {
            if (!IsSubwayTrackRoom(room))
                throw new ArgumentException("Room has no subway track.", nameof(room));
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int crossCenter = alongX ? room.Center.Z : room.Center.X;
            int edge = crossCenter + (outerEdge ? 1 : -1) * SubwayTrackWidthForRoom(room) / 2;
            int alongMin = alongX ? b.Minimum.X : b.Minimum.Z;
            int alongMax = alongX ? b.Maximum.X : b.Maximum.Z;
            int alongCenter = alongX ? room.Center.X : room.Center.Z;
            int stairHalfWidth = room.ModuleKind == DungeonModuleKind.TrackTunnel
                ? 1700 : 3500;
            int start = positiveAlong ? alongCenter + stairHalfWidth : alongMin;
            int end = positiveAlong ? alongMax : alongCenter - stairHalfWidth;
            return alongX
                ? Bounds(start, b.Minimum.Y - 1200, edge - 120,
                    end, b.Minimum.Y - 180, edge + 120)
                : Bounds(edge - 120, b.Minimum.Y - 1200, start,
                    edge + 120, b.Minimum.Y - 180, end);
        }

        public static IReadOnlyList<WorldBounds> SubwayTrackAccessSteps(DungeonRoom room)
        {
            if (!IsSubwayTrackRoom(room))
                throw new ArgumentException("Room has no subway track.", nameof(room));
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int crossMin = alongX ? b.Minimum.Z : b.Minimum.X;
            int crossSpan = alongX ? room.Size.Z : room.Size.X;
            int crossCenter = crossMin + crossSpan / 2;
            int lowEdge = crossCenter - SubwayTrackWidthForRoom(room) / 2;
            int highEdge = crossCenter + SubwayTrackWidthForRoom(room) / 2;
            int alongCenter = alongX ? room.Center.X : room.Center.Z;
            var steps = new List<WorldBounds>(12);
            if (room.ModuleKind == DungeonModuleKind.TrackTunnel)
            {
                const int stairHalfWidth = 1700;
                int treadDepth = (lowEdge - crossMin) / 6;
                for (int side = 0; side < 2; side++)
                    for (int step = 0; step < 6; step++)
                    {
                        int crossStart = side == 0
                            ? crossMin + step * treadDepth
                            : crossMin + crossSpan - (step + 1) * treadDepth;
                        int crossEnd = crossStart + treadDepth;
                        int level = b.Minimum.Y - step * 200;
                        steps.Add(alongX
                            ? Bounds(alongCenter - stairHalfWidth, level, crossStart,
                                alongCenter + stairHalfWidth, b.Maximum.Y, crossEnd)
                            : Bounds(crossStart, level, alongCenter - stairHalfWidth,
                                crossEnd, b.Maximum.Y, alongCenter + stairHalfWidth));
                    }
                return steps;
            }
            const int trainTreadDepth = 500;
            for (int side = 0; side < 2; side++)
                for (int step = 0; step < 6; step++)
                {
                    int crossStart = side == 0
                        ? lowEdge - (step + 1) * trainTreadDepth
                        : highEdge + step * trainTreadDepth;
                    int crossEnd = crossStart + trainTreadDepth;
                    int level = b.Minimum.Y - (5 - step) * 200;
                    steps.Add(alongX
                        ? Bounds(alongCenter - 3500, level, crossStart,
                            alongCenter + 3500, b.Maximum.Y, crossEnd)
                        : Bounds(crossStart, level, alongCenter - 3500,
                            crossEnd, b.Maximum.Y, alongCenter + 3500));
                }
            return steps;
        }

        public static WorldBounds SubwayTrackStairSideBounds(DungeonRoom room,
            bool outerPlatform, bool positiveAlongSide)
        {
            if (!IsSubwayTrackRoom(room))
                throw new ArgumentException("Room has no subway track.", nameof(room));
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int crossMin = alongX ? b.Minimum.Z : b.Minimum.X;
            int crossMax = alongX ? b.Maximum.Z : b.Maximum.X;
            int crossCenter = alongX ? room.Center.Z : room.Center.X;
            int lowEdge = crossCenter - SubwayTrackWidthForRoom(room) / 2;
            int highEdge = crossCenter + SubwayTrackWidthForRoom(room) / 2;
            int stairHalfWidth = room.ModuleKind == DungeonModuleKind.TrackTunnel
                ? 1700 : 3500;
            int alongCenter = alongX ? room.Center.X : room.Center.Z;
            int alongEdge = alongCenter + (positiveAlongSide ? 1 : -1) * stairHalfWidth;
            int stairDepth = room.ModuleKind == DungeonModuleKind.TrackTunnel
                ? lowEdge - crossMin : 3000;
            int stairCrossMin = outerPlatform ? highEdge : lowEdge - stairDepth;
            int stairCrossMax = outerPlatform ? highEdge + stairDepth : lowEdge;
            const int halfThickness = 100;
            return alongX
                ? Bounds(alongEdge - halfThickness, SubwayTrackFloorY(room), stairCrossMin,
                    alongEdge + halfThickness, b.Minimum.Y, stairCrossMax)
                : Bounds(stairCrossMin, SubwayTrackFloorY(room), alongEdge - halfThickness,
                    stairCrossMax, b.Minimum.Y, alongEdge + halfThickness);
        }

        public static WorldBounds SubwayTrainTunnelHeaderBounds(DungeonRoom room,
            bool positiveAlong)
        {
            if (room == null || room.ModuleKind != DungeonModuleKind.TrainChamber)
                throw new ArgumentException("Room has no train tunnel opening.", nameof(room));
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int alongEdge = alongX
                ? positiveAlong ? b.Maximum.X : b.Minimum.X
                : positiveAlong ? b.Maximum.Z : b.Minimum.Z;
            int crossCenter = alongX ? room.Center.Z : room.Center.X;
            // The procedural 5 m tunnel has 2.25 m vertical spring walls and a
            // 2.5 m arch radius, measured from the recessed track floor.
            int openingTop = SubwayTrackFloorY(room) + 4750;
            const int halfThickness = 125;
            return alongX
                ? Bounds(alongEdge - halfThickness, openingTop,
                    crossCenter - SubwayTrackWidthForRoom(room) / 2,
                    alongEdge + halfThickness, b.Maximum.Y,
                    crossCenter + SubwayTrackWidthForRoom(room) / 2)
                : Bounds(crossCenter - SubwayTrackWidthForRoom(room) / 2, openingTop,
                    alongEdge - halfThickness,
                    crossCenter + SubwayTrackWidthForRoom(room) / 2, b.Maximum.Y,
                    alongEdge + halfThickness);
        }

        public static WorldBounds SubwayTrainCarBounds(DungeonRoom room, int carIndex)
        {
            if (room == null || room.ModuleKind != DungeonModuleKind.TrainChamber)
                throw new ArgumentException("Room has no train.", nameof(room));
            if (carIndex < 0 || carIndex >= 3)
                throw new ArgumentOutOfRangeException(nameof(carIndex));
            bool alongX = room.Size.X >= room.Size.Z;
            int totalLength = (alongX ? room.Size.X : room.Size.Z) - 10000;
            int carLength = totalLength / 3 - 200;
            int alongCenter = (alongX ? room.Center.X : room.Center.Z)
                + (carIndex - 1) * (totalLength / 3);
            int crossCenter = alongX ? room.Center.Z : room.Center.X;
            int floor = SubwayTrackFloorY(room);
            return alongX
                ? Bounds(alongCenter - carLength / 2, floor + 650, crossCenter - 1175,
                    alongCenter + carLength / 2, floor + 3000, crossCenter + 1175)
                : Bounds(crossCenter - 1175, floor + 650, alongCenter - carLength / 2,
                    crossCenter + 1175, floor + 3000, alongCenter + carLength / 2);
        }

        private static IReadOnlyList<WorldBounds> SubwayTrackFloorSections(DungeonRoom room)
        {
            WorldBounds b = room.Bounds;
            bool alongX = room.Size.X >= room.Size.Z;
            int crossMin = alongX ? b.Minimum.Z : b.Minimum.X;
            int crossMax = alongX ? b.Maximum.Z : b.Maximum.X;
            int span = crossMax - crossMin;
            int crossCenter = crossMin + span / 2;
            int[] cuts =
            {
                crossMin,
                crossCenter - SubwayTrackWidthForRoom(room) / 2,
                crossCenter + SubwayTrackWidthForRoom(room) / 2,
                crossMax
            };
            int floor = b.Minimum.Y;
            // Do not approximate a slope with capsule-width ledges. Those narrow
            // intermediate strips offered several competing support heights at one
            // X/Z coordinate and produced correction loops when a player jumped near
            // the platform lip. Retaining walls separate two unambiguous platforms
            // from one continuous recessed track bed.
            if (room.ModuleKind == DungeonModuleKind.TrackTunnel)
            {
                const int stairHalfWidth = 1700;
                int alongMin = alongX ? b.Minimum.X : b.Minimum.Z;
                int alongMax = alongX ? b.Maximum.X : b.Maximum.Z;
                int alongCenter = alongX ? room.Center.X : room.Center.Z;
                var tunnelSections = new List<WorldBounds>(17)
                {
                    alongX
                        ? Bounds(alongMin, floor - 1200, cuts[1],
                            alongMax, b.Maximum.Y, cuts[2])
                        : Bounds(cuts[1], floor - 1200, alongMin,
                            cuts[2], b.Maximum.Y, alongMax)
                };
                for (int side = 0; side < 2; side++)
                {
                    int crossStart = cuts[side == 0 ? 0 : 2];
                    int crossEnd = cuts[side == 0 ? 1 : 3];
                    tunnelSections.Add(alongX
                        ? Bounds(alongMin, floor, crossStart,
                            alongCenter - stairHalfWidth, b.Maximum.Y, crossEnd)
                        : Bounds(crossStart, floor, alongMin,
                            crossEnd, b.Maximum.Y, alongCenter - stairHalfWidth));
                    tunnelSections.Add(alongX
                        ? Bounds(alongCenter + stairHalfWidth, floor, crossStart,
                            alongMax, b.Maximum.Y, crossEnd)
                        : Bounds(crossStart, floor, alongCenter + stairHalfWidth,
                            crossEnd, b.Maximum.Y, alongMax));
                }
                foreach (WorldBounds step in SubwayTrackAccessSteps(room))
                    tunnelSections.Add(step);
                return tunnelSections;
            }
            const int trainStairHalfWidth = 3500;
            const int trainStairDepth = 3000;
            int trainAlongMin = alongX ? b.Minimum.X : b.Minimum.Z;
            int trainAlongMax = alongX ? b.Maximum.X : b.Maximum.Z;
            int trainAlongCenter = alongX ? room.Center.X : room.Center.Z;
            var sections = new List<WorldBounds>(19)
            {
                alongX
                    ? Bounds(trainAlongMin, floor - 1200, cuts[1],
                        trainAlongMax, b.Maximum.Y, cuts[2])
                    : Bounds(cuts[1], floor - 1200, trainAlongMin,
                        cuts[2], b.Maximum.Y, trainAlongMax)
            };
            for (int side = 0; side < 2; side++)
            {
                int crossStart = cuts[side == 0 ? 0 : 2];
                int crossEnd = cuts[side == 0 ? 1 : 3];
                sections.Add(alongX
                    ? Bounds(trainAlongMin, floor, crossStart,
                        trainAlongCenter - trainStairHalfWidth, b.Maximum.Y, crossEnd)
                    : Bounds(crossStart, floor, trainAlongMin,
                        crossEnd, b.Maximum.Y, trainAlongCenter - trainStairHalfWidth));
                sections.Add(alongX
                    ? Bounds(trainAlongCenter + trainStairHalfWidth, floor, crossStart,
                        trainAlongMax, b.Maximum.Y, crossEnd)
                    : Bounds(crossStart, floor, trainAlongCenter + trainStairHalfWidth,
                        crossEnd, b.Maximum.Y, trainAlongMax));
                int outerStart = side == 0 ? crossStart : crossStart + trainStairDepth;
                int outerEnd = side == 0 ? crossEnd - trainStairDepth : crossEnd;
                sections.Add(alongX
                    ? Bounds(trainAlongCenter - trainStairHalfWidth, floor, outerStart,
                        trainAlongCenter + trainStairHalfWidth, b.Maximum.Y, outerEnd)
                    : Bounds(outerStart, floor, trainAlongCenter - trainStairHalfWidth,
                        outerEnd, b.Maximum.Y, trainAlongCenter + trainStairHalfWidth));
            }
            foreach (WorldBounds step in SubwayTrackAccessSteps(room))
                sections.Add(step);
            return sections;
        }

        public static IReadOnlyList<DungeonRoomWallSection> WallSections(DungeonRoom room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            WorldBounds b = room.Bounds;
            if (IsAuthoredSubwayEntrance(room))
            {
                const int halfWidth = 6000;
                int minX = room.Center.X - halfWidth;
                int maxX = room.Center.X + halfWidth;
                int minZ = room.Center.Z - halfWidth;
                int maxZ = room.Center.Z + halfWidth;
                return new[]
                {
                    // Closed outside edges of the L.
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ,
                        minZ, minX, b.Maximum.X, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeX,
                        minX, minZ, b.Maximum.Z, false),
                    // Precisely centered terminal sockets.
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX,
                        b.Maximum.X, minZ, maxZ, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ,
                        b.Maximum.Z, minX, maxX, true),
                    // Inner faces bordering the absent quadrant.
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ,
                        maxZ, maxX, b.Maximum.X, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX,
                        maxX, maxZ, b.Maximum.Z, false)
                };
            }
            if (room.Shape == DungeonRoomShape.Cavern)
            {
                CavernCuts(room, out int x0, out int x1, out int z0, out int z1);
                return new[]
                {
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeX, b.Minimum.X, z0, z1, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX, b.Maximum.X, z0, z1, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ, b.Minimum.Z, x0, x1, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ, b.Maximum.Z, x0, x1, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeX, x0, b.Minimum.Z, z0, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeX, x0, z1, b.Maximum.Z, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX, x1, b.Minimum.Z, z0, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX, x1, z1, b.Maximum.Z, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ, z0, b.Minimum.X, x0, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ, z0, x1, b.Maximum.X, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ, z1, b.Minimum.X, x0, false),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ, z1, x1, b.Maximum.X, false)
                };
            }
            if (room.Shape != DungeonRoomShape.LShaped)
                return new[]
                {
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeX, b.Minimum.X, b.Minimum.Z, b.Maximum.Z, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveX, b.Maximum.X, b.Minimum.Z, b.Maximum.Z, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ, b.Minimum.Z, b.Minimum.X, b.Maximum.X, true),
                    new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ, b.Maximum.Z, b.Minimum.X, b.Maximum.X, true)
                };
            ResolveNotch(room, out int signX, out int signZ, out int cutX, out int cutZ);
            int keptZStart = signZ > 0 ? b.Minimum.Z : cutZ;
            int keptZEnd = signZ > 0 ? cutZ : b.Maximum.Z;
            int keptXStart = signX > 0 ? b.Minimum.X : cutX;
            int keptXEnd = signX > 0 ? cutX : b.Maximum.X;
            return new[]
            {
                new DungeonRoomWallSection(DungeonPortalFacing.NegativeX, b.Minimum.X,
                    signX < 0 ? keptZStart : b.Minimum.Z, signX < 0 ? keptZEnd : b.Maximum.Z, true),
                new DungeonRoomWallSection(DungeonPortalFacing.PositiveX, b.Maximum.X,
                    signX > 0 ? keptZStart : b.Minimum.Z, signX > 0 ? keptZEnd : b.Maximum.Z, true),
                new DungeonRoomWallSection(DungeonPortalFacing.NegativeZ, b.Minimum.Z,
                    signZ < 0 ? keptXStart : b.Minimum.X, signZ < 0 ? keptXEnd : b.Maximum.X, true),
                new DungeonRoomWallSection(DungeonPortalFacing.PositiveZ, b.Maximum.Z,
                    signZ > 0 ? keptXStart : b.Minimum.X, signZ > 0 ? keptXEnd : b.Maximum.X, true),
                new DungeonRoomWallSection(signX > 0 ? DungeonPortalFacing.PositiveX : DungeonPortalFacing.NegativeX,
                    cutX, signZ > 0 ? cutZ : b.Minimum.Z, signZ > 0 ? b.Maximum.Z : cutZ, false),
                new DungeonRoomWallSection(signZ > 0 ? DungeonPortalFacing.PositiveZ : DungeonPortalFacing.NegativeZ,
                    cutZ, signX > 0 ? cutX : b.Minimum.X, signX > 0 ? b.Maximum.X : cutX, false)
            };
        }

        private static void ResolveNotch(DungeonRoom room, out int signX, out int signZ,
            out int cutX, out int cutZ)
        {
            signX = (room.Index & 1) == 0 ? 1 : -1;
            signZ = (room.Index & 2) == 0 ? 1 : -1;
            // The inner notch must stay outside the entire door aperture, not just
            // its center. A 1.2 m notch clipped the 4.2 m cave doors at some seeds.
            int clearance = Math.Min(3000, Math.Min(room.Size.X, room.Size.Z) / 4);
            cutX = room.Center.X + signX * clearance;
            cutZ = room.Center.Z + signZ * clearance;
        }

        private static WorldBounds Bounds(int minX, int minY, int minZ,
            int maxX, int maxY, int maxZ) => new WorldBounds(
            new WorldVector3(minX, minY, minZ), new WorldVector3(maxX, maxY, maxZ));
    }
}
