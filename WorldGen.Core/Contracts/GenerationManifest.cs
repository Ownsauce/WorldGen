using System;
using System.Collections.Generic;

namespace WorldGen.Contracts
{
    public sealed class GenerationManifest
    {
        public GenerationManifest(string generatorId, string generatorVersion, int parameterSchemaVersion,
            ulong seed, string worldId, string contentCatalogHash)
        {
            GeneratorId = Require(generatorId, nameof(generatorId));
            GeneratorVersion = Require(generatorVersion, nameof(generatorVersion));
            if (parameterSchemaVersion <= 0) throw new ArgumentOutOfRangeException(nameof(parameterSchemaVersion));
            ParameterSchemaVersion = parameterSchemaVersion;
            Seed = seed;
            WorldId = Require(worldId, nameof(worldId));
            ContentCatalogHash = Require(contentCatalogHash, nameof(contentCatalogHash));
        }

        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public int ParameterSchemaVersion { get; }
        public ulong Seed { get; }
        public string WorldId { get; }
        public string ContentCatalogHash { get; }
        public long PersistenceRevision { get; set; }
        public SortedDictionary<string, string> Parameters { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
        public SortedSet<string> FeatureFlags { get; } = new SortedSet<string>(StringComparer.Ordinal);
        public SortedDictionary<string, string> ContentPacks { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);

        private static string Require(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
            return value.Trim();
        }
    }
}
