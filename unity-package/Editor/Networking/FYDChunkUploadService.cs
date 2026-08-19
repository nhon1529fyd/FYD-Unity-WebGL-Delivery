using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FYD.UnityPublisher.Editor.Models;
using FYD.UnityPublisher.Editor.Packaging;

namespace FYD.UnityPublisher.Editor.Networking
{
    /// <summary>Sequential chunk uploader with server-side resume and bounded retry.</summary>
    public sealed class FYDChunkUploadService
    {
        private const int MaxAttempts = 3;
        private readonly FYDPublisherClient _client;

        public FYDChunkUploadService(FYDPublisherClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<FYDUploadSession> UploadAsync(
            FYDPackagingResult package,
            int requestedChunkSize,
            FYDUploadSession resumableSession,
            IProgress<float> progress,
            IProgress<string> log,
            CancellationToken cancellationToken)
        {
            if (package?.Manifest == null) throw new ArgumentNullException(nameof(package));
            FYDUploadSession session = await ResolveSessionAsync(package, requestedChunkSize, resumableSession, log, cancellationToken);
            FYDUploadStatusResponse status = await _client.GetUploadStatusAsync(session.UploadId, cancellationToken);
            var missing = new HashSet<int>(status.data.missingChunks ?? Array.Empty<int>());
            long archiveSize = new FileInfo(package.ArchivePath).Length;
            long completedBytes = Enumerable.Range(0, session.TotalChunks)
                .Where(index => !missing.Contains(index))
                .Sum(index => Math.Min((long)session.ChunkSize, archiveSize - ((long)index * session.ChunkSize)));

            using (FileStream stream = new FileStream(package.ArchivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                foreach (int index in missing.OrderBy(value => value))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int length = (int)Math.Min(session.ChunkSize, archiveSize - ((long)index * session.ChunkSize));
                    byte[] buffer = new byte[length];
                    stream.Seek((long)index * session.ChunkSize, SeekOrigin.Begin);
                    int offset = 0;
                    while (offset < length)
                    {
                        int read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken);
                        if (read == 0) throw new EndOfStreamException("Archive kết thúc trước chunk dự kiến.");
                        offset += read;
                    }

                    string chunkHash = FYDHashUtility.ComputeBytesSha256(buffer);
                    await UploadWithRetryAsync(package.Manifest.appId, session, index, buffer, chunkHash, log, cancellationToken);
                    completedBytes += length;
                    progress?.Report(archiveSize == 0 ? 1f : (float)completedBytes / archiveSize);
                }
            }
            progress?.Report(1f);
            log?.Report("Đã upload đủ " + session.TotalChunks + " chunk. Release đang chờ bước finalize.");
            return session;
        }

        private async Task<FYDUploadSession> ResolveSessionAsync(
            FYDPackagingResult package,
            int requestedChunkSize,
            FYDUploadSession resumable,
            IProgress<string> log,
            CancellationToken cancellationToken)
        {
            if (resumable != null && File.Exists(resumable.ArchivePath) &&
                string.Equals(resumable.ArchiveSha256, package.Manifest.archiveSha256, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _client.GetUploadStatusAsync(resumable.UploadId, cancellationToken);
                    log?.Report("Tiếp tục upload session " + resumable.UploadId + ".");
                    return resumable;
                }
                catch (FYDPublisherApiException exception) when (exception.StatusCode == 404 || exception.StatusCode == 410)
                {
                    log?.Report("Upload session cũ đã hết hạn; tạo session mới.");
                }
            }

            long archiveSize = new FileInfo(package.ArchivePath).Length;
            int chunkSize = Math.Max(1024 * 1024, Math.Min(20 * 1024 * 1024, requestedChunkSize));
            int totalChunks = (int)Math.Ceiling(archiveSize / (double)chunkSize);
            var request = new FYDUploadInitRequest
            {
                appId = package.Manifest.appId,
                displayName = package.Manifest.displayName,
                releaseId = package.Manifest.releaseId,
                releaseVersion = package.Manifest.releaseVersion,
                archiveSize = archiveSize,
                archiveSha256 = package.Manifest.archiveSha256,
                chunkSize = chunkSize,
                totalChunks = totalChunks,
                manifest = package.Manifest
            };
            FYDUploadInitResponse response = await _client.InitializeUploadAsync(request, cancellationToken);
            log?.Report("Đã tạo upload session " + response.data.uploadId + ".");
            return new FYDUploadSession
            {
                UploadId = response.data.uploadId,
                ArchivePath = package.ArchivePath,
                ArchiveSha256 = package.Manifest.archiveSha256,
                ChunkSize = response.data.chunkSize,
                TotalChunks = response.data.totalChunks,
                ExpiresAt = response.data.expiresAt
            };
        }

        private async Task UploadWithRetryAsync(
            string appId,
            FYDUploadSession session,
            int index,
            byte[] bytes,
            string hash,
            IProgress<string> log,
            CancellationToken cancellationToken)
        {
            Exception lastException = null;
            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await _client.UploadChunkAsync(appId, session.UploadId, index, session.TotalChunks, bytes, hash, cancellationToken);
                    log?.Report("Chunk " + (index + 1) + "/" + session.TotalChunks + " đã upload.");
                    return;
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    lastException = exception;
                    if (attempt == MaxAttempts) break;
                    int delayMilliseconds = 500 * (1 << (attempt - 1));
                    log?.Report("Chunk " + index + " lỗi, thử lại lần " + (attempt + 1) + ".");
                    await Task.Delay(delayMilliseconds, cancellationToken);
                }
            }
            throw new InvalidOperationException("Upload chunk " + index + " thất bại sau " + MaxAttempts + " lần.", lastException);
        }
    }
}
