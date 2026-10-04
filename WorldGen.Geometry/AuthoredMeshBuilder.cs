using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    /// <summary>Bounded, engine-neutral explicit mesh for offline authored components.</summary>
    public sealed class AuthoredMeshRecipe
    {
        public float[] Positions { get; set; } = Array.Empty<float>();
        public float[] Normals { get; set; } = Array.Empty<float>();
        public float[] UV { get; set; } = Array.Empty<float>();
        public int[] Indices { get; set; } = Array.Empty<int>();
        public int MaximumTriangles { get; set; } = 100000;
    }

    public static class AuthoredMeshBuilder
    {
        public static GeometryMeshData Build(AuthoredMeshRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Positions == null || recipe.Indices == null || recipe.UV == null || recipe.Normals == null ||
                recipe.Positions.Length % 3 != 0 || recipe.Positions.Length < 9 ||
                recipe.Positions.Length > 1200000 || recipe.Indices.Length % 3 != 0 ||
                recipe.Indices.Length < 3 || recipe.MaximumTriangles < 1 ||
                recipe.MaximumTriangles > 400000 || recipe.Indices.Length / 3 > recipe.MaximumTriangles)
                throw new ArgumentException("Explicit mesh has invalid buffers or exceeds its triangle budget.");
            int vertexCount = recipe.Positions.Length / 3;
            if (recipe.UV.Length != 0 && recipe.UV.Length != vertexCount * 2)
                throw new ArgumentException("Explicit mesh UV count does not match vertices.");
            if (recipe.Normals.Length != 0 && recipe.Normals.Length != vertexCount * 3)
                throw new ArgumentException("Explicit mesh normal count does not match vertices.");
            var vertices = new GeometryVector3[vertexCount];
            var uv = new GeometryVector2[vertexCount];
            var normals = new GeometryVector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
            {
                float x = recipe.Positions[i * 3], y = recipe.Positions[i * 3 + 1], z = recipe.Positions[i * 3 + 2];
                if (!Finite(x) || !Finite(y) || !Finite(z))
                    throw new ArgumentException("Explicit mesh contains a nonfinite vertex.");
                vertices[i] = new GeometryVector3(x, y, z);
                if (recipe.UV.Length != 0)
                {
                    float u = recipe.UV[i * 2], v = recipe.UV[i * 2 + 1];
                    if (!Finite(u) || !Finite(v)) throw new ArgumentException("Explicit mesh contains a nonfinite UV.");
                    uv[i] = new GeometryVector2(u, v);
                }
                if (recipe.Normals.Length != 0)
                {
                    float nx = recipe.Normals[i * 3], ny = recipe.Normals[i * 3 + 1],
                        nz = recipe.Normals[i * 3 + 2];
                    if (!Finite(nx) || !Finite(ny) || !Finite(nz) ||
                        nx * nx + ny * ny + nz * nz < .25f)
                        throw new ArgumentException("Explicit mesh contains an invalid normal.");
                    normals[i] = new GeometryVector3(nx, ny, nz);
                }
            }
            var faces = new HashSet<FaceKey>();
            for (int i = 0; i < recipe.Indices.Length; i += 3)
            {
                int a = recipe.Indices[i], b = recipe.Indices[i + 1], c = recipe.Indices[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount ||
                    a == b || b == c || a == c || !faces.Add(new FaceKey(a, b, c)))
                    throw new ArgumentException("Explicit mesh contains an invalid or duplicate face.");
                GeometryVector3 ab = Sub(vertices[b], vertices[a]), ac = Sub(vertices[c], vertices[a]);
                GeometryVector3 n = Cross(ab, ac);
                if (Length(n) < 1e-10f) throw new ArgumentException("Explicit mesh contains a degenerate face.");
                if (recipe.Normals.Length == 0)
                { normals[a] = Add(normals[a], n); normals[b] = Add(normals[b], n); normals[c] = Add(normals[c], n); }
            }
            for (int i = 0; i < normals.Length; i++)
            {
                float length = Length(normals[i]);
                normals[i] = length > 1e-8f ? Scale(normals[i], 1f / length) : new GeometryVector3(0, 1, 0);
            }
            return new GeometryMeshData(vertices, normals, uv, (int[])recipe.Indices.Clone());
        }

        public static AuthoredMeshRecipe FromMesh(GeometryMeshData mesh, int maximumTriangles)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            var cleaned = new List<int>(mesh.Indices.Length);
            var faces = new HashSet<FaceKey>();
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                int a = mesh.Indices[i], b = mesh.Indices[i + 1], c = mesh.Indices[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= mesh.Vertices.Length ||
                    b >= mesh.Vertices.Length || c >= mesh.Vertices.Length)
                    throw new ArgumentException("Source mesh contains an invalid index.");
                if (a == b || b == c || a == c || !faces.Add(new FaceKey(a, b, c))) continue;
                GeometryVector3 ab = Sub(mesh.Vertices[b], mesh.Vertices[a]);
                GeometryVector3 ac = Sub(mesh.Vertices[c], mesh.Vertices[a]);
                if (Length(Cross(ab, ac)) < 1e-10f) continue;
                cleaned.Add(a); cleaned.Add(b); cleaned.Add(c);
            }
            if (cleaned.Count == 0) throw new ArgumentException("Source mesh has no valid faces.");
            bool hasNormals = mesh.Normals.Length == mesh.Vertices.Length;
            if (hasNormals)
                foreach (GeometryVector3 normal in mesh.Normals)
                    if (!Finite(normal.X) || !Finite(normal.Y) || !Finite(normal.Z) ||
                        normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z < .25f)
                    { hasNormals = false; break; }
            var recipe = new AuthoredMeshRecipe
            { Positions = new float[mesh.Vertices.Length * 3],
                Normals = hasNormals ? new float[mesh.Vertices.Length * 3] : Array.Empty<float>(),
                UV = new float[mesh.Vertices.Length * 2],
                Indices = cleaned.ToArray(), MaximumTriangles = maximumTriangles };
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                recipe.Positions[i * 3] = mesh.Vertices[i].X;
                recipe.Positions[i * 3 + 1] = mesh.Vertices[i].Y;
                recipe.Positions[i * 3 + 2] = mesh.Vertices[i].Z;
                if (recipe.Normals.Length != 0)
                {
                    recipe.Normals[i * 3] = mesh.Normals[i].X;
                    recipe.Normals[i * 3 + 1] = mesh.Normals[i].Y;
                    recipe.Normals[i * 3 + 2] = mesh.Normals[i].Z;
                }
                if (i < mesh.UV.Length)
                { recipe.UV[i * 2] = mesh.UV[i].X; recipe.UV[i * 2 + 1] = mesh.UV[i].Y; }
            }
            Build(recipe);
            return recipe;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static GeometryVector3 Add(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static GeometryVector3 Sub(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static GeometryVector3 Scale(GeometryVector3 a, float s) =>
            new GeometryVector3(a.X * s, a.Y * s, a.Z * s);
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static float Length(GeometryVector3 a) => (float)Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);
        private readonly struct FaceKey : IEquatable<FaceKey>
        {
            private readonly int _a, _b, _c;
            public FaceKey(int a, int b, int c)
            {
                if (a <= b && a <= c) { _a = a; _b = b; _c = c; }
                else if (b <= a && b <= c) { _a = b; _b = c; _c = a; }
                else { _a = c; _b = a; _c = b; }
            }
            public bool Equals(FaceKey other) => _a == other._a && _b == other._b && _c == other._c;
            public override bool Equals(object obj) => obj is FaceKey other && Equals(other);
            public override int GetHashCode() => (_a * 397 ^ _b) * 397 ^ _c;
        }
    }
}
