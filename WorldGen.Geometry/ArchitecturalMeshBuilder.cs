using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    public sealed class ArchGeometryRecipe
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public float Depth { get; set; }
        public float OpeningWidth { get; set; }
        public float OpeningHeight { get; set; }
        public int CurveSegments { get; set; } = 20;
    }

    public sealed class ColumnGeometryRecipe
    {
        public float Width { get; set; }
        public float Height { get; set; }
        public float Depth { get; set; }
        public float BaseHeight { get; set; }
        public float CapitalHeight { get; set; }
        public float ShaftTaper { get; set; }
        public int RadialSegments { get; set; } = 16;
    }

    /// <summary>
    /// Engine-neutral architectural mesh evaluation. Units are supplied by the caller;
    /// WorldGen authoring uses metres after converting its integer millimetre contract.
    /// </summary>
    public static class ArchitecturalMeshBuilder
    {
        public static GeometryMeshData BuildArch(ArchGeometryRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            ValidateArch(recipe);

            var mesh = new MeshAccumulator();
            float halfWidth = recipe.Width * 0.5f;
            float halfHeight = recipe.Height * 0.5f;
            float halfDepth = recipe.Depth * 0.5f;
            float halfOpening = recipe.OpeningWidth * 0.5f;
            float openingBottom = -halfHeight;
            float radius = halfOpening;
            float springY = openingBottom + recipe.OpeningHeight - radius;

            float pierWidth = halfWidth - halfOpening;
            mesh.AddBox(-halfOpening - pierWidth * 0.5f, 0f, 0f,
                pierWidth, recipe.Height, recipe.Depth);
            mesh.AddBox(halfOpening + pierWidth * 0.5f, 0f, 0f,
                pierWidth, recipe.Height, recipe.Depth);

            int segments = Math.Max(6, recipe.CurveSegments);
            for (int i = 0; i < segments; i++)
            {
                float x0 = -halfOpening + recipe.OpeningWidth * i / segments;
                float x1 = -halfOpening + recipe.OpeningWidth * (i + 1) / segments;
                float y0 = springY + SafeSqrt(radius * radius - x0 * x0);
                float y1 = springY + SafeSqrt(radius * radius - x1 * x1);

                // Front and rear faces between the curved opening and flat module top.
                mesh.AddQuad(
                    new GeometryVector3(x0, y0, -halfDepth),
                    new GeometryVector3(x1, y1, -halfDepth),
                    new GeometryVector3(x1, halfHeight, -halfDepth),
                    new GeometryVector3(x0, halfHeight, -halfDepth),
                    new GeometryVector3(0f, 0f, -1f));
                mesh.AddQuad(
                    new GeometryVector3(x1, y1, halfDepth),
                    new GeometryVector3(x0, y0, halfDepth),
                    new GeometryVector3(x0, halfHeight, halfDepth),
                    new GeometryVector3(x1, halfHeight, halfDepth),
                    new GeometryVector3(0f, 0f, 1f));

                // Curved doorway reveal. Its normals point into the opening.
                float midX = (x0 + x1) * 0.5f;
                float midY = springY + SafeSqrt(radius * radius - midX * midX);
                float nx = -midX / radius;
                float ny = -(midY - springY) / radius;
                mesh.AddQuad(
                    new GeometryVector3(x0, y0, halfDepth),
                    new GeometryVector3(x1, y1, halfDepth),
                    new GeometryVector3(x1, y1, -halfDepth),
                    new GeometryVector3(x0, y0, -halfDepth),
                    new GeometryVector3(nx, ny, 0f));
            }

            // Horizontal cap and the two vertical end faces of the center arch mass.
            mesh.AddQuad(
                new GeometryVector3(-halfOpening, halfHeight, halfDepth),
                new GeometryVector3(-halfOpening, halfHeight, -halfDepth),
                new GeometryVector3(halfOpening, halfHeight, -halfDepth),
                new GeometryVector3(halfOpening, halfHeight, halfDepth),
                new GeometryVector3(0f, 1f, 0f));
            mesh.AddQuad(
                new GeometryVector3(-halfOpening, springY, -halfDepth),
                new GeometryVector3(-halfOpening, springY, halfDepth),
                new GeometryVector3(-halfOpening, halfHeight, halfDepth),
                new GeometryVector3(-halfOpening, halfHeight, -halfDepth),
                new GeometryVector3(-1f, 0f, 0f));
            mesh.AddQuad(
                new GeometryVector3(halfOpening, springY, halfDepth),
                new GeometryVector3(halfOpening, springY, -halfDepth),
                new GeometryVector3(halfOpening, halfHeight, -halfDepth),
                new GeometryVector3(halfOpening, halfHeight, halfDepth),
                new GeometryVector3(1f, 0f, 0f));

            return mesh.ToMeshData();
        }

        public static GeometryMeshData BuildColumn(ColumnGeometryRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            ValidateColumn(recipe);

            float halfHeight = recipe.Height * 0.5f;
            float baseHeight = recipe.BaseHeight;
            float capitalHeight = recipe.CapitalHeight;
            float shaftBottom = -halfHeight + baseHeight;
            float shaftTop = halfHeight - capitalHeight;
            float outerRadiusX = recipe.Width * 0.5f;
            float outerRadiusZ = recipe.Depth * 0.5f;
            float shaftScaleBottom = 0.72f;
            float shaftScaleTop = shaftScaleBottom * (1f - recipe.ShaftTaper);

            var profile = new[]
            {
                new ColumnProfile(-halfHeight, 0f, 0f),
                new ColumnProfile(-halfHeight, outerRadiusX, outerRadiusZ),
                new ColumnProfile(shaftBottom, outerRadiusX, outerRadiusZ),
                new ColumnProfile(shaftBottom, outerRadiusX * shaftScaleBottom,
                    outerRadiusZ * shaftScaleBottom),
                new ColumnProfile(shaftTop, outerRadiusX * shaftScaleTop,
                    outerRadiusZ * shaftScaleTop),
                new ColumnProfile(shaftTop, outerRadiusX, outerRadiusZ),
                new ColumnProfile(halfHeight, outerRadiusX, outerRadiusZ),
                new ColumnProfile(halfHeight, 0f, 0f)
            };

            int segments = Math.Max(6, recipe.RadialSegments);
            var vertices = new List<GeometryVector3>();
            var normals = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>();
            var indices = new List<int>();

            for (int p = 0; p < profile.Length; p++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    double angle = Math.PI * 2.0 * s / segments;
                    float cos = (float)Math.Cos(angle);
                    float sin = (float)Math.Sin(angle);
                    ColumnProfile point = profile[p];
                    vertices.Add(new GeometryVector3(cos * point.RadiusX, point.Y,
                        sin * point.RadiusZ));
                    float normalY = ProfileNormalY(profile, p);
                    normals.Add(Normalize(cos / Math.Max(point.RadiusX, 0.001f), normalY,
                        sin / Math.Max(point.RadiusZ, 0.001f)));
                    uv.Add(new GeometryVector2(s / (float)segments,
                        (point.Y + halfHeight) / recipe.Height));
                }
            }

            for (int p = 0; p < profile.Length - 1; p++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = p * (segments + 1) + s;
                    int b = a + segments + 1;
                    indices.Add(a); indices.Add(b); indices.Add(a + 1);
                    indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
                }
            }

            return new GeometryMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(),
                indices.ToArray());
        }

        private static void ValidateArch(ArchGeometryRecipe recipe)
        {
            if (recipe.Width <= 0f || recipe.Height <= 0f || recipe.Depth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Arch dimensions must be positive.");
            if (recipe.OpeningWidth <= 0f || recipe.OpeningWidth >= recipe.Width)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Opening width must be inside the arch width.");
            if (recipe.OpeningHeight <= recipe.OpeningWidth * 0.5f ||
                recipe.OpeningHeight >= recipe.Height)
                throw new ArgumentOutOfRangeException(nameof(recipe),
                    "Opening height must contain the round arch and remain below the module top.");
        }

        private static void ValidateColumn(ColumnGeometryRecipe recipe)
        {
            if (recipe.Width <= 0f || recipe.Height <= 0f || recipe.Depth <= 0f)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Column dimensions must be positive.");
            if (recipe.BaseHeight <= 0f || recipe.CapitalHeight <= 0f ||
                recipe.BaseHeight + recipe.CapitalHeight >= recipe.Height)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Column base and capital must leave a shaft.");
            if (recipe.ShaftTaper < 0f || recipe.ShaftTaper >= 0.8f)
                throw new ArgumentOutOfRangeException(nameof(recipe), "Column shaft taper must be in [0, 0.8).");
        }

        private static float SafeSqrt(float value) => (float)Math.Sqrt(Math.Max(0f, value));

        private static float ProfileNormalY(ColumnProfile[] profile, int index)
        {
            int previous = Math.Max(0, index - 1);
            int next = Math.Min(profile.Length - 1, index + 1);
            float dy = profile[next].Y - profile[previous].Y;
            if (Math.Abs(dy) < 0.0001f) return 0f;
            float dr = profile[next].RadiusX - profile[previous].RadiusX;
            return -dr / dy;
        }

        private static GeometryVector3 Normalize(float x, float y, float z)
        {
            float length = (float)Math.Sqrt(x * x + y * y + z * z);
            if (length < 0.000001f) return new GeometryVector3(0f, 1f, 0f);
            return new GeometryVector3(x / length, y / length, z / length);
        }

        private readonly struct ColumnProfile
        {
            public ColumnProfile(float y, float radiusX, float radiusZ)
            { Y = y; RadiusX = radiusX; RadiusZ = radiusZ; }
            public float Y { get; }
            public float RadiusX { get; }
            public float RadiusZ { get; }
        }

        private sealed class MeshAccumulator
        {
            private readonly List<GeometryVector3> _vertices = new List<GeometryVector3>();
            private readonly List<GeometryVector3> _normals = new List<GeometryVector3>();
            private readonly List<GeometryVector2> _uv = new List<GeometryVector2>();
            private readonly List<int> _indices = new List<int>();

            public void AddBox(float centerX, float centerY, float centerZ,
                float width, float height, float depth)
            {
                float x0 = centerX - width * 0.5f, x1 = centerX + width * 0.5f;
                float y0 = centerY - height * 0.5f, y1 = centerY + height * 0.5f;
                float z0 = centerZ - depth * 0.5f, z1 = centerZ + depth * 0.5f;
                AddQuad(V(x0,y0,z0),V(x0,y0,z1),V(x0,y1,z1),V(x0,y1,z0),V(-1,0,0));
                AddQuad(V(x1,y0,z1),V(x1,y0,z0),V(x1,y1,z0),V(x1,y1,z1),V(1,0,0));
                AddQuad(V(x0,y0,z1),V(x0,y0,z0),V(x1,y0,z0),V(x1,y0,z1),V(0,-1,0));
                AddQuad(V(x0,y1,z0),V(x0,y1,z1),V(x1,y1,z1),V(x1,y1,z0),V(0,1,0));
                AddQuad(V(x1,y0,z0),V(x0,y0,z0),V(x0,y1,z0),V(x1,y1,z0),V(0,0,-1));
                AddQuad(V(x0,y0,z1),V(x1,y0,z1),V(x1,y1,z1),V(x0,y1,z1),V(0,0,1));
            }

            public void AddQuad(GeometryVector3 a, GeometryVector3 b, GeometryVector3 c,
                GeometryVector3 d, GeometryVector3 normal)
            {
                int start = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c); _vertices.Add(d);
                _normals.Add(normal); _normals.Add(normal); _normals.Add(normal); _normals.Add(normal);
                _uv.Add(new GeometryVector2(0f, 0f)); _uv.Add(new GeometryVector2(1f, 0f));
                _uv.Add(new GeometryVector2(1f, 1f)); _uv.Add(new GeometryVector2(0f, 1f));
                _indices.Add(start); _indices.Add(start + 1); _indices.Add(start + 2);
                _indices.Add(start); _indices.Add(start + 2); _indices.Add(start + 3);
            }

            public GeometryMeshData ToMeshData() => new GeometryMeshData(_vertices.ToArray(),
                _normals.ToArray(), _uv.ToArray(), _indices.ToArray());

            private static GeometryVector3 V(float x, float y, float z) =>
                new GeometryVector3(x, y, z);
        }
    }
}
