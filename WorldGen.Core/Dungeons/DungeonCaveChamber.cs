using System;
using System.Collections.Generic;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    // One deliberately authored chamber outline. These normalized radial control
    // points are content data, not noise added to a rectangular room at runtime.
    // Later cave modules can supply their own point sets and socket definitions.
    public static class DungeonCaveChamber
    {
        public const int SocketSideInset = 200;
        public const int SocketBoundaryOverlap = 180;

        private static readonly int[] RadiusPermille =
        {
            1000, 940, 805, 665, 750, 915,
            1000, 920, 780, 690, 800, 915,
            1000, 955, 820, 695, 740, 910,
            1000, 900, 760, 675, 790, 945
        };

        public readonly struct WallSegment
        {
            public WallSegment(WorldVector3 start, WorldVector3 end, int outlineIndex)
            { Start = start; End = end; OutlineIndex = outlineIndex; }
            public WorldVector3 Start { get; }
            public WorldVector3 End { get; }
            public int OutlineIndex { get; }
        }

        public static bool IsEntranceModule(DungeonLayout layout, DungeonRoom room) =>
            layout != null && room != null && room.Index == 0
            && DungeonGenerationProfileCatalog.ReadProfile(layout.Manifest)
                == DungeonLayoutProfile.CaveDungeon;

        public static WorldVector3[] ControlPoints(DungeonRoom room)
        {
            if (room == null) throw new ArgumentNullException(nameof(room));
            var points = new WorldVector3[RadiusPermille.Length];
            double halfX = room.Size.X * 0.5;
            double halfZ = room.Size.Z * 0.5;
            for (int i = 0; i < points.Length; i++)
            {
                double angle = i * Math.PI * 2.0 / points.Length;
                double xDirection = Math.Cos(angle);
                double zDirection = Math.Sin(angle);
                double limitX = Math.Abs(xDirection) < 0.00001
                    ? double.MaxValue : halfX / Math.Abs(xDirection);
                double limitZ = Math.Abs(zDirection) < 0.00001
                    ? double.MaxValue : halfZ / Math.Abs(zDirection);
                double radius = Math.Min(limitX, limitZ) * RadiusPermille[i] / 1000.0;
                points[i] = new WorldVector3(
                    room.Center.X + (int)Math.Round(radius * xDirection),
                    room.Bounds.Minimum.Y,
                    room.Center.Z + (int)Math.Round(radius * zDirection));
            }
            return points;
        }

        public static WorldVector3[] Outline(DungeonRoom room)
        {
            WorldVector3[] controls = ControlPoints(room);
            const int subdivisions = 4;
            var outline = new WorldVector3[controls.Length * subdivisions];
            for (int i = 0; i < controls.Length; i++)
            for (int step = 0; step < subdivisions; step++)
            {
                WorldVector3 p0 = controls[(i + controls.Length - 1) % controls.Length];
                WorldVector3 p1 = controls[i];
                WorldVector3 p2 = controls[(i + 1) % controls.Length];
                WorldVector3 p3 = controls[(i + 2) % controls.Length];
                double t = step / (double)subdivisions;
                int x = (int)Math.Round(CatmullRom(p0.X, p1.X, p2.X, p3.X, t));
                int z = (int)Math.Round(CatmullRom(p0.Z, p1.Z, p2.Z, p3.Z, t));
                outline[i * subdivisions + step] = new WorldVector3(
                    Math.Max(room.Bounds.Minimum.X, Math.Min(room.Bounds.Maximum.X, x)),
                    room.Bounds.Minimum.Y,
                    Math.Max(room.Bounds.Minimum.Z, Math.Min(room.Bounds.Maximum.Z, z)));
            }
            return outline;
        }

        public static WorldVector3[] Outline(DungeonRoom room,
            IReadOnlyList<DungeonDoorPortal> portals)
        {
            WorldVector3[] outline = Outline(room);
            if (portals == null) return outline;
            for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
            {
                DungeonDoorPortal portal = portals[portalIndex];
                if (portal.RoomId != room.Id) continue;
                bool xFacing = portal.Facing == DungeonPortalFacing.NegativeX
                    || portal.Facing == DungeonPortalFacing.PositiveX;
                int side = portal.Facing == DungeonPortalFacing.PositiveX
                    || portal.Facing == DungeonPortalFacing.PositiveZ ? 1 : -1;
                int portalAxis = (xFacing ? portal.Center.X : portal.Center.Z)
                    + side * SocketBoundaryOverlap;
                int portalCross = xFacing ? portal.Center.Z : portal.Center.X;
                int portalWidth = xFacing
                    ? portal.ClosedBlockingBounds.Maximum.Z
                        - portal.ClosedBlockingBounds.Minimum.Z
                    : portal.ClosedBlockingBounds.Maximum.X
                        - portal.ClosedBlockingBounds.Minimum.X;
                int openingHalf = portalWidth / 2 - SocketSideInset;
                // The socket pad belongs to the chamber outline. Keep its blend
                // narrow so the room retains an organic silhouette instead of
                // stretching a broad flat wall toward every portal.
                const int shoulderWidth = 600;
                for (int pointIndex = 0; pointIndex < outline.Length; pointIndex++)
                {
                    WorldVector3 point = outline[pointIndex];
                    int axis = xFacing ? point.X : point.Z;
                    int centerAxis = xFacing ? room.Center.X : room.Center.Z;
                    if ((axis - centerAxis) * side <= 0) continue;
                    int cross = xFacing ? point.Z : point.X;
                    int outside = Math.Abs(cross - portalCross) - openingHalf;
                    if (outside >= shoulderWidth) continue;
                    double linear = 1.0 - Math.Max(0, outside) / (double)shoulderWidth;
                    double influence = linear * linear * (3.0 - 2.0 * linear);
                    int shapedAxis = (int)Math.Round(axis + (portalAxis - axis) * influence);
                    outline[pointIndex] = xFacing
                        ? new WorldVector3(shapedAxis, point.Y, point.Z)
                        : new WorldVector3(point.X, point.Y, shapedAxis);
                }
            }
            return outline;
        }

        private static double CatmullRom(double p0, double p1, double p2,
            double p3, double t)
        {
            double t2 = t * t, t3 = t2 * t;
            return 0.5 * ((2 * p1) + (-p0 + p2) * t
                + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
                + (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
        }

        public static IReadOnlyList<WallSegment> WallSegments(DungeonRoom room,
            IReadOnlyList<DungeonDoorPortal> portals)
        {
            WorldVector3[] outline = Outline(room, portals);
            var result = new List<WallSegment>(outline.Length);
            for (int i = 0; i < outline.Length; i++)
            {
                WorldVector3 a = outline[i];
                WorldVector3 b = outline[(i + 1) % outline.Length];
                if (!ClipAgainstDoorMouth(room, ref a, ref b, portals)) continue;
                result.Add(new WallSegment(a, b, i));
            }
            return result;
        }

        private static bool ClipAgainstDoorMouth(DungeonRoom room, ref WorldVector3 a,
            ref WorldVector3 b, IReadOnlyList<DungeonDoorPortal> portals)
        {
            if (portals == null) return true;
            for (int i = 0; i < portals.Count; i++)
            {
                DungeonDoorPortal portal = portals[i];
                if (portal.RoomId != room.Id) continue;
                bool xFacing = portal.Facing == DungeonPortalFacing.NegativeX
                    || portal.Facing == DungeonPortalFacing.PositiveX;
                int side = portal.Facing == DungeonPortalFacing.PositiveX
                    || portal.Facing == DungeonPortalFacing.PositiveZ ? 1 : -1;
                int aAlong = xFacing ? a.X - room.Center.X : a.Z - room.Center.Z;
                int bAlong = xFacing ? b.X - room.Center.X : b.Z - room.Center.Z;
                int aCross = xFacing ? a.Z - room.Center.Z : a.X - room.Center.X;
                int bCross = xFacing ? b.Z - room.Center.Z : b.X - room.Center.X;
                int portalCrossMinimum = xFacing
                    ? portal.ClosedBlockingBounds.Minimum.Z - room.Center.Z
                    : portal.ClosedBlockingBounds.Minimum.X - room.Center.X;
                int portalCrossMaximum = xFacing
                    ? portal.ClosedBlockingBounds.Maximum.Z - room.Center.Z
                    : portal.ClosedBlockingBounds.Maximum.X - room.Center.X;
                int socketInset = DungeonCaveSockets.Resolve(portal).SideInset;
                portalCrossMinimum += socketInset;
                portalCrossMaximum -= socketInset;
                if (aAlong * side <= 0 || bAlong * side <= 0) continue;
                bool aInside = aCross >= portalCrossMinimum
                    && aCross <= portalCrossMaximum;
                bool bInside = bCross >= portalCrossMinimum
                    && bCross <= portalCrossMaximum;
                if (aInside && bInside) return false;
                if (aInside)
                {
                    int boundary = bCross < portalCrossMinimum
                        ? portalCrossMinimum : portalCrossMaximum;
                    a = InterpolateAtCross(a, b, aCross, bCross, boundary);
                }
                else if (bInside)
                {
                    int boundary = aCross < portalCrossMinimum
                        ? portalCrossMinimum : portalCrossMaximum;
                    b = InterpolateAtCross(a, b, aCross, bCross, boundary);
                }
            }
            return true;
        }

        private static WorldVector3 InterpolateAtCross(WorldVector3 a, WorldVector3 b,
            int aCross, int bCross, int boundary)
        {
            double t = (boundary - aCross) / (double)(bCross - aCross);
            return new WorldVector3(
                (int)Math.Round(a.X + (b.X - a.X) * t),
                (int)Math.Round(a.Y + (b.Y - a.Y) * t),
                (int)Math.Round(a.Z + (b.Z - a.Z) * t));
        }
    }
}
