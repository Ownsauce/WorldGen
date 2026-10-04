using System;
using WorldGen.Spatial;

namespace WorldGen.Dungeons
{
    // Shared authored cave-floor recipe. Integer world units keep the collision
    // surface identical on server and client, including every portal edge.
    public static class DungeonCaveSurface
    {
        public sealed class MeshData
        {
            public MeshData(WorldVector3[] vertices, int[] triangles)
            { Vertices = vertices; Triangles = triangles; }

            public WorldVector3[] Vertices { get; }
            public int[] Triangles { get; }
        }

        public static MeshData BuildFloor(WorldBounds bounds, int seed,
            bool protectCenter = false)
        {
            int xSpan = bounds.Maximum.X - bounds.Minimum.X;
            int zSpan = bounds.Maximum.Z - bounds.Minimum.Z;
            int xCells = Math.Max(2, (xSpan + 1499) / 1500);
            int zCells = Math.Max(2, (zSpan + 1499) / 1500);
            var vertices = new WorldVector3[(xCells + 1) * (zCells + 1)];
            var triangles = new int[xCells * zCells * 6];
            for (int z = 0; z <= zCells; z++)
            for (int x = 0; x <= xCells; x++)
            {
                int worldX = bounds.Minimum.X + xSpan * x / xCells;
                int worldZ = bounds.Minimum.Z + zSpan * z / zCells;
                int edge = Math.Min(Math.Min(worldX - bounds.Minimum.X,
                    bounds.Maximum.X - worldX), Math.Min(worldZ - bounds.Minimum.Z,
                    bounds.Maximum.Z - worldZ));
                int fade = Math.Min(1000, Math.Max(0, edge * 1000 / 2500));
                if (protectCenter)
                {
                    int centerX = (bounds.Minimum.X + bounds.Maximum.X) / 2;
                    int centerZ = (bounds.Minimum.Z + bounds.Maximum.Z) / 2;
                    double distance = Math.Sqrt((double)(worldX - centerX) * (worldX - centerX)
                        + (double)(worldZ - centerZ) * (worldZ - centerZ));
                    fade = Math.Min(fade, Math.Min(1000,
                        Math.Max(0, (int)((distance - 1500.0) * 1000.0 / 2500.0))));
                }
                // Presentation-only relief. Keep this beneath the avatar's foot
                // tolerance while authoritative movement uses the flat support slab.
                // Edge fade guarantees a flush seam at doors and section boundaries.
                double xWave = Math.Sin((worldX + seed * 3187L) / 4700.0);
                double zWave = Math.Cos((worldZ - seed * 2017L) / 6100.0);
                int rise = (int)Math.Round((24 + 10 * xWave + 8 * zWave) * fade / 1000.0);
                vertices[z * (xCells + 1) + x] = new WorldVector3(
                    worldX, bounds.Minimum.Y + Math.Max(0, rise), worldZ);
            }
            for (int z = 0; z < zCells; z++)
            for (int x = 0; x < xCells; x++)
            {
                int t = (z * xCells + x) * 6;
                int a = z * (xCells + 1) + x;
                triangles[t] = a; triangles[t + 1] = a + xCells + 1;
                triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + xCells + 1;
                triangles[t + 5] = a + xCells + 2;
            }
            return new MeshData(vertices, triangles);
        }
    }
}
