using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    /// <summary>Conservative, deterministic spatial reduction for reviewed asset LODs.</summary>
    public static class MeshLodBuilder
    {
        public static GeometryMeshData Build(GeometryMeshData source, int maximumTriangles)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maximumTriangles < 4) throw new ArgumentOutOfRangeException(nameof(maximumTriangles));
            if (source.Indices.Length % 3 != 0 || source.Vertices.Length == 0)
                throw new ArgumentException("Source must contain indexed triangles.", nameof(source));
            if (source.Indices.Length / 3 <= maximumTriangles) return source;
            int sourceBoundaryEdges = BoundaryEdges(source);

            GeometryMeshData best = null;
            int bestCount = -1;
            // A coarse-to-fine sweep is deterministic and rejects branching edges.
            for (int resolution = 4; resolution <= 96; resolution++)
            {
                GeometryMeshData candidate = Cluster(source, resolution);
                int count = candidate.Indices.Length / 3;
                if (count > maximumTriangles || count <= bestCount ||
                    !ValidTopology(candidate, sourceBoundaryEdges)) continue;
                best = candidate;
                bestCount = count;
                if (count == maximumTriangles) break;
            }
            if (best == null)
                throw new InvalidOperationException("No topology-safe LOD satisfies the requested triangle budget.");
            return best;
        }

        private static GeometryMeshData Cluster(GeometryMeshData source, int resolution)
        {
            GeometryVector3 min = source.Vertices[0], max = min;
            foreach (GeometryVector3 p in source.Vertices)
            {
                min = new GeometryVector3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new GeometryVector3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
            float span = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
            if (span < 1e-8f) throw new ArgumentException("Mesh has no spatial extent.");
            float cell = span / resolution;
            var clusters = new Dictionary<CellKey, int>();
            var sums = new List<GeometryVector3>();
            var uvSums = new List<GeometryVector2>();
            var counts = new List<int>();
            var remap = new int[source.Vertices.Length];
            for (int i = 0; i < source.Vertices.Length; i++)
            {
                GeometryVector3 p = source.Vertices[i];
                var key = new CellKey((int)Math.Floor((p.X - min.X) / cell),
                    (int)Math.Floor((p.Y - min.Y) / cell), (int)Math.Floor((p.Z - min.Z) / cell));
                if (!clusters.TryGetValue(key, out int index))
                {
                    index = sums.Count; clusters.Add(key, index);
                    sums.Add(new GeometryVector3()); uvSums.Add(new GeometryVector2()); counts.Add(0);
                }
                remap[i] = index;
                GeometryVector3 s = sums[index];
                sums[index] = new GeometryVector3(s.X + p.X, s.Y + p.Y, s.Z + p.Z);
                GeometryVector2 uv = i < source.UV.Length ? source.UV[i] : new GeometryVector2();
                GeometryVector2 u = uvSums[index];
                uvSums[index] = new GeometryVector2(u.X + uv.X, u.Y + uv.Y);
                counts[index]++;
            }
            var positions = new GeometryVector3[sums.Count];
            var texcoords = new GeometryVector2[sums.Count];
            for (int i = 0; i < sums.Count; i++)
            {
                float scale = 1f / counts[i];
                positions[i] = new GeometryVector3(sums[i].X * scale, sums[i].Y * scale, sums[i].Z * scale);
                texcoords[i] = new GeometryVector2(uvSums[i].X * scale, uvSums[i].Y * scale);
            }
            var triangles = new List<int>(source.Indices.Length);
            var faces = new HashSet<FaceKey>();
            for (int i = 0; i < source.Indices.Length; i += 3)
            {
                int a = source.Indices[i], b = source.Indices[i + 1], c = source.Indices[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= remap.Length || b >= remap.Length || c >= remap.Length)
                    throw new ArgumentException("Source index is outside the vertex buffer.");
                a = remap[a]; b = remap[b]; c = remap[c];
                if (a == b || b == c || a == c || !faces.Add(new FaceKey(a, b, c))) continue;
                GeometryVector3 ab = Sub(positions[b], positions[a]), ac = Sub(positions[c], positions[a]);
                if (Length(Cross(ab, ac)) < 1e-9f) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }
            var normals = new GeometryVector3[positions.Length];
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                GeometryVector3 n = Cross(Sub(positions[b], positions[a]), Sub(positions[c], positions[a]));
                normals[a] = Add(normals[a], n); normals[b] = Add(normals[b], n); normals[c] = Add(normals[c], n);
            }
            for (int i = 0; i < normals.Length; i++)
            {
                float length = Length(normals[i]);
                if (length > 1e-8f) normals[i] = Scale(normals[i], 1f / length);
            }
            return new GeometryMeshData(positions, normals, texcoords, triangles.ToArray());
        }

        private static bool ValidTopology(GeometryMeshData mesh, int maximumBoundaryEdges)
        {
            if (mesh.Indices.Length == 0) return false;
            Dictionary<EdgeKey, int> edges = CountEdges(mesh);
            int boundary = 0;
            foreach (int incidences in edges.Values)
            { if (incidences > 2) return false; if (incidences == 1) boundary++; }
            return boundary <= maximumBoundaryEdges;
        }

        private static int BoundaryEdges(GeometryMeshData mesh)
        {
            int count = 0;
            foreach (int incidences in CountEdges(mesh).Values) if (incidences == 1) count++;
            return count;
        }

        private static Dictionary<EdgeKey, int> CountEdges(GeometryMeshData mesh)
        {
            var edges = new Dictionary<EdgeKey, int>();
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                Count(mesh.Indices[i], mesh.Indices[i + 1]);
                Count(mesh.Indices[i + 1], mesh.Indices[i + 2]);
                Count(mesh.Indices[i + 2], mesh.Indices[i]);
            }
            return edges;
            void Count(int a, int b)
            { var key = new EdgeKey(a, b); edges.TryGetValue(key, out int value); edges[key] = value + 1; }
        }

        private static GeometryVector3 Add(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        private static GeometryVector3 Sub(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static GeometryVector3 Scale(GeometryVector3 a, float s) =>
            new GeometryVector3(a.X * s, a.Y * s, a.Z * s);
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static float Length(GeometryVector3 a) => (float)Math.Sqrt(a.X * a.X + a.Y * a.Y + a.Z * a.Z);

        private readonly struct CellKey : IEquatable<CellKey>
        {
            private readonly int _x, _y, _z;
            public CellKey(int x, int y, int z) { _x = x; _y = y; _z = z; }
            public bool Equals(CellKey other) => _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is CellKey other && Equals(other);
            public override int GetHashCode() => (_x * 397 ^ _y) * 397 ^ _z;
        }
        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            private readonly int _a, _b;
            public EdgeKey(int a, int b) { _a = Math.Min(a, b); _b = Math.Max(a, b); }
            public bool Equals(EdgeKey other) => _a == other._a && _b == other._b;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => _a * 397 ^ _b;
        }
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
