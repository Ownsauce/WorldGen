using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGen.Geometry
{
    public sealed class SdfPrimitive
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public float[] Position { get; set; } = new float[] { 0, 0, 0 };
        public float[] Size { get; set; } = new float[] { 1, 1, 1 };
        public float Radius { get; set; } = 0.5f;
        public float Height { get; set; } = 1f;
        public float[] RotationDegrees { get; set; } = new float[] { 0, 0, 0 };
        public float[] Scale { get; set; } = new float[] { 1, 1, 1 };
    }

    public sealed class SdfOperation
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Left { get; set; }
        public string Right { get; set; }
        public float Radius { get; set; } = 0.1f;
    }

    public sealed class SdfGeometryRecipe
    {
        public List<SdfPrimitive> Primitives { get; set; } = new List<SdfPrimitive>();
        public List<SdfOperation> Operations { get; set; } = new List<SdfOperation>();
        public int Resolution { get; set; } = 24;
        public float[] BoundsMin { get; set; } = new float[] { -0.5f, -0.5f, -0.5f };
        public float[] BoundsMax { get; set; } = new float[] { 0.5f, 0.5f, 0.5f };
        public string RootId { get; set; }
    }

    /// <summary>Samples a bounded constructive signed-distance field into a welded-looking surface.</summary>
    public static class SdfMeshBuilder
    {
        private static readonly int[,] Tetrahedra =
        {
            { 0, 5, 1, 6 }, { 0, 1, 2, 6 }, { 0, 2, 3, 6 },
            { 0, 3, 7, 6 }, { 0, 7, 4, 6 }, { 0, 4, 5, 6 }
        };
        private static readonly int[,] Corners =
        {
            {0,0,0},{1,0,0},{1,1,0},{0,1,0},{0,0,1},{1,0,1},{1,1,1},{0,1,1}
        };

        public static GeometryMeshData Build(SdfGeometryRecipe recipe)
        {
            Validate(recipe);
            int n = recipe.Resolution;
            var values = new float[(n + 1) * (n + 1) * (n + 1)];
            float dx = (recipe.BoundsMax[0] - recipe.BoundsMin[0]) / n;
            float dy = (recipe.BoundsMax[1] - recipe.BoundsMin[1]) / n;
            float dz = (recipe.BoundsMax[2] - recipe.BoundsMin[2]) / n;
            for (int z = 0; z <= n; z++) for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++)
                values[Index(x,y,z,n)] = Evaluate(recipe,
                    new GeometryVector3(recipe.BoundsMin[0] + x * dx,
                        recipe.BoundsMin[1] + y * dy, recipe.BoundsMin[2] + z * dz));

            var vertices = new List<GeometryVector3>();
            var normals = new List<GeometryVector3>();
            var uv = new List<GeometryVector2>();
            var indices = new List<int>();
            var points = new GeometryVector3[8]; var samples = new float[8];
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                bool negative = false, positive = false;
                for (int corner = 0; corner < 8; corner++)
                {
                    int cx = x + Corners[corner,0], cy = y + Corners[corner,1], cz = z + Corners[corner,2];
                    points[corner] = new GeometryVector3(recipe.BoundsMin[0] + cx * dx,
                        recipe.BoundsMin[1] + cy * dy, recipe.BoundsMin[2] + cz * dz);
                    samples[corner] = values[Index(cx,cy,cz,n)];
                    negative |= samples[corner] <= 0; positive |= samples[corner] > 0;
                }
                if (!negative || !positive) continue;
                for (int tetra = 0; tetra < 6; tetra++) PolygonizeTetra(recipe, points, samples,
                    tetra, vertices, normals, uv, indices,
                    Math.Max(1e-5f, Math.Min(dx, Math.Min(dy, dz)) * 0.01f));
            }
            return new GeometryMeshData(vertices.ToArray(), normals.ToArray(), uv.ToArray(), indices.ToArray());
        }

        private static void PolygonizeTetra(SdfGeometryRecipe recipe, GeometryVector3[] cube,
            float[] cubeValues, int tetra, List<GeometryVector3> vertices,
            List<GeometryVector3> normals, List<GeometryVector2> uv, List<int> indices, float epsilon)
        {
            int[] ids = { Tetrahedra[tetra,0], Tetrahedra[tetra,1], Tetrahedra[tetra,2], Tetrahedra[tetra,3] };
            var inside = new List<int>(3);
            var outside = new List<int>(3);
            for (int i = 0; i < ids.Length; i++)
            {
                if (cubeValues[ids[i]] <= 0) inside.Add(ids[i]);
                else outside.Add(ids[i]);
            }

            if (inside.Count == 0 || outside.Count == 0) return;
            if (inside.Count == 1 || outside.Count == 1)
            {
                List<int> single = inside.Count == 1 ? inside : outside;
                List<int> opposite = inside.Count == 1 ? outside : inside;
                AddTriangle(recipe,
                    EdgeCrossing(cube, cubeValues, single[0], opposite[0]),
                    EdgeCrossing(cube, cubeValues, single[0], opposite[1]),
                    EdgeCrossing(cube, cubeValues, single[0], opposite[2]),
                    vertices, normals, uv, indices, epsilon);
                return;
            }

            // Two inside and two outside vertices form a quad. Its cyclic order is
            // i0-o0, i0-o1, i1-o1, i1-o0; discovery-order crossings can form a
            // bow-tie and leave holes or overlapping triangles.
            GeometryVector3 p00 = EdgeCrossing(cube, cubeValues, inside[0], outside[0]);
            GeometryVector3 p01 = EdgeCrossing(cube, cubeValues, inside[0], outside[1]);
            GeometryVector3 p11 = EdgeCrossing(cube, cubeValues, inside[1], outside[1]);
            GeometryVector3 p10 = EdgeCrossing(cube, cubeValues, inside[1], outside[0]);
            AddTriangle(recipe, p00, p01, p11, vertices, normals, uv, indices, epsilon);
            AddTriangle(recipe, p00, p11, p10, vertices, normals, uv, indices, epsilon);
        }

        private static GeometryVector3 EdgeCrossing(GeometryVector3[] points, float[] values, int a, int b)
        {
            float denominator = values[a] - values[b];
            float t = Math.Abs(denominator) < 1e-12f ? 0.5f : values[a] / denominator;
            return Lerp(points[a], points[b], Math.Max(0f, Math.Min(1f, t)));
        }

        private static void AddTriangle(SdfGeometryRecipe recipe, GeometryVector3 a, GeometryVector3 b,
            GeometryVector3 c, List<GeometryVector3> vertices, List<GeometryVector3> normals,
            List<GeometryVector2> uv, List<int> indices, float epsilon)
        {
            GeometryVector3 center = new GeometryVector3((a.X+b.X+c.X)/3, (a.Y+b.Y+c.Y)/3, (a.Z+b.Z+c.Z)/3);
            GeometryVector3 normal = Gradient(recipe, center, epsilon);
            GeometryVector3 cross = Cross(Subtract(b,a), Subtract(c,a));
            if (Dot(cross, cross) < 1e-16f) return;
            if (Dot(cross, normal) < 0) { GeometryVector3 swap = b; b = c; c = swap; }
            int start = vertices.Count;
            foreach (GeometryVector3 point in new[] { a,b,c })
            {
                vertices.Add(point); normals.Add(Gradient(recipe, point, epsilon));
                uv.Add(new GeometryVector2(point.X, point.Z));
            }
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
        }

        public static float Evaluate(SdfGeometryRecipe recipe, GeometryVector3 point)
        {
            var fields = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (SdfPrimitive primitive in recipe.Primitives) fields[primitive.Id] = Primitive(primitive, point);
            foreach (SdfOperation operation in recipe.Operations)
            {
                float left = fields[operation.Left], right = fields[operation.Right];
                fields[operation.Id] = operation.Type == "subtract" ? Math.Max(left, -right) :
                    operation.Type == "intersect" ? Math.Max(left, right) : SmoothUnion(left, right, operation.Radius);
            }
            string root = !string.IsNullOrEmpty(recipe.RootId) ? recipe.RootId :
                recipe.Operations.Count > 0 ? recipe.Operations[recipe.Operations.Count - 1].Id : recipe.Primitives[0].Id;
            return fields[root];
        }

        private static float Primitive(SdfPrimitive primitive, GeometryVector3 point)
        {
            float px = point.X - primitive.Position[0], py = point.Y - primitive.Position[1], pz = point.Z - primitive.Position[2];
            InverseRotate(ref px,ref py,ref pz,primitive.RotationDegrees);
            px/=Math.Max(.0001f,primitive.Scale[0]);py/=Math.Max(.0001f,primitive.Scale[1]);pz/=Math.Max(.0001f,primitive.Scale[2]);
            switch (primitive.Type)
            {
                case "sphere": return Length(px,py,pz) - primitive.Radius;
                case "ellipsoid":
                    float sx=Math.Max(.0001f,primitive.Size[0]*.5f), sy=Math.Max(.0001f,primitive.Size[1]*.5f), sz=Math.Max(.0001f,primitive.Size[2]*.5f);
                    return (Length(px/sx,py/sy,pz/sz)-1f)*Math.Min(sx,Math.Min(sy,sz));
                case "capsule":
                    float half=Math.Max(0,primitive.Height*.5f-primitive.Radius), qy=Math.Max(-half,Math.Min(half,py));
                    return Length(px,py-qy,pz)-primitive.Radius;
                case "cone":
                    float h=Math.Max(.0001f,primitive.Height), localY=py+h*.5f, radiusAt=primitive.Radius*(1-Math.Max(0,Math.Min(1,localY/h)));
                    return Math.Max(Length(px,0,pz)-radiusAt, Math.Max(-localY,localY-h));
                default:
                    float bx=Math.Abs(px)-primitive.Size[0]*.5f, by=Math.Abs(py)-primitive.Size[1]*.5f, bz=Math.Abs(pz)-primitive.Size[2]*.5f;
                    return Length(Math.Max(bx,0),Math.Max(by,0),Math.Max(bz,0))+Math.Min(Math.Max(bx,Math.Max(by,bz)),0);
            }
        }

        private static void Validate(SdfGeometryRecipe recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.Primitives == null || recipe.Primitives.Count < 1 || recipe.Primitives.Count > 64)
                throw new ArgumentException("SDF requires 1 to 64 primitives.");
            if (recipe.Operations == null || recipe.Operations.Count > 128) throw new ArgumentException("SDF supports at most 128 operations.");
            if (recipe.Resolution < 4 || recipe.Resolution > 64) throw new ArgumentOutOfRangeException(nameof(recipe), "SDF resolution must be in [4,64].");
            if (recipe.BoundsMin == null || recipe.BoundsMax == null || recipe.BoundsMin.Length != 3 || recipe.BoundsMax.Length != 3 ||
                Enumerable.Range(0,3).Any(i => recipe.BoundsMin[i] >= recipe.BoundsMax[i])) throw new ArgumentException("SDF bounds must be ordered 3D vectors.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SdfPrimitive p in recipe.Primitives)
            {
                if (string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id)) throw new ArgumentException("SDF primitive ids must be unique.");
                if (p.Position == null || p.Position.Length != 3 || p.Size == null || p.Size.Length != 3 || p.RotationDegrees == null || p.RotationDegrees.Length != 3 || p.Scale == null || p.Scale.Length != 3) throw new ArgumentException("SDF primitive vectors must have three values.");
                if (p.Type != "sphere" && p.Type != "capsule" && p.Type != "box" && p.Type != "cone" && p.Type != "ellipsoid") throw new ArgumentException("Unsupported SDF primitive type.");
            }
            foreach (SdfOperation op in recipe.Operations)
            {
                if (string.IsNullOrWhiteSpace(op.Id) || !ids.Add(op.Id) || !ids.Contains(op.Left) || !ids.Contains(op.Right)) throw new ArgumentException("SDF operations must reference earlier unique ids.");
                if (op.Type != "smooth-union" && op.Type != "subtract" && op.Type != "intersect") throw new ArgumentException("Unsupported SDF operation type.");
            }
            if (!string.IsNullOrEmpty(recipe.RootId) && !ids.Contains(recipe.RootId)) throw new ArgumentException("SDF root id is unknown.");
        }

        private static int Index(int x,int y,int z,int n) => x+(n+1)*(y+(n+1)*z);
        private static float SmoothUnion(float a,float b,float k) { if(k<=.00001f)return Math.Min(a,b); float h=Math.Max(0,Math.Min(1,.5f+.5f*(b-a)/k)); return b+(a-b)*h-k*h*(1-h); }
        private static float Length(float x,float y,float z)=>(float)Math.Sqrt(x*x+y*y+z*z);
        private static GeometryVector3 Lerp(GeometryVector3 a,GeometryVector3 b,float t)=>new GeometryVector3(a.X+(b.X-a.X)*t,a.Y+(b.Y-a.Y)*t,a.Z+(b.Z-a.Z)*t);
        private static GeometryVector3 Subtract(GeometryVector3 a,GeometryVector3 b)=>new GeometryVector3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        private static GeometryVector3 Cross(GeometryVector3 a,GeometryVector3 b)=>new GeometryVector3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        private static float Dot(GeometryVector3 a,GeometryVector3 b)=>a.X*b.X+a.Y*b.Y+a.Z*b.Z;
        private static void InverseRotate(ref float x,ref float y,ref float z,float[] degrees){float rx=-degrees[0]*(float)Math.PI/180f,ry=-degrees[1]*(float)Math.PI/180f,rz=-degrees[2]*(float)Math.PI/180f;float c=(float)Math.Cos(rz),s=(float)Math.Sin(rz),ox=x;x=x*c-y*s;y=ox*s+y*c;c=(float)Math.Cos(ry);s=(float)Math.Sin(ry);ox=x;x=x*c-z*s;z=ox*s+z*c;c=(float)Math.Cos(rx);s=(float)Math.Sin(rx);float oy=y;y=y*c-z*s;z=oy*s+z*c;}
        private static GeometryVector3 Gradient(SdfGeometryRecipe r,GeometryVector3 p,float e) { float x=Evaluate(r,new GeometryVector3(p.X+e,p.Y,p.Z))-Evaluate(r,new GeometryVector3(p.X-e,p.Y,p.Z)); float y=Evaluate(r,new GeometryVector3(p.X,p.Y+e,p.Z))-Evaluate(r,new GeometryVector3(p.X,p.Y-e,p.Z)); float z=Evaluate(r,new GeometryVector3(p.X,p.Y,p.Z+e))-Evaluate(r,new GeometryVector3(p.X,p.Y,p.Z-e)); float l=Length(x,y,z); return l<1e-6f?new GeometryVector3(0,1,0):new GeometryVector3(x/l,y/l,z/l); }
    }
}
