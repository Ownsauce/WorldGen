using System;
using System.Security.Cryptography;
using System.Text;

namespace WorldGen.Identity
{
    public static class StableWorldId
    {
        public static string Create(string scope, params string[] logicalPath)
        {
            if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("Scope is required.", nameof(scope));
            var canonical = new StringBuilder(scope.Trim());
            for (int i = 0; logicalPath != null && i < logicalPath.Length; i++)
            {
                string segment = logicalPath[i];
                if (string.IsNullOrWhiteSpace(segment)) throw new ArgumentException("Path segments cannot be empty.", nameof(logicalPath));
                if (segment.IndexOf('/') >= 0) throw new ArgumentException("Path segments cannot contain '/'.", nameof(logicalPath));
                canonical.Append('/').Append(segment.Trim());
            }
            byte[] hash;
            using (SHA256 sha = SHA256.Create()) hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return canonical + "-" + Hex(hash, 8);
        }

        internal static string Hex(byte[] bytes, int count)
        {
            const string digits = "0123456789abcdef";
            var result = new char[count * 2];
            for (int i = 0; i < count; i++)
            {
                result[i * 2] = digits[bytes[i] >> 4];
                result[i * 2 + 1] = digits[bytes[i] & 15];
            }
            return new string(result);
        }
    }
}
