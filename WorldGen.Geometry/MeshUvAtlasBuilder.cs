using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    /// <summary>Projects mesh faces into six padded charts in a deterministic 3x2 UV atlas.</summary>
    public static class MeshUvAtlasBuilder
    {
        public static GeometryMeshData Build(GeometryMeshData source,
            GeometryVector3 boundsMin, GeometryVector3 boundsMax)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Indices.Length % 3 != 0 || source.Vertices.Length == 0 ||
                boundsMax.X <= boundsMin.X || boundsMax.Y <= boundsMin.Y || boundsMax.Z <= boundsMin.Z)
                throw new ArgumentException("A nonempty triangle mesh and ordered bounds are required.");
            var positions = new List<GeometryVector3>();
            var normals = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>();
            var indices = new int[source.Indices.Length];
            var remap = new Dictionary<(int Vertex, int Chart), int>();
            Dictionary<PositionKey, GeometryVector3> smooth = SmoothNormals(source);
            for (int face = 0; face < source.Indices.Length; face += 3)
            {
                int a = source.Indices[face], b = source.Indices[face + 1], c = source.Indices[face + 2];
                if (a < 0 || b < 0 || c < 0 || a >= source.Vertices.Length ||
                    b >= source.Vertices.Length || c >= source.Vertices.Length)
                    throw new ArgumentException("Mesh index is outside the vertex buffer.");
                GeometryVector3 ab = Sub(source.Vertices[b], source.Vertices[a]);
                GeometryVector3 ac = Sub(source.Vertices[c], source.Vertices[a]);
                GeometryVector3 normal = Cross(ab, ac);
                int chart = Chart(normal);
                for (int corner = 0; corner < 3; corner++)
                {
                    int old = source.Indices[face + corner];
                    var key = (old, chart);
                    if (!remap.TryGetValue(key, out int index))
                    {
                        index = positions.Count;
                        remap.Add(key, index);
                        GeometryVector3 p = source.Vertices[old];
                        positions.Add(p);
                        GeometryVector3 averaged = smooth[new PositionKey(p)];
                        normals.Add(Length(averaged) > .25f ? averaged :
                            old < source.Normals.Length ? source.Normals[old] : normal);
                        uv.Add(Project(p, chart, boundsMin, boundsMax));
                    }
                    indices[face + corner] = index;
                }
            }
            return new GeometryMeshData(positions.ToArray(), normals.ToArray(), uv.ToArray(), indices);
        }

        private static int Chart(GeometryVector3 n)
        {
            float x = Math.Abs(n.X), y = Math.Abs(n.Y), z = Math.Abs(n.Z);
            if (x >= y && x >= z) return n.X >= 0 ? 0 : 1;
            if (y >= z) return n.Y >= 0 ? 2 : 3;
            return n.Z >= 0 ? 4 : 5;
        }

        private static Dictionary<PositionKey, GeometryVector3> SmoothNormals(GeometryMeshData mesh)
        {
            var sums = new Dictionary<PositionKey, GeometryVector3>();
            for (int face = 0; face < mesh.Indices.Length; face += 3)
            {
                GeometryVector3 a = mesh.Vertices[mesh.Indices[face]],
                    b = mesh.Vertices[mesh.Indices[face + 1]],
                    c = mesh.Vertices[mesh.Indices[face + 2]];
                GeometryVector3 normal = Cross(Sub(b, a), Sub(c, a));
                Add(a); Add(b); Add(c);
                void Add(GeometryVector3 point)
                {
                    var key = new PositionKey(point);
                    sums.TryGetValue(key, out GeometryVector3 previous);
                    sums[key] = new GeometryVector3(previous.X + normal.X,
                        previous.Y + normal.Y, previous.Z + normal.Z);
                }
            }
            var normalized = new Dictionary<PositionKey, GeometryVector3>(sums.Count);
            foreach (var pair in sums)
            {
                float length = Length(pair.Value);
                normalized.Add(pair.Key, length < 1e-8f ? new GeometryVector3() :
                    new GeometryVector3(pair.Value.X / length, pair.Value.Y / length, pair.Value.Z / length));
            }
            return normalized;
        }

        private static GeometryVector2 Project(GeometryVector3 p, int chart,
            GeometryVector3 min, GeometryVector3 max)
        {
            float x = Clamp((p.X - min.X) / (max.X - min.X));
            float y = Clamp((p.Y - min.Y) / (max.Y - min.Y));
            float z = Clamp((p.Z - min.Z) / (max.Z - min.Z));
            float u, v;
            switch (chart)
            {
                case 0: u = 1 - z; v = y; break;
                case 1: u = z; v = y; break;
                case 2: u = x; v = z; break;
                case 3: u = x; v = 1 - z; break;
                case 4: u = x; v = y; break;
                default: u = 1 - x; v = y; break;
            }
            const float pad = .012f;
            return new GeometryVector2(((chart % 3) + pad + u * (1 - 2 * pad)) / 3f,
                ((chart / 3) + pad + v * (1 - 2 * pad)) / 2f);
        }

        private static float Clamp(float v) => Math.Max(0, Math.Min(1, v));
        private static GeometryVector3 Sub(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z,
                a.X * b.Y - a.Y * b.X);
        private static float Length(GeometryVector3 v) =>
            (float)Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
        private readonly struct PositionKey : IEquatable<PositionKey>
        {
            private readonly long _x, _y, _z;
            public PositionKey(GeometryVector3 p)
            { _x = (long)Math.Round(p.X * 100000); _y = (long)Math.Round(p.Y * 100000); _z = (long)Math.Round(p.Z * 100000); }
            public bool Equals(PositionKey other) => _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is PositionKey other && Equals(other);
            public override int GetHashCode() => (_x.GetHashCode() * 397 ^ _y.GetHashCode()) * 397 ^ _z.GetHashCode();
        }
    }
}
