using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FYD.WebGLTools
{
    internal static class FYDWebGLBuilder
    {
        [MenuItem("FYD/WebGL/Build Production", priority = 120)]
        public static void BuildProductionMenu()
        {
            FYDWebGLSettings settings = FYDWebGLSettings.LoadOrCreate();
            settings.preset = FYDWebGLPreset.ProductionSharedHosting;
            settings.Persist();
            Build(settings, false);
        }

        [MenuItem("FYD/WebGL/Build & Run Development", priority = 121)]
        public static void BuildDevelopmentMenu()
        {
            FYDWebGLSettings settings = FYDWebGLSettings.LoadOrCreate();
            settings.preset = FYDWebGLPreset.Development;
            settings.Persist();
            Build(settings, true);
        }

        [MenuItem("FYD/WebGL/Publish Stable Deploy From Last Release", priority = 122)]
        public static void PublishLastReleaseMenu()
        {
            PublishLastSuccessfulRelease(true);
        }

        public static bool PublishLastSuccessfulRelease(bool showDialog)
        {
            FYDWebGLSettings settings = FYDWebGLSettings.LoadOrCreate();
            string releasePath = string.IsNullOrWhiteSpace(settings.LastBuildPath)
                ? string.Empty
                : Path.GetFullPath(settings.LastBuildPath);
            if (string.IsNullOrWhiteSpace(releasePath) || !Directory.Exists(releasePath))
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog(
                        "Không có release",
                        "Chưa tìm thấy release gần nhất để tạo deploy.",
                        "Đóng");
                }
                return false;
            }

            if (!FYDWebGLBuildProcessor.EnsureServerFiles(
                    releasePath, settings, out string serverFileError))
            {
                Debug.LogError("FYD WebGL Tools: Không tạo được stable deploy: " + serverFileError);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Không tạo được deploy", serverFileError, "Đóng");
                }
                return false;
            }

            if (!TryPublishStableDeploy(
                    settings, releasePath, out string deployPath, out string publishError))
            {
                Debug.LogError("FYD WebGL Tools: Không tạo được stable deploy: " + publishError);
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("Không tạo được deploy", publishError, "Đóng");
                }
                return false;
            }

            settings.RecordSuccessfulBuild(releasePath, deployPath);
            Debug.Log("FYD WebGL Tools: Đã tạo stable deploy tại " + deployPath);
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "Đã tạo deploy",
                    $"Release:\n{releasePath}\n\nThư mục upload cố định:\n{deployPath}",
                    "Đóng");
            }
            return true;
        }

        public static BuildReport Build(FYDWebGLSettings settings, bool forceRunPlayer)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (EditorApplication.isCompiling)
            {
                EditorUtility.DisplayDialog("Unity đang compile", "Hãy chờ Unity compile xong rồi build lại.", "Đóng");
                return null;
            }

            if (!FYDWebGLProjectExtensions.PrepareForBuild(out string preparationError))
            {
                if (!string.IsNullOrWhiteSpace(preparationError))
                {
                    EditorUtility.DisplayDialog("Đã dừng build", preparationError, "Đóng");
                }
                return null;
            }

            var projectChecks = new List<FYDCheckItem>();
            FYDWebGLProjectExtensions.AppendChecks(projectChecks);
            List<FYDCheckItem> projectErrors = projectChecks
                .Where(item => item.Status == FYDCheckStatus.Error)
                .ToList();
            if (settings.strictValidationBeforeBuild && projectErrors.Count > 0)
            {
                string projectErrorText = string.Join(
                    "\n",
                    projectErrors.Select(item => "- " + item.Title + ": " + item.Detail));
                EditorUtility.DisplayDialog("Arena WebGL chưa sẵn sàng", projectErrorText, "Đóng");
                return null;
            }

            try
            {
                FYDWebGLConfigurator.ApplyPreset(settings);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Không thể cấu hình WebGL", exception.Message, "Đóng");
                return null;
            }

            List<FYDCheckItem> checks = FYDWebGLValidation.Run(settings);
            if (settings.strictValidationBeforeBuild && FYDWebGLValidation.HasBlockingErrors(checks))
            {
                string errors = string.Join(
                    "\n",
                    checks.Where(item => item.Status == FYDCheckStatus.Error).Select(item => "• " + item.Title + ": " + item.Detail)
                );
                EditorUtility.DisplayDialog("Checklist chưa đạt", errors, "Đóng");
                return null;
            }

            if (settings.autoIncrementPatchVersion && settings.preset == FYDWebGLPreset.ProductionSharedHosting)
            {
                FYDWebGLConfigurator.TryIncrementPatchVersion(out string newVersion);
                Debug.Log("FYD WebGL Tools: Đã tăng Product Version thành " + newVersion);
            }

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                EditorUtility.DisplayDialog("Không có scene", "Hãy thêm ít nhất một scene vào Build Settings/Build Profiles.", "Đóng");
                return null;
            }

            string outputPath = Path.GetFullPath(settings.GetOutputFolder());
            if (settings.cleanOutputBeforeBuild && Directory.Exists(outputPath))
            {
                try
                {
                    Directory.Delete(outputPath, true);
                }
                catch (Exception exception)
                {
                    EditorUtility.DisplayDialog(
                        "Không xóa được build cũ",
                        outputPath + "\n\n" + exception.Message,
                        "Đóng"
                    );
                    return null;
                }
            }

            Directory.CreateDirectory(outputPath);

            BuildOptions options = BuildOptions.None;
            bool runPlayer = forceRunPlayer ||
                             (settings.preset == FYDWebGLPreset.Development && settings.buildAndRunAfterDevelopmentBuild);
            if (runPlayer)
            {
                options |= BuildOptions.AutoRunPlayer;
            }

            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = options
            };

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(buildOptions);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Build WebGL thất bại", exception.Message, "Đóng");
                return null;
            }

            if (report.summary.result == BuildResult.Succeeded)
            {
                if (!FYDWebGLBuildProcessor.EnsureServerFiles(
                        outputPath, settings, out string serverFileError))
                {
                    settings.RecordSuccessfulBuild(outputPath, string.Empty);
                    Debug.LogError("FYD WebGL Tools: Release đã build nhưng thiếu file server: " + serverFileError);
                    EditorUtility.DisplayDialog(
                        "Release đã build, hậu xử lý thất bại",
                        $"Release vẫn còn nguyên tại:\n{outputPath}\n\n{serverFileError}",
                        "Đóng");
                    return report;
                }

                string deployPath = string.Empty;
                if (settings.createStableDeployCopy &&
                    !TryPublishStableDeploy(settings, outputPath, out deployPath, out string publishError))
                {
                    settings.RecordSuccessfulBuild(outputPath, string.Empty);
                    Debug.LogError("FYD WebGL Tools: Release đã build nhưng không tạo được deploy: " + publishError);
                    EditorUtility.DisplayDialog(
                        "Release đã build, deploy thất bại",
                        $"Release vẫn còn nguyên tại:\n{outputPath}\n\n{publishError}",
                        "Đóng");
                    return report;
                }

                settings.RecordSuccessfulBuild(outputPath, deployPath);
                Debug.Log($"FYD WebGL Tools: Build thành công: {outputPath} ({FormatBytes(report.summary.totalSize)})");

                if (!runPlayer)
                {
                    string deployDetail = string.IsNullOrWhiteSpace(deployPath)
                        ? "Không tạo bản deploy cố định."
                        : $"Thư mục upload cố định:\n{deployPath}";
                    EditorUtility.DisplayDialog(
                        "Build WebGL thành công",
                        $"Release:\n{outputPath}\n\n{deployDetail}\n\nDung lượng output: {FormatBytes(report.summary.totalSize)}",
                        "Đóng"
                    );
                }
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Build WebGL chưa thành công",
                    $"Kết quả: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}",
                    "Đóng"
                );
            }

            return report;
        }

        private static bool TryPublishStableDeploy(
            FYDWebGLSettings settings,
            string releasePath,
            out string deployPath,
            out string error)
        {
            deployPath = Path.GetFullPath(settings.GetStableDeployFolder());
            error = string.Empty;

            string moduleRoot = Path.GetFullPath(settings.GetModuleFolder());
            string releaseRoot = Path.GetFullPath(releasePath);
            string stagingPath = deployPath + ".staging";
            string backupPath = deployPath + ".previous";

            if (!IsStrictChild(moduleRoot, releaseRoot) ||
                !IsStrictChild(moduleRoot, deployPath) ||
                !IsStrictChild(moduleRoot, stagingPath) ||
                !IsStrictChild(moduleRoot, backupPath) ||
                IsStrictChild(releaseRoot, deployPath) ||
                IsStrictChild(deployPath, releaseRoot))
            {
                error = "Đường dẫn release/deploy không an toàn hoặc đang lồng vào nhau.";
                return false;
            }

            if (!HasRequiredWebGLFiles(releaseRoot, out error))
            {
                return false;
            }

            bool movedPreviousDeploy = false;
            try
            {
                DeleteDirectoryIfPresent(stagingPath);
                CopyDirectory(releaseRoot, stagingPath);
                if (!HasRequiredWebGLFiles(stagingPath, out error))
                {
                    DeleteDirectoryIfPresent(stagingPath);
                    return false;
                }

                DeleteDirectoryIfPresent(backupPath);
                if (Directory.Exists(deployPath))
                {
                    Directory.Move(deployPath, backupPath);
                    movedPreviousDeploy = true;
                }

                Directory.Move(stagingPath, deployPath);
                File.WriteAllText(
                    Path.Combine(deployPath, "FYD-DEPLOY-INFO.txt"),
                    $"FYD WEBGL STABLE DEPLOY\n" +
                    $"Product Version: {PlayerSettings.bundleVersion}\n" +
                    $"Published UTC: {DateTime.UtcNow:O}\n" +
                    $"Source release: {releaseRoot.Replace('\\', '/')}\n" +
                    "Upload the contents of this folder to the module's stable public URL.\n");

                if (movedPreviousDeploy)
                {
                    try
                    {
                        DeleteDirectoryIfPresent(backupPath);
                    }
                    catch (Exception cleanupException)
                    {
                        Debug.LogWarning(
                            "FYD WebGL Tools: Không xóa được deploy.previous: " +
                            cleanupException.Message);
                    }
                }

                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                try
                {
                    DeleteDirectoryIfPresent(stagingPath);
                    if (movedPreviousDeploy && Directory.Exists(backupPath))
                    {
                        DeleteDirectoryIfPresent(deployPath);
                        Directory.Move(backupPath, deployPath);
                    }
                }
                catch (Exception rollbackException)
                {
                    error += "\nKhôi phục deploy cũ cũng thất bại: " + rollbackException.Message;
                }
                return false;
            }
        }

        private static bool HasRequiredWebGLFiles(string root, out string error)
        {
            string buildFolder = Path.Combine(root, "Build");
            string buildHtaccess = Path.Combine(buildFolder, ".htaccess");
            if (!File.Exists(Path.Combine(root, "index.html")) ||
                !Directory.Exists(buildFolder) ||
                !File.Exists(buildHtaccess))
            {
                error = "Release thiếu index.html, thư mục Build hoặc Build/.htaccess.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static void CopyDirectory(string sourceRoot, string destinationRoot)
        {
            string normalizedSource = Path.GetFullPath(sourceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string sourcePrefix = normalizedSource + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(destinationRoot);

            foreach (string directory in Directory.GetDirectories(
                         normalizedSource, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(sourcePrefix.Length);
                Directory.CreateDirectory(Path.Combine(destinationRoot, relative));
            }

            foreach (string file in Directory.GetFiles(
                         normalizedSource, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(sourcePrefix.Length);
                string destination = Path.Combine(destinationRoot, relative);
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }
                File.Copy(file, destination, true);
            }
        }

        private static bool IsStrictChild(string root, string candidate)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return normalizedCandidate.StartsWith(
                normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static void DeleteDirectoryIfPresent(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public static string FormatBytes(ulong bytes)
        {
            double value = bytes;
            string[] units = { "B", "KB", "MB", "GB" };
            int index = 0;
            while (value >= 1024 && index < units.Length - 1)
            {
                value /= 1024;
                index++;
            }
            return $"{value:0.##} {units[index]}";
        }

        public static string FormatBytes(long bytes)
        {
            return FormatBytes((ulong)Math.Max(0, bytes));
        }
    }
}
