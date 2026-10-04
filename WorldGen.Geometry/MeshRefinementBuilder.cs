using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    /// <summary>Deterministic indexed-mesh welding, Loop subdivision, and Taubin smoothing.</summary>
    public static class MeshRefinementBuilder
    {
        public static GeometryMeshData Refine(GeometryMeshData source, int subdivisionIterations,
            int smoothingIterations)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (subdivisionIterations < 0 || subdivisionIterations > 3)
                throw new ArgumentOutOfRangeException(nameof(subdivisionIterations));
            if (smoothingIterations < 0 || smoothingIterations > 20)
                throw new ArgumentOutOfRangeException(nameof(smoothingIterations));
            GeometryMeshData mesh = Weld(source);
            for (int i = 0; i < subdivisionIterations; i++) mesh = SubdivideOnce(mesh);
            if (smoothingIterations > 0) mesh = TaubinSmooth(mesh, smoothingIterations, .5f, -.53f);
            return RecalculateNormals(mesh);
        }

        public static GeometryMeshData Weld(GeometryMeshData source, float tolerance = .00001f)
        {
            var vertices=new List<GeometryVector3>();var uv=new List<GeometryVector2>();var indices=new int[source.Indices.Length];
            var map=new Dictionary<VertexKey,int>();
            for(int i=0;i<source.Indices.Length;i++)
            {
                int old=source.Indices[i];GeometryVector3 p=source.Vertices[old];var key=new VertexKey(p,tolerance);
                if(!map.TryGetValue(key,out int index)){index=vertices.Count;map.Add(key,index);vertices.Add(p);uv.Add(old<source.UV.Length?source.UV[old]:new GeometryVector2());}
                indices[i]=index;
            }
            return new GeometryMeshData(vertices.ToArray(),new GeometryVector3[vertices.Count],uv.ToArray(),indices);
        }

        private static GeometryMeshData SubdivideOnce(GeometryMeshData mesh)
        {
            int originalCount=mesh.Vertices.Length;var neighbors=BuildNeighbors(mesh);var edges=BuildEdges(mesh);
            var vertices=new List<GeometryVector3>(mesh.Vertices);var uv=new List<GeometryVector2>(mesh.UV);
            var edgeVertices=new Dictionary<EdgeKey,int>();
            foreach(KeyValuePair<EdgeKey,EdgeData> pair in edges)
            {
                EdgeKey edge=pair.Key;EdgeData data=pair.Value;GeometryVector3 a=mesh.Vertices[edge.A],b=mesh.Vertices[edge.B];
                GeometryVector3 point;
                if(data.Opposites.Count==2)
                {GeometryVector3 c=mesh.Vertices[data.Opposites[0]],d=mesh.Vertices[data.Opposites[1]];point=Combine(a,.375f,b,.375f,c,.125f,d,.125f);}
                else point=Combine(a,.5f,b,.5f);
                int index=vertices.Count;vertices.Add(point);uv.Add(Average(mesh.UV[edge.A],mesh.UV[edge.B]));edgeVertices.Add(edge,index);
            }
            for(int i=0;i<originalCount;i++)
            {
                List<int> adjacent=neighbors[i];if(adjacent.Count<3)continue;
                List<int> boundary=BoundaryNeighbors(i,edges);
                GeometryVector3 current=mesh.Vertices[i];
                if(boundary.Count>=2)vertices[i]=Combine(current,.75f,mesh.Vertices[boundary[0]],.125f,mesh.Vertices[boundary[1]],.125f);
                else
                {float beta=adjacent.Count==3?3f/16f:3f/(8f*adjacent.Count);GeometryVector3 sum=Sum(mesh.Vertices,adjacent);vertices[i]=Add(Scale(current,1-adjacent.Count*beta),Scale(sum,beta));}
            }
            var indices=new List<int>(mesh.Indices.Length*4);
            for(int i=0;i<mesh.Indices.Length;i+=3)
            {
                int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];
                int ab=edgeVertices[new EdgeKey(a,b)],bc=edgeVertices[new EdgeKey(b,c)],ca=edgeVertices[new EdgeKey(c,a)];
                AddTri(indices,a,ab,ca);AddTri(indices,b,bc,ab);AddTri(indices,c,ca,bc);AddTri(indices,ab,bc,ca);
            }
            return new GeometryMeshData(vertices.ToArray(),new GeometryVector3[vertices.Count],uv.ToArray(),indices.ToArray());
        }

        private static GeometryMeshData TaubinSmooth(GeometryMeshData mesh,int iterations,float lambda,float mu)
        {
            List<int>[] neighbors=BuildNeighbors(mesh);GeometryVector3[] positions=(GeometryVector3[])mesh.Vertices.Clone();
            for(int i=0;i<iterations;i++){positions=SmoothPass(positions,neighbors,lambda);positions=SmoothPass(positions,neighbors,mu);}
            return new GeometryMeshData(positions,new GeometryVector3[positions.Length],mesh.UV,mesh.Indices);
        }

        private static GeometryVector3[] SmoothPass(GeometryVector3[] input,List<int>[] neighbors,float factor)
        {
            var output=new GeometryVector3[input.Length];for(int i=0;i<input.Length;i++)
            {if(neighbors[i].Count==0){output[i]=input[i];continue;}GeometryVector3 average=Scale(Sum(input,neighbors[i]),1f/neighbors[i].Count);output[i]=Add(input[i],Scale(Subtract(average,input[i]),factor));}return output;
        }

        private static GeometryMeshData RecalculateNormals(GeometryMeshData mesh)
        {
            var normals=new GeometryVector3[mesh.Vertices.Length];for(int i=0;i<mesh.Indices.Length;i+=3)
            {int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];GeometryVector3 normal=Cross(Subtract(mesh.Vertices[b],mesh.Vertices[a]),Subtract(mesh.Vertices[c],mesh.Vertices[a]));normals[a]=Add(normals[a],normal);normals[b]=Add(normals[b],normal);normals[c]=Add(normals[c],normal);}
            for(int i=0;i<normals.Length;i++)normals[i]=Normalize(normals[i]);return new GeometryMeshData(mesh.Vertices,normals,mesh.UV,mesh.Indices);
        }

        private static List<int>[] BuildNeighbors(GeometryMeshData mesh)
        {var sets=new HashSet<int>[mesh.Vertices.Length];for(int i=0;i<sets.Length;i++)sets[i]=new HashSet<int>();for(int i=0;i<mesh.Indices.Length;i+=3){int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];sets[a].Add(b);sets[a].Add(c);sets[b].Add(a);sets[b].Add(c);sets[c].Add(a);sets[c].Add(b);}var result=new List<int>[sets.Length];for(int i=0;i<sets.Length;i++)result[i]=new List<int>(sets[i]);return result;}
        private static Dictionary<EdgeKey,EdgeData> BuildEdges(GeometryMeshData mesh)
        {var edges=new Dictionary<EdgeKey,EdgeData>();for(int i=0;i<mesh.Indices.Length;i+=3){int a=mesh.Indices[i],b=mesh.Indices[i+1],c=mesh.Indices[i+2];AddEdge(a,b,c);AddEdge(b,c,a);AddEdge(c,a,b);}return edges;void AddEdge(int a,int b,int opposite){var key=new EdgeKey(a,b);if(!edges.TryGetValue(key,out EdgeData data)){data=new EdgeData();edges.Add(key,data);}data.Opposites.Add(opposite);}}
        private static List<int> BoundaryNeighbors(int vertex,Dictionary<EdgeKey,EdgeData> edges){var result=new List<int>();foreach(var pair in edges)if(pair.Value.Opposites.Count==1){if(pair.Key.A==vertex)result.Add(pair.Key.B);else if(pair.Key.B==vertex)result.Add(pair.Key.A);}return result;}
        private static void AddTri(List<int> values,int a,int b,int c){values.Add(a);values.Add(b);values.Add(c);}
        private static GeometryVector2 Average(GeometryVector2 a,GeometryVector2 b)=>new GeometryVector2((a.X+b.X)*.5f,(a.Y+b.Y)*.5f);
        private static GeometryVector3 Sum(GeometryVector3[] values,List<int> indices){var sum=new GeometryVector3();foreach(int i in indices)sum=Add(sum,values[i]);return sum;}
        private static GeometryVector3 Combine(GeometryVector3 a,float aw,GeometryVector3 b,float bw)=>Add(Scale(a,aw),Scale(b,bw));
        private static GeometryVector3 Combine(GeometryVector3 a,float aw,GeometryVector3 b,float bw,GeometryVector3 c,float cw)=>Add(Combine(a,aw,b,bw),Scale(c,cw));
        private static GeometryVector3 Combine(GeometryVector3 a,float aw,GeometryVector3 b,float bw,GeometryVector3 c,float cw,GeometryVector3 d,float dw)=>Add(Combine(a,aw,b,bw,c,cw),Scale(d,dw));
        private static GeometryVector3 Add(GeometryVector3 a,GeometryVector3 b)=>new GeometryVector3(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        private static GeometryVector3 Subtract(GeometryVector3 a,GeometryVector3 b)=>new GeometryVector3(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        private static GeometryVector3 Scale(GeometryVector3 a,float s)=>new GeometryVector3(a.X*s,a.Y*s,a.Z*s);
        private static GeometryVector3 Cross(GeometryVector3 a,GeometryVector3 b)=>new GeometryVector3(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        private static GeometryVector3 Normalize(GeometryVector3 a){float l=(float)Math.Sqrt(a.X*a.X+a.Y*a.Y+a.Z*a.Z);return l<1e-8f?new GeometryVector3(0,1,0):Scale(a,1/l);}
        private readonly struct EdgeKey:IEquatable<EdgeKey>{public EdgeKey(int a,int b){A=Math.Min(a,b);B=Math.Max(a,b);}public int A{get;}public int B{get;}public bool Equals(EdgeKey other)=>A==other.A&&B==other.B;public override bool Equals(object obj)=>obj is EdgeKey other&&Equals(other);public override int GetHashCode()=>A*397^B;}
        private sealed class EdgeData{public List<int> Opposites{get;}=new List<int>(2);}
        private readonly struct VertexKey:IEquatable<VertexKey>{private readonly long _x,_y,_z;public VertexKey(GeometryVector3 p,float t){_x=(long)Math.Round(p.X/t);_y=(long)Math.Round(p.Y/t);_z=(long)Math.Round(p.Z/t);}public bool Equals(VertexKey other)=>_x==other._x&&_y==other._y&&_z==other._z;public override bool Equals(object obj)=>obj is VertexKey other&&Equals(other);public override int GetHashCode()=>_x.GetHashCode()*397^_y.GetHashCode()*31^_z.GetHashCode();}
    }
}
