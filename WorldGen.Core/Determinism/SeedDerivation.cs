using System;
using System.Text;

namespace WorldGen.Determinism
{
    public static class SeedDerivation
    {
        /// <summary>Derives an independent seed using FNV-1a followed by SplitMix64 avalanche.</summary>
        public static ulong Derive(ulong parentSeed, string streamName)
        {
            if (string.IsNullOrWhiteSpace(streamName))
                throw new ArgumentException("A deterministic stream name is required.", nameof(streamName));

            ulong hash = 14695981039346656037UL;
            for (int shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(parentSeed >> shift);
                hash = unchecked(hash * 1099511628211UL);
            }

            byte[] name = Encoding.UTF8.GetBytes(streamName.Trim());
            for (int i = 0; i < name.Length; i++)
            {
                hash ^= name[i];
                hash = unchecked(hash * 1099511628211UL);
            }
            return Avalanche(hash);
        }

        public static DeterministicRandom CreateStream(ulong parentSeed, string streamName) =>
            new DeterministicRandom(Derive(parentSeed, streamName), Derive(parentSeed, streamName + "/sequence"));

        private static ulong Avalanche(ulong value)
        {
            value ^= value >> 30;
            value = unchecked(value * 0xBF58476D1CE4E5B9UL);
            value ^= value >> 27;
            value = unchecked(value * 0x94D049BB133111EBUL);
            return value ^ (value >> 31);
        }
    }
}
