using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    public enum AssetPrimitiveShape
    {
        Box,
        RoundedBox,
        Cylinder,
        Ellipsoid,
        TaperedCylinder,
        Torus,
        ExtrudedPolygon,
        LathedProfile
    }

    public enum AssetPrimitiveAxis { X, Y, Z }

    public readonly struct AssetProfilePoint
    {
        public AssetProfilePoint(float height, float radius)
        { Height = height; Radius = radius; }
        public float Height { get; }
        public float Radius { get; }
    }

    public readonly struct AssetPolygonPoint
    {
        public AssetPolygonPoint(float x, float y) { X = x; Y = y; }
        public float X { get; }
        public float Y { get; }
    }

    public sealed class AssetPrimitiveRecipe
    {
        public AssetPrimitiveShape Shape { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float Depth { get; set; }
        public int Segments { get; set; } = 20;
        public int Sides { get; set; } = 6;
        public float TopScale { get; set; } = 1f;
        public float Roundness { get; set; } = 0.35f;
        public float RotationDegrees { get; set; }
        public AssetPrimitiveAxis Axis { get; set; } = AssetPrimitiveAxis.Y;
        public IReadOnlyList<AssetProfilePoint> Profile { get; set; } = Array.Empty<AssetProfilePoint>();
        public IReadOnlyList<AssetPolygonPoint> PolygonOutline { get; set; } = Array.Empty<AssetPolygonPoint>();
    }

    /// <summary>Portable primitive meshes used by offline asset authoring and Unity preview.</summary>
    public static class AssetPrimitiveMeshBuilder
    {
        public static GeometryMeshData Build(AssetPrimitiveRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Width <= 0 || recipe.Height <= 0 || recipe.Depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Primitive dimensions must be positive.");
            int segments = Math.Max(6, Math.Min(128, recipe.Segments));
            switch (recipe.Shape)
            {
                case AssetPrimitiveShape.Box: return BuildBox(recipe);
                case AssetPrimitiveShape.RoundedBox: return BuildSuperellipsoid(recipe, segments, recipe.Roundness);
                case AssetPrimitiveShape.Ellipsoid: return BuildSuperellipsoid(recipe, segments, 1f);
                case AssetPrimitiveShape.Cylinder: return BuildCylinder(recipe, segments, 1f);
                case AssetPrimitiveShape.TaperedCylinder: return BuildCylinder(recipe, segments, recipe.TopScale);
                case AssetPrimitiveShape.Torus: return BuildTorus(recipe, segments);
                case AssetPrimitiveShape.ExtrudedPolygon: return recipe.PolygonOutline.Count >= 3
                    ? PolygonExtrusionBuilder.Build(recipe) : BuildPrism(recipe);
                case AssetPrimitiveShape.LathedProfile: return BuildLathe(recipe, segments);
                default: throw new NotSupportedException("Unsupported asset primitive shape.");
            }
        }

        private static GeometryMeshData BuildBox(AssetPrimitiveRecipe r)
        {
            var m = new MeshAccumulator();
            m.AddBox(r.Width, r.Height, r.Depth);
            return m.ToMeshData();
        }

        private static GeometryMeshData BuildSuperellipsoid(AssetPrimitiveRecipe r, int segments,
            float roundness)
        {
            int rings = Math.Max(4, segments / 2);
            float exponent = Math.Max(0.15f, Math.Min(1f, roundness));
            var m = new MeshAccumulator();
            for (int ring = 0; ring <= rings; ring++)
            {
                double latitude = -Math.PI * 0.5 + Math.PI * ring / rings;
                for (int segment = 0; segment <= segments; segment++)
                {
                    double longitude = Math.PI * 2 * segment / segments;
                    float x = SignedPow((float)(Math.Cos(latitude) * Math.Cos(longitude)), exponent);
                    float y = SignedPow((float)Math.Sin(latitude), exponent);
                    float z = SignedPow((float)(Math.Cos(latitude) * Math.Sin(longitude)), exponent);
                    m.AddVertex(new GeometryVector3(x * r.Width * 0.5f, y * r.Height * 0.5f,
                        z * r.Depth * 0.5f), Normalize(x / r.Width, y / r.Height, z / r.Depth),
                        new GeometryVector2(segment / (float)segments, ring / (float)rings));
                }
            }
            m.AddGridIndices(rings + 1, segments + 1);
            return m.ToMeshData();
        }

        private static GeometryMeshData BuildCylinder(AssetPrimitiveRecipe r, int segments, float topScale)
        {
            topScale = Math.Max(0.02f, Math.Min(2f, topScale));
            var m = new MeshAccumulator();
            for (int row = 0; row < 2; row++)
            {
                float along = row == 0 ? -0.5f : 0.5f;
                float scale = row == 0 ? 1f : topScale;
                for (int s = 0; s <= segments; s++)
                {
                    double angle = Math.PI * 2 * s / segments + r.RotationDegrees * Math.PI / 180.0;
                    float u = (float)Math.Cos(angle), v = (float)Math.Sin(angle);
                    GeometryVector3 point = AxisPoint(r, along, u * scale, v * scale);
                    GeometryVector3 normal = AxisNormal(r.Axis, u, v);
                    m.AddVertex(point, normal, new GeometryVector2(s / (float)segments, row));
                }
            }
            m.AddGridIndices(2, segments + 1);
            m.AddCap(r, segments, -0.5f, 1f, false);
            m.AddCap(r, segments, 0.5f, topScale, true);
            return m.ToMeshData();
        }

        private static GeometryMeshData BuildPrism(AssetPrimitiveRecipe r)
        {
            int sides = Math.Max(3, Math.Min(32, r.Sides));
            return BuildCylinder(r, sides, r.TopScale);
        }

        private static GeometryMeshData BuildTorus(AssetPrimitiveRecipe r, int segments)
        {
            int tubeSegments = Math.Max(6, segments / 2);
            float ratio = Math.Max(.02f, Math.Min(.95f, r.Roundness));
            var m = new MeshAccumulator();
            for (int ring = 0; ring <= tubeSegments; ring++)
                for (int segment = 0; segment <= segments; segment++)
                {
                    double around = Math.PI * 2 * segment / segments;
                    double tube = Math.PI * 2 * ring / tubeSegments;
                    float radial = .5f / (1 + ratio) * (1 + ratio * (float)Math.Cos(tube));
                    float x = (float)Math.Cos(around) * radial * r.Width;
                    float y = (float)Math.Sin(around) * radial * r.Height;
                    float z = (float)Math.Sin(tube) * r.Depth * .5f * ratio / (1 + ratio);
                    m.AddVertex(new GeometryVector3(x, y, z), Normalize(
                        (float)(Math.Cos(around) * Math.Cos(tube)),
                        (float)(Math.Sin(around) * Math.Cos(tube)), (float)Math.Sin(tube)),
                        new GeometryVector2(segment / (float)segments, ring / (float)tubeSegments));
                }
            m.AddGridIndices(tubeSegments + 1, segments + 1);
            return m.ToMeshData();
        }

        private static GeometryMeshData BuildLathe(AssetPrimitiveRecipe r, int segments)
        {
            if (r.Profile == null || r.Profile.Count < 2)
                throw new ArgumentException("Lathed profile requires at least two points.", nameof(r));
            var m = new MeshAccumulator();
            for (int p = 0; p < r.Profile.Count; p++)
            {
                AssetProfilePoint profile = r.Profile[p];
                for (int s = 0; s <= segments; s++)
                {
                    double angle = Math.PI * 2 * s / segments + r.RotationDegrees * Math.PI / 180.0;
                    float u = (float)Math.Cos(angle), v = (float)Math.Sin(angle);
                    m.AddVertex(AxisPoint(r, profile.Height, u * profile.Radius, v * profile.Radius),
                        AxisNormal(r.Axis, u, v),
                        new GeometryVector2(s / (float)segments, p / (float)(r.Profile.Count - 1)));
                }
            }
            m.AddGridIndices(r.Profile.Count, segments + 1);
            return m.ToMeshData();
        }

        private static GeometryVector3 AxisPoint(AssetPrimitiveRecipe r, float along, float u, float v)
        {
            switch (r.Axis)
            {
                case AssetPrimitiveAxis.X: return new GeometryVector3(along * r.Width, u * r.Height * 0.5f, v * r.Depth * 0.5f);
                case AssetPrimitiveAxis.Z: return new GeometryVector3(u * r.Width * 0.5f, v * r.Height * 0.5f, along * r.Depth);
                default: return new GeometryVector3(u * r.Width * 0.5f, along * r.Height, v * r.Depth * 0.5f);
            }
        }

        private static GeometryVector3 AxisNormal(AssetPrimitiveAxis axis, float u, float v)
        {
            switch (axis)
            {
                case AssetPrimitiveAxis.X: return Normalize(0, u, v);
                case AssetPrimitiveAxis.Z: return Normalize(u, v, 0);
                default: return Normalize(u, 0, v);
            }
        }

        private static float SignedPow(float value, float power) =>
            Math.Sign(value) * (float)Math.Pow(Math.Abs(value), power);

        private static GeometryVector3 Normalize(float x, float y, float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            return length < 0.000001f ? new GeometryVector3(0, 1, 0) :
                new GeometryVector3(x / length, y / length, z / length);
        }

        private sealed class MeshAccumulator
        {
            private readonly List<GeometryVector3> _vertices = new List<GeometryVector3>();
            private readonly List<GeometryVector3> _normals = new List<GeometryVector3>();
            private readonly List<GeometryVector2> _uv = new List<GeometryVector2>();
            private readonly List<int> _indices = new List<int>();

            public void AddVertex(GeometryVector3 vertex, GeometryVector3 normal, GeometryVector2 uv)
            { _vertices.Add(vertex); _normals.Add(normal); _uv.Add(uv); }

            public void AddGridIndices(int rows, int columns)
            {
                for (int row = 0; row < rows - 1; row++)
                    for (int column = 0; column < columns - 1; column++)
                    {
                        int a = row * columns + column, b = a + columns;
                        _indices.Add(a); _indices.Add(b); _indices.Add(a + 1);
                        _indices.Add(a + 1); _indices.Add(b); _indices.Add(b + 1);
                    }
            }

            public void AddCap(AssetPrimitiveRecipe r, int segments, float along, float scale, bool positive)
            {
                int center = _vertices.Count;
                GeometryVector3 normal;
                switch (r.Axis)
                {
                    case AssetPrimitiveAxis.X: normal = new GeometryVector3(positive ? 1 : -1, 0, 0); break;
                    case AssetPrimitiveAxis.Z: normal = new GeometryVector3(0, 0, positive ? 1 : -1); break;
                    default: normal = new GeometryVector3(0, positive ? 1 : -1, 0); break;
                }
                AddVertex(AxisPoint(r, along, 0, 0), normal, new GeometryVector2(0.5f, 0.5f));
                for (int s = 0; s <= segments; s++)
                {
                    double angle = Math.PI * 2 * s / segments;
                    float u = (float)Math.Cos(angle) * scale, v = (float)Math.Sin(angle) * scale;
                    AddVertex(AxisPoint(r, along, u, v), normal,
                        new GeometryVector2(u * 0.5f + 0.5f, v * 0.5f + 0.5f));
                }
                for (int s = 0; s < segments; s++)
                {
                    if (positive) { _indices.Add(center); _indices.Add(center + s + 1); _indices.Add(center + s + 2); }
                    else { _indices.Add(center); _indices.Add(center + s + 2); _indices.Add(center + s + 1); }
                }
            }

            public void AddBox(float width, float height, float depth)
            {
                float x = width * .5f, y = height * .5f, z = depth * .5f;
                AddQuad(V(-x,-y,-z),V(-x,-y,z),V(-x,y,z),V(-x,y,-z),V(-1,0,0));
                AddQuad(V(x,-y,z),V(x,-y,-z),V(x,y,-z),V(x,y,z),V(1,0,0));
                AddQuad(V(-x,-y,z),V(-x,-y,-z),V(x,-y,-z),V(x,-y,z),V(0,-1,0));
                AddQuad(V(-x,y,-z),V(-x,y,z),V(x,y,z),V(x,y,-z),V(0,1,0));
                AddQuad(V(x,-y,-z),V(-x,-y,-z),V(-x,y,-z),V(x,y,-z),V(0,0,-1));
                AddQuad(V(-x,-y,z),V(x,-y,z),V(x,y,z),V(-x,y,z),V(0,0,1));
            }

            private void AddQuad(GeometryVector3 a, GeometryVector3 b, GeometryVector3 c,
                GeometryVector3 d, GeometryVector3 normal)
            {
                int start = _vertices.Count;
                AddVertex(a, normal, new GeometryVector2(0,0)); AddVertex(b, normal, new GeometryVector2(1,0));
                AddVertex(c, normal, new GeometryVector2(1,1)); AddVertex(d, normal, new GeometryVector2(0,1));
                _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
                _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
            }

            public GeometryMeshData ToMeshData() => new GeometryMeshData(_vertices.ToArray(),
                _normals.ToArray(), _uv.ToArray(), _indices.ToArray());
            private static GeometryVector3 V(float x, float y, float z) => new GeometryVector3(x,y,z);
        }
    }
}
