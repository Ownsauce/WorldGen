using System;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    // First authored tunnel recipe: a socket-aligned S-bend inside the safe
    // corridor envelope. Later modules may use their own curves and junctions.
    public static class DungeonCaveTunnel
    {
        public const string LedgeSideParameter = "caveLedgeSide";
        public const int UpperRouteWidth = 18000;
        private static readonly int[] OffsetControls =
            { 0, 220, 700, 950, 430, -550, -950, -300, 0 };
        private static readonly int[] WidthControls =
            { 5400, 5500, 5600, 5400, 5500, 5600, 5400, 5500, 5400 };

        public readonly struct Section
        {
            public Section(WorldVector3 center, WorldVector3 left,
                WorldVector3 right, int width)
            { Center = center; Left = left; Right = right; Width = width; }
            public WorldVector3 Center { get; }
            public WorldVector3 Left { get; }
            public WorldVector3 Right { get; }
            public int Width { get; }
        }

        public readonly struct LedgeSegment
        {
            public LedgeSegment(WorldVector3 start, WorldVector3 end, int floorY,
                int baseFloorY, int width)
            { Start = start; End = end; FloorY = floorY; BaseFloorY = baseFloorY; Width = width; }
            public WorldVector3 Start { get; }
            public WorldVector3 End { get; }
            public int FloorY { get; }
            public int BaseFloorY { get; }
            public int Width { get; }
        }

        public static bool IsFirstBend(DungeonLayout layout, DungeonCorridor corridor) =>
            layout != null && corridor != null && layout.Corridors.Count > 0
            && layout.Corridors[0].Id == corridor.Id && !corridor.ChangesElevation
            && DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                == DungeonLayoutProfile.CaveDungeon;

        public static bool IsWideDescent(DungeonLayout layout, DungeonCorridor corridor) =>
            layout != null && corridor != null && layout.Corridors.Count > 1
            && layout.Corridors[1].Id == corridor.Id && corridor.ChangesElevation
            && DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                == DungeonLayoutProfile.CaveDungeon;

        // Side is relative to travel from the upper room into the passage.
        public static int LedgeCrossSign(DungeonLayout layout, DungeonCorridor corridor)
        {
            bool alongX = DungeonCorridorGeometry.IsAlongX(layout, corridor);
            bool forward = IsIncreasingAxis(layout, corridor, alongX);
            int right = alongX ? (forward ? -1 : 1) : (forward ? 1 : -1);
            string side;
            return layout.Manifest.Parameters.TryGetValue(LedgeSideParameter, out side)
                && string.Equals(side, "left", StringComparison.OrdinalIgnoreCase)
                ? -right : right;
        }

        private static bool IsIncreasingAxis(DungeonLayout layout, DungeonCorridor corridor,
            bool alongX)
        {
            DungeonConnection connection = null;
            foreach (DungeonConnection candidate in layout.Connections)
                if (candidate.Id == corridor.ConnectionId) { connection = candidate; break; }
            if (connection == null) throw new ArgumentException("Missing cave connection.");
            DungeonRoom from = null, to = null;
            foreach (DungeonRoom room in layout.Rooms)
            {
                if (room.Id == connection.FromRoomId) from = room;
                if (room.Id == connection.ToRoomId) to = room;
            }
            if (from == null || to == null) throw new ArgumentException("Missing cave room.");
            return alongX ? to.Center.X > from.Center.X : to.Center.Z > from.Center.Z;
        }

        public static int LedgeLift(DungeonLayout layout, DungeonCorridor corridor, double axisT)
        {
            bool forward = IsIncreasingAxis(layout, corridor,
                DungeonCorridorGeometry.IsAlongX(layout, corridor));
            double travelT = forward ? axisT : 1.0 - axisT;
            // Leave a level, walk-on lip through the first ~30 m. The old ramp
            // already stood >0.5 m above the main floor at x≈62 m, so the
            // player hit its vertical support before they could enter it.
            const double rampStart = 0.035;
            // Roughly 26 samples keeps each authoritative rise below the
            // controller's 240 mm safe step while reaching the plateau sooner
            // than the former 28%-of-route ramp.
            const double rampEnd = 0.235;
            double rise = Math.Max(0.0,
                Math.Min(1.0, (travelT - rampStart) / (rampEnd - rampStart)));
            // Merge back into the main route before the far room socket. Keeping
            // an 18 m-wide, 6 m-high shelf through that smaller opening made the
            // room wall appear as a giant obstruction across the landing.
            double descent = 1.0 - Math.Max(0.0,
                Math.Min(1.0, (travelT - 0.48) / 0.48));
            return (int)Math.Round(6000 * rise * descent);
        }

        public static int UpperRouteWidthAt(DungeonLayout layout,
            DungeonCorridor corridor, double axisT)
        {
            bool forward = IsIncreasingAxis(layout, corridor,
                DungeonCorridorGeometry.IsAlongX(layout, corridor));
            double travelT = forward ? axisT : 1.0 - axisT;
            double grow = Math.Max(0.0, Math.Min(1.0, travelT / 0.12));
            double taper = 1.0 - Math.Max(0.0,
                Math.Min(1.0, (travelT - 0.50) / 0.46));
            return (int)Math.Round(UpperRouteWidth * grow * taper);
        }

        public static WorldBounds[] LedgeSections(DungeonLayout layout, DungeonCorridor corridor)
        {
            if (!IsWideDescent(layout, corridor)) return Array.Empty<WorldBounds>();
            Section[] path = Sections(layout, corridor);
            bool alongX = DungeonCorridorGeometry.IsAlongX(layout, corridor);
            int side = LedgeCrossSign(layout, corridor);
            var ledge = new WorldBounds[(path.Length - 1) * 2];
            for (int i = 0; i < path.Length - 1; i++)
            {
                double t = (i + 0.5) / (path.Length - 1);
                int lift = LedgeLift(layout, corridor, t);
                int floor = (path[i].Center.Y + path[i + 1].Center.Y) / 2 + lift;
                int routeWidth = UpperRouteWidthAt(layout, corridor, t);
                int a = alongX ? path[i].Center.X : path[i].Center.Z;
                int b = alongX ? path[i + 1].Center.X : path[i + 1].Center.Z;
                int crossA = alongX ? path[i].Center.Z : path[i].Center.X;
                int crossB = alongX ? path[i + 1].Center.Z : path[i + 1].Center.X;
                // Build a solid, overlapping shelf without letting an AABB's
                // inside corner protrude across the curved lower route.
                int middle = (a + b) / 2;
                for (int half = 0; half < 2; half++)
                {
                    int localCross = half == 0 ? crossA : crossB;
                    int edge = localCross + side * path[i + half].Width / 2;
                    int crossMin = side > 0 ? edge : edge - routeWidth;
                    int crossMax = side > 0 ? edge + routeWidth : edge;
                    int axisMin = half == 0 ? Math.Min(a, b) : middle - 100;
                    int axisMax = half == 0 ? middle + 100 : Math.Max(a, b);
                    ledge[i * 2 + half] = alongX
                        ? new WorldBounds(new WorldVector3(axisMin, floor, crossMin),
                            new WorldVector3(axisMax, corridor.WalkableBounds.Maximum.Y, crossMax))
                        : new WorldBounds(new WorldVector3(crossMin, floor, axisMin),
                            new WorldVector3(crossMax, corridor.WalkableBounds.Maximum.Y, axisMax));
                }
            }
            return ledge;
        }

        public static LedgeSegment[] LedgeSegments(DungeonLayout layout,
            DungeonCorridor corridor)
        {
            if (!IsWideDescent(layout, corridor)) return Array.Empty<LedgeSegment>();
            Section[] path = Sections(layout, corridor);
            bool alongX = DungeonCorridorGeometry.IsAlongX(layout, corridor);
            int side = LedgeCrossSign(layout, corridor);
            const int lanes = 6;
            int sectionCount = path.Length - 1;
            var result = new LedgeSegment[sectionCount * lanes];
            for (int lane = 0; lane < lanes; lane++)
            for (int i = 0; i < sectionCount; i++)
            {
                WorldVector3 edgeA = side > 0 ? path[i].Right : path[i].Left;
                WorldVector3 edgeB = side > 0 ? path[i + 1].Right : path[i + 1].Left;
                double t = (i + 0.5) / sectionCount;
                int routeWidthA = UpperRouteWidthAt(layout, corridor,
                    i / (double)sectionCount);
                int routeWidthB = UpperRouteWidthAt(layout, corridor,
                    (i + 1) / (double)sectionCount);
                double laneCenter = (lane + 0.5) / lanes;
                int shiftA = side * (int)Math.Round(routeWidthA * laneCenter);
                int shiftB = side * (int)Math.Round(routeWidthB * laneCenter);
                WorldVector3 start = alongX
                    ? new WorldVector3(edgeA.X, edgeA.Y, edgeA.Z + shiftA)
                    : new WorldVector3(edgeA.X + shiftA, edgeA.Y, edgeA.Z);
                WorldVector3 end = alongX
                    ? new WorldVector3(edgeB.X, edgeB.Y, edgeB.Z + shiftB)
                    : new WorldVector3(edgeB.X + shiftB, edgeB.Y, edgeB.Z);
                int floorY = (path[i].Center.Y + path[i + 1].Center.Y) / 2
                    + LedgeLift(layout, corridor, t);
                double dx = end.X - start.X;
                double dz = end.Z - start.Z;
                double horizontal = Math.Sqrt(dx * dx + dz * dz);
                double axis = alongX ? Math.Abs(dx) : Math.Abs(dz);
                // The visible ledge is 4.5 m across the world cross-axis. A
                // yawed box of that same width protrudes into the main path at
                // sharp turns; use the perpendicular cross-section instead.
                // Keep the support's inner edge back from the drop. Even the
                // perpendicular width can poke into the lower route at the
                // chord joins of this tight C-curve; leaving a 0.5 m edge
                // allowance gives the falling player capsule clear space.
                // A yawed support's local width projects onto the world's cross
                // axis by axis/horizontal. Compensate with the inverse ratio so
                // its collision still reaches both visible ledge edges in bends.
                int width = (int)Math.Round(Math.Max(routeWidthA, routeWidthB)
                    / (double)lanes * horizontal / Math.Max(1.0, axis));
                // The visible ledge is a solid rock shelf down to the main
                // floor, not a floating slab a capsule can wedge underneath.
                int baseFloorY = (path[i].Center.Y + path[i + 1].Center.Y) / 2 - 820;
                result[lane * sectionCount + i] = new LedgeSegment(
                    start, end, floorY, baseFloorY, width);
            }
            return result;
        }

        public static Section[] Sections(DungeonLayout layout, DungeonCorridor corridor)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (corridor == null) throw new ArgumentNullException(nameof(corridor));
            bool alongX = DungeonCorridorGeometry.IsAlongX(layout, corridor);
            WorldBounds b = corridor.WalkableBounds;
            int axisMin = alongX ? b.Minimum.X : b.Minimum.Z;
            int axisSpan = alongX ? b.Maximum.X - b.Minimum.X : b.Maximum.Z - b.Minimum.Z;
            int crossMin = alongX ? b.Minimum.Z : b.Minimum.X;
            int crossSpan = alongX ? b.Maximum.Z - b.Minimum.Z : b.Maximum.X - b.Minimum.X;
            int crossCenter = crossMin + crossSpan / 2;
            bool wide = IsWideDescent(layout, corridor);
            int startSocketWidth = SocketWidth(layout, corridor, alongX, true);
            int endSocketWidth = SocketWidth(layout, corridor, alongX, false);
            if (wide) crossCenter -= 92500;
            int steps = wide ? 128 : 32;
            var sections = new Section[steps + 1];
            bool increasingFloor = !wide || IsIncreasingAxis(layout, corridor, alongX);
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                int axis = axisMin + (int)Math.Round(axisSpan * t);
                int width;
                if (wide)
                {
                    int wideInterior = 18000
                        + (int)Math.Round(300 * Math.Sin(t * Math.PI * 4));
                    if (t < 0.08)
                        width = startSocketWidth + (int)Math.Round(
                            (wideInterior - startSocketWidth) * (t / 0.08));
                    else if (t > 0.92)
                        width = wideInterior + (int)Math.Round(
                            (endSocketWidth - wideInterior) * ((t - 0.92) / 0.08));
                    else width = wideInterior;
                }
                else if (IsFirstBend(layout, corridor))
                {
                    int middleWidth = crossSpan - 2500;
                    if (t < 0.15)
                        width = startSocketWidth + (int)Math.Round(
                            (middleWidth - startSocketWidth) * (t / 0.15));
                    else if (t > 0.85)
                        width = middleWidth + (int)Math.Round(
                            (endSocketWidth - middleWidth) * ((t - 0.85) / 0.15));
                    else width = middleWidth;
                }
                else
                    width = (int)Math.Round(Sample(WidthControls, t));
                width = Math.Min(width, IsFirstBend(layout, corridor)
                    ? crossSpan : crossSpan - 700);
                int maxOffset = Math.Max(0, (crossSpan - width) / 2 - 350);
                int offset = wide
                    ? (int)Math.Round(175000 * Math.Pow(Math.Sin(t * Math.PI), 2))
                    : Math.Max(-maxOffset,
                        Math.Min(maxOffset, (int)Math.Round(Sample(OffsetControls, t))));
                int cross = crossCenter + offset;
                int floor = wide
                    ? corridor.FromFloorY + (int)Math.Round((corridor.ToFloorY - corridor.FromFloorY)
                        * (increasingFloor ? t : 1 - t)) : corridor.FromFloorY;
                var center = alongX
                    ? new WorldVector3(axis, floor, cross)
                    : new WorldVector3(cross, floor, axis);
                var left = alongX
                    ? new WorldVector3(axis, floor, cross - width / 2)
                    : new WorldVector3(cross - width / 2, floor, axis);
                var right = alongX
                    ? new WorldVector3(axis, floor,
                        cross + (width - width / 2))
                    : new WorldVector3(cross + (width - width / 2), floor, axis);
                sections[i] = new Section(center, left, right, width);
            }
            return sections;
        }

        private static int SocketWidth(DungeonLayout layout, DungeonCorridor corridor,
            bool alongX, bool minimumEnd)
        {
            DungeonDoorPortal selected = null;
            int selectedAxis = minimumEnd ? int.MaxValue : int.MinValue;
            foreach (DungeonDoorPortal portal in layout.DoorPortals)
            {
                if (portal.ConnectionId != corridor.ConnectionId) continue;
                int axis = alongX ? portal.Center.X : portal.Center.Z;
                if (minimumEnd ? axis < selectedAxis : axis > selectedAxis)
                { selected = portal; selectedAxis = axis; }
            }
            return selected == null
                ? (alongX ? corridor.WalkableBounds.Maximum.Z
                    - corridor.WalkableBounds.Minimum.Z
                    : corridor.WalkableBounds.Maximum.X - corridor.WalkableBounds.Minimum.X)
                : DungeonCaveSockets.Resolve(selected).VisualWidth;
        }

        private static double Sample(int[] controls, double t)
        {
            double scaled = Math.Min(controls.Length - 1, t * (controls.Length - 1));
            int index = Math.Min(controls.Length - 2, (int)scaled);
            double u = scaled - index;
            double p0 = controls[Math.Max(0, index - 1)];
            double p1 = controls[index];
            double p2 = controls[index + 1];
            double p3 = controls[Math.Min(controls.Length - 1, index + 2)];
            double u2 = u * u, u3 = u2 * u;
            return 0.5 * (2 * p1 + (-p0 + p2) * u
                + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u2
                + (-p0 + 3 * p1 - 3 * p2 + p3) * u3);
        }
    }
}
