using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    /// <summary>Closed extrusion of a simple ordered outline in normalized local coordinates.</summary>
    public static class PolygonExtrusionBuilder
    {
        public static GeometryMeshData Build(AssetPrimitiveRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            IReadOnlyList<AssetPolygonPoint> points = recipe.PolygonOutline;
            if (points == null || points.Count < 3 || points.Count > 64)
                throw new ArgumentException("Polygon outline needs 3 to 64 points.");
            for (int i = 0; i < points.Count; i++)
            {
                AssetPolygonPoint p = points[i];
                if (float.IsNaN(p.X) || float.IsInfinity(p.X) || float.IsNaN(p.Y) ||
                    float.IsInfinity(p.Y) || Math.Abs(p.X) > 1 || Math.Abs(p.Y) > 1)
                    throw new ArgumentException("Polygon points must be finite and within [-1,1].");
            }
            var order = new List<int>();
            for (int i = 0; i < points.Count; i++) order.Add(i);
            float area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                AssetPolygonPoint a = points[i], b = points[(i + 1) % points.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            if (Math.Abs(area) < 1e-5f) throw new ArgumentException("Polygon outline has zero area.");
            for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
            {
                if (j == i + 1 || i == 0 && j == points.Count - 1) continue;
                if (SegmentsIntersect(points[i], points[(i + 1) % points.Count],
                    points[j], points[(j + 1) % points.Count]))
                    throw new ArgumentException("Polygon outline intersects itself.");
            }
            if (area < 0) order.Reverse();
            var triangles = Triangulate(points, order);
            var vertices = new List<GeometryVector3>();
            var normals = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>();
            var indices = new List<int>();
            GeometryVector3 axis = recipe.Axis == AssetPrimitiveAxis.X ? new GeometryVector3(1, 0, 0) :
                recipe.Axis == AssetPrimitiveAxis.Y ? new GeometryVector3(0, 1, 0) : new GeometryVector3(0, 0, 1);
            for (int t = 0; t < triangles.Count; t += 3)
            {
                int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                AddCap(a, b, c, true);
                AddCap(a, c, b, false);
            }
            for (int i = 0; i < order.Count; i++)
            {
                int a = order[i], b = order[(i + 1) % order.Count];
                GeometryVector3 lowA = Position(points[a], -.5f), lowB = Position(points[b], -.5f),
                    highA = Position(points[a], .5f), highB = Position(points[b], .5f);
                GeometryVector3 normal = Cross(Sub(lowB, lowA), Sub(highB, lowA));
                if (recipe.Axis == AssetPrimitiveAxis.Y) normal = Scale(normal, -1);
                normal = Unit(normal);
                int start = vertices.Count;
                Add(lowA, normal, 0, 0); Add(lowB, normal, 1, 0);
                Add(highB, normal, 1, 1); Add(highA, normal, 0, 1);
                bool forward = recipe.Axis != AssetPrimitiveAxis.Y;
                Triangle(start, start + (forward ? 1 : 2), start + (forward ? 2 : 1));
                Triangle(start, start + (forward ? 2 : 3), start + (forward ? 3 : 2));
            }
            return new GeometryMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), indices.ToArray());

            GeometryVector3 Position(AssetPolygonPoint p, float along)
            {
                switch (recipe.Axis)
                {
                    case AssetPrimitiveAxis.X: return new GeometryVector3(along * recipe.Width,
                        p.X * recipe.Height * .5f, p.Y * recipe.Depth * .5f);
                    case AssetPrimitiveAxis.Y: return new GeometryVector3(p.X * recipe.Width * .5f,
                        along * recipe.Height, p.Y * recipe.Depth * .5f);
                    default: return new GeometryVector3(p.X * recipe.Width * .5f,
                        p.Y * recipe.Height * .5f, along * recipe.Depth);
                }
            }
            void Add(GeometryVector3 point, GeometryVector3 normal, float u, float v)
            { vertices.Add(point); normals.Add(normal); uv.Add(new GeometryVector2(u, v)); }
            void Triangle(int a, int b, int c) { indices.Add(a); indices.Add(b); indices.Add(c); }
            void AddCap(int a, int b, int c, bool positive)
            {
                GeometryVector3 pa = Position(points[a], positive ? .5f : -.5f),
                    pb = Position(points[b], positive ? .5f : -.5f),
                    pc = Position(points[c], positive ? .5f : -.5f);
                GeometryVector3 desired = positive ? axis : Scale(axis, -1);
                if (Dot(Cross(Sub(pb, pa), Sub(pc, pa)), desired) < 0)
                { int swap = b; b = c; c = swap; pb = Position(points[b], positive ? .5f : -.5f);
                  pc = Position(points[c], positive ? .5f : -.5f); }
                int start = vertices.Count;
                Add(pa, desired, points[a].X * .5f + .5f, points[a].Y * .5f + .5f);
                Add(pb, desired, points[b].X * .5f + .5f, points[b].Y * .5f + .5f);
                Add(pc, desired, points[c].X * .5f + .5f, points[c].Y * .5f + .5f);
                Triangle(start, start + 1, start + 2);
            }
        }

        private static List<int> Triangulate(IReadOnlyList<AssetPolygonPoint> points, List<int> ccw)
        {
            var remaining = new List<int>(ccw);
            var output = new List<int>();
            while (remaining.Count > 3)
            {
                bool cut = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count],
                        b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                    if (Cross2(points[a], points[b], points[c]) <= 1e-6f) continue;
                    bool contains = false;
                    foreach (int other in remaining)
                        if (other != a && other != b && other != c && Inside(points[other],
                            points[a], points[b], points[c])) { contains = true; break; }
                    if (contains) continue;
                    output.Add(a); output.Add(b); output.Add(c);
                    remaining.RemoveAt(i); cut = true; break;
                }
                if (!cut) throw new ArgumentException("Polygon outline is not a valid simple polygon.");
            }
            output.Add(remaining[0]); output.Add(remaining[1]); output.Add(remaining[2]);
            return output;
        }
        private static bool Inside(AssetPolygonPoint p, AssetPolygonPoint a,
            AssetPolygonPoint b, AssetPolygonPoint c) =>
            Cross2(a, b, p) >= 0 && Cross2(b, c, p) >= 0 && Cross2(c, a, p) >= 0;
        private static float Cross2(AssetPolygonPoint a, AssetPolygonPoint b, AssetPolygonPoint c) =>
            (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        private static bool SegmentsIntersect(AssetPolygonPoint a, AssetPolygonPoint b,
            AssetPolygonPoint c, AssetPolygonPoint d)
        {
            float abC = Cross2(a, b, c), abD = Cross2(a, b, d),
                cdA = Cross2(c, d, a), cdB = Cross2(c, d, b);
            if (abC * abD < 0 && cdA * cdB < 0) return true;
            return OnSegment(a, b, c, abC) || OnSegment(a, b, d, abD) ||
                OnSegment(c, d, a, cdA) || OnSegment(c, d, b, cdB);
        }
        private static bool OnSegment(AssetPolygonPoint a, AssetPolygonPoint b,
            AssetPolygonPoint p, float area) => Math.Abs(area) < 1e-6f &&
            p.X >= Math.Min(a.X, b.X) - 1e-6f && p.X <= Math.Max(a.X, b.X) + 1e-6f &&
            p.Y >= Math.Min(a.Y, b.Y) - 1e-6f && p.Y <= Math.Max(a.Y, b.Y) + 1e-6f;
        private static GeometryVector3 Sub(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static GeometryVector3 Scale(GeometryVector3 a, float s) =>
            new GeometryVector3(a.X * s, a.Y * s, a.Z * s);
        private static float Dot(GeometryVector3 a, GeometryVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) =>
            new GeometryVector3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static GeometryVector3 Unit(GeometryVector3 a)
        {
            float length = (float)Math.Sqrt(Dot(a, a));
            if (length < 1e-8f) throw new ArgumentException("Degenerate polygon edge.");
            return Scale(a, 1 / length);
        }
    }
}
