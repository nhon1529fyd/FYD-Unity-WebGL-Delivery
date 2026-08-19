using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace FYD.WebGLTools
{
    public sealed class FYDWebGLSetupWindow : EditorWindow
    {
        private FYDWebGLSettings _settings;
        private SerializedObject _serializedSettings;
        private List<FYDCheckItem> _checks = new List<FYDCheckItem>();
        private Vector2 _scroll;
        private Vector2 _checkScroll;

        [MenuItem("FYD/WebGL/Setup & Builder", priority = 100)]
        public static void Open()
        {
            var window = GetWindow<FYDWebGLSetupWindow>("FYD WebGL");
            window.minSize = new Vector2(620, 640);
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            RefreshChecklist();
        }

        private void LoadSettings()
        {
            _settings = FYDWebGLSettings.LoadOrCreate();
            _serializedSettings = new SerializedObject(_settings);
        }

        private void OnGUI()
        {
            if (_settings == null || _serializedSettings == null)
            {
                LoadSettings();
            }

            _serializedSettings.Update();
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scrollView.scrollPosition;

                DrawHeader();
                DrawOneClickActions();
                DrawSettings();
                DrawChecklist();
                DrawBuildActions();
            }

            if (_serializedSettings.ApplyModifiedProperties())
            {
                _settings.Persist();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("FYD WebGL Auto Setup & Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Tự cấu hình WebGL, kiểm tra template 9:16, build nén cho Apache/LiteSpeed và sinh checklist upload hosting.",
                MessageType.Info
            );

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Unity", Application.unityVersion);
                EditorGUILayout.LabelField("Tool", FYDWebGLSettings.ToolVersion);
                EditorGUILayout.LabelField("Target", EditorUserBuildSettings.activeBuildTarget.ToString());
            }
        }

        private void DrawOneClickActions()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Thiết lập một chạm", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Cấu hình Production", GUILayout.Height(36)))
                {
                    RunDeferred(() =>
                    {
                        _settings.preset = FYDWebGLPreset.ProductionSharedHosting;
                        _settings.Persist();
                        FYDWebGLConfigurator.ApplyPreset(_settings);
                    });
                }

                if (GUILayout.Button("Cấu hình Development", GUILayout.Height(36)))
                {
                    RunDeferred(() =>
                    {
                        _settings.preset = FYDWebGLPreset.Development;
                        _settings.Persist();
                        FYDWebGLConfigurator.ApplyPreset(_settings);
                    });
                }

                if (GUILayout.Button("Chỉ chạy Checklist", GUILayout.Height(36)))
                {
                    RefreshChecklist();
                }
            }
        }

        private void DrawSettings()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Cấu hình dự án", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("preset"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("productionCompression"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("buildRoot"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("moduleId"));
            EditorGUILayout.PropertyField(
                _serializedSettings.FindProperty("appendVersionToFolder"),
                new GUIContent("Lưu release theo version"));
            EditorGUILayout.PropertyField(
                _serializedSettings.FindProperty("releasesFolderName"),
                new GUIContent("Tên thư mục releases"));
            EditorGUILayout.PropertyField(
                _serializedSettings.FindProperty("createStableDeployCopy"),
                new GUIContent("Tạo deploy cố định"));
            EditorGUILayout.PropertyField(
                _serializedSettings.FindProperty("stableDeployFolderName"),
                new GUIContent("Tên thư mục deploy"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("cleanOutputBeforeBuild"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("autoIncrementPatchVersion"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("buildAndRunAfterDevelopmentBuild"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Production", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("enableDataCaching"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("enableNameFilesAsHashes"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("disableThreads"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("setManagedStrippingMedium"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("optimizeIl2CppForSize"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Validation & deployment", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("strictValidationBeforeBuild"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("copyHtaccessToBuildFolder"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("generateDeploymentChecklist"));
            EditorGUILayout.PropertyField(_serializedSettings.FindProperty("generateBuildReport"));

            EditorGUILayout.HelpBox(
                "Release dự kiến: " + _settings.GetOutputFolder() +
                "\nThư mục upload cố định: " + _settings.GetStableDeployFolder(),
                MessageType.None);
        }

        private void DrawChecklist()
        {
            EditorGUILayout.Space(10);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Checklist", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Làm mới", GUILayout.Width(90)))
                {
                    RefreshChecklist();
                }
            }

            int errors = 0;
            int warnings = 0;
            foreach (FYDCheckItem check in _checks)
            {
                if (check.Status == FYDCheckStatus.Error) errors++;
                if (check.Status == FYDCheckStatus.Warning) warnings++;
            }

            MessageType summaryType = errors > 0 ? MessageType.Error : warnings > 0 ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox($"{_checks.Count} mục · {errors} lỗi · {warnings} cảnh báo", summaryType);

            using (var scrollView = new EditorGUILayout.ScrollViewScope(
                       _checkScroll,
                       GUILayout.MinHeight(260),
                       GUILayout.MaxHeight(390)))
            {
                _checkScroll = scrollView.scrollPosition;
                foreach (FYDCheckItem check in _checks)
                {
                    DrawCheckItem(check);
                }
            }
        }

        private void DrawCheckItem(FYDCheckItem check)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(StatusIcon(check.Status), GUILayout.Width(22));
                    EditorGUILayout.LabelField(check.Title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(check.Status.ToString(), EditorStyles.miniLabel, GUILayout.Width(58));

                    if (check.Fix != null && GUILayout.Button("Sửa", GUILayout.Width(50)))
                    {
                        RunDeferred(check.Fix);
                    }
                }

                EditorGUILayout.LabelField(check.Detail, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawBuildActions()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !EditorApplication.isCompiling;

                if (GUILayout.Button("Tự cấu hình + Build", GUILayout.Height(42)))
                {
                    RunDeferred(() => FYDWebGLBuilder.Build(_settings, false));
                }

                if (GUILayout.Button("Build & Run", GUILayout.Height(42)))
                {
                    RunDeferred(() => FYDWebGLBuilder.Build(_settings, true));
                }

                GUI.enabled = true;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Mở thư mục deploy"))
                {
                    string path = Path.GetFullPath(_settings.GetStableDeployFolder());
                    Directory.CreateDirectory(path);
                    EditorUtility.RevealInFinder(path);
                }

                if (GUILayout.Button("Mở release"))
                {
                    string path = Path.GetFullPath(_settings.GetOutputFolder());
                    Directory.CreateDirectory(path);
                    EditorUtility.RevealInFinder(path);
                }
            }

            if (GUILayout.Button("Tạo lại deploy từ release gần nhất"))
            {
                RunDeferred(() => FYDWebGLBuilder.PublishLastSuccessfulRelease(true));
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tăng patch version"))
                {
                    RunDeferred(() =>
                    {
                        FYDWebGLConfigurator.TryIncrementPatchVersion(out string version);
                        ShowNotification(new GUIContent("Version: " + version));
                    });
                }

                if (GUILayout.Button("Mở Player Settings"))
                {
                    RunDeferred(
                        () => SettingsService.OpenProjectSettings("Project/Player"),
                        false
                    );
                }
            }

            if (!string.IsNullOrWhiteSpace(_settings.LastBuildPath))
            {
                string deployLine = string.IsNullOrWhiteSpace(_settings.LastDeployPath)
                    ? "Deploy: chưa tạo"
                    : "Deploy cố định: " + _settings.LastDeployPath;
                EditorGUILayout.HelpBox(
                    $"Build gần nhất: {_settings.LastBuildVersion}\n" +
                    $"Release: {_settings.LastBuildPath}\n" +
                    $"{deployLine}\n{_settings.LastBuildUtc}",
                    MessageType.None
                );
            }
        }

        private void RefreshChecklist()
        {
            if (_settings == null)
            {
                LoadSettings();
            }

            _checks = FYDWebGLValidation.Run(_settings);
            Repaint();
        }

        private static string StatusIcon(FYDCheckStatus status)
        {
            switch (status)
            {
                case FYDCheckStatus.Pass: return "✓";
                case FYDCheckStatus.Warning: return "⚠";
                case FYDCheckStatus.Error: return "✕";
                default: return "ℹ";
            }
        }

        private void RunDeferred(Action action, bool refreshChecklist = true)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                RunSafe(action);

                if (this != null && refreshChecklist)
                {
                    RefreshChecklist();
                }
            };
        }

        private static void RunSafe(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("FYD WebGL Tools", exception.Message, "Đóng");
            }
        }
    }
}
