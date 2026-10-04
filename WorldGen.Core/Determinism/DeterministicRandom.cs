using System;

namespace WorldGen.Determinism
{
    /// <summary>PCG-XSH-RR 32 with explicitly defined cross-platform behavior.</summary>
    public sealed class DeterministicRandom
    {
        private ulong _state;
        private readonly ulong _increment;

        public DeterministicRandom(ulong seed, ulong stream = 0)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0;
            NextUInt32();
            _state += seed;
            NextUInt32();
        }

        public uint NextUInt32()
        {
            ulong previous = _state;
            _state = unchecked(previous * 6364136223846793005UL + _increment);
            uint value = (uint)(((previous >> 18) ^ previous) >> 27);
            int rotation = (int)(previous >> 59);
            return (value >> rotation) | (value << ((-rotation) & 31));
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));

            uint bound = (uint)exclusiveMaximum;
            uint threshold = unchecked((uint)(0 - bound)) % bound;
            uint value;
            do value = NextUInt32(); while (value < threshold);
            return (int)(value % bound);
        }

        public int NextInt(int inclusiveMinimum, int exclusiveMaximum)
        {
            if (exclusiveMaximum <= inclusiveMinimum)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            long range = (long)exclusiveMaximum - inclusiveMinimum;
            if (range > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            return inclusiveMinimum + NextInt((int)range);
        }

        public bool NextBool() => (NextUInt32() & 1U) != 0;

        public double NextDouble() => NextUInt32() / 4294967296.0;

        public float NextFloat() => (float)NextDouble();

        public float NextFloat(float minimum, float maximum)
        {
            if (!(maximum >= minimum))
                throw new ArgumentOutOfRangeException(nameof(maximum));
            return minimum + (maximum - minimum) * NextFloat();
        }
    }
}
