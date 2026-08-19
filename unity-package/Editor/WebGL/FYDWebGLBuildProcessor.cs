using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FYD.WebGLTools
{
    internal sealed class FYDWebGLBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
            {
                return;
            }

            FYDWebGLSettings settings = FYDWebGLSettings.LoadOrCreate();
            if (!settings.strictValidationBeforeBuild)
            {
                return;
            }

            List<FYDCheckItem> checks = FYDWebGLValidation.Run(settings);
            List<FYDCheckItem> errors = checks.Where(item => item.Status == FYDCheckStatus.Error).ToList();
            if (errors.Count == 0)
            {
                return;
            }

            string message = "FYD WebGL checklist chưa đạt:\n" +
                             string.Join("\n", errors.Select(item => "- " + item.Title + ": " + item.Detail));
            throw new BuildFailedException(message);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL || report.summary.totalErrors > 0)
            {
                return;
            }

            FYDWebGLSettings settings = FYDWebGLSettings.LoadOrCreate();
            string outputRoot = Path.GetFullPath(report.summary.outputPath);

            try
            {
                if (!EnsureServerFiles(outputRoot, settings, out string serverFileError))
                {
                    Debug.LogError("FYD WebGL Tools: " + serverFileError);
                    return;
                }
                List<string> validation = ValidateOutput(outputRoot, settings);

                if (settings.generateDeploymentChecklist)
                {
                    WriteDeploymentChecklist(outputRoot, settings, report, validation);
                }

                if (settings.generateBuildReport)
                {
                    WriteBuildReport(outputRoot, settings, report, validation);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("FYD WebGL Tools: Hậu xử lý build gặp lỗi: " + exception);
            }
        }

        internal static bool EnsureServerFiles(
            string outputRoot,
            FYDWebGLSettings settings,
            out string error)
        {
            error = string.Empty;
            try
            {
                EnsureHtaccess(outputRoot, settings);
                if (!File.Exists(Path.Combine(outputRoot, "index.html")))
                {
                    error = "Release thiếu index.html.";
                    return false;
                }

                string buildDirectory = Path.Combine(outputRoot, "Build");
                if (!Directory.Exists(buildDirectory))
                {
                    error = "Release thiếu thư mục Build.";
                    return false;
                }

                if (settings.copyHtaccessToBuildFolder &&
                    !File.Exists(Path.Combine(buildDirectory, ".htaccess")))
                {
                    error = "Không thể tạo Build/.htaccess.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                error = "Không thể chuẩn bị file server: " + exception.Message;
                return false;
            }
        }

        private static void EnsureHtaccess(string outputRoot, FYDWebGLSettings settings)
        {
            string source = Path.Combine(Directory.GetCurrentDirectory(), FYDWebGLSettings.TemplateAssetPath, ".htaccess");
            if (!File.Exists(source))
            {
                Debug.LogWarning("FYD WebGL Tools: Không tìm thấy .htaccess nguồn tại " + source);
                return;
            }

            File.Copy(source, Path.Combine(outputRoot, ".htaccess"), true);

            if (settings.copyHtaccessToBuildFolder)
            {
                string buildDirectory = Path.Combine(outputRoot, "Build");
                if (Directory.Exists(buildDirectory))
                {
                    File.Copy(source, Path.Combine(buildDirectory, ".htaccess"), true);
                }
            }
        }

        private static List<string> ValidateOutput(string outputRoot, FYDWebGLSettings settings)
        {
            var results = new List<string>();
            AddCheck(results, File.Exists(Path.Combine(outputRoot, "index.html")), "index.html");
            AddCheck(results, Directory.Exists(Path.Combine(outputRoot, "Build")), "thư mục Build");
            AddCheck(results, File.Exists(Path.Combine(outputRoot, ".htaccess")), ".htaccess ở thư mục gốc");

            string buildDirectory = Path.Combine(outputRoot, "Build");
            if (Directory.Exists(buildDirectory))
            {
                if (settings.copyHtaccessToBuildFolder)
                {
                    AddCheck(
                        results,
                        File.Exists(Path.Combine(buildDirectory, ".htaccess")),
                        ".htaccess trong thư mục Build");
                }

                string[] files = Directory.GetFiles(buildDirectory, "*", SearchOption.TopDirectoryOnly);
                AddCheck(results, files.Any(file => Path.GetFileName(file).Contains("loader") && file.EndsWith(".js", StringComparison.OrdinalIgnoreCase)), "loader.js");
                AddCheck(results, files.Any(file => Path.GetFileName(file).Contains("framework") && ContainsAny(file, ".js", ".js.gz", ".js.br")), "framework.js");
                AddCheck(results, files.Any(file => ContainsAny(file, ".wasm", ".wasm.gz", ".wasm.br")), "WebAssembly .wasm");
                AddCheck(results, files.Any(file => ContainsAny(file, ".data", ".data.gz", ".data.br")), "Unity data file");
            }

            return results;
        }

        private static bool ContainsAny(string path, params string[] endings)
        {
            return endings.Any(ending => path.EndsWith(ending, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddCheck(List<string> results, bool passed, string label)
        {
            results.Add((passed ? "[OK] " : "[THIẾU] ") + label);
        }

        private static void WriteDeploymentChecklist(
            string outputRoot,
            FYDWebGLSettings settings,
            BuildReport report,
            IReadOnlyCollection<string> outputValidation)
        {
            string compression = PlayerSettings.WebGL.compressionFormat.ToString();
            string text =
$@"FYD WEBGL — CHECKLIST TRIỂN KHAI
================================

Build UTC: {DateTime.UtcNow:O}
Unity: {Application.unityVersion}
Module: {settings.moduleId}
Product: {PlayerSettings.productName}
Version: {PlayerSettings.bundleVersion}
Compression: {compression}
Template: {PlayerSettings.WebGL.template}
Output: {outputRoot}
Stable deploy: {Path.GetFullPath(settings.GetStableDeployFolder())}
Build size: {FYDWebGLBuilder.FormatBytes(report.summary.totalSize)}

1. KIỂM TRA OUTPUT
------------------
{string.Join(Environment.NewLine, outputValidation)}

2. UPLOAD HOSTING
-----------------
[ ] Upload NỘI DUNG của thư mục deploy cố định, không upload cả thư mục releases.
[ ] URL người chơi luôn trỏ vào thư mục public cố định tương ứng với deploy.
[ ] Bật hiển thị hidden files và xác nhận có cả .htaccess ở root lẫn Build/.htaccess.
[ ] Không upload qua WordPress Media Library.
[ ] Giữ nguyên cấu trúc index.html, Build, TemplateData, StreamingAssets.
[ ] Dùng HTTPS.

3. KIỂM TRA HEADER TRÊN TRÌNH DUYỆT
----------------------------------
Mở DevTools → Network → chọn file .wasm.br/.wasm.gz:
[ ] Brotli: Content-Type = application/wasm; Content-Encoding = br.
[ ] Gzip: Content-Type = application/wasm; Content-Encoding = gzip.
[ ] File .js nén có Content-Type = application/javascript.
[ ] Không request nào trả Content-Type = text/html do lỗi 404/rewrite.

4. KIỂM TRA CHẠY THỰC TẾ
------------------------
[ ] Mở game bằng tab ẩn danh để kiểm tra lần tải đầu.
[ ] Reload lần hai để kiểm tra cache.
[ ] Kiểm tra Chrome Android và Safari iPhone/iPad nếu có.
[ ] Kiểm tra xoay màn hình, safe area, fullscreen và âm thanh.
[ ] Kiểm tra Console không có lỗi MIME, CORS, Wasm hoặc Service Worker.
[ ] Nếu cập nhật build, tăng Product Version trước khi phát hành.

5. KIẾN TRÚC NHIỀU MODULE
-------------------------
[ ] Mỗi module dùng moduleId riêng.
[ ] HTML Host preload file nhưng chỉ giữ một Unity instance trên mobile.
[ ] Chờ MODULE_VISUALLY_READY trước khi gỡ transition.
[ ] Service Worker module dùng ?sw=0 nếu HTML Host đã có Service Worker chung.
";

            File.WriteAllText(Path.Combine(outputRoot, "DEPLOY-CHECKLIST-VI.txt"), text);
        }

        [Serializable]
        private sealed class SerializableBuildReport
        {
            public string toolVersion;
            public string unityVersion;
            public string buildUtc;
            public string moduleId;
            public string companyName;
            public string productName;
            public string productVersion;
            public string preset;
            public string compression;
            public bool dataCaching;
            public bool nameFilesAsHashes;
            public bool threadsSupport;
            public string template;
            public string outputPath;
            public string result;
            public ulong totalSizeBytes;
            public int totalWarnings;
            public int totalErrors;
            public string[] validation;
            public BuildFileInfo[] largestFiles;
        }

        [Serializable]
        private sealed class BuildFileInfo
        {
            public string path;
            public long bytes;
            public string size;
        }

        private static void WriteBuildReport(
            string outputRoot,
            FYDWebGLSettings settings,
            BuildReport report,
            IReadOnlyCollection<string> validation)
        {
            BuildFileInfo[] largestFiles = Directory.GetFiles(outputRoot, "*", SearchOption.AllDirectories)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.Length)
                .Take(20)
                .Select(file => new BuildFileInfo
                {
                    path = file.FullName.Substring(outputRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/'),
                    bytes = file.Length,
                    size = FYDWebGLBuilder.FormatBytes(file.Length)
                })
                .ToArray();

            FYDWebGLCompatibility.TryGetWebGLProperty("threadsSupport", out bool threadsSupport);

            var serializable = new SerializableBuildReport
            {
                toolVersion = FYDWebGLSettings.ToolVersion,
                unityVersion = Application.unityVersion,
                buildUtc = DateTime.UtcNow.ToString("O"),
                moduleId = settings.moduleId,
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                productVersion = PlayerSettings.bundleVersion,
                preset = settings.preset.ToString(),
                compression = PlayerSettings.WebGL.compressionFormat.ToString(),
                dataCaching = PlayerSettings.WebGL.dataCaching,
                nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes,
                threadsSupport = threadsSupport,
                template = PlayerSettings.WebGL.template,
                outputPath = outputRoot.Replace('\\', '/'),
                result = report.summary.result.ToString(),
                totalSizeBytes = report.summary.totalSize,
                totalWarnings = report.summary.totalWarnings,
                totalErrors = report.summary.totalErrors,
                validation = validation.ToArray(),
                largestFiles = largestFiles
            };

            string json = JsonUtility.ToJson(serializable, true);
            File.WriteAllText(Path.Combine(outputRoot, "fyd-build-report.json"), json);
        }
    }
}
