using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FYD.UnityPublisher.Editor.Validation
{
    public sealed class FYDValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>Validates publisher settings and WebGL build structure before packaging.</summary>
    public static class FYDPublisherValidation
    {
        private static readonly Regex AppIdPattern = new Regex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);
        private static readonly string[] ExecutableExtensions =
        {
            ".php", ".phtml", ".phar", ".cgi", ".pl", ".py", ".sh", ".bash", ".exe", ".dll", ".so"
        };
        private static readonly string[] ServerConfigNames = { ".htaccess", ".user.ini", "web.config" };

        public static bool IsValidAppId(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 100 && AppIdPattern.IsMatch(value);
        }

        public static FYDValidationResult ValidateWebGLBuild(string buildDirectory)
        {
            var result = new FYDValidationResult();
            if (string.IsNullOrWhiteSpace(buildDirectory) || !Directory.Exists(buildDirectory))
            {
                result.Errors.Add("Không tìm thấy thư mục WebGL build.");
                return result;
            }

            string root = Path.GetFullPath(buildDirectory);
            if (!File.Exists(Path.Combine(root, "index.html")))
            {
                result.Errors.Add("Thiếu index.html ở thư mục gốc WebGL build.");
            }

            bool hasBuildDirectory = Directory.Exists(Path.Combine(root, "Build"));
            bool hasKnownBuildFile = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Any(path => IsKnownWebGLFile(path));
            if (!hasBuildDirectory && !hasKnownBuildFile)
            {
                result.Errors.Add("Không tìm thấy thư mục Build hoặc file loader/data/framework/wasm của Unity WebGL.");
            }

            long totalSize = 0;
            foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    result.Errors.Add("Không cho phép symlink/reparse point: " + GetRelativePath(root, file));
                    continue;
                }

                totalSize += info.Length;
                string relative = GetRelativePath(root, file);
                if (IsExecutable(relative))
                {
                    result.Errors.Add("Build chứa file thực thi bị cấm: " + relative);
                }
                else if (IsServerConfig(relative))
                {
                    result.Warnings.Add("File cấu hình server sẽ không được đưa vào ZIP: " + relative);
                }
            }

            if (totalSize <= 0)
            {
                result.Errors.Add("WebGL build rỗng.");
            }

            return result;
        }

        public static bool IsArchiveCandidate(string relativePath)
        {
            return !IsExecutable(relativePath) && !IsServerConfig(relativePath);
        }

        public static bool IsImportantWebGLFile(string relativePath)
        {
            string lower = relativePath.Replace('\\', '/').ToLowerInvariant();
            return lower == "index.html" || IsKnownWebGLFile(lower);
        }

        public static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('\\', '/');
        }

        private static bool IsKnownWebGLFile(string path)
        {
            string lower = path.ToLowerInvariant();
            return lower.Contains(".loader.js") || lower.Contains(".data") || lower.Contains(".framework.js") ||
                   lower.Contains(".wasm") || lower.EndsWith(".unityweb", StringComparison.Ordinal);
        }

        private static bool IsExecutable(string relativePath)
        {
            string fileName = Path.GetFileName(relativePath).ToLowerInvariant();
            return ExecutableExtensions.Any(extension => fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsServerConfig(string relativePath)
        {
            string fileName = Path.GetFileName(relativePath);
            return ServerConfigNames.Any(name => string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }
    }
}
