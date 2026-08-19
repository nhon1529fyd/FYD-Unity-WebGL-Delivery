using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FYD.WebGLTools
{
    [InitializeOnLoad]
    internal static class FYDWebGLTemplateInstaller
    {
        static FYDWebGLTemplateInstaller()
        {
            EditorApplication.delayCall += InstallWhenMissing;
        }

        [MenuItem("FYD/WebGL/Install or Refresh Template", priority = 119)]
        private static void InstallFromMenu()
        {
            EnsureInstalled(true);
        }

        public static bool EnsureInstalled(bool overwrite)
        {
            string source = FYDWebGLPackagePaths.TemplateSourcePath;
            string destination = Path.GetFullPath(FYDWebGLSettings.TemplateAssetPath);

            if (!HasCoreFiles(source))
            {
                Debug.LogError(
                    "FYD WebGL Tools: Package không có đủ nguồn template tại " + source);
                return false;
            }

            if (!overwrite && HasCoreFiles(destination))
            {
                return true;
            }

            try
            {
                CopyDirectory(source, destination, overwrite);
                AssetDatabase.Refresh();
                Debug.Log(
                    "FYD WebGL Tools: Đã cài template từ UPM package vào " +
                    FYDWebGLSettings.TemplateAssetPath + ".");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "FYD WebGL Tools: Không thể cài WebGL template: " +
                    exception.Message);
                return false;
            }
        }

        private static void InstallWhenMissing()
        {
            EnsureInstalled(false);
        }

        private static bool HasCoreFiles(string root)
        {
            return !string.IsNullOrWhiteSpace(root) &&
                   File.Exists(Path.Combine(root, "index.html")) &&
                   File.Exists(Path.Combine(root, ".htaccess")) &&
                   File.Exists(Path.Combine(root, "TemplateData", "fyd-template.js"));
        }

        private static void CopyDirectory(
            string source,
            string destination,
            bool overwrite)
        {
            Directory.CreateDirectory(destination);

            foreach (string directory in Directory.GetDirectories(
                         source, "*", SearchOption.AllDirectories))
            {
                string relative = directory.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.CreateDirectory(Path.Combine(destination, relative));
            }

            foreach (string file in Directory.GetFiles(
                         source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = file.Substring(source.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string target = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);

                if (overwrite || !File.Exists(target))
                {
                    File.Copy(file, target, true);
                }
            }
        }
    }
}
