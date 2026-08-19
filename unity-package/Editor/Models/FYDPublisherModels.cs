using System;

namespace FYD.UnityPublisher.Editor.Models
{
    [Serializable]
    public sealed class FYDManifestFile
    {
        public string path;
        public long size;
        public string sha256;
    }

    [Serializable]
    public sealed class FYDDeployManifest
    {
        public int schemaVersion = 1;
        public string appId;
        public string displayName;
        public string releaseVersion;
        public string releaseId;
        public string builtAtUtc;
        public string unityVersion;
        public string buildTarget = "WebGL";
        public bool developmentBuild;
        public string compression;
        public string gitCommit;
        public string gitBranch;
        public string entryFile = "index.html";
        public string archiveSha256;
        public long archiveSize;
        public string releaseNotes;
        public FYDManifestFile[] files;
    }

    public sealed class FYDPackagingResult
    {
        public string BuildDirectory { get; set; }
        public string OutputDirectory { get; set; }
        public string ArchivePath { get; set; }
        public string ManifestPath { get; set; }
        public FYDDeployManifest Manifest { get; set; }
    }

    [Serializable]
    public sealed class FYDApiError
    {
        public string code;
        public string message;
        public string details;
    }

    [Serializable]
    public sealed class FYDStatusData
    {
        public string pluginVersion;
        public string apiVersion;
        public string user;
    }

    [Serializable]
    public sealed class FYDStatusResponse
    {
        public bool ok;
        public FYDStatusData data;
        public FYDApiError error;
        public string requestId;
    }

    [Serializable]
    public sealed class FYDUploadInitData
    {
        public string uploadId;
        public int chunkSize;
        public int totalChunks;
        public string expiresAt;
    }

    [Serializable]
    public sealed class FYDUploadInitResponse
    {
        public bool ok;
        public FYDUploadInitData data;
        public FYDApiError error;
        public string requestId;
    }

    [Serializable]
    public sealed class FYDUploadStatusData
    {
        public string uploadId;
        public int totalChunks;
        public int[] receivedChunks;
        public int[] missingChunks;
        public string expiresAt;
    }

    [Serializable]
    public sealed class FYDUploadStatusResponse
    {
        public bool ok;
        public FYDUploadStatusData data;
        public FYDApiError error;
        public string requestId;
    }

    [Serializable]
    public sealed class FYDUploadInitRequest
    {
        public string appId;
        public string displayName;
        public string releaseId;
        public string releaseVersion;
        public long archiveSize;
        public string archiveSha256;
        public int chunkSize;
        public int totalChunks;
        public FYDDeployManifest manifest;
    }

    public sealed class FYDUploadSession
    {
        public string UploadId { get; set; }
        public string ArchivePath { get; set; }
        public string ArchiveSha256 { get; set; }
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public string ExpiresAt { get; set; }
    }
}
