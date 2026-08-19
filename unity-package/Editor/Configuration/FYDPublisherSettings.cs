using System;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;

namespace FYD.UnityPublisher.Editor.Configuration
{
    public enum FYDCompressionMethod
    {
        Disabled,
        Gzip,
        Brotli
    }

    /// <summary>Project-scoped, non-secret publishing configuration.</summary>
    [FilePath("ProjectSettings/FYDUnityPublisherSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class FYDPublisherSettings : ScriptableSingleton<FYDPublisherSettings>
    {
        public const int DefaultChunkSizeMiB = 5;

        public string appId = "imperial-bloodline";
        public string displayName = "Imperial Bloodline";
        public string websiteUrl = string.Empty;
        public string wordpressUsername = string.Empty;
        public bool rememberCredential;
        public string buildOutputFolder = "Builds/WebGL/imperial-bloodline/releases";
        public string packageOutputFolder = "Builds/FYDPublisher";
        public string releaseVersion = "0.1.0";
        public string releaseNotes = string.Empty;
        public bool developmentBuild;
        public FYDCompressionMethod compression = FYDCompressionMethod.Brotli;
        public int chunkSizeMiB = DefaultChunkSizeMiB;
        public int requestTimeoutSeconds = 120;

        public void Persist()
        {
            chunkSizeMiB = Math.Max(1, Math.Min(20, chunkSizeMiB));
            requestTimeoutSeconds = Math.Max(15, Math.Min(600, requestTimeoutSeconds));
            Save(true);
        }

        public string LoadCredential()
        {
            return rememberCredential ? EditorPrefs.GetString(GetCredentialKey(), string.Empty) : string.Empty;
        }

        public void StoreCredential(string password)
        {
            if (!rememberCredential || string.IsNullOrEmpty(password))
            {
                ForgetCredential();
                return;
            }

            EditorPrefs.SetString(GetCredentialKey(), password);
        }

        public void ForgetCredential()
        {
            EditorPrefs.DeleteKey(GetCredentialKey());
        }

        private string GetCredentialKey()
        {
            string identity = (websiteUrl ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                              (appId ?? string.Empty).Trim().ToLowerInvariant() + "|" +
                              (wordpressUsername ?? string.Empty).Trim().ToLowerInvariant();
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(identity));
                return "FYD.UnityPublisher.Credential." + BitConverter.ToString(digest).Replace("-", string.Empty);
            }
        }
    }
}
