using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FYD.UnityPublisher.Editor.Configuration;
using FYD.UnityPublisher.Editor.Models;
using FYD.UnityPublisher.Editor.Packaging;
using FYD.UnityPublisher.Editor.Validation;
using NUnit.Framework;

namespace FYD.UnityPublisher.Editor.Tests
{
    public sealed class FYDPublisherValidationTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "fyd-publisher-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Build"));
            File.WriteAllText(Path.Combine(_temporaryDirectory, "index.html"), "<html></html>");
            File.WriteAllText(Path.Combine(_temporaryDirectory, "Build", "game.loader.js"), "loader");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
        }

        [TestCase("mind-kingdom", true)]
        [TestCase("imperial-bloodline-2", true)]
        [TestCase("Mind-Kingdom", false)]
        [TestCase("../game", false)]
        [TestCase("game_1", false)]
        public void AppIdValidationMatchesContract(string value, bool expected)
        {
            Assert.AreEqual(expected, FYDPublisherValidation.IsValidAppId(value));
        }

        [Test]
        public void WebGLValidationAcceptsMinimalBuild()
        {
            FYDValidationResult result = FYDPublisherValidation.ValidateWebGLBuild(_temporaryDirectory);
            Assert.IsTrue(result.IsValid, string.Join("\n", result.Errors));
        }

        [Test]
        public void WebGLValidationRejectsDoubleExtensionPhp()
        {
            File.WriteAllText(Path.Combine(_temporaryDirectory, "Build", "image.png.php"), "<?php");
            FYDValidationResult result = FYDPublisherValidation.ValidateWebGLBuild(_temporaryDirectory);
            Assert.IsFalse(result.IsValid);
        }

        [Test]
        public void ServerConfigIsExcludedFromArchiveCandidates()
        {
            Assert.IsFalse(FYDPublisherValidation.IsArchiveCandidate("Build/.htaccess"));
            Assert.IsFalse(FYDPublisherValidation.IsArchiveCandidate("web.config"));
        }

        [Test]
        public void Sha256MatchesKnownVector()
        {
            Assert.AreEqual(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                FYDHashUtility.ComputeTextSha256("abc"));
        }

        [Test]
        public void ReleaseIdUsesUtcTimestamp()
        {
            string value = FYDPackagingService.CreateReleaseId(
                "mind-kingdom", "1.2.5", new DateTime(2026, 7, 29, 5, 13, 0, DateTimeKind.Utc));
            Assert.AreEqual("mind-kingdom-1.2.5-20260729-051300", value);
        }

        [Test]
        public async Task PackagingCreatesHashVerifiedArchiveWithoutServerConfig()
        {
            File.WriteAllText(Path.Combine(_temporaryDirectory, ".htaccess"), "deny");
            FYDPublisherSettings settings = FYDPublisherSettings.instance;
            settings.appId = "test-game";
            settings.displayName = "Test Game";
            settings.releaseVersion = "1.0.0";
            settings.packageOutputFolder = Path.Combine(_temporaryDirectory, "packages");

            FYDPackagingResult result = await FYDPackagingService.PackageAsync(
                _temporaryDirectory, settings, null, CancellationToken.None);

            Assert.IsTrue(File.Exists(result.ArchivePath));
            Assert.AreEqual(
                result.Manifest.archiveSha256,
                await FYDHashUtility.ComputeFileSha256Async(result.ArchivePath, CancellationToken.None));
            using (ZipArchive archive = ZipFile.OpenRead(result.ArchivePath))
            {
                string[] names = archive.Entries.Select(entry => entry.FullName).ToArray();
                CollectionAssert.Contains(names, "index.html");
                CollectionAssert.DoesNotContain(names, ".htaccess");
            }
        }
    }
}
