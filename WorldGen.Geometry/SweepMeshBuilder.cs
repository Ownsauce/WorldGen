using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    public sealed class SweepStation
    {
        public float[] Position { get; set; } = new float[] { 0, 0, 0 };
        public float RadiusX { get; set; } = .1f;
        public float RadiusZ { get; set; } = .1f;
    }

    public sealed class SweepGeometryRecipe
    {
        public List<SweepStation> Stations { get; set; } = new List<SweepStation>();
        public int RadialSegments { get; set; } = 12;
        public bool CapStart { get; set; } = true;
        public bool CapEnd { get; set; } = true;
    }

    /// <summary>Builds a closed elliptical section along a 3D station path.</summary>
    public static class SweepMeshBuilder
    {
        public static GeometryMeshData Build(SweepGeometryRecipe recipe)
        {
            Validate(recipe);
            int radial = Math.Max(6, Math.Min(64, recipe.RadialSegments));
            var vertices = new List<GeometryVector3>();
            var normals = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>();
            var indices = new List<int>();
            for (int station = 0; station < recipe.Stations.Count; station++)
            {
                SweepStation value = recipe.Stations[station];
                GeometryVector3 center = V(value.Position);
                GeometryVector3 tangent = Tangent(recipe.Stations, station);
                GeometryVector3 reference = Math.Abs(tangent.Y) > .9f
                    ? new GeometryVector3(1, 0, 0) : new GeometryVector3(0, 1, 0);
                GeometryVector3 right = Normalize(Cross(reference, tangent));
                GeometryVector3 up = Normalize(Cross(tangent, right));
                for (int side = 0; side <= radial; side++)
                {
                    double angle = Math.PI * 2 * side / radial;
                    float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
                    GeometryVector3 offset = Add(Scale(right, c * value.RadiusX),
                        Scale(up, s * value.RadiusZ));
                    GeometryVector3 normal = Normalize(Add(Scale(right, c / Math.Max(value.RadiusX, 1e-5f)),
                        Scale(up, s / Math.Max(value.RadiusZ, 1e-5f))));
                    vertices.Add(Add(center, offset)); normals.Add(normal);
                    uv.Add(new GeometryVector2(side / (float)radial,
                        station / (float)(recipe.Stations.Count - 1)));
                }
            }
            int columns = radial + 1;
            for (int row = 0; row < recipe.Stations.Count - 1; row++)
                for (int side = 0; side < radial; side++)
                {
                    int a = row * columns + side, b = a + columns;
                    indices.Add(a); indices.Add(b); indices.Add(a + 1);
                    indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
                }
            if (recipe.CapStart) AddCap(recipe.Stations[0], Tangent(recipe.Stations, 0), false,
                radial, vertices, normals, uv, indices);
            if (recipe.CapEnd) AddCap(recipe.Stations[recipe.Stations.Count - 1],
                Tangent(recipe.Stations, recipe.Stations.Count - 1), true,
                radial, vertices, normals, uv, indices);
            return new GeometryMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), indices.ToArray());
        }

        private static void AddCap(SweepStation station, GeometryVector3 tangent, bool end, int radial,
            List<GeometryVector3> vertices, List<GeometryVector3> normals, List<GeometryVector2> uv,
            List<int> indices)
        {
            GeometryVector3 reference = Math.Abs(tangent.Y) > .9f
                ? new GeometryVector3(1, 0, 0) : new GeometryVector3(0, 1, 0);
            GeometryVector3 right = Normalize(Cross(reference, tangent));
            GeometryVector3 up = Normalize(Cross(tangent, right));
            GeometryVector3 normal = end ? tangent : Scale(tangent, -1);
            int center = vertices.Count; vertices.Add(V(station.Position)); normals.Add(normal);
            uv.Add(new GeometryVector2(.5f, .5f));
            for (int side = 0; side <= radial; side++)
            {
                double angle = Math.PI * 2 * side / radial;
                float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
                vertices.Add(Add(V(station.Position), Add(Scale(right, c * station.RadiusX),
                    Scale(up, s * station.RadiusZ)))); normals.Add(normal);
                uv.Add(new GeometryVector2(c * .5f + .5f, s * .5f + .5f));
            }
            for (int side = 0; side < radial; side++)
            {
                if (end) { indices.Add(center); indices.Add(center + side + 1); indices.Add(center + side + 2); }
                else { indices.Add(center); indices.Add(center + side + 2); indices.Add(center + side + 1); }
            }
        }

        private static GeometryVector3 Tangent(IReadOnlyList<SweepStation> stations, int index)
        {
            GeometryVector3 before = V(stations[Math.Max(0, index - 1)].Position);
            GeometryVector3 after = V(stations[Math.Min(stations.Count - 1, index + 1)].Position);
            return Normalize(Subtract(after, before));
        }

        private static void Validate(SweepGeometryRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Stations == null || recipe.Stations.Count < 2 || recipe.Stations.Count > 128)
                throw new ArgumentException("Sweep requires 2 to 128 stations.");
            if (recipe.RadialSegments < 6 || recipe.RadialSegments > 64)
                throw new ArgumentOutOfRangeException(nameof(recipe.RadialSegments));
            foreach (SweepStation station in recipe.Stations)
            {
                if (station.Position == null || station.Position.Length != 3)
                    throw new ArgumentException("Sweep station positions require three values.");
                if (station.RadiusX < 0 || station.RadiusZ < 0)
                    throw new ArgumentException("Sweep station radii cannot be negative.");
            }
            for (int index = 1; index < recipe.Stations.Count; index++)
                if (Length(Subtract(V(recipe.Stations[index].Position), V(recipe.Stations[index - 1].Position))) < 1e-6f)
                    throw new ArgumentException("Adjacent sweep stations must be distinct.");
        }

        private static GeometryVector3 V(float[] v) => new GeometryVector3(v[0], v[1], v[2]);
        private static GeometryVector3 Add(GeometryVector3 a, GeometryVector3 b) => new GeometryVector3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        private static GeometryVector3 Subtract(GeometryVector3 a, GeometryVector3 b) => new GeometryVector3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        private static GeometryVector3 Scale(GeometryVector3 a, float s) => new GeometryVector3(a.X*s,a.Y*s,a.Z*s);
        private static GeometryVector3 Cross(GeometryVector3 a, GeometryVector3 b) => new GeometryVector3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        private static float Length(GeometryVector3 a) => (float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z);
        private static GeometryVector3 Normalize(GeometryVector3 a) { float length=Length(a); return length<1e-7f?new GeometryVector3(0,1,0):Scale(a,1/length); }
    }
}
