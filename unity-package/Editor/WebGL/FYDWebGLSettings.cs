using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace FYD.WebGLTools
{
    public enum FYDWebGLPreset
    {
        Development,
        ProductionSharedHosting
    }

    public enum FYDWebGLCompression
    {
        Gzip,
        Brotli
    }

    /// <summary>
    /// Project-level settings for FYD WebGL setup and build automation.
    /// Stored in ProjectSettings so the reusable tool itself can stay in a UPM package.
    /// </summary>
    public sealed class FYDWebGLSettings : ScriptableObject
    {
        public const string ToolVersion = "2.0.0";
        public const string ProjectSettingsPath = "ProjectSettings/FYDWebGLToolsSettings.asset";
        public const string LegacyAssetPath = "Assets/FYDWebGLTools/FYDWebGLSettings.asset";
        private const int CurrentSettingsSchemaVersion = 2;
        private static string ProjectSettingsFullPath =>
            Path.Combine(
                Path.GetDirectoryName(Application.dataPath) ?? string.Empty,
                ProjectSettingsPath);
        public const string TemplateFolderName = "FYDTemplateOptimized";
        public const string TemplateAssetPath = "Assets/WebGLTemplates/" + TemplateFolderName;
        public const string TemplatePlayerSetting = "PROJECT:" + TemplateFolderName;

        [Header("Build")]
        public FYDWebGLPreset preset = FYDWebGLPreset.ProductionSharedHosting;
        public FYDWebGLCompression productionCompression = FYDWebGLCompression.Brotli;
        public string buildRoot = "Builds/WebGL";
        public string moduleId = "mind-kingdom";
        public bool appendVersionToFolder = true;
        public string releasesFolderName = "releases";
        public bool createStableDeployCopy = true;
        public string stableDeployFolderName = "deploy";
        public bool cleanOutputBeforeBuild = true;
        public bool autoIncrementPatchVersion = true;
        public bool buildAndRunAfterDevelopmentBuild = true;

        [Header("Production defaults")]
        public bool enableDataCaching = true;
        public bool enableNameFilesAsHashes = true;
        public bool disableThreads = true;
        public bool setManagedStrippingMedium = true;
        public bool optimizeIl2CppForSize = true;

        [Header("Validation and deployment")]
        public bool strictValidationBeforeBuild = true;
        public bool copyHtaccessToBuildFolder = true;
        public bool generateDeploymentChecklist = true;
        public bool generateBuildReport = true;

        [Header("Last successful build")]
        [SerializeField] private string lastBuildVersion = string.Empty;
        [SerializeField] private string lastBuildPath = string.Empty;
        [SerializeField] private string lastDeployPath = string.Empty;
        [SerializeField] private string lastBuildUtc = string.Empty;
        [SerializeField, HideInInspector] private int settingsSchemaVersion;
        private static FYDWebGLSettings cachedInstance;

        public string LastBuildVersion => lastBuildVersion;
        public string LastBuildPath => lastBuildPath;
        public string LastDeployPath => lastDeployPath;
        public string LastBuildUtc => lastBuildUtc;

        public string GetOutputFolder()
        {
            string releasesRoot = Path.Combine(
                GetModuleFolder(),
                SanitizePathSegment(releasesFolderName));
            string releaseName = appendVersionToFolder
                ? "v" + SanitizePathSegment(PlayerSettings.bundleVersion)
                : "latest-build";

            return Path.Combine(releasesRoot, releaseName).Replace('\\', '/');
        }

        public string GetStableDeployFolder()
        {
            return Path.Combine(
                GetModuleFolder(),
                SanitizePathSegment(stableDeployFolderName)).Replace('\\', '/');
        }

        public string GetModuleFolder()
        {
            string safeModule = SanitizePathSegment(
                string.IsNullOrWhiteSpace(moduleId) ? PlayerSettings.productName : moduleId);

            return Path.Combine(buildRoot, safeModule).Replace('\\', '/');
        }

        public void RecordSuccessfulBuild(string releasePath, string deployPath)
        {
            lastBuildVersion = PlayerSettings.bundleVersion;
            lastBuildPath = releasePath.Replace('\\', '/');
            lastDeployPath = string.IsNullOrWhiteSpace(deployPath)
                ? string.Empty
                : deployPath.Replace('\\', '/');
            lastBuildUtc = DateTime.UtcNow.ToString("O");
            Persist();
        }

        public static FYDWebGLSettings LoadOrCreate()
        {
            if (cachedInstance != null)
            {
                cachedInstance.EnsureCurrentSchema();
                return cachedInstance;
            }

            bool hasProjectSettings = File.Exists(ProjectSettingsFullPath);
            FYDWebGLSettings settings = hasProjectSettings
                ? InternalEditorUtility
                    .LoadSerializedFileAndForget(ProjectSettingsFullPath)
                    .OfType<FYDWebGLSettings>()
                    .FirstOrDefault()
                : null;

            if (settings == null)
            {
                settings = CreateInstance<FYDWebGLSettings>();
            }

            settings.hideFlags = HideFlags.HideAndDontSave;
            cachedInstance = settings;

            if (!hasProjectSettings)
            {
                settings.TryMigrateLegacyAsset();
            }

            settings.EnsureCurrentSchema();
            return settings;
        }

        public void Persist()
        {
            InternalEditorUtility.SaveToSerializedFileAndForget(
                new UnityEngine.Object[] { this },
                ProjectSettingsFullPath,
                true);
        }

        private void TryMigrateLegacyAsset()
        {
            FYDWebGLSettings legacy =
                AssetDatabase.LoadAssetAtPath<FYDWebGLSettings>(LegacyAssetPath);
            if (legacy == null || ReferenceEquals(legacy, this))
            {
                return;
            }

            string serializedLegacy = EditorJsonUtility.ToJson(legacy);
            EditorJsonUtility.FromJsonOverwrite(serializedLegacy, this);
            Debug.Log(
                "FYD WebGL Tools: Đã chuyển cấu hình cũ từ Assets sang " +
                ProjectSettingsPath + ".");
        }

        private void EnsureCurrentSchema()
        {
            if (settingsSchemaVersion >= CurrentSettingsSchemaVersion)
            {
                return;
            }

            releasesFolderName = string.IsNullOrWhiteSpace(releasesFolderName)
                ? "releases"
                : releasesFolderName;
            stableDeployFolderName = string.IsNullOrWhiteSpace(stableDeployFolderName)
                ? "deploy"
                : stableDeployFolderName;
            createStableDeployCopy = true;
            settingsSchemaVersion = CurrentSettingsSchemaVersion;
            Persist();
        }

        private static string SanitizePathSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "webgl-module";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '-');
            }

            return value.Trim().Replace(' ', '-').ToLowerInvariant();
        }
    }
}
