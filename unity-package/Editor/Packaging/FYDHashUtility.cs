using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FYD.UnityPublisher.Editor.Packaging
{
    /// <summary>Streaming SHA-256 helpers used by manifests and chunk uploads.</summary>
    public static class FYDHashUtility
    {
        public static Task<string> ComputeFileSha256Async(string path, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(stream);
                    cancellationToken.ThrowIfCancellationRequested();
                    return ToHex(hash);
                }
            }, cancellationToken);
        }

        public static string ComputeBytesSha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return ToHex(sha.ComputeHash(bytes));
            }
        }

        public static string ComputeTextSha256(string value)
        {
            return ComputeBytesSha256(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
