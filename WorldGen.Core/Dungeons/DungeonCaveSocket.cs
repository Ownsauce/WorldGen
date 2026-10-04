using System;

namespace WorldGen.Dungeons
{
    public enum DungeonCaveSocketKind
    {
        Standard = 0,
        Medium = 1,
        Wide = 2,
        Tall = 3,
        ExtraLarge = 4
    }

    public readonly struct DungeonCaveSocketProfile
    {
        public DungeonCaveSocketProfile(DungeonCaveSocketKind kind, int width,
            int crownHeight, int sideInset, int archSegments)
        {
            Kind = kind; Width = width; CrownHeight = crownHeight;
            SideInset = sideInset; ArchSegments = archSegments;
        }
        public DungeonCaveSocketKind Kind { get; }
        public int Width { get; }
        public int CrownHeight { get; }
        public int SideInset { get; }
        public int ArchSegments { get; }
        public int VisualWidth => Width - SideInset * 2;
    }

    public static class DungeonCaveSockets
    {
        private static readonly DungeonCaveSocketProfile[] Profiles =
        {
            new DungeonCaveSocketProfile(DungeonCaveSocketKind.Standard, 4200, 3000, 200, 16),
            new DungeonCaveSocketProfile(DungeonCaveSocketKind.Medium, 6500, 4000, 200, 16),
            new DungeonCaveSocketProfile(DungeonCaveSocketKind.Wide, 8500, 4800, 250, 16),
            new DungeonCaveSocketProfile(DungeonCaveSocketKind.Tall, 7000, 6500, 250, 16),
            new DungeonCaveSocketProfile(DungeonCaveSocketKind.ExtraLarge, 12000, 7500, 350, 16)
        };

        public static DungeonCaveSocketProfile Get(DungeonCaveSocketKind kind) =>
            Profiles[(int)kind];

        public static double NormalizedCross(int point, int segments)
        {
            double angle = Math.PI * point / segments;
            return -Math.Cos(angle);
        }

        public static double NormalizedHeight(int point, int segments)
        {
            double angle = Math.PI * point / segments;
            return Math.Sin(angle);
        }

        public static DungeonCaveSocketProfile Fit(DungeonCaveSocketProfile selected,
            int maximumWidth)
        {
            if (selected.Width <= maximumWidth) return selected;
            DungeonCaveSocketProfile result = Profiles[0];
            for (int i = 0; i < Profiles.Length; i++)
                if (Profiles[i].Width <= maximumWidth && Profiles[i].Width > result.Width)
                    result = Profiles[i];
            return result;
        }

        public static DungeonCaveSocketProfile Select(ulong seed, int connectionIndex,
            int endpointIndex)
        {
            ulong value = seed ^ ((ulong)(connectionIndex + 1) * 0x9E3779B97F4A7C15UL)
                ^ ((ulong)(endpointIndex + 11) * 0xBF58476D1CE4E5B9UL);
            value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL; value ^= value >> 27;
            return Profiles[(int)(value % (ulong)Profiles.Length)];
        }

        public static DungeonCaveSocketProfile Resolve(DungeonDoorPortal portal)
        {
            if (portal == null) throw new ArgumentNullException(nameof(portal));
            bool xFacing = portal.Facing == DungeonPortalFacing.NegativeX
                || portal.Facing == DungeonPortalFacing.PositiveX;
            int width = xFacing
                ? portal.ClosedBlockingBounds.Maximum.Z - portal.ClosedBlockingBounds.Minimum.Z
                : portal.ClosedBlockingBounds.Maximum.X - portal.ClosedBlockingBounds.Minimum.X;
            for (int i = 0; i < Profiles.Length; i++)
                if (Profiles[i].Width == width) return Profiles[i];
            throw new InvalidOperationException("Unknown cave socket width " + width + ".");
        }
    }
}
