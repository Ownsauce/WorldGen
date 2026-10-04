using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WorldGen.Dungeons
{
    [Serializable] public sealed class NativeRoomSourceRegistration
    {
        public string Id = "", Name = "", Provider = "ao-install", ActiveRevision = "";
        public int Playfield;
    }
    [Serializable] public sealed class NativeRoomSourceRegistry
    {
        public int Version = 1;
        public string DefaultSource = "";
        public NativeRoomSourceRegistration[] Sources = Array.Empty<NativeRoomSourceRegistration>();
    }
    [Serializable] public sealed class NativeRoomCatalogPublication
    {
        public int Version = 1, SourcePlayfield;
        public long AuthoringRevision;
        public string SourceId = "", CatalogSha256 = "", CollisionSha256 = "", SourceSha256 = "", SurfaceSha256 = "";
    }
    /// <summary>Hosts supply JSON serialization; shared generation has no engine or JSON-library dependency.</summary>
    public interface INativeRoomCatalogCodec
    {
        NativeRoomCatalog ReadCatalog(string json);
        NativeRoomSourceRegistry ReadRegistry(string json);
        string WriteRegistry(NativeRoomSourceRegistry value);
        NativeRoomCatalogPublication ReadPublication(string json);
        string WritePublication(NativeRoomCatalogPublication value);
    }
    public sealed class PublishedNativeRoomCatalog
    {
        public NativeRoomSourceRegistration Source { get; }
        public NativeRoomCatalog Catalog { get; }
        public NativeRoomCatalogPublication Publication { get; }
        public string Revision => Publication.CatalogSha256;
        public string CollisionPath { get; }
        internal PublishedNativeRoomCatalog(NativeRoomSourceRegistration source, NativeRoomCatalog catalog,
            NativeRoomCatalogPublication publication, string collisionPath)
        { Source = source; Catalog = catalog; Publication = publication; CollisionPath = collisionPath; }
    }
    /// <summary>Immutable local catalog revisions selected by a small data-driven registry.</summary>
    public sealed class NativeRoomCatalogStore
    {
        public const long MaximumFileBytes = 64 * 1024 * 1024;
        private readonly string root;
        private readonly INativeRoomCatalogCodec codec;
        private readonly Dictionary<string, PublishedNativeRoomCatalog> loaded = new Dictionary<string, PublishedNativeRoomCatalog>();
        private readonly object sync = new object();
        public NativeRoomCatalogStore(string root, INativeRoomCatalogCodec codec)
        { this.root = Path.GetFullPath(root); this.codec = codec ?? throw new ArgumentNullException(nameof(codec)); }
        private string RegistryPath => Path.Combine(root, "sources.json");
        public NativeRoomSourceRegistry Registry()
        {
            var value = codec.ReadRegistry(ReadText(RegistryPath));
            if (value == null || value.Version != 1 || value.Sources == null || value.Sources.Length == 0)
                throw new InvalidDataException("Missing or unsupported native source registry.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var playfields = new HashSet<int>();
            foreach (var source in value.Sources)
            {
                ValidateSource(source); ValidateHash(source.ActiveRevision);
                if (!ids.Add(source.Id) || !playfields.Add(source.Playfield)) throw new InvalidDataException("Duplicate native source identity.");
            }
            if (!ids.Contains(value.DefaultSource)) throw new InvalidDataException("Unknown default native source.");
            return value;
        }
        public PublishedNativeRoomCatalog Load(string sourceId = null, string revision = null)
        {
            var registry = Registry();
            var source = registry.Sources.SingleOrDefault(s => s.Id == (sourceId ?? registry.DefaultSource))
                ?? throw new InvalidDataException("Unknown native room source: " + sourceId);
            revision = revision ?? source.ActiveRevision; ValidateHash(revision);
            string key = source.Id + "/" + revision;
            lock (sync)
            {
                if (loaded.TryGetValue(key, out var cached)) return cached;
                string directory = Path.Combine(root, "RoomCatalogs", source.Id, revision);
                string catalogPath = Path.Combine(directory, "catalog.json"), collisionPath = Path.Combine(directory, "collision.bin");
                var publication = codec.ReadPublication(ReadText(Path.Combine(directory, "publication.json")));
                if (publication == null || publication.Version != 1 || publication.SourceId != source.Id
                    || publication.SourcePlayfield != source.Playfield || publication.CatalogSha256 != revision
                    || publication.AuthoringRevision < 0) throw new InvalidDataException("Native publication identity mismatch.");
                ValidateHash(publication.CollisionSha256);
                byte[] bytes = ReadBytes(catalogPath);
                if (NativeRoomDungeon.Sha(bytes) != revision || HashFile(collisionPath) != publication.CollisionSha256)
                    throw new InvalidDataException("Native publication files do not match their recorded hashes.");
                var catalog = codec.ReadCatalog(Encoding.UTF8.GetString(bytes)); NativeRoomDungeon.Validate(catalog);
                if (catalog.SourcePlayfield != source.Playfield || catalog.SourceSha256 != publication.SourceSha256
                    || catalog.SurfaceSha256 != publication.SurfaceSha256) throw new InvalidDataException("Native publication source mismatch.");
                ReadCollision(collisionPath, catalog);
                var result = new PublishedNativeRoomCatalog(source, catalog, publication, collisionPath);
                loaded.Add(key, result); return result;
            }
        }
        public PublishedNativeRoomCatalog LoadPlayfield(int playfield, string revision = null)
        {
            var source = Registry().Sources.SingleOrDefault(s => s.Playfield == playfield)
                ?? throw new InvalidDataException("Unregistered native room playfield: " + playfield);
            return Load(source.Id, revision);
        }
        public static Dictionary<int, float[]> ReadCollision(string path, NativeRoomCatalog catalog)
        {
            CheckFile(path);
            var result = new Dictionary<int, float[]>();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (new string(reader.ReadChars(4)) != "AONR" || reader.ReadInt32() != 1 || reader.ReadInt32() != catalog.SourcePlayfield)
                    throw new InvalidDataException("Invalid native collision header.");
                int count = reader.ReadInt32();
                if (count < 1 || count > 65535) throw new InvalidDataException("Invalid native collision room count.");
                for (int r = 0; r < count; r++)
                {
                    int id = reader.ReadInt32(), triangles = reader.ReadInt32();
                    if (id < 0 || triangles < 1 || triangles > 400000 || result.ContainsKey(id)
                        || (long)triangles * 36 > reader.BaseStream.Length - reader.BaseStream.Position)
                        throw new InvalidDataException("Invalid native collision room block.");
                    var values = new float[checked(triangles * 9)];
                    for (int i = 0; i < values.Length; i++)
                    {
                        values[i] = reader.ReadSingle();
                        if (float.IsNaN(values[i]) || float.IsInfinity(values[i])) throw new InvalidDataException("Nonfinite native collision coordinate.");
                    }
                    result.Add(id, values);
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing native collision data.");
            }
            foreach (var room in catalog.Rooms)
                if (!result.ContainsKey(room.SourceIndex)) throw new InvalidDataException("Missing collision for native source room " + room.SourceIndex);
            return result;
        }
        /// <summary>Stage both immutable packages before selecting either. Retained revisions are never overwritten.</summary>
        public static string Publish(NativeRoomSourceRegistration source, string catalogPath, string collisionPath,
            long authoringRevision, INativeRoomCatalogCodec codec, params string[] destinations)
        {
            ValidateSource(source);
            if (authoringRevision < 0 || destinations == null || destinations.Length == 0) throw new ArgumentException("Publication destinations and revision are required.");
            byte[] bytes = ReadBytes(catalogPath);
            var catalog = codec.ReadCatalog(Encoding.UTF8.GetString(bytes)); NativeRoomDungeon.Validate(catalog);
            ValidateHash(catalog.SourceSha256); ValidateHash(catalog.SurfaceSha256);
            if (catalog.SourcePlayfield != source.Playfield) throw new InvalidDataException("Selected source differs from the prepared catalog.");
            ReadCollision(collisionPath, catalog);
            string revision = NativeRoomDungeon.Sha(bytes);
            var publication = new NativeRoomCatalogPublication { SourceId = source.Id, SourcePlayfield = source.Playfield,
                CatalogSha256 = revision, CollisionSha256 = HashFile(collisionPath), SourceSha256 = catalog.SourceSha256,
                SurfaceSha256 = catalog.SurfaceSha256, AuthoringRevision = authoringRevision };
            var roots = destinations.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var locks = new List<FileStream>();
            var previous = new Dictionary<string, byte[]>();
            var activated = new List<string>();
            try
            {
                foreach (string target in roots)
                {
                    Directory.CreateDirectory(target);
                    locks.Add(new FileStream(Path.Combine(target, ".publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                }
                foreach (string target in roots)
                {
                    string registryPath = Path.Combine(target, "sources.json");
                    if (File.Exists(registryPath)) new NativeRoomCatalogStore(target, codec).Registry();
                    previous.Add(registryPath, File.Exists(registryPath) ? ReadBytes(registryPath) : null);
                    string folder = Path.Combine(target, "RoomCatalogs", source.Id, revision);
                    if (!Directory.Exists(folder))
                    {
                        string temporary = folder + "." + Guid.NewGuid().ToString("N") + ".tmp";
                        Directory.CreateDirectory(temporary);
                        try
                        {
                            File.WriteAllBytes(Path.Combine(temporary, "catalog.json"), bytes);
                            File.Copy(collisionPath, Path.Combine(temporary, "collision.bin"));
                            File.WriteAllText(Path.Combine(temporary, "publication.json"), codec.WritePublication(publication), new UTF8Encoding(false));
                            Directory.Move(temporary, folder);
                        }
                        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
                    }
                    // Existing revisions must have the same collision as well as catalog; never silently repair them.
                    var saved = codec.ReadPublication(ReadText(Path.Combine(folder, "publication.json")));
                    if (saved.Version != 1 || saved.AuthoringRevision < 0 || saved.SourceId != source.Id || saved.SourcePlayfield != source.Playfield || saved.CatalogSha256 != revision
                        || saved.CollisionSha256 != publication.CollisionSha256 || saved.SourceSha256 != publication.SourceSha256
                        || saved.SurfaceSha256 != publication.SurfaceSha256 || HashFile(Path.Combine(folder, "catalog.json")) != revision
                        || HashFile(Path.Combine(folder, "collision.bin")) != publication.CollisionSha256)
                        throw new InvalidDataException("An existing immutable revision differs from this publication.");
                }
                foreach (var entry in previous)
                {
                    var registry = entry.Value == null ? new NativeRoomSourceRegistry() : codec.ReadRegistry(Encoding.UTF8.GetString(entry.Value));
                    if (registry == null || registry.Version != 1 || registry.Sources == null) throw new InvalidDataException("Unsupported existing source registry.");
                    var existing = registry.Sources.SingleOrDefault(s => s.Id == source.Id);
                    if (existing != null && (existing.Playfield != source.Playfield || existing.Provider != source.Provider))
                        throw new InvalidDataException("A source identity cannot change provider or playfield.");
                    if (registry.Sources.Any(s => s.Id != source.Id && s.Playfield == source.Playfield)) throw new InvalidDataException("Playfield already has a registered source.");
                    registry.Sources = registry.Sources.Where(s => s.Id != source.Id).Concat(new[] {
                        new NativeRoomSourceRegistration { Id = source.Id, Name = source.Name, Provider = source.Provider, Playfield = source.Playfield, ActiveRevision = revision }
                    }).OrderBy(s => s.Id, StringComparer.Ordinal).ToArray();
                    if (registry.DefaultSource.Length == 0) registry.DefaultSource = source.Id;
                    AtomicWrite(entry.Key, Encoding.UTF8.GetBytes(codec.WriteRegistry(registry))); activated.Add(entry.Key);
                }
                return revision;
            }
            catch (Exception error)
            {
                var failures = new List<Exception> { error };
                foreach (string path in activated)
                    try { if (previous[path] == null) File.Delete(path); else AtomicWrite(path, previous[path]); }
                    catch (Exception rollback) { failures.Add(rollback); }
                if (failures.Count > 1) throw new AggregateException("Publication and registry rollback failed; inspect destinations before using this source.", failures);
                throw;
            }
            finally { foreach (var handle in locks) handle.Dispose(); }
        }
        private static void AtomicWrite(string path, byte[] bytes)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private static void ValidateSource(NativeRoomSourceRegistration source)
        {
            if (source == null || string.IsNullOrEmpty(source.Id) || source.Id.Length > 80 || source.Playfield <= 0
                || source.Provider != "ao-install" || string.IsNullOrWhiteSpace(source.Name)
                || !source.Id.All(c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-'))
                throw new InvalidDataException("Invalid native source registration.");
        }
        private static void ValidateHash(string value)
        {
            if (value == null || value.Length != 64 || !value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                throw new InvalidDataException("Invalid native catalog revision hash.");
        }
        private static void CheckFile(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaximumFileBytes) throw new InvalidDataException("Missing or oversized native catalog resource: " + path);
        }
        private static byte[] ReadBytes(string path) { CheckFile(path); return File.ReadAllBytes(path); }
        private static string ReadText(string path) => Encoding.UTF8.GetString(ReadBytes(path));
        private static string HashFile(string path)
        {
            CheckFile(path);
            using (var stream = File.OpenRead(path)) using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
