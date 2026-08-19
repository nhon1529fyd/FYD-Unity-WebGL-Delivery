using System.IO;
using UnityEditor.PackageManager;

namespace FYD.WebGLTools
{
    internal static class FYDWebGLPackagePaths
    {
        private static string PackageRoot =>
            PackageInfo.FindForAssembly(typeof(FYDWebGLPackagePaths).Assembly)?.resolvedPath
            ?? string.Empty;

        public static string TemplateSourcePath =>
            CombinePackagePath("Editor", "Templates", FYDWebGLSettings.TemplateFolderName);

        public static bool RuntimeBridgeSourceExists =>
            File.Exists(CombinePackagePath(
                "Runtime", "Bridge", "FYDWebGLModuleBridge.cs"));

        public static bool WebGLPluginSourceExists =>
            File.Exists(CombinePackagePath(
                "Runtime", "Plugins", "WebGL", "FYDWebGLModuleBridge.jslib"));

        private static string CombinePackagePath(params string[] parts)
        {
            string path = PackageRoot;
            foreach (string part in parts)
            {
                path = Path.Combine(path, part);
            }

            return path;
        }
    }
}
