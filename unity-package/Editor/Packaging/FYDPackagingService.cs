using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FYD.UnityPublisher.Editor.Configuration;
using FYD.UnityPublisher.Editor.Models;
using FYD.UnityPublisher.Editor.Validation;
using UnityEngine;

namespace FYD.UnityPublisher.Editor.Packaging
{
    /// <summary>Creates a deterministic WebGL archive and its sidecar deployment manifest.</summary>
    public static class FYDPackagingService
    {
        private static readonly DateTimeOffset DeterministicTimestamp = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public static async Task<FYDPackagingResult> PackageAsync(
            string buildDirectory,
            FYDPublisherSettings settings,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!FYDPublisherValidation.IsValidAppId(settings.appId))
            {
                throw new InvalidOperationException("App ID chỉ được gồm chữ thường, số và dấu gạch ngang.");
            }

            FYDValidationResult validation = await Task.Run(
                () => FYDPublisherValidation.ValidateWebGLBuild(buildDirectory), cancellationToken);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(string.Join("\n", validation.Errors));
            }

            string buildRoot = Path.GetFullPath(buildDirectory);
            string releaseId = CreateReleaseId(settings.appId, settings.releaseVersion, DateTime.UtcNow);
            string outputRoot = Path.GetFullPath(Path.Combine(settings.packageOutputFolder, settings.appId, releaseId));
            Directory.CreateDirectory(outputRoot);
            string archivePath = Path.Combine(outputRoot, releaseId + ".zip");
            string manifestPath = Path.Combine(outputRoot, "fyd-deploy-manifest.json");

            List<string> files = await Task.Run(
                () => Directory.EnumerateFiles(buildRoot, "*", SearchOption.AllDirectories)
                    .Where(path => FYDPublisherValidation.IsArchiveCandidate(FYDPublisherValidation.GetRelativePath(buildRoot, path)))
                    .OrderBy(path => FYDPublisherValidation.GetRelativePath(buildRoot, path), StringComparer.Ordinal)
                    .ToList(), cancellationToken);

            var manifestFiles = new List<FYDManifestFile>();
            List<string> importantFiles = files
                .Where(path => FYDPublisherValidation.IsImportantWebGLFile(FYDPublisherValidation.GetRelativePath(buildRoot, path)))
                .ToList();
            for (int index = 0; index < importantFiles.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = importantFiles[index];
                manifestFiles.Add(new FYDManifestFile
                {
                    path = FYDPublisherValidation.GetRelativePath(buildRoot, path),
                    size = new FileInfo(path).Length,
                    sha256 = await FYDHashUtility.ComputeFileSha256Async(path, cancellationToken)
                });
                progress?.Report(0.2f * (index + 1) / Math.Max(1, importantFiles.Count));
            }

            await Task.Run(() => CreateArchive(buildRoot, archivePath, files, progress, cancellationToken), cancellationToken);
            string archiveHash = await FYDHashUtility.ComputeFileSha256Async(archivePath, cancellationToken);
            FYDGitInfo git = await Task.Run(
                () => FYDGitMetadata.Read(Directory.GetCurrentDirectory()), cancellationToken);
            var manifest = new FYDDeployManifest
            {
                appId = settings.appId,
                displayName = settings.displayName,
                releaseVersion = settings.releaseVersion,
                releaseId = releaseId,
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                developmentBuild = settings.developmentBuild,
                compression = settings.compression.ToString().ToLowerInvariant(),
                gitCommit = git.Commit,
                gitBranch = git.Branch,
                archiveSha256 = archiveHash,
                archiveSize = new FileInfo(archivePath).Length,
                releaseNotes = settings.releaseNotes ?? string.Empty,
                files = manifestFiles.ToArray()
            };

            string manifestJson = JsonUtility.ToJson(manifest, true);
            await Task.Run(() => File.WriteAllText(manifestPath, manifestJson), cancellationToken);
            progress?.Report(1f);
            return new FYDPackagingResult
            {
                BuildDirectory = buildRoot,
                OutputDirectory = outputRoot,
                ArchivePath = archivePath,
                ManifestPath = manifestPath,
                Manifest = manifest
            };
        }

        public static string CreateReleaseId(string appId, string releaseVersion, DateTime utcNow)
        {
            string version = string.IsNullOrWhiteSpace(releaseVersion) ? "0.0.0" : releaseVersion.Trim();
            version = new string(version.Select(character => char.IsLetterOrDigit(character) || character == '.' || character == '-'
                ? char.ToLowerInvariant(character)
                : '-').ToArray()).Trim('-');
            return appId + "-" + version + "-" + utcNow.ToUniversalTime().ToString("yyyyMMdd-HHmmss");
        }

        private static void CreateArchive(
            string buildRoot,
            string archivePath,
            IReadOnlyList<string> files,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            string temporaryPath = archivePath + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            try
            {
                using (FileStream output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (var archive = new ZipArchive(output, ZipArchiveMode.Create, false))
                {
                    for (int index = 0; index < files.Count; index++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        string sourcePath = files[index];
                        string relativePath = FYDPublisherValidation.GetRelativePath(buildRoot, sourcePath);
                        ZipArchiveEntry entry = archive.CreateEntry(relativePath, System.IO.Compression.CompressionLevel.Optimal);
                        entry.LastWriteTime = DeterministicTimestamp;
                        using (Stream input = File.OpenRead(sourcePath))
                        using (Stream target = entry.Open())
                        {
                            input.CopyTo(target, 1024 * 1024);
                        }
                        progress?.Report(0.2f + (0.65f * (index + 1) / Math.Max(1, files.Count)));
                    }
                }

                if (File.Exists(archivePath)) File.Delete(archivePath);
                File.Move(temporaryPath, archivePath);
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }
    }
}
