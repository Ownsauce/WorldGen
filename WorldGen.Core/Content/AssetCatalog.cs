using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace WorldGen.Content
{
    // Public fields keep the portable JSON contract usable with Unity JsonUtility,
    // System.Text.Json (IncludeFields), or another client's serializer.
    [Serializable] public sealed class AssetCatalog
    {
        public int schemaVersion;
        public string contentVersion;
        public string coordinateSystem;
        public CatalogAsset[] assets;
    }
    [Serializable] public sealed class CatalogVector
    {
        public float x, y, z;
    }
    [Serializable] public sealed class CatalogBounds
    {
        public CatalogVector minimum, maximum;
    }
    [Serializable] public sealed class CatalogBox
    {
        public CatalogVector centerMeters, sizeMeters;
    }
    [Serializable] public sealed class CatalogCollision
    {
        public string mode;
        public CatalogBox[] boxes;
    }
    [Serializable] public sealed class CatalogSocket
    {
        public string id;
        public CatalogVector positionMeters, forward;
        public float widthMeters, heightMeters;
    }
    [Serializable] public sealed class CatalogPlacement
    {
        public string slot;
        public float openingWidthMeters, openingHeightMeters;
    }
    public enum DungeonAssetRole { Unspecified, Floor, Wall, Ceiling, Pillar, Light, Safety, Rail, Train, Stair, Handrail, HandrailPost, WallCorner, Toilet, Sink, DoorFrame, Door }

    [Serializable] public sealed class CatalogLighting
    {
        public bool enabled;
        public CatalogVector socketMeters, color;
        public float intensity = 4, rangeMeters = 24, coneDegrees = 110, emissionIntensity = 4;
    }

    [Serializable] public sealed class CatalogAsset
    {
        public string id, theme, category, purpose, path, sha256, pivot, notes;
        public CatalogVector dimensionsMeters;
        public CatalogBounds sourceBounds;
        public CatalogBox clearance;
        public CatalogCollision collision;
        public CatalogSocket[] sockets;
        public CatalogPlacement placement;
        public int[] allowedRotations;
        public int triangles;
        public string[] tags, allowedThemes, excludedThemes;
        public string fitMode, approvalStatus;
        public CatalogLighting lighting;

        public bool SupportsTheme(string requested)
        {
            if (excludedThemes != null && Array.Exists(excludedThemes, x => string.Equals(x, requested, StringComparison.OrdinalIgnoreCase))) return false;
            return allowedThemes == null || allowedThemes.Length == 0
                ? string.Equals(theme, requested, StringComparison.OrdinalIgnoreCase)
                : Array.Exists(allowedThemes, x => string.Equals(x, requested, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static class AssetCatalogValidator
    {
        public const int SchemaVersion = 1;
        private const float Epsilon = 0.00001f;

        public static void Validate(AssetCatalog catalog)
        {
            Require(catalog != null && (catalog.schemaVersion == SchemaVersion || catalog.schemaVersion == 2), "Unsupported catalog schema.");
            if (catalog.schemaVersion == 2) { ValidateReusable(catalog); return; }
            Require(!string.IsNullOrWhiteSpace(catalog.contentVersion), "Missing content version.");
            Require(catalog.coordinateSystem == "right-handed-y-up", "Unsupported coordinate system.");
            Require(catalog.assets != null && catalog.assets.Length > 0, "Catalog is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogAsset asset in catalog.assets)
            {
                Require(asset != null, "Null asset.");
                Require(!string.IsNullOrEmpty(asset.id) && Regex.IsMatch(asset.id, "^[a-z0-9][a-z0-9/-]*$")
                    && !asset.id.Contains("//") && !asset.id.EndsWith("/", StringComparison.Ordinal), "Invalid asset ID.");
                Require(ids.Add(asset.id), "Duplicate asset ID: " + asset.id);
                string[] parts = asset.id.Split('/');
                Require(parts.Length >= 3 && asset.theme == parts[0] && asset.category == parts[1], "Asset identity differs from folder path.");
                Require(asset.path == asset.id + "/game.glb" && paths.Add(asset.path), "Invalid or duplicate GLB path.");
                Require(asset.sha256 != null && Regex.IsMatch(asset.sha256, "^[0-9a-f]{64}$"), "Invalid asset hash.");
                Require(asset.purpose == "doorway-frame" && asset.placement != null
                    && asset.placement.slot == "doorway-frame", "Unsupported placement contract.");
                Positive(asset.dimensionsMeters, "dimensions");
                Require(asset.pivot == "bottom-center", "Unsupported pivot.");
                Require(asset.triangles > 0 && asset.sourceBounds != null, "Missing measured geometry.");
                Finite(asset.sourceBounds.minimum); Finite(asset.sourceBounds.maximum);
                Require(asset.sourceBounds.maximum.x > asset.sourceBounds.minimum.x
                    && asset.sourceBounds.maximum.y > asset.sourceBounds.minimum.y
                    && asset.sourceBounds.maximum.z > asset.sourceBounds.minimum.z, "Invalid source bounds.");
                Require(asset.allowedRotations != null && asset.allowedRotations.Length > 0, "No allowed rotations.");
                var rotations = new HashSet<int>();
                foreach (int yaw in asset.allowedRotations)
                    Require((yaw == 0 || yaw == 90 || yaw == 180 || yaw == 270) && rotations.Add(yaw), "Invalid rotations.");
                float w = asset.dimensionsMeters.x, h = asset.dimensionsMeters.y, d = asset.dimensionsMeters.z;
                float ow = asset.placement.openingWidthMeters, oh = asset.placement.openingHeightMeters;
                Require(ow > 0 && ow < w && oh > 0 && oh < h, "Opening exceeds frame bounds.");
                Require(asset.clearance != null, "Missing clearance.");
                Positive(asset.clearance.sizeMeters, "clearance"); Finite(asset.clearance.centerMeters);
                Require(Near(asset.clearance.centerMeters.x, 0) && Near(asset.clearance.centerMeters.y, oh / 2)
                    && Near(asset.clearance.centerMeters.z, 0) && Near(asset.clearance.sizeMeters.x, ow)
                    && Near(asset.clearance.sizeMeters.y, oh) && asset.clearance.sizeMeters.z >= d, "Clearance differs from doorway opening.");
                Require(asset.collision != null && asset.collision.mode == "boxes"
                    && asset.collision.boxes != null && asset.collision.boxes.Length > 0, "Missing collision proxies.");
                foreach (CatalogBox box in asset.collision.boxes)
                {
                    Require(box != null, "Null collision box."); Positive(box.sizeMeters, "collision"); Finite(box.centerMeters);
                    Require(Math.Abs(box.centerMeters.x) + box.sizeMeters.x / 2 <= w / 2 + Epsilon
                        && box.centerMeters.y - box.sizeMeters.y / 2 >= -Epsilon
                        && box.centerMeters.y + box.sizeMeters.y / 2 <= h + Epsilon
                        && Math.Abs(box.centerMeters.z) + box.sizeMeters.z / 2 <= d / 2 + Epsilon,
                        "Collision proxy exceeds normalized bounds.");
                    CatalogVector c = box.centerMeters, s = box.sizeMeters, cc = asset.clearance.centerMeters, cs = asset.clearance.sizeMeters;
                    Require(!(Math.Abs(c.x-cc.x) < (s.x+cs.x)/2-Epsilon
                        && Math.Abs(c.y-cc.y) < (s.y+cs.y)/2-Epsilon
                        && Math.Abs(c.z-cc.z) < (s.z+cs.z)/2-Epsilon), "Collision proxy blocks opening.");
                }
                Require(asset.sockets != null && asset.sockets.Length == 2, "Doorway needs front and back sockets.");
                var sockets = new HashSet<string>(StringComparer.Ordinal);
                foreach (CatalogSocket socket in asset.sockets)
                {
                    Require(socket != null && sockets.Add(socket.id), "Null or duplicate socket.");
                    Require(socket.id == "front" || socket.id == "back", "Unsupported socket.");
                    Finite(socket.positionMeters); Finite(socket.forward);
                    float sign = socket.id == "front" ? 1 : -1;
                    Require(Near(socket.positionMeters.x, 0) && Near(socket.positionMeters.y, 0)
                        && Near(socket.positionMeters.z, sign*d/2) && Near(socket.forward.x, 0)
                        && Near(socket.forward.y, 0) && Near(socket.forward.z, sign)
                        && Near(socket.widthMeters, ow) && Near(socket.heightMeters, oh), "Invalid socket position, facing, or clearance.");
                }
            }
        }

        public static string ResolvePath(string assetRoot, string relativePath)
        {
            Require(!string.IsNullOrWhiteSpace(relativePath) && !Path.IsPathRooted(relativePath)
                && !relativePath.Contains("\\") && !relativePath.Contains(":"), "Unsafe asset path.");
            foreach (string part in relativePath.Split('/'))
                Require(part.Length > 0 && part != "." && part != "..", "Unsafe asset path.");
            return Path.Combine(Path.GetFullPath(assetRoot), relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void ValidateReusable(AssetCatalog catalog)
        {
            Require(!string.IsNullOrWhiteSpace(catalog.contentVersion), "Missing content version.");
            Require(catalog.coordinateSystem == "right-handed-y-up", "Unsupported coordinate system.");
            Require(catalog.assets != null && catalog.assets.Length > 0, "Catalog is empty.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CatalogAsset asset in catalog.assets)
            {
                Require(asset != null && !string.IsNullOrWhiteSpace(asset.id) && ids.Add(asset.id), "Missing or duplicate asset ID.");
                Require(!string.IsNullOrEmpty(asset.path) && !Path.IsPathRooted(asset.path)
                    && !asset.path.Contains("\\") && Array.IndexOf(asset.path.Split('/'), "..") < 0
                    && asset.path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase), "Unsafe asset path.");
                Require(Enum.TryParse(asset.purpose, true, out DungeonAssetRole role) && Enum.IsDefined(typeof(DungeonAssetRole), role) && role != DungeonAssetRole.Unspecified,
                    "Unknown asset role: " + asset.purpose);
                Positive(asset.dimensionsMeters, "dimensions");
                Require(asset.pivot == "center" || asset.pivot == "bottom-center", "Unsupported pivot.");
                Require(asset.sha256 != null && Regex.IsMatch(asset.sha256, "^[0-9a-f]{64}$"), "Invalid asset hash.");
                Require(asset.triangles > 0, "Missing measured geometry.");
                Require(asset.allowedRotations != null && asset.allowedRotations.Length > 0, "No allowed rotations.");
                foreach (int rotation in asset.allowedRotations)
                    Require(rotation == 0 || rotation == 90 || rotation == 180 || rotation == 270, "Invalid rotation.");
                Require(asset.fitMode == "stretch" || asset.fitMode == "native" || asset.fitMode == "repeat", "Unsupported fit mode.");
                if (asset.collision != null)
                {
                    Require(asset.collision.mode == "none" || asset.collision.mode == "boxes", "Unsupported collision mode.");
                    if (asset.collision.mode == "boxes")
                    {
                        Require(asset.collision.boxes != null && asset.collision.boxes.Length > 0, "Missing collision proxies.");
                        foreach (CatalogBox box in asset.collision.boxes)
                        { Require(box != null, "Null collision box."); Finite(box.centerMeters); Positive(box.sizeMeters, "collision"); }
                    }
                }
                if (asset.lighting != null && asset.lighting.enabled)
                {
                    Require(role == DungeonAssetRole.Light, "Light definition requires a Light role.");
                    Finite(asset.lighting.socketMeters); Finite(asset.lighting.color);
                    Require(asset.lighting.intensity > 0 && asset.lighting.rangeMeters > 0
                        && asset.lighting.coneDegrees > 0 && asset.lighting.coneDegrees < 180
                        && asset.lighting.emissionIntensity > 0, "Invalid lighting definition.");
                }
            }
        }

        public static void VerifyFiles(AssetCatalog catalog, string assetRoot)
        {
            Validate(catalog);
            foreach (CatalogAsset asset in catalog.assets)
            {
                string path = ResolvePath(assetRoot, asset.path);
                using (var stream = File.OpenRead(path))
                using (var hash = SHA256.Create())
                {
                    string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    Require(actual == asset.sha256, "Asset hash mismatch: " + asset.id);
                }
            }
        }

        private static bool Near(float a, float b) => Math.Abs(a-b) <= Epsilon;
        private static void Finite(CatalogVector value)
        {
            Require(value != null && Valid(value.x) && Valid(value.y) && Valid(value.z), "Nonfinite vector.");
        }
        private static bool Valid(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void Positive(CatalogVector value, string name)
        {
            Finite(value); Require(value.x > 0 && value.y > 0 && value.z > 0, "Nonpositive " + name + ".");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidDataException(message);
        }
    }
}
