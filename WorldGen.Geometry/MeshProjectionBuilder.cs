using System;

namespace WorldGen.Geometry
{
    /// <summary>Camera parameters recorded with a reference image in authoring space.</summary>
    public sealed class MeshProjectionCamera
    {
        public GeometryVector3 Position { get; set; }
        public GeometryVector3 Target { get; set; }
        public GeometryVector3 Up { get; set; } = new GeometryVector3(0, 1, 0);
        public bool Perspective { get; set; } = true;
        public float VerticalFieldOfViewDegrees { get; set; } = 45;
        public float OrthographicHeight { get; set; } = 2;
        public float NearPlane { get; set; } = .01f;
        public float PrincipalPointX { get; set; } = .5f;
        public float PrincipalPointY { get; set; } = .5f;
    }

    public sealed class MeshProjectionResult
    {
        public MeshProjectionResult(bool[] mask, float[] depth, GeometryVector3[] normals,
            int width, int height)
        { Mask = mask; Depth = depth; Normals = normals; Width = width; Height = height; }
        public bool[] Mask { get; }
        public float[] Depth { get; }
        public GeometryVector3[] Normals { get; }
        public int Width { get; }
        public int Height { get; }
    }

    /// <summary>Deterministic CPU silhouette and depth capture for fixed authoring cameras.</summary>
    public static class MeshProjectionBuilder
    {
        public static MeshProjectionResult Render(GeometryMeshData mesh,
            MeshProjectionCamera camera, int width, int height)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (width < 2 || height < 2 || width > 4096 || height > 4096)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (camera.NearPlane <= 0 || camera.VerticalFieldOfViewDegrees <= 0 ||
                camera.VerticalFieldOfViewDegrees >= 170 || camera.OrthographicHeight <= 0 ||
                camera.PrincipalPointX < 0 || camera.PrincipalPointX > 1 ||
                camera.PrincipalPointY < 0 || camera.PrincipalPointY > 1)
                throw new ArgumentException("Camera projection parameters are invalid.", nameof(camera));
            if (mesh.Indices.Length % 3 != 0) throw new ArgumentException("Mesh indices must form triangles.");

            GeometryVector3 forward = Normalize(Sub(camera.Target, camera.Position));
            // Unity camera screen-right is up x forward. Keep CPU evidence in that frame.
            GeometryVector3 right = Normalize(Cross(camera.Up, forward));
            GeometryVector3 up = Normalize(Cross(forward, right));
            if (Length(right) < .5f || Length(up) < .5f || Length(forward) < .5f)
                throw new ArgumentException("Camera position, target, and up must define a valid basis.");

            var points = new Projected[mesh.Vertices.Length];
            float aspect = width / (float)height;
            float focal = 1f / (float)Math.Tan(camera.VerticalFieldOfViewDegrees * Math.PI / 360);
            for (int i = 0; i < points.Length; i++)
            {
                GeometryVector3 delta = Sub(mesh.Vertices[i], camera.Position);
                float z = Dot(delta, forward);
                float scale = camera.Perspective ? focal / Math.Max(z, camera.NearPlane) :
                    2f / camera.OrthographicHeight;
                points[i] = new Projected(
                    (camera.PrincipalPointX + Dot(delta, right) * scale / (2 * aspect)) * width,
                    (camera.PrincipalPointY - Dot(delta, up) * scale / 2) * height, z);
            }

            var mask = new bool[width * height];
            var depth = new float[mask.Length];
            var normals = new GeometryVector3[mask.Length];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.PositiveInfinity;
            for (int i = 0; i < mesh.Indices.Length; i += 3)
            {
                int ia = mesh.Indices[i], ib = mesh.Indices[i + 1], ic = mesh.Indices[i + 2];
                if (ia < 0 || ib < 0 || ic < 0 || ia >= points.Length || ib >= points.Length || ic >= points.Length)
                    throw new ArgumentException("Mesh index is outside the vertex buffer.");
                Projected a = points[ia], b = points[ib], c = points[ic];
                GeometryVector3 faceNormal = Normalize(Cross(
                    Sub(mesh.Vertices[ib], mesh.Vertices[ia]),
                    Sub(mesh.Vertices[ic], mesh.Vertices[ia])));
                // A clipped triangle needs proper near-plane clipping. Reject it here so a
                // vertex behind the camera cannot create a false full-frame silhouette.
                if (a.Z < camera.NearPlane || b.Z < camera.NearPlane || c.Z < camera.NearPlane) continue;
                float area = Edge(a, b, c.X, c.Y);
                if (Math.Abs(area) < 1e-8f) continue;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X))));
                int maxX = Math.Min(width - 1, (int)Math.Ceiling(Math.Max(a.X, Math.Max(b.X, c.X))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y))));
                int maxY = Math.Min(height - 1, (int)Math.Ceiling(Math.Max(a.Y, Math.Max(b.Y, c.Y))));
                for (int y = minY; y <= maxY; y++) for (int x = minX; x <= maxX; x++)
                {
                    float px = x + .5f, py = y + .5f;
                    float wa = Edge(b, c, px, py) / area;
                    float wb = Edge(c, a, px, py) / area;
                    float wc = 1 - wa - wb;
                    if (wa < -1e-6f || wb < -1e-6f || wc < -1e-6f) continue;
                    float z = camera.Perspective ? 1f / (wa / a.Z + wb / b.Z + wc / c.Z) :
                        wa * a.Z + wb * b.Z + wc * c.Z;
                    int pixel = y * width + x;
                    if (z >= depth[pixel]) continue;
                    mask[pixel] = true;
                    depth[pixel] = z;
                    normals[pixel] = mesh.Normals.Length == mesh.Vertices.Length
                        ? Normalize(new GeometryVector3(
                            mesh.Normals[ia].X * wa + mesh.Normals[ib].X * wb + mesh.Normals[ic].X * wc,
                            mesh.Normals[ia].Y * wa + mesh.Normals[ib].Y * wb + mesh.Normals[ic].Y * wc,
                            mesh.Normals[ia].Z * wa + mesh.Normals[ib].Z * wb + mesh.Normals[ic].Z * wc))
                        : faceNormal;
                }
            }
            return new MeshProjectionResult(mask, depth, normals, width, height);
        }

        private readonly struct Projected
        {
            public Projected(float x, float y, float z) { X = x; Y = y; Z = z; }
            public float X { get; } public float Y { get; } public float Z { get; }
        }
        private static float Edge(Projected a, Projected b, float x, float y) =>
            (b.X - a.X) * (y - a.Y) - (b.Y - a.Y) * (x - a.X);
        private static GeometryVector3 Sub(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static float Dot(GeometryVector3 a, GeometryVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static float Length(GeometryVector3 a) => (float)Math.Sqrt(Dot(a, a));
        private static GeometryVector3 Normalize(GeometryVector3 a)
        { float length = Length(a); return length < 1e-8f ? new GeometryVector3() :
            new GeometryVector3(a.X / length, a.Y / length, a.Z / length); }
    }
}
