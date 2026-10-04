using System;

namespace WorldGen.Spatial
{
    public readonly struct WorldBounds : IEquatable<WorldBounds>
    {
        public WorldBounds(WorldVector3 minimum, WorldVector3 maximum)
        {
            if (maximum.X < minimum.X || maximum.Y < minimum.Y || maximum.Z < minimum.Z)
                throw new ArgumentException("Bounds maximum must not be below minimum.");
            Minimum = minimum;
            Maximum = maximum;
        }

        public WorldVector3 Minimum { get; }
        public WorldVector3 Maximum { get; }
        public bool Contains(WorldVector3 value) =>
            value.X >= Minimum.X && value.X <= Maximum.X &&
            value.Y >= Minimum.Y && value.Y <= Maximum.Y &&
            value.Z >= Minimum.Z && value.Z <= Maximum.Z;
        public bool Equals(WorldBounds other) => Minimum.Equals(other.Minimum) && Maximum.Equals(other.Maximum);
        public override bool Equals(object obj) => obj is WorldBounds other && Equals(other);
        public override int GetHashCode() => unchecked(Minimum.GetHashCode() * 397 ^ Maximum.GetHashCode());
    }
}
