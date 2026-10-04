using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using WorldGen.Dungeons;

namespace WorldGen.Contracts
{
    public sealed class DungeonManifestEnvelope
    {
        public DungeonManifestEnvelope(GenerationManifest manifest, string expectedLayoutHash)
        {
            Manifest = manifest ?? throw new ArgumentNullException(nameof(manifest));
            if (string.IsNullOrWhiteSpace(expectedLayoutHash))
                throw new ArgumentException("Expected layout hash is required.", nameof(expectedLayoutHash));
            ExpectedLayoutHash = expectedLayoutHash.Trim().ToLowerInvariant();
        }

        public GenerationManifest Manifest { get; }
        public string ExpectedLayoutHash { get; }
    }

    /// <summary>
    /// Transport-neutral, versioned envelope. The encoded value is safe to carry
    /// inside an existing AO chat message while protocol negotiation is developed.
    /// </summary>
    public static class DungeonManifestEnvelopeCodec
    {
        public const string Prefix = "AOWG1:";
        private const int WireVersion = 1;
        private const int MaximumEntries = 256;
        private const int MaximumStringBytes = 32767;

        public static string Encode(DungeonManifestEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            GenerationManifest manifest = envelope.Manifest;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(WireVersion);
                Write(writer, manifest.GeneratorId);
                Write(writer, manifest.GeneratorVersion);
                writer.Write(manifest.ParameterSchemaVersion);
                writer.Write(manifest.Seed);
                Write(writer, manifest.WorldId);
                Write(writer, manifest.ContentCatalogHash);
                writer.Write(manifest.PersistenceRevision);
                Write(writer, envelope.ExpectedLayoutHash);
                WriteMap(writer, manifest.Parameters);
                writer.Write(manifest.FeatureFlags.Count);
                foreach (string flag in manifest.FeatureFlags) Write(writer, flag);
                WriteMap(writer, manifest.ContentPacks);
                writer.Flush();
                return Prefix + Convert.ToBase64String(stream.ToArray());
            }
        }

        public static bool TryDecode(string text, out DungeonManifestEnvelope envelope, out string error)
        {
            envelope = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            try
            {
                byte[] payload = Convert.FromBase64String(text.Substring(Prefix.Length));
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false), true))
                {
                    int version = reader.ReadInt32();
                    if (version != WireVersion) throw new InvalidDataException("Unsupported envelope version " + version);
                    string generatorId = Read(reader);
                    string generatorVersion = Read(reader);
                    int schemaVersion = reader.ReadInt32();
                    ulong seed = reader.ReadUInt64();
                    string worldId = Read(reader);
                    string catalogHash = Read(reader);
                    long persistenceRevision = reader.ReadInt64();
                    string layoutHash = Read(reader);
                    var manifest = new GenerationManifest(generatorId, generatorVersion, schemaVersion,
                        seed, worldId, catalogHash) { PersistenceRevision = persistenceRevision };
                    ReadMap(reader, manifest.Parameters);
                    int flagCount = ReadCount(reader, "feature flag");
                    for (int i = 0; i < flagCount; i++) manifest.FeatureFlags.Add(Read(reader));
                    ReadMap(reader, manifest.ContentPacks);
                    if (stream.Position != stream.Length) throw new InvalidDataException("Envelope has trailing bytes.");
                    envelope = new DungeonManifestEnvelope(manifest, layoutHash);
                    return true;
                }
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException
                                               || exception is ArgumentException || exception is EndOfStreamException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void WriteMap(BinaryWriter writer, IEnumerable<KeyValuePair<string, string>> values)
        {
            var entries = new List<KeyValuePair<string, string>>(values);
            writer.Write(entries.Count);
            foreach (KeyValuePair<string, string> entry in entries)
            { Write(writer, entry.Key); Write(writer, entry.Value); }
        }

        private static void ReadMap(BinaryReader reader, IDictionary<string, string> destination)
        {
            int count = ReadCount(reader, "map");
            for (int i = 0; i < count; i++) destination.Add(Read(reader), Read(reader));
        }

        private static int ReadCount(BinaryReader reader, string name)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaximumEntries)
                throw new InvalidDataException("Invalid " + name + " count " + count.ToString(CultureInfo.InvariantCulture));
            return count;
        }

        private static void Write(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaximumStringBytes) throw new InvalidDataException("Envelope string is too long.");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string Read(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            if (length > MaximumStringBytes) throw new InvalidDataException("Envelope string is too long.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
    }

    public sealed class DungeonDoorStateEnvelope
    {
        public DungeonDoorStateEnvelope(string worldId, bool isSnapshot,
            IReadOnlyList<DungeonDoorStateSnapshot> doors)
        {
            if (string.IsNullOrWhiteSpace(worldId))
                throw new ArgumentException("World ID is required.", nameof(worldId));
            WorldId = worldId;
            IsSnapshot = isSnapshot;
            Doors = doors ?? throw new ArgumentNullException(nameof(doors));
        }

        public string WorldId { get; }
        public bool IsSnapshot { get; }
        public IReadOnlyList<DungeonDoorStateSnapshot> Doors { get; }
    }

    /// <summary>Versioned development transport for full door snapshots and deltas.</summary>
    public static class DungeonDoorStateEnvelopeCodec
    {
        public const string Prefix = "AOWD1:";
        private const int WireVersion = 1;
        private const int MaximumDoors = 512;

        public static string Encode(DungeonDoorStateEnvelope envelope)
        {
            if (envelope == null) throw new ArgumentNullException(nameof(envelope));
            if (envelope.Doors.Count > MaximumDoors)
                throw new InvalidDataException("Door state count exceeds the transport limit.");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(WireVersion);
                WriteString(writer, envelope.WorldId);
                writer.Write(envelope.IsSnapshot);
                writer.Write(envelope.Doors.Count);
                foreach (DungeonDoorStateSnapshot door in envelope.Doors)
                {
                    WriteString(writer, door.DoorId);
                    writer.Write((byte)door.State);
                    writer.Write(door.Revision);
                    WriteString(writer, door.RequiredKeyId);
                }
                writer.Flush();
                return Prefix + Convert.ToBase64String(stream.ToArray());
            }
        }

        public static bool TryDecode(string text, out DungeonDoorStateEnvelope envelope,
            out string error)
        {
            envelope = null;
            error = null;
            if (string.IsNullOrWhiteSpace(text)
                || !text.StartsWith(Prefix, StringComparison.Ordinal))
                return false;
            try
            {
                byte[] payload = Convert.FromBase64String(text.Substring(Prefix.Length));
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false), true))
                {
                    if (reader.ReadInt32() != WireVersion)
                        throw new InvalidDataException("Unsupported door envelope version.");
                    string worldId = ReadString(reader);
                    bool isSnapshot = reader.ReadBoolean();
                    int count = reader.ReadInt32();
                    if (count < 0 || count > MaximumDoors)
                        throw new InvalidDataException("Invalid door state count.");
                    var doors = new List<DungeonDoorStateSnapshot>(count);
                    for (int i = 0; i < count; i++)
                    {
                        string doorId = ReadString(reader);
                        byte rawState = reader.ReadByte();
                        if (rawState > (byte)DungeonDoorState.Sealed)
                            throw new InvalidDataException("Invalid door state.");
                        long revision = reader.ReadInt64();
                        string keyId = ReadString(reader);
                        doors.Add(new DungeonDoorStateSnapshot(
                            doorId, (DungeonDoorState)rawState, revision, keyId));
                    }
                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Door envelope has trailing bytes.");
                    envelope = new DungeonDoorStateEnvelope(worldId, isSnapshot, doors.AsReadOnly());
                    return true;
                }
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException
                                               || exception is ArgumentException || exception is EndOfStreamException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > ushort.MaxValue)
                throw new InvalidDataException("Door envelope string is too long.");
            writer.Write((ushort)bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
