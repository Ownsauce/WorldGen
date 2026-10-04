using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using WorldGen.Identity;

namespace WorldGen.Contracts
{
    public static class ManifestHasher
    {
        public static string Compute(GenerationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            byte[] canonical = SerializeCanonical(manifest);
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(canonical);
            return StableWorldId.Hex(hash, hash.Length);
        }

        public static byte[] SerializeCanonical(GenerationManifest manifest)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write((byte)1);
                Write(writer, manifest.GeneratorId);
                Write(writer, manifest.GeneratorVersion);
                writer.Write(manifest.ParameterSchemaVersion);
                writer.Write(manifest.Seed);
                Write(writer, manifest.WorldId);
                Write(writer, manifest.ContentCatalogHash);
                writer.Write(manifest.PersistenceRevision);
                writer.Write(manifest.Parameters.Count);
                foreach (var pair in manifest.Parameters) { Write(writer, pair.Key); Write(writer, pair.Value); }
                writer.Write(manifest.FeatureFlags.Count);
                foreach (string flag in manifest.FeatureFlags) Write(writer, flag);
                writer.Write(manifest.ContentPacks.Count);
                foreach (var pair in manifest.ContentPacks) { Write(writer, pair.Key); Write(writer, pair.Value); }
                writer.Flush();
                return stream.ToArray();
            }
        }

        private static void Write(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }
    }
}
