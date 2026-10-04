using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public enum DungeonCollisionKind
    {
        Floor, Ceiling, Wall, Foundation, RetainingWall, TrainBody, Fixture
    }

    public sealed class DungeonCollisionBox
    {
        public DungeonCollisionBox(string id, DungeonCollisionKind kind, WorldBounds bounds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Kind = kind;
            Bounds = bounds;
        }
        public string Id { get; }
        public DungeonCollisionKind Kind { get; }
        public WorldBounds Bounds { get; }
    }

    // Single source of truth for axis-aligned structural collision. Renderers may
    // decorate these volumes, but clients and servers must consume these boxes
    // verbatim instead of independently deciding which visuals are solid.
    public static class DungeonCollisionBaker
    {
        public static IReadOnlyList<DungeonCollisionBox> BakeCorridor(
            DungeonLayout layout, DungeonCorridor corridor)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (corridor == null) throw new ArgumentNullException(nameof(corridor));
            var boxes = new List<DungeonCollisionBox>();
            IReadOnlyList<WorldBounds> floors =
                DungeonCorridorGeometry.FloorSections(layout, corridor);
            for (int i = 0; i < floors.Count; i++)
            {
                WorldBounds floor = floors[i];
                int depth = DungeonCorridorGeometry.FloorSupportDepth(corridor, floor);
                boxes.Add(new DungeonCollisionBox("corridor-floor-" + i,
                    DungeonCollisionKind.Floor, Box(floor.Minimum.X,
                        floor.Minimum.Y - depth, floor.Minimum.Z,
                        floor.Maximum.X, floor.Minimum.Y, floor.Maximum.Z)));
            }
            WorldBounds b = corridor.WalkableBounds;
            boxes.Add(new DungeonCollisionBox("corridor-ceiling",
                DungeonCollisionKind.Ceiling, Box(b.Minimum.X, b.Maximum.Y, b.Minimum.Z,
                    b.Maximum.X, b.Maximum.Y + 200, b.Maximum.Z)));
            const int half = 125;
            if (DungeonCorridorGeometry.IsAlongX(layout, corridor))
            {
                boxes.Add(new DungeonCollisionBox("corridor-wall-negative",
                    DungeonCollisionKind.Wall, Box(b.Minimum.X, b.Minimum.Y,
                        b.Minimum.Z - half, b.Maximum.X, b.Maximum.Y, b.Minimum.Z + half)));
                boxes.Add(new DungeonCollisionBox("corridor-wall-positive",
                    DungeonCollisionKind.Wall, Box(b.Minimum.X, b.Minimum.Y,
                        b.Maximum.Z - half, b.Maximum.X, b.Maximum.Y, b.Maximum.Z + half)));
            }
            else
            {
                boxes.Add(new DungeonCollisionBox("corridor-wall-negative",
                    DungeonCollisionKind.Wall, Box(b.Minimum.X - half, b.Minimum.Y,
                        b.Minimum.Z, b.Minimum.X + half, b.Maximum.Y, b.Maximum.Z)));
                boxes.Add(new DungeonCollisionBox("corridor-wall-positive",
                    DungeonCollisionKind.Wall, Box(b.Maximum.X - half, b.Minimum.Y,
                        b.Minimum.Z, b.Maximum.X + half, b.Maximum.Y, b.Maximum.Z)));
            }
            if (DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                    == DungeonLayoutProfile.Subway
                && !IsSubwayRailJoin(layout, corridor.ConnectionId))
                BakeSubwayPassageEndFacades(layout, corridor, boxes);
            return boxes;
        }

        private static void BakeSubwayPassageEndFacades(DungeonLayout layout,
            DungeonCorridor corridor, List<DungeonCollisionBox> boxes)
        {
            WorldBounds hall = corridor.WalkableBounds;
            const int half = 125;
            const int doorHeight = 2800;
            int facade = 0;
            foreach (DungeonDoorPortal portal in layout.DoorPortals)
            {
                if (portal.ConnectionId != corridor.ConnectionId) continue;
                bool xWall = portal.Facing == DungeonPortalFacing.NegativeX
                    || portal.Facing == DungeonPortalFacing.PositiveX;
                int fixedAxis = xWall ? portal.Center.X : portal.Center.Z;
                int hallStart = xWall ? hall.Minimum.Z : hall.Minimum.X;
                int hallEnd = xWall ? hall.Maximum.Z : hall.Maximum.X;
                int doorStart = xWall ? portal.ClosedBlockingBounds.Minimum.Z
                    : portal.ClosedBlockingBounds.Minimum.X;
                int doorEnd = xWall ? portal.ClosedBlockingBounds.Maximum.Z
                    : portal.ClosedBlockingBounds.Maximum.X;
                int floor = portal.Center.Y;
                int doorTop = Math.Min(floor + doorHeight, hall.Maximum.Y);
                AddFacade("left", floor, doorTop, hallStart, doorStart);
                AddFacade("right", floor, doorTop, doorEnd, hallEnd);
                AddFacade("header", doorTop, hall.Maximum.Y, hallStart, hallEnd);
                facade++;

                void AddFacade(string part, int minY, int maxY, int start, int end)
                {
                    if (maxY <= minY || end <= start) return;
                    WorldBounds bounds = xWall
                        ? Box(fixedAxis - half, minY, start,
                            fixedAxis + half, maxY, end)
                        : Box(start, minY, fixedAxis - half,
                            end, maxY, fixedAxis + half);
                    boxes.Add(new DungeonCollisionBox("corridor-end-" + facade + "-" + part,
                        DungeonCollisionKind.Wall, bounds));
                }
            }
        }

        private static bool IsSubwayRailJoin(DungeonLayout layout, string connectionId)
        {
            DungeonConnection connection = null;
            for (int i = 0; i < layout.Connections.Count; i++)
                if (layout.Connections[i].Id == connectionId)
                { connection = layout.Connections[i]; break; }
            if (connection == null) return false;
            DungeonRoom from = null;
            DungeonRoom to = null;
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Id == connection.FromRoomId) from = layout.Rooms[i];
                if (layout.Rooms[i].Id == connection.ToRoomId) to = layout.Rooms[i];
            }
            if (from == null || to == null) return false;
            bool fromRail = from.ModuleKind == DungeonModuleKind.TrainChamber
                || from.ModuleKind == DungeonModuleKind.TrackTunnel
                || from.ModuleKind == DungeonModuleKind.TrackJunction;
            bool toRail = to.ModuleKind == DungeonModuleKind.TrainChamber
                || to.ModuleKind == DungeonModuleKind.TrackTunnel
                || to.ModuleKind == DungeonModuleKind.TrackJunction;
            return fromRail && toRail;
        }

        public static IReadOnlyList<DungeonCollisionBox> BakeRoom(
            DungeonLayout layout, DungeonRoom room)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (room == null) throw new ArgumentNullException(nameof(room));
            var boxes = new List<DungeonCollisionBox>();
            IReadOnlyList<WorldBounds> floors = DungeonRoomGeometry.FloorSections(room);
            int floorDepth = DungeonRoomGeometry.IsSubwayTrackRoom(room) ? 200 : 750;
            for (int i = 0; i < floors.Count; i++)
            {
                WorldBounds floor = floors[i];
                boxes.Add(new DungeonCollisionBox("room-floor-" + i,
                    DungeonCollisionKind.Floor, Box(floor.Minimum.X,
                        floor.Minimum.Y - floorDepth, floor.Minimum.Z,
                        floor.Maximum.X, floor.Minimum.Y, floor.Maximum.Z)));
                boxes.Add(new DungeonCollisionBox("room-ceiling-" + i,
                    DungeonCollisionKind.Ceiling, Box(floor.Minimum.X,
                        floor.Maximum.Y, floor.Minimum.Z, floor.Maximum.X,
                        floor.Maximum.Y + 200, floor.Maximum.Z)));
            }

            if (DungeonRoomGeometry.IsSubwayTrackRoom(room))
            {
                boxes.Add(new DungeonCollisionBox("station-foundation",
                    DungeonCollisionKind.Foundation,
                    DungeonRoomGeometry.SubwayTrackFoundationBounds(room)));
                for (int side = 0; side < 2; side++)
                    for (int segment = 0; segment < 2; segment++)
                        boxes.Add(new DungeonCollisionBox(
                            "platform-wall-" + side + "-" + segment,
                            DungeonCollisionKind.RetainingWall,
                            DungeonRoomGeometry.SubwayPlatformEdgeSegmentBounds(
                                room, side == 1, segment == 1)));
                for (int platform = 0; platform < 2; platform++)
                    for (int stairSide = 0; stairSide < 2; stairSide++)
                        boxes.Add(new DungeonCollisionBox(
                            "stair-cheek-" + platform + "-" + stairSide,
                            DungeonCollisionKind.RetainingWall,
                            DungeonRoomGeometry.SubwayTrackStairSideBounds(
                                room, platform == 1, stairSide == 1)));
                if (room.ModuleKind == DungeonModuleKind.TrainChamber)
                {
                    for (int end = 0; end < 2; end++)
                        boxes.Add(new DungeonCollisionBox("train-tunnel-header-" + end,
                            DungeonCollisionKind.Wall,
                            DungeonRoomGeometry.SubwayTrainTunnelHeaderBounds(
                                room, end == 1)));
                    for (int car = 0; car < 3; car++)
                        boxes.Add(new DungeonCollisionBox("train-body-" + car,
                            DungeonCollisionKind.TrainBody,
                            DungeonRoomGeometry.SubwayTrainCarBounds(room, car)));
                }
            }

            foreach (DungeonRoomWallSection section in DungeonRoomGeometry.WallSections(room))
                BakeWall(layout, room, section, boxes);
            foreach (DungeonPresentationElement element in layout.Presentation)
                if (element.OwnerId == room.Id && element.BlocksMovement)
                    boxes.Add(new DungeonCollisionBox("presentation-" + element.Id,
                        DungeonCollisionKind.Fixture, element.Bounds));
            return boxes;
        }

        private static void BakeWall(DungeonLayout layout, DungeonRoom room,
            DungeonRoomWallSection section, List<DungeonCollisionBox> boxes)
        {
            bool xWall = section.Facing == DungeonPortalFacing.NegativeX
                || section.Facing == DungeonPortalFacing.PositiveX;
            var openings = new List<Tuple<int, int>>();
            if (section.AcceptsPortals)
                foreach (DungeonDoorPortal portal in layout.DoorPortals)
                {
                    if (portal.RoomId != room.Id || portal.Facing != section.Facing) continue;
                    int start = xWall ? portal.ClosedBlockingBounds.Minimum.Z
                        : portal.ClosedBlockingBounds.Minimum.X;
                    int end = xWall ? portal.ClosedBlockingBounds.Maximum.Z
                        : portal.ClosedBlockingBounds.Maximum.X;
                    openings.Add(Tuple.Create(Math.Max(section.Start, start),
                        Math.Min(section.End, end)));
                }
            openings.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            int cursor = section.Start;
            int index = 0;
            foreach (Tuple<int, int> opening in openings)
            {
                if (opening.Item2 <= opening.Item1) continue;
                AddWall(room, section, xWall, cursor, opening.Item1, index++, boxes);
                cursor = Math.Max(cursor, opening.Item2);
            }
            AddWall(room, section, xWall, cursor, section.End, index, boxes);
        }

        private static void AddWall(DungeonRoom room, DungeonRoomWallSection section,
            bool xWall, int start, int end, int index, List<DungeonCollisionBox> boxes)
        {
            if (end <= start) return;
            const int halfThickness = 125;
            int wallMinimumY = DungeonRoomGeometry.IsSubwayTrackRoom(room)
                ? DungeonRoomGeometry.SubwayTrackFloorY(room)
                : room.Bounds.Minimum.Y;
            WorldBounds bounds = xWall
                ? Box(section.FixedAxis - halfThickness, wallMinimumY, start,
                    section.FixedAxis + halfThickness, room.Bounds.Maximum.Y, end)
                : Box(start, wallMinimumY, section.FixedAxis - halfThickness,
                    end, room.Bounds.Maximum.Y, section.FixedAxis + halfThickness);
            boxes.Add(new DungeonCollisionBox("room-wall-" + section.Facing + "-" + index,
                DungeonCollisionKind.Wall, bounds));
        }

        private static WorldBounds Box(int minX, int minY, int minZ,
            int maxX, int maxY, int maxZ) => new WorldBounds(
                new WorldVector3(minX, minY, minZ),
                new WorldVector3(maxX, maxY, maxZ));
    }
}
