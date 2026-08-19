using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FYD.WebGLTools
{
    public enum FYDCheckStatus
    {
        Pass,
        Warning,
        Error,
        Info
    }

    public sealed class FYDCheckItem
    {
        public FYDCheckItem(string title, FYDCheckStatus status, string detail, Action fix = null)
        {
            Title = title;
            Status = status;
            Detail = detail;
            Fix = fix;
        }

        public string Title { get; }
        public FYDCheckStatus Status { get; }
        public string Detail { get; }
        public Action Fix { get; }
    }

    internal static class FYDWebGLValidation
    {
        public static List<FYDCheckItem> Run(FYDWebGLSettings settings)
        {
            var items = new List<FYDCheckItem>();

            bool supported = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            items.Add(new FYDCheckItem(
                "Web Build Support đã được cài",
                supported ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                supported
                    ? "Unity có thể build mục tiêu WebGL/Web."
                    : "Thiếu Web Build Support. Mở Unity Hub → Installs → Add modules → Web Build Support."
            ));

            bool activeTarget = EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL;
            items.Add(new FYDCheckItem(
                "Build Target đang là WebGL",
                activeTarget ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                activeTarget ? "Đúng mục tiêu WebGL." : "Cần chuyển Build Target trước khi build.",
                activeTarget ? null : () => FYDWebGLConfigurator.SwitchToWebGL()
            ));

            string projectRoot = Directory.GetCurrentDirectory();
            string templateRoot = Path.Combine(projectRoot, FYDWebGLSettings.TemplateAssetPath);
            bool templateExists = Directory.Exists(templateRoot);
            items.Add(new FYDCheckItem(
                "Template FYDTemplateOptimized tồn tại",
                templateExists ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                templateExists ? FYDWebGLSettings.TemplateAssetPath : "Không tìm thấy template trong Assets/WebGLTemplates.",
                templateExists ? null : () => FYDWebGLTemplateInstaller.EnsureInstalled(true)
            ));

            bool templateFilesOk = templateExists &&
                                   File.Exists(Path.Combine(templateRoot, "index.html")) &&
                                   File.Exists(Path.Combine(templateRoot, ".htaccess")) &&
                                   File.Exists(Path.Combine(templateRoot, "TemplateData", "fyd-template.js"));
            items.Add(new FYDCheckItem(
                "Template có đủ file cốt lõi",
                templateFilesOk ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                templateFilesOk
                    ? "index.html, .htaccess và fyd-template.js đều có."
                    : "Template thiếu index.html, .htaccess hoặc TemplateData/fyd-template.js."
            ));

            string htaccessPath = Path.Combine(templateRoot, ".htaccess");
            string htaccess = File.Exists(htaccessPath)
                ? File.ReadAllText(htaccessPath)
                : string.Empty;
            bool brotliRulesOk = htaccess.Contains("Options -MultiViews") &&
                                  htaccess.Contains("AddEncoding br .br") &&
                                  htaccess.Contains("ForceType application/javascript") &&
                                  htaccess.Contains("ForceType application/wasm") &&
                                  htaccess.Contains("ForceType application/octet-stream");
            items.Add(new FYDCheckItem(
                ".htaccess có rule Brotli tương thích hosting",
                brotliRulesOk ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                brotliRulesOk
                    ? "Có MultiViews guard, Content-Encoding và MIME cho JavaScript/WebAssembly/data."
                    : "Thiếu rule Brotli/ForceType cần thiết trong template .htaccess."
            ));

            bool copyHtaccessToBuild = settings.copyHtaccessToBuildFolder;
            items.Add(new FYDCheckItem(
                ".htaccess sẽ được đặt trong thư mục Build",
                copyHtaccessToBuild ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                copyHtaccessToBuild
                    ? "Sau build sẽ có cả <Application>/.htaccess và <Application>/Build/.htaccess."
                    : "Unity yêu cầu rule file nén tại <Application>/Build/.htaccess.",
                copyHtaccessToBuild ? null : () =>
                {
                    settings.copyHtaccessToBuildFolder = true;
                    settings.Persist();
                }
            ));

            bool templateSelected = string.Equals(
                PlayerSettings.WebGL.template,
                FYDWebGLSettings.TemplatePlayerSetting,
                StringComparison.OrdinalIgnoreCase
            );
            items.Add(new FYDCheckItem(
                "Đã chọn FYDTemplateOptimized",
                templateSelected ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                templateSelected ? PlayerSettings.WebGL.template : $"Hiện tại: {PlayerSettings.WebGL.template}",
                templateSelected ? null : () => FYDWebGLConfigurator.SelectTemplate()
            ));

            bool compressionOk = settings.preset == FYDWebGLPreset.Development
                ? PlayerSettings.WebGL.compressionFormat == WebGLCompressionFormat.Disabled
                : PlayerSettings.WebGL.compressionFormat == ExpectedCompression(settings);
            items.Add(new FYDCheckItem(
                "Compression đúng preset",
                compressionOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                $"Hiện tại: {PlayerSettings.WebGL.compressionFormat}; preset: {settings.preset}."
            ));

            bool fallbackOk = !PlayerSettings.WebGL.decompressionFallback;
            items.Add(new FYDCheckItem(
                "Decompression Fallback đã tắt",
                fallbackOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                fallbackOk
                    ? "Server dùng native Gzip/Brotli qua .htaccess."
                    : "Fallback tạo file .unityweb và giải nén bằng JavaScript; không khớp cấu hình production hiện tại."
            ));

            bool dataCachingExpected = settings.preset == FYDWebGLPreset.Development ? false : settings.enableDataCaching;
            bool dataCachingOk = PlayerSettings.WebGL.dataCaching == dataCachingExpected;
            items.Add(new FYDCheckItem(
                "Data Caching đúng preset",
                dataCachingOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                $"Hiện tại: {PlayerSettings.WebGL.dataCaching}; mong đợi: {dataCachingExpected}."
            ));

            bool hashesExpected = settings.preset == FYDWebGLPreset.Development ? false : settings.enableNameFilesAsHashes;
            bool hashesOk = PlayerSettings.WebGL.nameFilesAsHashes == hashesExpected;
            items.Add(new FYDCheckItem(
                "Name Files As Hashes đúng preset",
                hashesOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                $"Hiện tại: {PlayerSettings.WebGL.nameFilesAsHashes}; mong đợi: {hashesExpected}."
            ));

            bool hasThreadsSetting = FYDWebGLCompatibility.TryGetWebGLProperty("threadsSupport", out bool threadsEnabled);
            bool threadsOk = !hasThreadsSetting || !threadsEnabled;
            items.Add(new FYDCheckItem(
                "WebAssembly Threads đang tắt",
                threadsOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                !hasThreadsSetting
                    ? "Unity hiện tại không cung cấp setting threadsSupport qua API."
                    : threadsOk
                        ? "Phù hợp shared hosting/mobile và không cần COOP/COEP."
                        : "Threads yêu cầu SharedArrayBuffer cùng COOP/COEP; tài nguyên bên thứ ba có thể bị chặn."
            ));

            bool memoryGrowthOk = FYDWebGLCompatibility.IsMemoryGrowthGeometricOrUnavailable();
            items.Add(new FYDCheckItem(
                "Memory Growth dùng Geometric",
                memoryGrowthOk ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                memoryGrowthOk
                    ? "Đúng khuyến nghị hoặc Unity hiện tại không có setting này."
                    : "Nên dùng Geometric để heap WebAssembly tăng linh hoạt."
            ));

            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                .Select(scene => scene.path)
                .ToArray();
            items.Add(new FYDCheckItem(
                "Có scene được chọn để build",
                enabledScenes.Length > 0 ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                enabledScenes.Length > 0
                    ? $"{enabledScenes.Length} scene đang bật."
                    : "Chưa có scene. Nút Sửa sẽ thêm scene hiện đang mở nếu scene đã được lưu.",
                enabledScenes.Length > 0 ? null : () => FYDWebGLConfigurator.AddActiveSceneToBuildSettings()
            ));

            bool metadataOk = !string.IsNullOrWhiteSpace(PlayerSettings.companyName) &&
                              !string.IsNullOrWhiteSpace(PlayerSettings.productName) &&
                              !string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion);
            items.Add(new FYDCheckItem(
                "Company, Product và Version đã điền",
                metadataOk ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                $"{PlayerSettings.companyName} / {PlayerSettings.productName} / {PlayerSettings.bundleVersion}"
            ));

            bool versionFresh = string.IsNullOrEmpty(settings.LastBuildVersion) ||
                                !string.Equals(settings.LastBuildVersion, PlayerSettings.bundleVersion, StringComparison.Ordinal);
            items.Add(new FYDCheckItem(
                "Version khác bản build gần nhất",
                versionFresh ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                versionFresh
                    ? "Cache-busting sẽ nhận đúng phiên bản."
                    : $"Version {PlayerSettings.bundleVersion} đã từng build. Hãy tăng version trước khi phát hành lại."
            ));

            string output = settings.GetOutputFolder();
            bool outputOutsideAssets = !output.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                                       !string.Equals(output, "Assets", StringComparison.OrdinalIgnoreCase);
            items.Add(new FYDCheckItem(
                "Build output nằm ngoài Assets",
                outputOutsideAssets ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                output
            ));

            bool stableDeployEnabled = settings.createStableDeployCopy;
            items.Add(new FYDCheckItem(
                "Tạo thư mục deploy có đường dẫn cố định",
                stableDeployEnabled ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                stableDeployEnabled
                    ? settings.GetStableDeployFolder()
                    : "Đang tắt stable deploy; người chơi sẽ phải mở URL chứa version.",
                stableDeployEnabled ? null : () =>
                {
                    settings.createStableDeployCopy = true;
                    settings.Persist();
                }
            ));

            string deployOutput = settings.GetStableDeployFolder();
            bool deploymentPathsSafe = AreSeparateSiblingOutputs(
                settings.GetModuleFolder(), output, deployOutput);
            items.Add(new FYDCheckItem(
                "Release và deploy tách biệt an toàn",
                deploymentPathsSafe ? FYDCheckStatus.Pass : FYDCheckStatus.Error,
                deploymentPathsSafe
                    ? $"Release: {output}\nDeploy: {deployOutput}"
                    : "Release/deploy đang trùng, nằm ngoài module hoặc lồng vào nhau."
            ));

            bool bridgeExists =
                FYDWebGLPackagePaths.RuntimeBridgeSourceExists &&
                FYDWebGLPackagePaths.WebGLPluginSourceExists;
            items.Add(new FYDCheckItem(
                "Bridge Unity → HTML đã được cài",
                bridgeExists ? FYDCheckStatus.Pass : FYDCheckStatus.Warning,
                bridgeExists
                    ? "Có ReportVisualReady và EmitHostEvent cho kiến trúc module."
                    : "Thiếu bridge; build vẫn chạy nhưng Host không nhận được MODULE_VISUALLY_READY tự động."
            ));

            items.Add(new FYDCheckItem(
                "Triển khai qua HTTPS",
                FYDCheckStatus.Info,
                "Unity không thể kiểm tra hosting từ Editor. Khi upload, dùng HTTPS và kiểm tra MIME/Content-Encoding trong DevTools."
            ));

            FYDWebGLProjectExtensions.AppendChecks(items);
            return items;
        }

        public static bool HasBlockingErrors(IEnumerable<FYDCheckItem> items)
        {
            return items.Any(item => item.Status == FYDCheckStatus.Error);
        }

        private static bool AreSeparateSiblingOutputs(
            string moduleFolder,
            string releaseFolder,
            string deployFolder)
        {
            try
            {
                string module = NormalizeDirectory(moduleFolder);
                string release = NormalizeDirectory(releaseFolder);
                string deploy = NormalizeDirectory(deployFolder);
                return release.StartsWith(module, StringComparison.OrdinalIgnoreCase) &&
                       deploy.StartsWith(module, StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(release, deploy, StringComparison.OrdinalIgnoreCase) &&
                       !release.StartsWith(deploy, StringComparison.OrdinalIgnoreCase) &&
                       !deploy.StartsWith(release, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string NormalizeDirectory(string path)
        {
            return Path.GetFullPath(path)
                       .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                   Path.DirectorySeparatorChar;
        }

        public static WebGLCompressionFormat ExpectedCompression(FYDWebGLSettings settings)
        {
            return settings.productionCompression == FYDWebGLCompression.Brotli
                ? WebGLCompressionFormat.Brotli
                : WebGLCompressionFormat.Gzip;
        }
    }
}
