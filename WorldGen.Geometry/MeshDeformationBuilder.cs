using System;
using System.Collections.Generic;

namespace WorldGen.Geometry
{
    public sealed class MeshDeformation
    {
        public string Type { get; set; }
        public string Axis { get; set; } = "y";
        public float Factor { get; set; } = 1f;
        public float AngleDegrees { get; set; }
    }

    public static class MeshDeformationBuilder
    {
        public static GeometryMeshData Apply(GeometryMeshData source,
            IReadOnlyList<MeshDeformation> deformations)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (deformations == null || deformations.Count == 0) return source;
            GeometryVector3[] vertices = (GeometryVector3[])source.Vertices.Clone();
            foreach (MeshDeformation deformation in deformations)
            {
                Bounds(vertices, deformation.Axis, out float minimum, out float maximum);
                float span = Math.Max(1e-6f, maximum - minimum);
                for (int index = 0; index < vertices.Length; index++)
                {
                    GeometryVector3 value = vertices[index];
                    float along = Coordinate(value, deformation.Axis);
                    float t = (along - minimum) / span;
                    if (deformation.Type == "taper")
                        value = ScalePerpendicular(value, deformation.Axis,
                            deformation.Factor + (1 - deformation.Factor) * t);
                    else if (deformation.Type == "twist")
                        value = RotatePerpendicular(value, deformation.Axis,
                            deformation.AngleDegrees * t * (float)Math.PI / 180f);
                    else if (deformation.Type == "bend")
                        value = Bend(value, deformation.Axis, minimum, span,
                            deformation.AngleDegrees * (float)Math.PI / 180f);
                    else throw new NotSupportedException($"Unsupported deformation '{deformation.Type}'.");
                    vertices[index] = value;
                }
            }
            return MeshRefinementBuilder.Refine(new GeometryMeshData(vertices,
                new GeometryVector3[vertices.Length], source.UV, source.Indices), 0, 0);
        }

        private static GeometryVector3 Bend(GeometryVector3 value, string axis,
            float minimum, float span, float angle)
        {
            if (Math.Abs(angle) < 1e-6f) return value;
            float along = Coordinate(value, axis) - minimum, radius = span / angle;
            float theta = along / radius, c = (float)Math.Cos(theta), s = (float)Math.Sin(theta);
            if (axis == "x") return new GeometryVector3(minimum + s * (radius - value.Y),
                radius - c * (radius - value.Y), value.Z);
            if (axis == "z") return new GeometryVector3(value.X,
                radius - c * (radius - value.Y), minimum + s * (radius - value.Y));
            return new GeometryVector3(radius - c * (radius - value.X), minimum + s * (radius - value.X), value.Z);
        }

        private static GeometryVector3 RotatePerpendicular(GeometryVector3 v, string axis, float angle)
        {
            float c=(float)Math.Cos(angle),s=(float)Math.Sin(angle);
            if(axis=="x")return new GeometryVector3(v.X,v.Y*c-v.Z*s,v.Y*s+v.Z*c);
            if(axis=="z")return new GeometryVector3(v.X*c-v.Y*s,v.X*s+v.Y*c,v.Z);
            return new GeometryVector3(v.X*c-v.Z*s,v.Y,v.X*s+v.Z*c);
        }

        private static GeometryVector3 ScalePerpendicular(GeometryVector3 v,string axis,float scale)
        {if(axis=="x")return new GeometryVector3(v.X,v.Y*scale,v.Z*scale);if(axis=="z")return new GeometryVector3(v.X*scale,v.Y*scale,v.Z);return new GeometryVector3(v.X*scale,v.Y,v.Z*scale);}
        private static float Coordinate(GeometryVector3 value,string axis)=>axis=="x"?value.X:axis=="z"?value.Z:value.Y;
        private static void Bounds(GeometryVector3[] vertices,string axis,out float minimum,out float maximum)
        {minimum=float.PositiveInfinity;maximum=float.NegativeInfinity;foreach(GeometryVector3 vertex in vertices){float value=Coordinate(vertex,axis);minimum=Math.Min(minimum,value);maximum=Math.Max(maximum,value);}}
    }
}
