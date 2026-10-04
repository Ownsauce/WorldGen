using System;
using System.Collections.Generic;
using WorldGen.Contracts;
using WorldGen.Determinism;
using WorldGen.Identity;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public sealed class DungeonGenerator
    {
        private static readonly GridPoint[] Directions =
        {
            new GridPoint(1, 0), new GridPoint(-1, 0),
            new GridPoint(0, 1), new GridPoint(0, -1)
        };

        public DungeonLayout Generate(GenerationManifest manifest, DungeonGenerationParameters parameters)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            DungeonLayoutProfile requestedProfile =
                DungeonGenerationProfileCatalog.ReadProfile(manifest);
            if (requestedProfile == DungeonLayoutProfile.TempleReference)
                return TempleReferenceLayout.Generate(manifest, parameters);

            var layoutRandom = SeedDerivation.CreateStream(manifest.Seed, "dungeon/layout");
            var architectureRandom = SeedDerivation.CreateStream(manifest.Seed, "dungeon/architecture");
            DungeonLayoutProfile profile = DungeonGenerationProfileCatalog.ReadProfile(manifest);
            bool steepCave = profile == DungeonLayoutProfile.CaveDungeon
                && (manifest.Seed & 1UL) != 0;
            int criticalCount = profile == DungeonLayoutProfile.Subway
                ? Math.Max(6, (parameters.RoomCount + 2) / 3)
                : profile == DungeonLayoutProfile.GroupDungeon
                  || profile == DungeonLayoutProfile.CaveDungeon
                    ? Math.Min(parameters.RoomCount, Math.Max(5, (parameters.RoomCount + 1) / 2))
                : Math.Max(3, (parameters.RoomCount * 2 + 2) / 3);
            var grid = PlaceRooms(parameters.RoomCount, criticalCount, profile, layoutRandom,
                manifest.GeneratorVersion);
            int subwayGrandHallIndex = criticalCount + 4;
            int subwayRestroomIndex = criticalCount + 5;
            if (profile == DungeonLayoutProfile.Subway
                && (string.Equals(manifest.GeneratorVersion, "5.9.0", StringComparison.Ordinal)
                    || string.Equals(manifest.GeneratorVersion, "5.10.0", StringComparison.Ordinal)
                    || string.Equals(manifest.GeneratorVersion, "5.11.0", StringComparison.Ordinal)
                    || (string.Equals(manifest.GeneratorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(manifest.GeneratorVersion, "5.13.0", StringComparison.Ordinal))))
            {
                var landmarkRandom = SeedDerivation.CreateStream(
                    manifest.Seed, "dungeon/subway-landmarks");
                int optionalCount = Math.Max(0, parameters.RoomCount - criticalCount);
                int lateStart = Math.Min(parameters.RoomCount - 1,
                    criticalCount + Math.Max(6, optionalCount / 2));
                subwayGrandHallIndex = landmarkRandom.NextInt(
                    lateStart, parameters.RoomCount);
                // Keep the restroom on an earlier side wing and never replace the hall.
                subwayRestroomIndex = Math.Min(parameters.RoomCount - 1,
                    criticalCount + 5);
                if (subwayRestroomIndex == subwayGrandHallIndex)
                    subwayRestroomIndex = Math.Max(criticalCount + 3,
                        subwayGrandHallIndex - 1);
            }
            bool pedestrianSubwayBranches = profile == DungeonLayoutProfile.Subway
                && (string.Equals(manifest.GeneratorVersion, "5.11.0", StringComparison.Ordinal)
                    || (string.Equals(manifest.GeneratorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(manifest.GeneratorVersion, "5.13.0", StringComparison.Ordinal)));
            var degrees = new int[grid.Count];
            for (int i = 1; i < grid.Count; i++)
            { degrees[i]++; degrees[grid[i].ParentIndex]++; }
            var rooms = new List<DungeonRoom>(parameters.RoomCount);
            var floorElevations = new int[grid.Count];
            for (int i = 0; i < grid.Count; i++)
            {
                DungeonRoomRole role = ResolveRole(i, criticalCount, parameters.RoomCount);
                DungeonRoomShape shape = ResolveShape(role, degrees[i], profile, architectureRandom);
                if (profile == DungeonLayoutProfile.CaveDungeon && (i == 1 || i == 2))
                    shape = DungeonRoomShape.GrandChamber;
                DungeonModuleKind moduleKind = ResolveModuleKind(
                    role, shape, degrees[i], profile);
                if (profile == DungeonLayoutProfile.Subway)
                {
                    int trainRoomIndex = SubwayTrainRoomIndex(criticalCount);
                    if (i == 0)
                    {
                        shape = DungeonRoomShape.LShaped;
                        moduleKind = DungeonModuleKind.Entrance;
                    }
                    else if (i == trainRoomIndex)
                        moduleKind = DungeonModuleKind.TrainChamber;
                    else if (i == trainRoomIndex - 1 || i == trainRoomIndex + 1)
                    {
                        moduleKind = DungeonModuleKind.TrackTunnel;
                        shape = DungeonRoomShape.LongHall;
                    }
                    else if (moduleKind == DungeonModuleKind.TrainChamber)
                        moduleKind = DungeonModuleKind.StationConcourse;
                    // Subway generation is restricted to the approved authored kit.
                    // Every non-special node is a clean square circulation room;
                    // older service/treasure/terminal treatments are not selected.
                    if (i != 0 && i != trainRoomIndex
                        && i != trainRoomIndex - 1 && i != trainRoomIndex + 1)
                    {
                        moduleKind = DungeonModuleKind.StationConcourse;
                        shape = DungeonRoomShape.Rectangle;
                    }
                    if (i == criticalCount && !pedestrianSubwayBranches)
                    {
                        moduleKind = DungeonModuleKind.TrackJunction;
                        shape = DungeonRoomShape.GrandChamber;
                    }
                    else if ((i == criticalCount + 1 || i == criticalCount + 2)
                        && !pedestrianSubwayBranches)
                    {
                        moduleKind = DungeonModuleKind.TrackTunnel;
                        shape = DungeonRoomShape.LongHall;
                    }
                    else if (i == subwayGrandHallIndex)
                    {
                        moduleKind = DungeonModuleKind.GrandHall;
                        shape = DungeonRoomShape.GrandChamber;
                    }
                    else if (i == subwayRestroomIndex)
                    {
                        moduleKind = DungeonModuleKind.Restroom;
                        shape = DungeonRoomShape.Rectangle;
                    }
                }
                else if (profile == DungeonLayoutProfile.Temple)
                {
                    moduleKind = ResolveTempleModuleKind(i, criticalCount);
                    shape = ResolveTempleShape(moduleKind);
                }
                int width = architectureRandom.NextInt(parameters.MinimumRoomSize, parameters.MaximumRoomSize + 1);
                int depth = architectureRandom.NextInt(parameters.MinimumRoomSize, parameters.MaximumRoomSize + 1);
                if (shape == DungeonRoomShape.WideHall)
                { width = parameters.MaximumRoomSize; depth = parameters.MinimumRoomSize; }
                else if (shape == DungeonRoomShape.LongHall)
                { width = parameters.MinimumRoomSize; depth = parameters.MaximumRoomSize; }
                else if (shape == DungeonRoomShape.GrandChamber)
                { width = parameters.MaximumRoomSize; depth = parameters.MaximumRoomSize; }
                if (profile == DungeonLayoutProfile.Subway
                    && moduleKind == DungeonModuleKind.StationConcourse)
                {
                    int squareSize = (width + depth) / 2;
                    width = squareSize;
                    depth = squareSize;
                }
                if (profile == DungeonLayoutProfile.GroupDungeon
                    && (role == DungeonRoomRole.Boss || role == DungeonRoomRole.Objective))
                { width = 24000; depth = 24000; }
                if (profile == DungeonLayoutProfile.CaveDungeon
                    && (role == DungeonRoomRole.Boss || role == DungeonRoomRole.Objective))
                { width = 30000; depth = 30000; }
                if (profile == DungeonLayoutProfile.CaveDungeon
                    && role == DungeonRoomRole.Entrance)
                { width = 24000; depth = 24000; }
                if (profile == DungeonLayoutProfile.CaveDungeon && (i == 1 || i == 2))
                { width = 26000; depth = 26000; }
                if (moduleKind == DungeonModuleKind.TrainChamber)
                {
                    // This is a genuine station concourse and through-route, not a
                    // train-sized prop squeezed into an ordinary 9-15 m room. Align
                    // its long axis with the critical path through the station.
                    bool routeAlongX = grid[i + 1].X != grid[i].X;
                    width = routeAlongX ? 44000 : 40000;
                    depth = routeAlongX ? 40000 : 44000;
                    if ((string.Equals(manifest.GeneratorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(manifest.GeneratorVersion, "5.13.0", StringComparison.Ordinal)))
                    {
                        width += 2000;
                        depth += 2000;
                    }
                }
                else if (moduleKind == DungeonModuleKind.TrackTunnel)
                {
                    // Repeatable rail bays share a narrow cross-section and align
                    // their long axis with the critical route. Side connections are
                    // still cut from the room walls by the ordinary portal contract,
                    // yielding sealed, one-exit, and two-exit variants automatically.
                    bool routeAlongX = i > criticalCount
                        ? grid[i].X != grid[grid[i].ParentIndex].X
                        : SubwayTrackAlongX(grid, i, criticalCount);
                    width = routeAlongX ? 28000 : 12000;
                    depth = routeAlongX ? 12000 : 28000;
                }
                else if (moduleKind == DungeonModuleKind.TrackJunction)
                { width = 18000; depth = 18000; }
                else if (moduleKind == DungeonModuleKind.GrandHall)
                { width = 44000; depth = 40000; }
                else if (moduleKind == DungeonModuleKind.Restroom)
                { width = 18000; depth = 14000; }
                else if (profile == DungeonLayoutProfile.Subway && i == 0)
                {
                    // Authored entrance L: each leg now has an 18 m centerline run
                    // from the bend to its terminal wall (36 m overall footprint).
                    width = 36000;
                    depth = 36000;
                }
                else if (profile == DungeonLayoutProfile.Temple)
                {
                    ResolveTempleSize(moduleKind, grid, i, out width, out depth);
                }
                int height = architectureRandom.NextInt(3500, 5501);
                if (profile == DungeonLayoutProfile.CaveDungeon
                    && role == DungeonRoomRole.Entrance)
                    height = 9000;
                if (profile == DungeonLayoutProfile.CaveDungeon && (i == 1 || i == 2))
                    height = 9000;
                if (moduleKind == DungeonModuleKind.TrainChamber)
                    height = 10000;
                else if (moduleKind == DungeonModuleKind.TrackTunnel)
                    height = 6000;
                else if (moduleKind == DungeonModuleKind.GrandHall)
                    height = 10000;
                else if (moduleKind == DungeonModuleKind.Restroom)
                    height = 5000;
                else if (profile == DungeonLayoutProfile.Temple)
                    height = TempleRoomHeight(moduleKind);
                int floorY = ResolveFloorElevation(i, criticalCount, profile, grid,
                    floorElevations, steepCave);
                if (profile == DungeonLayoutProfile.Subway && i == 1
                    && (manifest.Seed & 1UL) != 0)
                    // Seeded entrance variant: the same centered L socket can feed
                    // either a level passage or the shared 3 m subway stair module.
                    floorY = 3000;
                if (profile == DungeonLayoutProfile.Subway && i == 2
                    && (manifest.Seed & 1UL) != 0)
                    floorY = 0;
                if (profile == DungeonLayoutProfile.Subway
                    && Math.Abs(i - SubwayTrainRoomIndex(criticalCount)) <= 1)
                    floorY = 0;
                if (profile == DungeonLayoutProfile.Subway && !pedestrianSubwayBranches
                    && (i == criticalCount + 1 || i == criticalCount + 2))
                    floorY = 0;
                if (moduleKind == DungeonModuleKind.TrackJunction)
                    floorY = -1200;
                floorElevations[i] = floorY;
                var center = new WorldVector3(grid[i].X * parameters.GridSpacing, floorY,
                    grid[i].Z * parameters.GridSpacing);
                var size = new WorldVector3(width, height, depth);
                var minimum = new WorldVector3(center.X - width / 2, center.Y, center.Z - depth / 2);
                var maximum = new WorldVector3(center.X + (width - width / 2), center.Y + height,
                    center.Z + (depth - depth / 2));
                string id = StableWorldId.Create(manifest.WorldId, "room-" + i.ToString("D3"));
                rooms.Add(new DungeonRoom(id, i, role, center, size,
                    new WorldBounds(minimum, maximum), shape, moduleKind));
            }

            var connections = new List<DungeonConnection>(parameters.RoomCount - 1);
            for (int i = 1; i < rooms.Count; i++)
            {
                int parent = grid[i].ParentIndex;
                string id = StableWorldId.Create(manifest.WorldId, "connection-" + parent.ToString("D3") + "-" + i.ToString("D3"));
                connections.Add(new DungeonConnection(id, rooms[parent].Id, rooms[i].Id,
                    i < criticalCount && parent == i - 1));
            }

            var corridors = new List<DungeonCorridor>(connections.Count);
            var portals = new List<DungeonDoorPortal>(connections.Count * 2);
            for (int i = 0; i < connections.Count; i++)
                AddPassage(manifest, parameters, rooms, connections[i], i, corridors, portals);

            DungeonRoom entrance = rooms[0];
            DungeonRoom boss = rooms[criticalCount - 1];
            var spawns = new List<DungeonSpawnPoint>
            {
                CreateSpawn(manifest, entrance, DungeonSpawnRole.PlayerEntrance, "entrance", 90),
                CreateSpawn(manifest, entrance, DungeonSpawnRole.PlayerExit, "exit", 270),
                CreateSpawn(manifest, boss, DungeonSpawnRole.Boss, "boss", 180)
            };

            var result = new DungeonLayout(manifest, rooms, connections, corridors, portals, spawns);
            DungeonValidationResult validation = DungeonValidator.Validate(result);
            if (!validation.IsValid)
                throw new InvalidOperationException("Generated dungeon was invalid: " + string.Join("; ", validation.Errors));
            return result;
        }

        internal static DungeonSpawnPoint CreateSpawn(GenerationManifest manifest, DungeonRoom room,
            DungeonSpawnRole role, string key, int yaw) =>
            new DungeonSpawnPoint(StableWorldId.Create(manifest.WorldId, "spawn-" + key), room.Id,
                role, new WorldVector3(room.Center.X, room.Bounds.Minimum.Y, room.Center.Z), yaw);

        internal static void AddPassage(GenerationManifest manifest, DungeonGenerationParameters parameters,
            IReadOnlyList<DungeonRoom> rooms, DungeonConnection connection, int connectionIndex,
            ICollection<DungeonCorridor> corridors, ICollection<DungeonDoorPortal> portals)
        {
            DungeonRoom from = FindRoom(rooms, connection.FromRoomId);
            DungeonRoom to = FindRoom(rooms, connection.ToRoomId);
            bool railModuleJoin = IsRailModule(from) && IsRailModule(to);
            int dx = Math.Sign(to.Center.X - from.Center.X);
            int dz = Math.Sign(to.Center.Z - from.Center.Z);
            bool overlapsX = from.Bounds.Minimum.X < to.Bounds.Maximum.X
                && from.Bounds.Maximum.X > to.Bounds.Minimum.X;
            bool overlapsZ = from.Bounds.Minimum.Z < to.Bounds.Maximum.Z
                && from.Bounds.Maximum.Z > to.Bounds.Minimum.Z;
            bool alongX = !overlapsX && (overlapsZ
                || Math.Abs(to.Center.X - from.Center.X)
                    >= Math.Abs(to.Center.Z - from.Center.Z));
            if (alongX) dz = 0;
            else dx = 0;
            int floorY = railModuleJoin
                ? DungeonRoomGeometry.SubwayTrackFloorY(from)
                : Math.Min(from.Bounds.Minimum.Y, to.Bounds.Minimum.Y);
            int ceilingY = Math.Max(from.Bounds.Maximum.Y, to.Bounds.Maximum.Y);
            if (railModuleJoin)
                ceilingY = Math.Min(from.Bounds.Maximum.Y, to.Bounds.Maximum.Y);
            int fromEdge = alongX ? (dx > 0 ? from.Bounds.Maximum.X : from.Bounds.Minimum.X)
                                  : (dz > 0 ? from.Bounds.Maximum.Z : from.Bounds.Minimum.Z);
            int toEdge = alongX ? (dx > 0 ? to.Bounds.Minimum.X : to.Bounds.Maximum.X)
                                : (dz > 0 ? to.Bounds.Minimum.Z : to.Bounds.Maximum.Z);
            DungeonPortalFacing fromFacing = dx > 0 ? DungeonPortalFacing.PositiveX
                : dx < 0 ? DungeonPortalFacing.NegativeX
                : dz > 0 ? DungeonPortalFacing.PositiveZ : DungeonPortalFacing.NegativeZ;
            DungeonPortalFacing toFacing = dx > 0 ? DungeonPortalFacing.NegativeX
                : dx < 0 ? DungeonPortalFacing.PositiveX
                : dz > 0 ? DungeonPortalFacing.NegativeZ : DungeonPortalFacing.PositiveZ;
            int corridorWidth = connection.IsCriticalPath
                ? parameters.CorridorWidth
                : Math.Max(parameters.DoorwayWidth, parameters.CorridorWidth * 3 / 5);
            DungeonLayoutProfile profile = DungeonGenerationProfileCatalog.ReadProfile(manifest);
            bool wideSubwayBranchDoor = profile == DungeonLayoutProfile.Subway
                && (string.Equals(manifest.GeneratorVersion, "5.11.0", StringComparison.Ordinal)
                    || (string.Equals(manifest.GeneratorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(manifest.GeneratorVersion, "5.13.0", StringComparison.Ordinal)))
                && !connection.IsCriticalPath
                && from.ModuleKind == DungeonModuleKind.StationConcourse
                && to.ModuleKind == DungeonModuleKind.StationConcourse;
            if (wideSubwayBranchDoor) corridorWidth = Math.Max(corridorWidth, 4000);
            bool cave = profile == DungeonLayoutProfile.CaveDungeon;
            bool subwayWalkingTunnel = profile == DungeonLayoutProfile.Subway
                && !railModuleJoin
                && (from.ModuleKind == DungeonModuleKind.TrackTunnel
                    || to.ModuleKind == DungeonModuleKind.TrackTunnel);
            bool subwayEntranceJoin = profile == DungeonLayoutProfile.Subway
                && (from.ModuleKind == DungeonModuleKind.Entrance
                    || to.ModuleKind == DungeonModuleKind.Entrance);
            if (subwayWalkingTunnel)
                corridorWidth = 12000;
            if (subwayEntranceJoin)
                corridorWidth = 8000;
            DungeonCaveSocketProfile fromSocket = cave
                ? DungeonCaveSockets.Select(manifest.Seed, connectionIndex, 0) : default;
            DungeonCaveSocketProfile toSocket = cave
                ? DungeonCaveSockets.Select(manifest.Seed, connectionIndex, 1) : default;
            if (cave)
            {
                // The current authored entrance chamber owns one proven 3 m-high
                // Standard socket. Add its other four opening variants only when
                // their local ceiling silhouettes have been authored as well.
                if (from.Index == 0)
                    fromSocket = DungeonCaveSockets.Get(DungeonCaveSocketKind.Standard);
                if (to.Index == 0)
                    toSocket = DungeonCaveSockets.Get(DungeonCaveSocketKind.Standard);
                fromSocket = DungeonCaveSockets.Fit(fromSocket,
                    PortalWallWidth(from, fromFacing));
                toSocket = DungeonCaveSockets.Fit(toSocket,
                    PortalWallWidth(to, toFacing));
            }
            if (cave && connectionIndex == 0 && connection.IsCriticalPath)
                // The entrance chamber, connector, and authored S-bend share the
                // standard cave socket. Expanding this corridor to nearly the full
                // room width cut away most of the chamber wall and produced a
                // second chamber-sized tunnel over the entrance spawn.
                corridorWidth = parameters.CorridorWidth;
            else if (cave && connectionIndex == 1 && connection.IsCriticalPath)
            {
                corridorWidth = 215000;
                // The wide two-level bend needs its own high interior envelope.
                // Endpoint socket crowns still control the visible openings.
                ceilingY = Math.Max(ceilingY, floorY + 18000);
            }
            else if (cave && connection.IsCriticalPath)
                corridorWidth -= connectionIndex % 3 * 650;
            if (cave)
                corridorWidth = Math.Max(corridorWidth,
                    Math.Max(fromSocket.Width, toSocket.Width));
            int crossOffset = cave
                ? connectionIndex <= 1 ? 0
                    : (connectionIndex % 5 - 2) * (connection.IsCriticalPath ? 400 : 100)
                : 0;
            int halfCorridor = corridorWidth / 2;
            if (cave && connectionIndex == 1 && connection.IsCriticalPath)
                crossOffset = 92500;
            WorldBounds corridorBounds = alongX
                ? Bounds(Math.Min(fromEdge, toEdge), floorY,
                    from.Center.Z + crossOffset - halfCorridor,
                    Math.Max(fromEdge, toEdge), ceilingY,
                    from.Center.Z + crossOffset + (corridorWidth - halfCorridor))
                : Bounds(from.Center.X + crossOffset - halfCorridor, floorY,
                    Math.Min(fromEdge, toEdge),
                    from.Center.X + crossOffset + (corridorWidth - halfCorridor),
                    ceilingY, Math.Max(fromEdge, toEdge));
            string passageKey = connectionIndex.ToString("D3");
            corridors.Add(new DungeonCorridor(StableWorldId.Create(manifest.WorldId, "corridor-" + passageKey),
                connection.Id, corridorBounds,
                railModuleJoin ? floorY : from.Bounds.Minimum.Y,
                railModuleJoin ? floorY : to.Bounds.Minimum.Y));

            AddPortal(manifest, parameters, connection, connectionIndex, from, fromEdge, alongX,
                cave ? fromSocket.Width : railModuleJoin || subwayEntranceJoin ? corridorWidth
                    : wideSubwayBranchDoor ? 3600 : parameters.DoorwayWidth,
                fromFacing, portals);
            AddPortal(manifest, parameters, connection, connectionIndex, to, toEdge, alongX,
                cave ? toSocket.Width : railModuleJoin || subwayEntranceJoin ? corridorWidth
                    : wideSubwayBranchDoor ? 3600 : parameters.DoorwayWidth,
                toFacing, portals);
        }

        private static bool IsRailModule(DungeonRoom room) =>
            room.ModuleKind == DungeonModuleKind.TrainChamber
            || room.ModuleKind == DungeonModuleKind.TrackTunnel
            || room.ModuleKind == DungeonModuleKind.TrackJunction;

        private static int PortalWallWidth(DungeonRoom room, DungeonPortalFacing facing)
        {
            int width = 0;
            int cross = facing == DungeonPortalFacing.NegativeX
                || facing == DungeonPortalFacing.PositiveX ? room.Center.Z : room.Center.X;
            foreach (DungeonRoomWallSection wall in DungeonRoomGeometry.WallSections(room))
                if (wall.AcceptsPortals && wall.Facing == facing)
                {
                    if (cross < wall.Start || cross > wall.End) continue;
                    width = Math.Max(width, 2 * Math.Min(cross - wall.Start, wall.End - cross));
                }
            return width;
        }

        private static void AddPortal(GenerationManifest manifest, DungeonGenerationParameters parameters,
            DungeonConnection connection, int connectionIndex, DungeonRoom room, int edge, bool alongX,
            int doorwayWidth,
            DungeonPortalFacing facing, ICollection<DungeonDoorPortal> portals)
        {
            int halfWidth = doorwayWidth / 2;
            int halfThickness = parameters.DoorThickness / 2;
            WorldVector3 center = alongX
                ? new WorldVector3(edge, room.Bounds.Minimum.Y, room.Center.Z)
                : new WorldVector3(room.Center.X, room.Bounds.Minimum.Y, edge);
            WorldBounds blocker = alongX
                ? Bounds(edge - halfThickness, room.Bounds.Minimum.Y, room.Center.Z - halfWidth,
                    edge + (parameters.DoorThickness - halfThickness), room.Bounds.Maximum.Y,
                    room.Center.Z + (doorwayWidth - halfWidth))
                : Bounds(room.Center.X - halfWidth, room.Bounds.Minimum.Y, edge - halfThickness,
                    room.Center.X + (doorwayWidth - halfWidth), room.Bounds.Maximum.Y,
                    edge + (parameters.DoorThickness - halfThickness));
            string side = room.Id == connection.FromRoomId ? "from" : "to";
            portals.Add(new DungeonDoorPortal(
                StableWorldId.Create(manifest.WorldId, "door-" + connectionIndex.ToString("D3") + "-" + side),
                connection.Id, room.Id, facing, center, blocker));
        }

        private static WorldBounds Bounds(int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
            new WorldBounds(new WorldVector3(minX, minY, minZ), new WorldVector3(maxX, maxY, maxZ));

        private static DungeonRoom FindRoom(IReadOnlyList<DungeonRoom> rooms, string id)
        {
            for (int i = 0; i < rooms.Count; i++) if (rooms[i].Id == id) return rooms[i];
            throw new InvalidOperationException("Connection references missing room " + id);
        }

        private static List<PlacedGridPoint> PlaceRooms(int count, int criticalCount,
            DungeonLayoutProfile profile, DeterministicRandom random, string generatorVersion)
        {
            if (profile == DungeonLayoutProfile.Subway)
                return string.Equals(generatorVersion, "5.8.0", StringComparison.Ordinal)
                    || string.Equals(generatorVersion, "5.9.0", StringComparison.Ordinal)
                    || string.Equals(generatorVersion, "5.10.0", StringComparison.Ordinal)
                    || string.Equals(generatorVersion, "5.11.0", StringComparison.Ordinal)
                    || (string.Equals(generatorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(generatorVersion, "5.13.0", StringComparison.Ordinal))
                    ? PlaceVariedSubwayRooms(count, criticalCount, random,
                        string.Equals(generatorVersion, "5.10.0", StringComparison.Ordinal)
                            || string.Equals(generatorVersion, "5.11.0", StringComparison.Ordinal)
                            || (string.Equals(generatorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(generatorVersion, "5.13.0", StringComparison.Ordinal)),
                        string.Equals(generatorVersion, "5.11.0", StringComparison.Ordinal)
                            || (string.Equals(generatorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(generatorVersion, "5.13.0", StringComparison.Ordinal)),
                        (string.Equals(generatorVersion, "5.12.0", StringComparison.Ordinal) || string.Equals(generatorVersion, "5.13.0", StringComparison.Ordinal)))
                    : PlaceSubwayRooms(count, criticalCount, random);
            if (profile == DungeonLayoutProfile.Temple)
                return PlaceTempleRooms(count, criticalCount, random);
            if (profile == DungeonLayoutProfile.GroupDungeon
                || profile == DungeonLayoutProfile.CaveDungeon)
                return PlaceGroupDungeonRooms(count, criticalCount, random,
                    profile == DungeonLayoutProfile.CaveDungeon);
            var result = new List<PlacedGridPoint>(count) { new PlacedGridPoint(0, 0, -1) };
            var occupied = new HashSet<GridPoint> { new GridPoint(0, 0) };
            int primary = random.NextInt(Directions.Length);
            GridPoint primaryDirection = Directions[primary];
            GridPoint turnDirection = primaryDirection.X != 0
                ? Directions[random.NextInt(2) == 0 ? 2 : 3]
                : Directions[random.NextInt(2) == 0 ? 0 : 1];
            for (int i = 1; i < count; i++)
            {
                if (i < criticalCount)
                {
                    PlacedGridPoint parent = result[i - 1];
                    GridPoint direction = random.NextInt(100) < 58
                        ? primaryDirection : turnDirection;
                    var critical = new PlacedGridPoint(parent.X + direction.X,
                        parent.Z + direction.Z, i - 1);
                    occupied.Add(new GridPoint(critical.X, critical.Z));
                    result.Add(critical);
                    continue;
                }
                int firstParent = random.NextInt(i);
                if (!TryPlaceFromParent(result, occupied, firstParent, random, out PlacedGridPoint placed))
                {
                    bool found = false;
                    for (int parent = 0; parent < result.Count && !found; parent++)
                        found = TryPlaceFromParent(result, occupied, parent, random, out placed);
                    if (!found) throw new InvalidOperationException("No free dungeon grid cell was available.");
                }
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
            return result;
        }

        private static List<PlacedGridPoint> PlaceTempleRooms(int count,
            int criticalCount, DeterministicRandom random)
        {
            // Five readable temple silhouettes. F moves along the processional axis,
            // T crosses a transept, N returns across it, and B briefly moves back.
            // Rotation and reflection are independently seeded after selection.
            string[] routePatterns =
            {
                "FFFFFTTFFFNFFFTTFFF",
                "FFFTTTTFFFNNFFFTTT",
                "FFFFTFFTFFTFFNFFFF",
                "FFTTFFNFFTTFFNFFFF",
                "FFFTFFFTBTTTBTTFFF"
            };
            string route = routePatterns[random.NextInt(routePatterns.Length)];
            bool alongX = random.NextInt(2) == 0;
            int forwardSign = random.NextInt(2) == 0 ? 1 : -1;
            int turnSign = random.NextInt(2) == 0 ? 1 : -1;
            GridPoint forward = alongX
                ? new GridPoint(forwardSign, 0) : new GridPoint(0, forwardSign);
            GridPoint turn = alongX
                ? new GridPoint(0, turnSign) : new GridPoint(turnSign, 0);
            var result = new List<PlacedGridPoint>(count)
                { new PlacedGridPoint(0, 0, -1) };
            var occupied = new HashSet<GridPoint> { new GridPoint(0, 0) };

            for (int i = 1; i < criticalCount; i++)
            {
                char step = i - 1 < route.Length ? route[i - 1] : 'F';
                GridPoint direction = step == 'T' ? turn
                    : step == 'N' ? new GridPoint(-turn.X, -turn.Z)
                    : step == 'B' ? new GridPoint(-forward.X, -forward.Z)
                    : forward;
                PlacedGridPoint parent = result[i - 1];
                var cell = new GridPoint(parent.X + direction.X,
                    parent.Z + direction.Z);
                if (!occupied.Add(cell))
                    throw new InvalidOperationException(
                        "Temple critical path overlaps itself.");
                result.Add(new PlacedGridPoint(cell.X, cell.Z, i - 1));
            }

            int[] anchors =
            {
                Math.Max(2, criticalCount / 4),
                Math.Max(2, criticalCount / 3),
                Math.Max(2, criticalCount / 2),
                Math.Max(2, criticalCount * 3 / 4)
            };
            for (int i = criticalCount; i < count; i++)
            {
                int branchNumber = i - criticalCount;
                int branch = branchNumber % anchors.Length;
                int parentIndex = branchNumber < anchors.Length
                    ? anchors[branch] : i - anchors.Length;
                PlacedGridPoint parent = result[parentIndex];
                int side = branch % 2 == 0 ? 1 : -1;
                var candidate = new GridPoint(parent.X + turn.X * side,
                    parent.Z + turn.Z * side);
                PlacedGridPoint placed;
                if (occupied.Contains(candidate))
                {
                    if (!TryPlaceFromParent(result, occupied, parentIndex, random,
                        out placed))
                    {
                        bool found = false;
                        for (int fallback = 2; fallback < result.Count && !found;
                             fallback++)
                            found = TryPlaceFromParent(result, occupied, fallback,
                                random, out placed);
                        if (!found)
                            throw new InvalidOperationException(
                                "No free temple branch cell was available.");
                    }
                }
                else placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
            return result;
        }

        private static List<PlacedGridPoint> PlaceVariedSubwayRooms(int count,
            int criticalCount, DeterministicRandom random, bool recoverOccupiedOutlets,
            bool pedestrianBranches, bool sideOnlyRailBranches)
        {
            // Retain the approved entrance orientation and the three-room rail station
            // run, but select one of several non-overlapping approach compositions.
            // The named layout stream makes the choice deterministic for a given seed.
            int variant = random.NextInt(6);
            int bendSign = random.NextInt(2) == 0 ? 1 : -1;
            // X/+Z/-X/-Z direction codes. Z is mirrored by bendSign. All six
            // templates preserve the entrance socket and the index 6-8 rail run,
            // while three include broad U/S-shaped macro composition.
            string[] routePatterns =
            {
                "XXZXXZXXZ",
                "XZZZXZXXZ",
                "XZXXZZXXZ",
                "XXZZLZXXZ",
                "XZZXXNXXN",
                "XXXZLZXXZ"
            };
            string route = routePatterns[variant];
            if (sideOnlyRailBranches)
            {
                int railIndex = SubwayTrainRoomIndex(criticalCount);
                // The approach and departure must meet the rail run from its sides.
                // Shorter critical paths otherwise reuse a 30-room template at the
                // wrong offset and can cut a pedestrian door into a tunnel end.
                if (railIndex >= 2 && (route[railIndex - 2] != 'Z'
                    && route[railIndex - 2] != 'N'
                    || route[railIndex - 1] != 'X'
                    || route[railIndex] != 'X'
                    || route[railIndex + 1] != 'Z'
                    && route[railIndex + 1] != 'N'))
                    route = new string('X', railIndex - 2) + "ZXXZ";
            }

            var result = new List<PlacedGridPoint>(count)
                { new PlacedGridPoint(0, 0, -1) };
            var occupied = new HashSet<GridPoint> { new GridPoint(0, 0) };
            GridPoint spine = new GridPoint(1, 0);
            GridPoint branch = new GridPoint(0, bendSign);
            for (int i = 1; i < criticalCount; i++)
            {
                char step = i - 1 < route.Length ? route[i - 1] : 'X';
                GridPoint direction = step == 'Z' ? branch
                    : step == 'N' ? new GridPoint(-branch.X, -branch.Z)
                    : step == 'L' ? new GridPoint(-spine.X, -spine.Z)
                    : spine;
                PlacedGridPoint parent = result[i - 1];
                var cell = new GridPoint(parent.X + direction.X, parent.Z + direction.Z);
                if (!occupied.Add(cell))
                    throw new InvalidOperationException(
                        "Varied subway critical path overlaps itself.");
                result.Add(new PlacedGridPoint(cell.X, cell.Z, i - 1));
            }

            if (recoverOccupiedOutlets)
                PlaceRecoveredSubwayBranches(result, occupied, count, criticalCount,
                    random, bendSign, pedestrianBranches, sideOnlyRailBranches);
            else
                PlaceSubwayBranches(result, occupied, count, criticalCount, random,
                    spine, new GridPoint(0, 1), bendSign);
            return result;
        }

        private static void PlaceRecoveredSubwayBranches(List<PlacedGridPoint> result,
            HashSet<GridPoint> occupied, int count, int criticalCount,
            DeterministicRandom random, int bendSign, bool pedestrianBranches,
            bool sideOnlyRailBranches)
        {
            int trainHub = SubwayTrainRoomIndex(criticalCount);
            for (int i = criticalCount; i < count; i++)
            {
                int branchNumber = i - criticalCount;
                int parentIndex = branchNumber == 0
                    ? pedestrianBranches ? criticalCount - 1 : trainHub + 1
                    : branchNumber < 3 ? criticalCount
                    : branchNumber < 6 ? trainHub - 1 + branchNumber - 3
                    : criticalCount + branchNumber - 3;
                PlacedGridPoint parent = result[parentIndex];
                PlacedGridPoint placed = default;
                bool found = false;
                GridPoint[] directions =
                {
                    new GridPoint(1, 0), new GridPoint(0, bendSign),
                    new GridPoint(-1, 0), new GridPoint(0, -bendSign)
                };
                // The junction needs two later outlets. Reserve that capacity before
                // committing the first optional room, instead of retrying too late.
                if (branchNumber == 0)
                {
                    foreach (GridPoint direction in directions)
                    {
                        var cell = new GridPoint(parent.X + direction.X,
                            parent.Z + direction.Z);
                        if (occupied.Contains(cell)) continue;
                        int freeNeighbors = 0;
                        foreach (GridPoint neighbor in directions)
                            if (!occupied.Contains(new GridPoint(cell.X + neighbor.X,
                                cell.Z + neighbor.Z))) freeNeighbors++;
                        if (freeNeighbors < 2) continue;
                        placed = new PlacedGridPoint(cell.X, cell.Z, parentIndex);
                        found = true;
                        break;
                    }
                }
                else if (branchNumber < 3)
                {
                    int start = branchNumber == 1 ? 0 : 1;
                    for (int attempt = 0; attempt < directions.Length; attempt++)
                    {
                        GridPoint direction = directions[(start + attempt) % directions.Length];
                        var cell = new GridPoint(parent.X + direction.X,
                            parent.Z + direction.Z);
                        if (occupied.Contains(cell)) continue;
                        placed = new PlacedGridPoint(cell.X, cell.Z, parentIndex);
                        found = true;
                        break;
                    }
                }
                else if (sideOnlyRailBranches && branchNumber < 6)
                {
                    bool railAlongX = result[trainHub + 1].X != result[trainHub].X;
                    GridPoint side = railAlongX ? new GridPoint(0, bendSign)
                        : new GridPoint(bendSign, 0);
                    foreach (GridPoint direction in new[] { side,
                        new GridPoint(-side.X, -side.Z) })
                    {
                        var cell = new GridPoint(parent.X + direction.X,
                            parent.Z + direction.Z);
                        if (occupied.Contains(cell)) continue;
                        placed = new PlacedGridPoint(cell.X, cell.Z, parentIndex);
                        found = true;
                        break;
                    }
                }
                else found = TryPlaceFromParent(result, occupied, parentIndex,
                    random, out placed);
                if (!found)
                {
                    for (int fallback = criticalCount; fallback < result.Count && !found;
                         fallback++)
                        found = TryPlaceFromParent(result, occupied, fallback,
                            random, out placed);
                }
                if (!found)
                    throw new InvalidOperationException(
                        "No free subway branch cell was available at room " + i + ".");
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
        }

        private static void PlaceSubwayBranches(List<PlacedGridPoint> result,
            HashSet<GridPoint> occupied, int count, int criticalCount,
            DeterministicRandom random, GridPoint spine, GridPoint branch, int bendSign)
        {
            for (int i = criticalCount; i < count; i++)
            {
                int branchNumber = i - criticalCount;
                int cluster = branchNumber % 3;
                int parentIndex;
                if (branchNumber == 0)
                {
                    int trainHub = SubwayTrainRoomIndex(criticalCount);
                    parentIndex = trainHub + 1;
                    PlacedGridPoint railParent = result[parentIndex];
                    var railCell = new GridPoint(railParent.X + spine.X, railParent.Z + spine.Z);
                    if (occupied.Contains(railCell))
                        railCell = new GridPoint(railParent.X - spine.X, railParent.Z - spine.Z);
                    occupied.Add(railCell);
                    result.Add(new PlacedGridPoint(railCell.X, railCell.Z, parentIndex));
                    continue;
                }
                if (branchNumber == 1 || branchNumber == 2)
                {
                    parentIndex = criticalCount;
                    PlacedGridPoint junction = result[parentIndex];
                    GridPoint direction = branchNumber == 1 ? spine
                        : new GridPoint(branch.X * bendSign, branch.Z * bendSign);
                    var railCell = new GridPoint(junction.X + direction.X,
                        junction.Z + direction.Z);
                    if (occupied.Contains(railCell))
                        throw new InvalidOperationException(
                            "No subway junction outlet cell was available.");
                    occupied.Add(railCell);
                    result.Add(new PlacedGridPoint(railCell.X, railCell.Z, parentIndex));
                    continue;
                }
                if (branchNumber < 6)
                {
                    int trainHub = SubwayTrainRoomIndex(criticalCount);
                    int[] hubs = { trainHub - 1, trainHub, trainHub + 1 };
                    parentIndex = hubs[(branchNumber - 3) % 3];
                }
                else parentIndex = criticalCount + branchNumber - 3;
                int side = cluster == 0 ? 1 : -1;
                PlacedGridPoint parent = result[parentIndex];
                var candidate = new GridPoint(parent.X + branch.X * side,
                    parent.Z + branch.Z * side);
                PlacedGridPoint placed;
                if (occupied.Contains(candidate))
                {
                    if (branchNumber < 6)
                    {
                        candidate = new GridPoint(parent.X - branch.X * side,
                            parent.Z - branch.Z * side);
                        if (occupied.Contains(candidate))
                            throw new InvalidOperationException(
                                "No perpendicular subway rail branch cell was available.");
                        placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                    }
                    else if (!TryPlaceFromParent(result, occupied, parentIndex, random, out placed))
                    {
                        bool found = false;
                        int trainRoomIndex = SubwayTrainRoomIndex(criticalCount);
                        for (int fallback = criticalCount; fallback < result.Count && !found;
                             fallback++)
                        {
                            if (fallback == trainRoomIndex - 1 || fallback == trainRoomIndex
                                || fallback == trainRoomIndex + 1)
                                continue;
                            found = TryPlaceFromParent(result, occupied, fallback, random,
                                out placed);
                        }
                        if (!found)
                            throw new InvalidOperationException(
                                "No free subway branch cell was available.");
                    }
                }
                else placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
        }

        private static List<PlacedGridPoint> PlaceSubwayRooms(int count, int criticalCount,
            DeterministicRandom random)
        {
            var result = new List<PlacedGridPoint>(count) { new PlacedGridPoint(0, 0, -1) };
            var occupied = new HashSet<GridPoint> { new GridPoint(0, 0) };
            // The authored entrance occupies the south/west legs of its footprint.
            // Its dungeon socket is the centered +X end cap; +Z is reserved for
            // the locked exterior door. Downstream subway bends remain seeded.
            GridPoint spine = new GridPoint(1, 0);
            GridPoint branch = new GridPoint(0, 1);
            int bendSign = random.NextInt(2) == 0 ? 1 : -1;

            // The critical path is a legible transit line. Optional rooms attach as
            // alternating station/service branches instead of continuing the generic maze.
            for (int i = 1; i < criticalCount; i++)
            {
                PlacedGridPoint parent = result[i - 1];
                int progress = i * 100 / Math.Max(1, criticalCount - 1);
                int trainRoomIndex = SubwayTrainRoomIndex(criticalCount);
                GridPoint direction;
                if (i == trainRoomIndex - 1 || i == trainRoomIndex + 2)
                    direction = new GridPoint(branch.X * bendSign, branch.Z * bendSign);
                else if (i == trainRoomIndex || i == trainRoomIndex + 1)
                    direction = spine;
                else
                    direction = progress >= 36 && progress < 55
                        ? new GridPoint(branch.X * bendSign, branch.Z * bendSign) : spine;
                var next = new PlacedGridPoint(parent.X + direction.X,
                    parent.Z + direction.Z, i - 1);
                occupied.Add(new GridPoint(next.X, next.Z));
                result.Add(next);
            }
            for (int i = criticalCount; i < count; i++)
            {
                int branchNumber = i - criticalCount;
                int cluster = branchNumber % 3;
                int parentIndex;
                if (branchNumber == 0)
                {
                    // The first optional module extends the rail line beyond the
                    // far tunnel. It is the authored straight-plus-turn junction.
                    int trainHub = SubwayTrainRoomIndex(criticalCount);
                    parentIndex = trainHub + 1;
                    PlacedGridPoint railParent = result[parentIndex];
                    var railCell = new GridPoint(railParent.X + spine.X, railParent.Z + spine.Z);
                    if (occupied.Contains(railCell))
                        railCell = new GridPoint(railParent.X - spine.X, railParent.Z - spine.Z);
                    var railPlaced = new PlacedGridPoint(railCell.X, railCell.Z, parentIndex);
                    occupied.Add(railCell);
                    result.Add(railPlaced);
                    continue;
                }
                if (branchNumber == 1 || branchNumber == 2)
                {
                    parentIndex = criticalCount;
                    PlacedGridPoint junction = result[parentIndex];
                    GridPoint direction = branchNumber == 1 ? spine
                        : new GridPoint(branch.X * bendSign, branch.Z * bendSign);
                    var railCell = new GridPoint(junction.X + direction.X,
                        junction.Z + direction.Z);
                    if (occupied.Contains(railCell))
                        throw new InvalidOperationException(
                            "No subway junction outlet cell was available.");
                    occupied.Add(railCell);
                    result.Add(new PlacedGridPoint(railCell.X, railCell.Z, parentIndex));
                    continue;
                }
                if (branchNumber < 6)
                {
                    int trainHub = SubwayTrainRoomIndex(criticalCount);
                    int[] hubs = { trainHub - 1, trainHub, trainHub + 1 };
                    parentIndex = hubs[(branchNumber - 3) % 3];
                }
                else parentIndex = criticalCount + branchNumber - 3;
                int side = cluster == 0 ? 1 : -1;
                PlacedGridPoint parent = result[parentIndex];
                var candidate = new GridPoint(parent.X + branch.X * side,
                    parent.Z + branch.Z * side);
                PlacedGridPoint placed;
                if (occupied.Contains(candidate))
                {
                    if (branchNumber < 6)
                    {
                        candidate = new GridPoint(parent.X - branch.X * side,
                            parent.Z - branch.Z * side);
                        if (occupied.Contains(candidate))
                            throw new InvalidOperationException(
                                "No perpendicular subway rail branch cell was available.");
                        placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                    }
                    else if (!TryPlaceFromParent(result, occupied, parentIndex, random, out placed))
                    {
                        bool found = false;
                        int trainRoomIndex = SubwayTrainRoomIndex(criticalCount);
                        for (int fallback = criticalCount; fallback < result.Count && !found;
                             fallback++)
                        {
                            if (fallback == trainRoomIndex - 1 || fallback == trainRoomIndex
                                || fallback == trainRoomIndex + 1)
                                continue;
                            found = TryPlaceFromParent(result, occupied, fallback, random,
                                out placed);
                        }
                        if (!found)
                            throw new InvalidOperationException(
                                "No free subway branch cell was available.");
                    }
                }
                else placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
            return result;
        }

        private static List<PlacedGridPoint> PlaceGroupDungeonRooms(int count, int criticalCount,
            DeterministicRandom random, bool cave)
        {
            var result = new List<PlacedGridPoint>(count) { new PlacedGridPoint(0, 0, -1) };
            var occupied = new HashSet<GridPoint> { new GridPoint(0, 0) };
            bool alongX = random.NextInt(2) == 0;
            int forwardSign = random.NextInt(2) == 0 ? 1 : -1;
            int turnSign = random.NextInt(2) == 0 ? 1 : -1;
            GridPoint forward = alongX
                ? new GridPoint(forwardSign, 0) : new GridPoint(0, forwardSign);
            GridPoint turn = alongX
                ? new GridPoint(0, turnSign) : new GridPoint(turnSign, 0);
            // A readable entrance-to-finale route, with a middle wing change rather
            // than a random succession of unrelated boxes.
            for (int i = 1; i < criticalCount; i++)
            {
                PlacedGridPoint parent = result[i - 1];
                int progress = i * 100 / Math.Max(1, criticalCount - 1);
                GridPoint direction = cave
                    ? progress >= 25 && progress < 42 ? turn
                        : progress >= 62 && progress < 78
                            ? new GridPoint(-turn.X, -turn.Z) : forward
                    : progress >= 35 && progress < 65 ? turn : forward;
                int stride = cave && i == 2 ? 13 : 1;
                if (stride > 1)
                    for (int reserved = 1; reserved < stride; reserved++)
                        occupied.Add(new GridPoint(parent.X + direction.X * reserved,
                            parent.Z + direction.Z * reserved));
                var next = new PlacedGridPoint(parent.X + direction.X * stride,
                    parent.Z + direction.Z * stride, i - 1);
                if (!occupied.Add(new GridPoint(next.X, next.Z)))
                    throw new InvalidOperationException("Group dungeon main route overlaps itself.");
                result.Add(next);
            }
            int[] anchors = { Math.Min(2, criticalCount - 1),
                Math.Max(1, criticalCount / 3),
                Math.Max(1, criticalCount * 2 / 3),
                Math.Max(1, criticalCount - 2) };
            for (int i = criticalCount; i < count; i++)
            {
                int branchNumber = i - criticalCount;
                int cluster = branchNumber % anchors.Length;
                int parentIndex = branchNumber < anchors.Length
                    ? anchors[cluster] : i - anchors.Length;
                PlacedGridPoint parent = result[parentIndex];
                int side = cluster % 2 == 0 ? 1 : -1;
                var candidate = new GridPoint(parent.X + turn.X * side,
                    parent.Z + turn.Z * side);
                PlacedGridPoint placed;
                if (occupied.Contains(candidate))
                {
                    if (!TryPlaceFromParent(result, occupied, parentIndex, random, out placed))
                    {
                        bool found = false;
                        for (int fallback = 0; fallback < result.Count && !found; fallback++)
                            found = TryPlaceFromParent(result, occupied, fallback, random, out placed);
                        if (!found)
                            throw new InvalidOperationException("No free group dungeon side-room cell remains.");
                    }
                }
                else placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                occupied.Add(new GridPoint(placed.X, placed.Z));
                result.Add(placed);
            }
            return result;
        }

        private static bool TryPlaceFromParent(List<PlacedGridPoint> rooms, HashSet<GridPoint> occupied,
            int parentIndex, DeterministicRandom random, out PlacedGridPoint placed)
        {
            var order = new[] { 0, 1, 2, 3 };
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = random.NextInt(i + 1);
                int temporary = order[i]; order[i] = order[other]; order[other] = temporary;
            }
            PlacedGridPoint parent = rooms[parentIndex];
            for (int i = 0; i < order.Length; i++)
            {
                GridPoint direction = Directions[order[i]];
                var candidate = new GridPoint(parent.X + direction.X, parent.Z + direction.Z);
                if (occupied.Contains(candidate)) continue;
                placed = new PlacedGridPoint(candidate.X, candidate.Z, parentIndex);
                return true;
            }
            placed = default(PlacedGridPoint);
            return false;
        }

        private static DungeonRoomRole ResolveRole(int index, int criticalCount, int roomCount)
        {
            if (index == 0) return DungeonRoomRole.Entrance;
            if (index == criticalCount - 1) return DungeonRoomRole.Boss;
            if (index == criticalCount - 2) return DungeonRoomRole.Objective;
            if (index == roomCount - 1 && roomCount > criticalCount) return DungeonRoomRole.Treasure;
            return index < criticalCount ? DungeonRoomRole.Normal : DungeonRoomRole.Branch;
        }

        private static int ResolveFloorElevation(int index, int criticalCount,
            DungeonLayoutProfile profile, IReadOnlyList<PlacedGridPoint> grid,
            IReadOnlyList<int> resolvedElevations, bool steepCave)
        {
            if (profile == DungeonLayoutProfile.CaveDungeon)
                return index >= criticalCount
                    ? resolvedElevations[grid[index].ParentIndex]
                    : index < 2 ? 0 : steepCave ? -18000 : -3000;
            if (profile != DungeonLayoutProfile.Subway) return 0;
            if (index >= criticalCount)
                return resolvedElevations[grid[index].ParentIndex];
            if (index < 2) return 6000;
            int progress = index * 100 / Math.Max(1, criticalCount - 1);
            if (progress < 30) return 3000;
            if (progress < 68) return 0;
            if (progress < 88) return -3000;
            return -6000;
        }

        private static DungeonRoomShape ResolveShape(DungeonRoomRole role, int degree,
            DungeonLayoutProfile profile,
            DeterministicRandom random)
        {
            if (degree >= 3) return DungeonRoomShape.GrandChamber;
            if (role == DungeonRoomRole.Boss || role == DungeonRoomRole.Objective)
                return DungeonRoomShape.GrandChamber;
            if (role == DungeonRoomRole.Entrance) return profile == DungeonLayoutProfile.CaveDungeon
                ? DungeonRoomShape.Rectangle : DungeonRoomShape.WideHall;
            int roll = random.NextInt(100);
            if (profile == DungeonLayoutProfile.Subway)
                return roll < 52 ? DungeonRoomShape.LongHall
                    : roll < 84 ? DungeonRoomShape.WideHall : DungeonRoomShape.GrandChamber;
            if (profile == DungeonLayoutProfile.GroupDungeon)
                return roll < 30 ? DungeonRoomShape.LongHall
                    : roll < 50 ? DungeonRoomShape.WideHall
                    : roll < 72 ? DungeonRoomShape.GrandChamber
                    : roll < 88 ? DungeonRoomShape.LShaped : DungeonRoomShape.Rectangle;
            if (profile == DungeonLayoutProfile.CaveDungeon)
                return roll < 55 ? DungeonRoomShape.Cavern
                    : roll < 70 ? DungeonRoomShape.LShaped
                    : roll < 82 ? DungeonRoomShape.GrandChamber
                    : roll < 91 ? DungeonRoomShape.WideHall : DungeonRoomShape.LongHall;
            if (profile == DungeonLayoutProfile.Temple)
                return roll < 34 ? DungeonRoomShape.LShaped
                    : roll < 66 ? DungeonRoomShape.GrandChamber : DungeonRoomShape.Rectangle;
            if (profile == DungeonLayoutProfile.Raid)
                return roll < 42 ? DungeonRoomShape.GrandChamber
                    : roll < 67 ? DungeonRoomShape.WideHall
                    : roll < 84 ? DungeonRoomShape.LShaped : DungeonRoomShape.LongHall;
            if (roll < 22) return DungeonRoomShape.LShaped;
            if (roll < 43) return DungeonRoomShape.WideHall;
            if (roll < 64) return DungeonRoomShape.LongHall;
            return DungeonRoomShape.Rectangle;
        }

        private static DungeonModuleKind ResolveModuleKind(DungeonRoomRole role,
            DungeonRoomShape shape, int degree, DungeonLayoutProfile profile)
        {
            if (role == DungeonRoomRole.Entrance) return DungeonModuleKind.Entrance;
            if (role == DungeonRoomRole.Boss)
                return profile == DungeonLayoutProfile.Subway
                    ? DungeonModuleKind.Terminal : DungeonModuleKind.BossArena;
            if (role == DungeonRoomRole.Objective)
                return profile == DungeonLayoutProfile.Subway
                    ? DungeonModuleKind.TrainChamber : DungeonModuleKind.ObjectiveRoom;
            if (role == DungeonRoomRole.Treasure) return DungeonModuleKind.TreasureRoom;
            if (profile == DungeonLayoutProfile.Subway)
            {
                if (role == DungeonRoomRole.Branch) return DungeonModuleKind.ServiceRoom;
                if (degree >= 3 || shape == DungeonRoomShape.GrandChamber)
                    return DungeonModuleKind.StationConcourse;
                return DungeonModuleKind.StationPlatform;
            }
            if (degree >= 3) return DungeonModuleKind.Junction;
            return DungeonModuleKind.CombatRoom;
        }

        private static DungeonModuleKind ResolveTempleModuleKind(int index,
            int criticalCount)
        {
            if (index == 0) return DungeonModuleKind.PilgrimEntry;
            if (index == 1) return DungeonModuleKind.ProcessionalHall;
            if (index == Math.Max(2, criticalCount / 4))
                return DungeonModuleKind.NaveHub;
            if (index == Math.Max(3, criticalCount / 2))
                return DungeonModuleKind.InnerGate;
            if (index == criticalCount - 3)
                return DungeonModuleKind.ExarchGallery;
            if (index == criticalCount - 2)
                return DungeonModuleKind.HighSanctuary;
            if (index == criticalCount - 1)
                return DungeonModuleKind.ProfaneSanctum;
            if (index < criticalCount) return DungeonModuleKind.ProcessionalHall;
            switch ((index - criticalCount) % 6)
            {
                case 0: return DungeonModuleKind.RitualChapel;
                case 1: return DungeonModuleKind.SunkenCrypt;
                case 2: return DungeonModuleKind.Scriptorium;
                case 3: return DungeonModuleKind.Vestry;
                case 4: return DungeonModuleKind.CollapsedCloister;
                default: return DungeonModuleKind.Reliquary;
            }
        }

        private static DungeonRoomShape ResolveTempleShape(DungeonModuleKind moduleKind)
        {
            switch (moduleKind)
            {
                case DungeonModuleKind.NaveHub:
                case DungeonModuleKind.InnerGate:
                case DungeonModuleKind.HighSanctuary:
                case DungeonModuleKind.ProfaneSanctum:
                case DungeonModuleKind.RitualChapel:
                    return DungeonRoomShape.GrandChamber;
                case DungeonModuleKind.ProcessionalHall:
                case DungeonModuleKind.ExarchGallery:
                case DungeonModuleKind.CollapsedCloister:
                    return DungeonRoomShape.LongHall;
                case DungeonModuleKind.SunkenCrypt:
                    return DungeonRoomShape.LShaped;
                case DungeonModuleKind.PilgrimEntry:
                    return DungeonRoomShape.WideHall;
                default:
                    return DungeonRoomShape.Rectangle;
            }
        }

        private static void ResolveTempleSize(DungeonModuleKind moduleKind,
            IReadOnlyList<PlacedGridPoint> grid, int index, out int width,
            out int depth)
        {
            bool alongX = index > 0
                && grid[index].X != grid[grid[index].ParentIndex].X;
            int along = moduleKind == DungeonModuleKind.ProcessionalHall ? 14000 : 12000;
            int across = moduleKind == DungeonModuleKind.ProcessionalHall ? 8500 : 10000;
            if (moduleKind == DungeonModuleKind.ProcessionalHall
                || moduleKind == DungeonModuleKind.ExarchGallery
                || moduleKind == DungeonModuleKind.CollapsedCloister)
            {
                width = alongX ? along : across;
                depth = alongX ? across : along;
                return;
            }
            if (moduleKind == DungeonModuleKind.NaveHub
                || moduleKind == DungeonModuleKind.InnerGate
                || moduleKind == DungeonModuleKind.HighSanctuary
                || moduleKind == DungeonModuleKind.ProfaneSanctum
                || moduleKind == DungeonModuleKind.RitualChapel)
            {
                width = 14000;
                depth = 14000;
                return;
            }
            if (moduleKind == DungeonModuleKind.PilgrimEntry)
            {
                width = alongX ? 12000 : 14000;
                depth = alongX ? 14000 : 12000;
                return;
            }
            width = 11000;
            depth = 11000;
        }

        private static int TempleRoomHeight(DungeonModuleKind moduleKind)
        {
            switch (moduleKind)
            {
                case DungeonModuleKind.NaveHub: return 11000;
                case DungeonModuleKind.HighSanctuary:
                case DungeonModuleKind.ProfaneSanctum: return 10000;
                case DungeonModuleKind.InnerGate:
                case DungeonModuleKind.RitualChapel: return 8500;
                case DungeonModuleKind.SunkenCrypt: return 5000;
                case DungeonModuleKind.ProcessionalHall:
                case DungeonModuleKind.ExarchGallery: return 7500;
                default: return 6500;
            }
        }

        private static int SubwayTrainRoomIndex(int criticalCount) =>
            Math.Max(2, criticalCount - 3);

        private static bool SubwayTrackAlongX(IReadOnlyList<PlacedGridPoint> grid,
            int index, int criticalCount)
        {
            int train = SubwayTrainRoomIndex(criticalCount);
            return grid[index].X != grid[train].X;
        }

        private readonly struct GridPoint : IEquatable<GridPoint>
        {
            public GridPoint(int x, int z) { X = x; Z = z; }
            public int X { get; }
            public int Z { get; }
            public bool Equals(GridPoint other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is GridPoint other && Equals(other);
            public override int GetHashCode() => unchecked(X * 397 ^ Z);
        }

        private readonly struct PlacedGridPoint
        {
            public PlacedGridPoint(int x, int z, int parentIndex) { X = x; Z = z; ParentIndex = parentIndex; }
            public int X { get; }
            public int Z { get; }
            public int ParentIndex { get; }
        }
    }
}
