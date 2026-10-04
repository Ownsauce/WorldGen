using System;

namespace WorldGen.Spatial
{
    public readonly struct WorldVector3 : IEquatable<WorldVector3>
    {
        public WorldVector3(int x, int y, int z) { X = x; Y = y; Z = z; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public bool Equals(WorldVector3 other) => X == other.X && Y == other.Y && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldVector3 other && Equals(other);
        public override int GetHashCode() => unchecked(((X * 397) ^ Y) * 397 ^ Z);
        public override string ToString() => $"({X},{Y},{Z})";
    }
}
