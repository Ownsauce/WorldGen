using System;
using System.Collections.Generic;
using WorldGen.Dungeons;

namespace WorldGen.Geometry
{
    public readonly struct GeometryVector3
    {
        public GeometryVector3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public float X { get; } public float Y { get; } public float Z { get; }
    }

    public readonly struct GeometryVector2
    {
        public GeometryVector2(float x, float y) { X = x; Y = y; }
        public float X { get; } public float Y { get; }
    }

    public sealed class GeometryMeshData
    {
        public GeometryMeshData(GeometryVector3[] vertices, GeometryVector3[] normals,
            GeometryVector2[] uv, int[] indices)
        { Vertices = vertices; Normals = normals; UV = uv; Indices = indices; }
        public GeometryVector3[] Vertices { get; }
        public GeometryVector3[] Normals { get; }
        public GeometryVector2[] UV { get; }
        public int[] Indices { get; }
    }

    public static class ConstructiveMeshBuilder
    {
        public static GeometryMeshData Build(DungeonPrimitiveRecipe recipe, int segments = 20)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            segments = Math.Max(8, segments);
            switch (recipe.Shape)
            {
                case DungeonConstructiveShape.Cylinder: return BuildLathe(recipe,
                    new[] { (-0.5f, 0.5f), (0.5f, 0.5f) }, segments);
                case DungeonConstructiveShape.RoundedRing: return BuildRing(recipe, segments);
                case DungeonConstructiveShape.LathedProfile: return BuildProfile(recipe, segments);
                case DungeonConstructiveShape.Basin: return BuildBasin(recipe, segments);
                default: return BuildRoundedBox(recipe, segments);
            }
        }

        private static GeometryMeshData BuildProfile(DungeonPrimitiveRecipe r, int segments)
        {
            var profile = new (float y, float radius)[r.Profile.Count];
            for (int i = 0; i < profile.Length; i++)
                profile[i] = (r.Profile[i].Height / (float)Math.Max(1, r.Size.Y),
                    r.Profile[i].Radius / 430f * 0.5f);
            return BuildLathe(r, profile, segments);
        }

        private static GeometryMeshData BuildLathe(DungeonPrimitiveRecipe r,
            (float y, float radius)[] profile, int segments)
        {
            var v = new List<GeometryVector3>(); var n = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>(); var ix = new List<int>();
            for (int p = 0; p < profile.Length; p++)
                for (int s = 0; s <= segments; s++)
                {
                    double a = Math.PI * 2 * s / segments;
                    float x = (float)Math.Cos(a) * profile[p].radius * r.Size.X;
                    float z = (float)Math.Sin(a) * profile[p].radius * r.Size.Z;
                    v.Add(P(r, x, profile[p].y * r.Size.Y, z));
                    n.Add(N(r, (float)Math.Cos(a), 0.25f, (float)Math.Sin(a)));
                    uv.Add(new GeometryVector2(s / (float)segments,
                        p / (float)Math.Max(1, profile.Length - 1)));
                }
            GridIndices(ix, profile.Length, segments + 1);
            return new GeometryMeshData(v.ToArray(), n.ToArray(), uv.ToArray(), ix.ToArray());
        }

        private static GeometryMeshData BuildRing(DungeonPrimitiveRecipe r, int segments)
        {
            var v = new List<GeometryVector3>(); var n = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>(); var ix = new List<int>();
            int tubeSegments = 8;
            for (int ring = 0; ring <= tubeSegments; ring++)
                for (int s = 0; s <= segments; s++)
                {
                    double a = Math.PI * 2 * s / segments;
                    double b = Math.PI * 2 * ring / tubeSegments;
                    float tube = 0.075f;
                    float radial = 0.39f + (float)Math.Cos(b) * tube;
                    float x = (float)Math.Cos(a) * radial * r.Size.X;
                    float z = (float)Math.Sin(a) * radial * r.Size.Z;
                    float y = (float)Math.Sin(b) * r.Size.Y * 0.5f;
                    v.Add(P(r, x, y, z));
                    n.Add(N(r, (float)(Math.Cos(a) * Math.Cos(b)),
                        (float)Math.Sin(b), (float)(Math.Sin(a) * Math.Cos(b))));
                    uv.Add(new GeometryVector2(s / (float)segments, ring / (float)tubeSegments));
                }
            GridIndices(ix, tubeSegments + 1, segments + 1);
            return new GeometryMeshData(v.ToArray(), n.ToArray(), uv.ToArray(), ix.ToArray());
        }

        private static GeometryMeshData BuildBasin(DungeonPrimitiveRecipe r, int segments)
        {
            // An open, smoothly curved elliptical bowl with a rolled rim.
            var profile = new[] { (-0.50f, 0.18f), (-0.32f, 0.34f),
                (-0.08f, 0.47f), (0.12f, 0.50f), (0.22f, 0.46f) };
            return BuildLathe(r, profile, segments);
        }

        private static GeometryMeshData BuildRoundedBox(DungeonPrimitiveRecipe r, int segments)
        {
            // Superellipsoid evaluation produces a genuinely rounded box without
            // tying the recipe to any engine's primitive implementation.
            int rings = Math.Max(6, segments / 2);
            var v = new List<GeometryVector3>(); var n = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>(); var ix = new List<int>();
            for (int y = 0; y <= rings; y++)
                for (int s = 0; s <= segments; s++)
                {
                    double lat = -Math.PI / 2 + Math.PI * y / rings;
                    double lon = Math.PI * 2 * s / segments;
                    float cx = SignedPow((float)(Math.Cos(lat) * Math.Cos(lon)), 0.35f);
                    float cy = SignedPow((float)Math.Sin(lat), 0.35f);
                    float cz = SignedPow((float)(Math.Cos(lat) * Math.Sin(lon)), 0.35f);
                    v.Add(P(r, cx * r.Size.X * 0.5f, cy * r.Size.Y * 0.5f,
                        cz * r.Size.Z * 0.5f));
                    n.Add(N(r, cx, cy, cz));
                    uv.Add(new GeometryVector2(s / (float)segments, y / (float)rings));
                }
            GridIndices(ix, rings + 1, segments + 1);
            return new GeometryMeshData(v.ToArray(), n.ToArray(), uv.ToArray(), ix.ToArray());
        }

        private static float SignedPow(float value, float power) =>
            Math.Sign(value) * (float)Math.Pow(Math.Abs(value), power);

        private static void GridIndices(List<int> indices, int rows, int columns)
        {
            for (int y = 0; y < rows - 1; y++) for (int x = 0; x < columns - 1; x++)
            { int a = y * columns + x, b = a + columns; indices.Add(a); indices.Add(b); indices.Add(a + 1); indices.Add(a + 1); indices.Add(b); indices.Add(b + 1); }
        }

        private static GeometryVector3 P(DungeonPrimitiveRecipe r, float x, float y, float z)
        { Rotate(r.YawDegrees, ref x, ref z); return new GeometryVector3(r.Position.X + x, r.Position.Y + y, r.Position.Z + z); }
        private static GeometryVector3 N(DungeonPrimitiveRecipe r, float x, float y, float z)
        { Rotate(r.YawDegrees, ref x, ref z); float l = (float)Math.Sqrt(x*x+y*y+z*z); return new GeometryVector3(x/l,y/l,z/l); }
        private static void Rotate(int degrees, ref float x, ref float z)
        { double a = degrees * Math.PI / 180; float ox=x; x=(float)(x*Math.Cos(a)-z*Math.Sin(a)); z=(float)(ox*Math.Sin(a)+z*Math.Cos(a)); }
    }
}
