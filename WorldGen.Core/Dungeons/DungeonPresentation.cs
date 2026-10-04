using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public enum DungeonFixtureRole { Unspecified, Toilet, Sink, Partition, Light }

    public enum DungeonPresentationKind { Surface = 0, Fixture = 1, Light = 2 }

    public enum DungeonMaterialRole
    {
        ModernPanelMetal = 0,
        DarkStructuralMetal = 1,
        WayfindingBlue = 2,
        CoolWhiteLight = 3,
        CoolRailLight = 4,
        RestroomPartition = 5,
        Porcelain = 6
    }

    // Engine-neutral authored presentation. A Unity prefab, Godot scene, Unreal
    // actor, or procedural renderer can resolve the same semantic record.
    public sealed class DungeonPresentationElement
    {
        public DungeonPresentationElement(string id, string ownerId,
            DungeonPresentationKind kind, DungeonMaterialRole materialRole,
            WorldBounds bounds, bool blocksMovement = false, DungeonFixtureRole fixtureRole = DungeonFixtureRole.Unspecified, int yawDegrees = 0)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
            Kind = kind;
            MaterialRole = materialRole;
            Bounds = bounds;
            BlocksMovement = blocksMovement;
            FixtureRole = fixtureRole;
            YawDegrees = yawDegrees;
        }
        public string Id { get; }
        public string OwnerId { get; }
        public DungeonPresentationKind Kind { get; }
        public DungeonMaterialRole MaterialRole { get; }
        public WorldBounds Bounds { get; }
        public bool BlocksMovement { get; }
        public DungeonFixtureRole FixtureRole { get; }
        public int YawDegrees { get; }
    }

    public static class DungeonPresentationBuilder
    {
        public static IReadOnlyList<DungeonPresentationElement> Build(DungeonLayout layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                != DungeonLayoutProfile.Subway)
                return Array.Empty<DungeonPresentationElement>();
            var result = new List<DungeonPresentationElement>();
            foreach (DungeonRoom room in layout.Rooms)
            {
                AddWallStructure(layout, room, result);
                AddLighting(room, result);
                if (room.ModuleKind == DungeonModuleKind.Restroom)
                    AddRestroom(layout, room, result);
            }
            return result.AsReadOnly();
        }

        private static void AddWallStructure(DungeonLayout layout, DungeonRoom room,
            List<DungeonPresentationElement> result)
        {
            int floor = room.Bounds.Minimum.Y;
            int height = room.Bounds.Maximum.Y - floor;
            IReadOnlyList<DungeonRoomWallSection> walls = DungeonRoomGeometry.WallSections(room);
            for (int w = 0; w < walls.Count; w++)
            {
                DungeonRoomWallSection wall = walls[w];
                bool xWall = wall.Facing == DungeonPortalFacing.NegativeX
                    || wall.Facing == DungeonPortalFacing.PositiveX;
                int start = wall.Start + 350, end = wall.End - 350;
                if (end <= start) continue;
                Add(result, room, "upper-band-" + w, DungeonPresentationKind.Surface,
                    DungeonMaterialRole.WayfindingBlue,
                    xWall ? Box(wall.FixedAxis - 50, floor + height * 70 / 100 - 60, start,
                        wall.FixedAxis + 50, floor + height * 70 / 100 + 60, end)
                    : Box(start, floor + height * 70 / 100 - 60, wall.FixedAxis - 50,
                        end, floor + height * 70 / 100 + 60, wall.FixedAxis + 50));
                int bays = Math.Max(1, (end - start) / 3200);
                for (int bay = 1; bay < bays; bay++)
                {
                    int axis = start + (end - start) * bay / bays;
                    if (CrossesPortal(layout, room, wall, axis)) continue;
                    Add(result, room, "mullion-" + w + "-" + bay,
                        DungeonPresentationKind.Surface,
                        DungeonMaterialRole.DarkStructuralMetal,
                        xWall ? Box(wall.FixedAxis - 55, floor + 175, axis - 40,
                            wall.FixedAxis + 55, room.Bounds.Maximum.Y - 175, axis + 40)
                        : Box(axis - 40, floor + 175, wall.FixedAxis - 55,
                            axis + 40, room.Bounds.Maximum.Y - 175, wall.FixedAxis + 55));
                }
            }
        }

        private static bool CrossesPortal(DungeonLayout layout, DungeonRoom room,
            DungeonRoomWallSection wall, int axis)
        {
            bool xWall = wall.Facing == DungeonPortalFacing.NegativeX
                || wall.Facing == DungeonPortalFacing.PositiveX;
            foreach (DungeonDoorPortal portal in layout.DoorPortals)
                if (portal.RoomId == room.Id && portal.Facing == wall.Facing)
                {
                    int start = (xWall ? portal.ClosedBlockingBounds.Minimum.Z
                        : portal.ClosedBlockingBounds.Minimum.X) - 200;
                    int end = (xWall ? portal.ClosedBlockingBounds.Maximum.Z
                        : portal.ClosedBlockingBounds.Maximum.X) + 200;
                    if (axis >= start && axis <= end) return true;
                }
            return false;
        }

        private static void AddLighting(DungeonRoom room,
            List<DungeonPresentationElement> result)
        {
            if (room.ModuleKind != DungeonModuleKind.Entrance
                && room.ModuleKind != DungeonModuleKind.Restroom
                && room.ModuleKind != DungeonModuleKind.TrackJunction) return;
            IReadOnlyList<WorldBounds> sections = DungeonRoomGeometry.FloorSections(room);
            for (int i = 0; i < sections.Count; i++)
            {
                WorldBounds s = sections[i];
                int cx = (s.Minimum.X + s.Maximum.X) / 2;
                int cz = (s.Minimum.Z + s.Maximum.Z) / 2;
                bool longX = s.Maximum.X - s.Minimum.X >= s.Maximum.Z - s.Minimum.Z;
                int halfLength = Math.Min(2500, (longX
                    ? s.Maximum.X - s.Minimum.X : s.Maximum.Z - s.Minimum.Z) * 21 / 100);
                int y = room.Bounds.Maximum.Y - 330;
                Add(result, room, "light-" + i, DungeonPresentationKind.Light,
                    room.ModuleKind == DungeonModuleKind.TrackJunction
                        ? DungeonMaterialRole.CoolRailLight
                        : DungeonMaterialRole.CoolWhiteLight,
                    longX ? Box(cx - halfLength, y, cz - 130, cx + halfLength, y + 80, cz + 130)
                    : Box(cx - 130, y, cz - halfLength, cx + 130, y + 80, cz + halfLength));
            }
        }

        private static void AddRestroom(DungeonLayout layout, DungeonRoom room,
            List<DungeonPresentationElement> result)
        {
            bool resolved = layout.Manifest.GeneratorVersion == "5.13.0";
            bool[] used = new bool[4];
            DungeonPortalFacing primary = DungeonPortalFacing.NegativeX;
            bool found = false;
            foreach (DungeonDoorPortal portal in layout.DoorPortals)
                if (portal.RoomId == room.Id)
                { used[(int)portal.Facing] = true; if (!found) { primary = portal.Facing; found = true; } }
            bool alongX = primary == DungeonPortalFacing.NegativeX
                || primary == DungeonPortalFacing.PositiveX;
            DungeonPortalFacing wall = alongX && !used[(int)DungeonPortalFacing.PositiveZ]
                ? DungeonPortalFacing.PositiveZ
                : alongX && !used[(int)DungeonPortalFacing.NegativeZ]
                    ? DungeonPortalFacing.NegativeZ
                    : !alongX && !used[(int)DungeonPortalFacing.PositiveX]
                        ? DungeonPortalFacing.PositiveX : DungeonPortalFacing.NegativeX;
            bool bankAlongX = wall == DungeonPortalFacing.PositiveZ
                || wall == DungeonPortalFacing.NegativeZ;
            int sign = wall == DungeonPortalFacing.PositiveX
                || wall == DungeonPortalFacing.PositiveZ ? 1 : -1;
            int wallAxis = bankAlongX
                ? room.Center.Z + sign * room.Size.Z / 2
                : room.Center.X + sign * room.Size.X / 2;
            int front = bankAlongX ? room.Center.Z + sign * room.Size.Z / 10
                : room.Center.X + sign * room.Size.X / 10;
            int low = Math.Min(front, wallAxis), high = Math.Max(front, wallAxis);
            for (int stall = -2; stall <= 2; stall++)
            {
                int lateral = stall * 1450;
                Add(result, room, "stall-divider-" + stall, DungeonPresentationKind.Fixture,
                    DungeonMaterialRole.RestroomPartition,
                    bankAlongX ? Box(room.Center.X + lateral - 740, room.Bounds.Minimum.Y,
                        low, room.Center.X + lateral - 660, room.Bounds.Minimum.Y + 2300, high)
                    : Box(low, room.Bounds.Minimum.Y, room.Center.Z + lateral - 740,
                        high, room.Bounds.Minimum.Y + 2300, room.Center.Z + lateral - 660), true);
                if (!resolved) Add(result, room, "stall-door-" + stall, DungeonPresentationKind.Fixture,
                    DungeonMaterialRole.RestroomPartition,
                    bankAlongX ? Box(room.Center.X + lateral - 590, room.Bounds.Minimum.Y,
                        front - 40, room.Center.X + lateral + 590,
                        room.Bounds.Minimum.Y + 2100, front + 40)
                    : Box(front - 40, room.Bounds.Minimum.Y,
                        room.Center.Z + lateral - 590, front + 40,
                        room.Bounds.Minimum.Y + 2100, room.Center.Z + lateral + 590));
                int toiletAxis = wallAxis - sign * 720;
                Add(result, room, "toilet-" + stall, DungeonPresentationKind.Fixture,
                    DungeonMaterialRole.Porcelain,
                    bankAlongX ? Box(room.Center.X + lateral - 310, room.Bounds.Minimum.Y,
                        toiletAxis - 425, room.Center.X + lateral + 310,
                        room.Bounds.Minimum.Y + (resolved ? 840 : 600), toiletAxis + 425)
                    : Box(toiletAxis - 425, room.Bounds.Minimum.Y,
                        room.Center.Z + lateral - 310, toiletAxis + 425,
                        room.Bounds.Minimum.Y + (resolved ? 840 : 600), room.Center.Z + lateral + 310), true,
                    resolved ? DungeonFixtureRole.Toilet : DungeonFixtureRole.Unspecified,
                    bankAlongX ? (sign > 0 ? 180 : 0) : (sign > 0 ? 270 : 90));
            }
            int rear = wallAxis - sign * 60;
            Add(result, room, "stall-rear-infill", DungeonPresentationKind.Fixture,
                DungeonMaterialRole.RestroomPartition,
                bankAlongX ? Box(room.Center.X - 3700, room.Bounds.Minimum.Y,
                    rear - 70, room.Center.X + 3700, room.Bounds.Minimum.Y + 2300, rear + 70)
                : Box(rear - 70, room.Bounds.Minimum.Y, room.Center.Z - 3700,
                    rear + 70, room.Bounds.Minimum.Y + 2300, room.Center.Z + 3700), true);
            int sinkAxis = resolved
                ? (bankAlongX ? room.Center.Z - sign * (room.Size.Z / 2 - 380)
                    : room.Center.X - sign * (room.Size.X / 2 - 380))
                : (bankAlongX ? room.Center.Z - sign * room.Size.Z / 5 : room.Center.X - sign * room.Size.X / 5);
            var placedSinks = new List<WorldBounds>();
            for (int sink = -1; sink <= 1; sink++)
            {
                int lateral = sink * 1800;
                WorldBounds SinkBounds(int offset) => bankAlongX
                    ? Box(room.Center.X + offset - 575, room.Bounds.Minimum.Y + (resolved ? 540 : 670),
                        sinkAxis - 310, room.Center.X + offset + 575, room.Bounds.Minimum.Y + (resolved ? 1260 : 890), sinkAxis + 310)
                    : Box(sinkAxis - 310, room.Bounds.Minimum.Y + (resolved ? 540 : 670), room.Center.Z + offset - 575,
                        sinkAxis + 310, room.Bounds.Minimum.Y + (resolved ? 1260 : 890), room.Center.Z + offset + 575);
                WorldBounds bounds = SinkBounds(lateral);
                if (resolved)
                {
                    bool foundPosition = false;
                    int span = bankAlongX ? room.Size.X : room.Size.Z;
                    for (int attempt = 0; attempt <= span / 75; attempt++)
                    {
                        int offset = lateral + (attempt == 0 ? 0 : (attempt % 2 == 1 ? 1 : -1) * ((attempt + 1) / 2) * 150);
                        if (Math.Abs(offset) + 775 > span / 2) continue;
                        WorldBounds candidate = SinkBounds(offset);
                        bool occupied = false;
                        foreach (DungeonDoorPortal portal in layout.DoorPortals)
                            if (portal.RoomId == room.Id && OverlapsFootprint(candidate, portal.ClosedBlockingBounds, 200))
                            { occupied = true; break; }
                        foreach (WorldBounds placed in placedSinks)
                            if (OverlapsFootprint(candidate, placed, 100)) { occupied = true; break; }
                        if (occupied) continue;
                        bounds = candidate; foundPosition = true; break;
                    }
                    if (!foundPosition) throw new InvalidOperationException("No wall clearance for sink in room " + room.Id);
                    placedSinks.Add(bounds);
                }
                Add(result, room, "sink-" + sink, DungeonPresentationKind.Fixture,
                    DungeonMaterialRole.Porcelain, bounds, true,
                    resolved ? DungeonFixtureRole.Sink : DungeonFixtureRole.Unspecified,
                    bankAlongX ? (sign > 0 ? 0 : 180) : (sign > 0 ? 90 : 270));
            }
        }

        private static bool OverlapsFootprint(WorldBounds a, WorldBounds b, int clearance) =>
            a.Minimum.X < b.Maximum.X + clearance && a.Maximum.X > b.Minimum.X - clearance
            && a.Minimum.Z < b.Maximum.Z + clearance && a.Maximum.Z > b.Minimum.Z - clearance;

        private static void Add(List<DungeonPresentationElement> target, DungeonRoom room,
            string suffix, DungeonPresentationKind kind, DungeonMaterialRole role,
            WorldBounds bounds, bool blocks = false, DungeonFixtureRole fixtureRole = DungeonFixtureRole.Unspecified, int yaw = 0) => target.Add(
                new DungeonPresentationElement(room.Id + "/" + suffix, room.Id,
                    kind, role, bounds, blocks, fixtureRole == DungeonFixtureRole.Unspecified
                        ? (kind == DungeonPresentationKind.Light ? DungeonFixtureRole.Light
                            : role == DungeonMaterialRole.RestroomPartition ? DungeonFixtureRole.Partition : fixtureRole)
                        : fixtureRole, yaw));

        private static WorldBounds Box(int minX, int minY, int minZ,
            int maxX, int maxY, int maxZ) => new WorldBounds(
                new WorldVector3(minX, minY, minZ), new WorldVector3(maxX, maxY, maxZ));
    }
}
