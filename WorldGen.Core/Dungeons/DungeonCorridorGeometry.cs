using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    public static class DungeonCorridorGeometry
    {
        public static int FloorSupportDepth(DungeonCorridor corridor, WorldBounds section)
        {
            if (corridor == null) throw new ArgumentNullException(nameof(corridor));
            // Descending stair treads must be solid to the lower landing. A thin,
            // floating slab can be missed at a frame spike and has no catch below it.
            return corridor.ChangesElevation
                ? Math.Max(750, section.Minimum.Y - corridor.WalkableBounds.Minimum.Y + 750)
                : 750;
        }

        public static bool IsAlongX(DungeonLayout layout, DungeonCorridor corridor)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (corridor == null) throw new ArgumentNullException(nameof(corridor));

            for (int i = 0; i < layout.DoorPortals.Count; i++)
            {
                DungeonDoorPortal portal = layout.DoorPortals[i];
                if (!string.Equals(portal.ConnectionId, corridor.ConnectionId,
                        StringComparison.Ordinal))
                    continue;
                return portal.Facing == DungeonPortalFacing.NegativeX
                       || portal.Facing == DungeonPortalFacing.PositiveX;
            }

            throw new ArgumentException(
                "Corridor connection has no door portal: " + corridor.ConnectionId,
                nameof(corridor));
        }

        public static IReadOnlyList<WorldBounds> FloorSections(DungeonLayout layout,
            DungeonCorridor corridor, int maximumStepRise = 240)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (corridor == null) throw new ArgumentNullException(nameof(corridor));
            if (maximumStepRise <= 0) throw new ArgumentOutOfRangeException(nameof(maximumStepRise));
            if (DungeonCaveTunnel.IsWideDescent(layout, corridor))
            {
                DungeonCaveTunnel.Section[] path = DungeonCaveTunnel.Sections(layout, corridor);
                bool along = IsAlongX(layout, corridor);
                var curved = new List<WorldBounds>(path.Length - 1);
                for (int i = 0; i < path.Length - 1; i++)
                {
                    int axisA = along ? path[i].Center.X : path[i].Center.Z;
                    int axisB = along ? path[i + 1].Center.X : path[i + 1].Center.Z;
                    int left = Math.Min(along ? path[i].Left.Z : path[i].Left.X,
                        along ? path[i + 1].Left.Z : path[i + 1].Left.X) - 150;
                    int right = Math.Max(along ? path[i].Right.Z : path[i].Right.X,
                        along ? path[i + 1].Right.Z : path[i + 1].Right.X) + 150;
                    int floor = Math.Min(path[i].Center.Y, path[i + 1].Center.Y);
                    WorldBounds envelope = corridor.WalkableBounds;
                    curved.Add(along
                        ? new WorldBounds(new WorldVector3(Math.Min(axisA, axisB), floor, left),
                            new WorldVector3(Math.Max(axisA, axisB), envelope.Maximum.Y, right))
                        : new WorldBounds(new WorldVector3(left, floor, Math.Min(axisA, axisB)),
                            new WorldVector3(right, envelope.Maximum.Y, Math.Max(axisA, axisB))));
                }
                return curved;
            }
            if (!corridor.ChangesElevation)
                return new[] { WithFloor(corridor.WalkableBounds, corridor.FromFloorY) };

            DungeonConnection connection = null;
            DungeonRoom from = null;
            DungeonRoom to = null;
            for (int i = 0; i < layout.Connections.Count; i++)
                if (layout.Connections[i].Id == corridor.ConnectionId)
                { connection = layout.Connections[i]; break; }
            if (connection == null)
                throw new ArgumentException("Corridor references a missing connection.", nameof(corridor));
            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                if (layout.Rooms[i].Id == connection.FromRoomId) from = layout.Rooms[i];
                if (layout.Rooms[i].Id == connection.ToRoomId) to = layout.Rooms[i];
            }
            if (from == null || to == null)
                throw new ArgumentException("Corridor connection references a missing room.", nameof(corridor));

            int rise = corridor.ToFloorY - corridor.FromFloorY;
            int count = Math.Max(2,
                (Math.Abs(rise) + maximumStepRise - 1) / maximumStepRise + 1);
            bool alongX = IsAlongX(layout, corridor);
            bool increasingAxis = alongX ? to.Center.X > from.Center.X : to.Center.Z > from.Center.Z;
            WorldBounds b = corridor.WalkableBounds;
            int axisMin = alongX ? b.Minimum.X : b.Minimum.Z;
            int axisMax = alongX ? b.Maximum.X : b.Maximum.Z;
            var sections = new List<WorldBounds>(count);
            for (int step = 0; step < count; step++)
            {
                int orderedStep = increasingAxis ? step : count - 1 - step;
                int start = axisMin + (axisMax - axisMin) * step / count;
                int end = axisMin + (axisMax - axisMin) * (step + 1) / count;
                int y = corridor.FromFloorY + rise * orderedStep / (count - 1);
                sections.Add(alongX
                    ? new WorldBounds(new WorldVector3(start, y, b.Minimum.Z),
                        new WorldVector3(end, b.Maximum.Y, b.Maximum.Z))
                    : new WorldBounds(new WorldVector3(b.Minimum.X, y, start),
                        new WorldVector3(b.Maximum.X, b.Maximum.Y, end)));
            }
            return sections;
        }

        private static WorldBounds WithFloor(WorldBounds bounds, int floorY) =>
            new WorldBounds(new WorldVector3(bounds.Minimum.X, floorY, bounds.Minimum.Z),
                bounds.Maximum);
    }
}
