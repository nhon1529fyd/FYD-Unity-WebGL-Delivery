using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace FYD.WebGLTools
{
    internal static class FYDWebGLConfigurator
    {
        public static bool SwitchToWebGL()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog(
                    "Thiếu Web Build Support",
                    "Hãy mở Unity Hub → Installs → Add modules → Web Build Support, sau đó mở lại project.",
                    "Đã hiểu"
                );
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
            {
                return true;
            }

            try
            {
                EditorUtility.DisplayProgressBar("FYD WebGL", "Đang chuyển Build Target sang WebGL...", 0.35f);
                return EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void SelectTemplate()
        {
            PlayerSettings.WebGL.template = FYDWebGLSettings.TemplatePlayerSetting;
        }

        public static void ApplyPreset(FYDWebGLSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!SwitchToWebGL())
            {
                throw new InvalidOperationException("Không thể chuyển Build Target sang WebGL.");
            }

            SelectTemplate();
            PlayerSettings.WebGL.decompressionFallback = false;
            FYDWebGLCompatibility.TrySetWebGLProperty("closeOnQuit", false);

            if (settings.disableThreads)
            {
                FYDWebGLCompatibility.TrySetWebGLProperty("threadsSupport", false);
            }

            FYDWebGLCompatibility.SetRecommendedMemoryGrowthIfSupported();

            if (settings.preset == FYDWebGLPreset.Development)
            {
                ApplyDevelopmentPreset();
            }
            else
            {
                ApplyProductionPreset(settings);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"FYD WebGL Tools: Đã áp dụng preset {settings.preset}.");
        }

        private static void ApplyDevelopmentPreset()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.dataCaching = false;
            PlayerSettings.WebGL.nameFilesAsHashes = false;

            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = true;
        }

        private static void ApplyProductionPreset(FYDWebGLSettings settings)
        {
            PlayerSettings.WebGL.compressionFormat = FYDWebGLValidation.ExpectedCompression(settings);
            PlayerSettings.WebGL.dataCaching = settings.enableDataCaching;
            PlayerSettings.WebGL.nameFilesAsHashes = settings.enableNameFilesAsHashes;

            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.allowDebugging = false;

#if UNITY_2021_2_OR_NEWER
            if (settings.setManagedStrippingMedium)
            {
                PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
            }

            if (settings.optimizeIl2CppForSize)
            {
#if UNITY_2022_1_OR_NEWER
                PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);
#else
                EditorUserBuildSettings.il2CppCodeGeneration = Il2CppCodeGeneration.OptimizeSize;
#endif
            }
#else
            if (settings.setManagedStrippingMedium)
            {
                PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);
            }
#endif
        }

        public static void AddActiveSceneToBuildSettings()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || string.IsNullOrWhiteSpace(scene.path))
            {
                EditorUtility.DisplayDialog(
                    "Scene chưa được lưu",
                    "Hãy lưu scene hiện tại trước, sau đó bấm Sửa lại.",
                    "Đóng"
                );
                return;
            }

            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int existing = scenes.FindIndex(item => string.Equals(item.path, scene.path, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                EditorBuildSettingsScene item = scenes[existing];
                item.enabled = true;
                scenes[existing] = item;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(scene.path, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("FYD WebGL Tools: Đã thêm scene vào build: " + scene.path);
        }

        public static bool TryIncrementPatchVersion(out string newVersion)
        {
            string current = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "1.0.0"
                : PlayerSettings.bundleVersion.Trim();

            string[] parts = current.Split('.');
            int major = 1;
            int minor = 0;
            int patch = 0;

            if (parts.Length > 0) int.TryParse(OnlyLeadingDigits(parts[0]), out major);
            if (parts.Length > 1) int.TryParse(OnlyLeadingDigits(parts[1]), out minor);
            if (parts.Length > 2) int.TryParse(OnlyLeadingDigits(parts[2]), out patch);

            patch++;
            newVersion = $"{Math.Max(0, major)}.{Math.Max(0, minor)}.{Math.Max(0, patch)}";
            PlayerSettings.bundleVersion = newVersion;
            AssetDatabase.SaveAssets();
            return true;
        }

        private static string OnlyLeadingDigits(string value)
        {
            if (string.IsNullOrEmpty(value)) return "0";
            int length = 0;
            while (length < value.Length && char.IsDigit(value[length])) length++;
            return length == 0 ? "0" : value.Substring(0, length);
        }
    }
}
