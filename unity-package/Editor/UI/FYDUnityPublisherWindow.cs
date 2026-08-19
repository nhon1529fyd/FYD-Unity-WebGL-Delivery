using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FYD.UnityPublisher.Editor.Build;
using FYD.UnityPublisher.Editor.Configuration;
using FYD.UnityPublisher.Editor.Models;
using FYD.UnityPublisher.Editor.Networking;
using FYD.UnityPublisher.Editor.Packaging;
using FYD.UnityPublisher.Editor.Validation;
using UnityEditor;
using UnityEngine;

namespace FYD.UnityPublisher.Editor.UI
{
    /// <summary>Main Editor UI for build, package, connection test and chunk upload.</summary>
    public sealed class FYDUnityPublisherWindow : EditorWindow
    {
        private FYDPublisherSettings _settings;
        private string _password = string.Empty;
        private string _existingBuildFolder = string.Empty;
        private string _status = "Sẵn sàng";
        private readonly StringBuilder _log = new StringBuilder();
        private Vector2 _scroll;
        private Vector2 _logScroll;
        private float _progress;
        private bool _isBusy;
        private CancellationTokenSource _cancellation;
        private FYDPackagingResult _lastPackage;
        private FYDUploadSession _lastUploadSession;

        [MenuItem("Tools/FYD/Unity Publisher", priority = 1200)]
        public static void Open()
        {
            var window = GetWindow<FYDUnityPublisherWindow>("FYD Unity Publisher");
            window.minSize = new Vector2(650, 720);
            window.Show();
        }

        private void OnEnable()
        {
            _settings = FYDPublisherSettings.instance;
            _password = _settings.LoadCredential();
            _existingBuildFolder = _settings.buildOutputFolder;
        }

        private void OnDisable()
        {
            _settings?.Persist();
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;
        }

        private void OnGUI()
        {
            if (_settings == null) OnEnable();
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                DrawHeader();
                EditorGUI.BeginDisabledGroup(_isBusy);
                DrawProjectSettings();
                DrawBuildSettings();
                DrawActions();
                EditorGUI.EndDisabledGroup();
                DrawProgress();
                DrawLog();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("FYD Unity Publisher", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build/đóng gói WebGL thành release bất biến và upload an toàn theo chunk qua WordPress REST API.",
                MessageType.Info);
        }

        private void DrawProjectSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Project / App", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _settings.appId = EditorGUILayout.TextField("App ID", _settings.appId);
            _settings.displayName = EditorGUILayout.TextField("Display Name", _settings.displayName);
            _settings.websiteUrl = EditorGUILayout.TextField("Website HTTPS", _settings.websiteUrl);
            _settings.wordpressUsername = EditorGUILayout.TextField("WordPress Username", _settings.wordpressUsername);
            _password = EditorGUILayout.PasswordField("Application Password", _password);
            _settings.rememberCredential = EditorGUILayout.Toggle("Lưu credential cục bộ", _settings.rememberCredential);
            if (EditorGUI.EndChangeCheck()) _settings.Persist();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Test Connection")) RunOperationAsync(TestConnectionAsync);
                if (GUILayout.Button("Forget Credential"))
                {
                    _settings.ForgetCredential();
                    _settings.rememberCredential = false;
                    _password = string.Empty;
                    _settings.Persist();
                    AppendLog("Đã xóa credential khỏi EditorPrefs.");
                }
            }
        }

        private void DrawBuildSettings()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Build / Package", EditorStyles.boldLabel);
            _settings.buildOutputFolder = DrawFolderField("Build output", _settings.buildOutputFolder);
            _existingBuildFolder = DrawFolderField("Build có sẵn", _existingBuildFolder);
            _settings.packageOutputFolder = DrawFolderField("Package output", _settings.packageOutputFolder);
            _settings.releaseVersion = EditorGUILayout.TextField("Version", _settings.releaseVersion);
            _settings.releaseNotes = EditorGUILayout.TextArea(_settings.releaseNotes, GUILayout.MinHeight(48));
            _settings.developmentBuild = EditorGUILayout.Toggle("Development Build", _settings.developmentBuild);
            _settings.compression = (FYDCompressionMethod)EditorGUILayout.EnumPopup("Compression", _settings.compression);
            _settings.chunkSizeMiB = EditorGUILayout.IntSlider("Chunk size (MiB)", _settings.chunkSizeMiB, 1, 20);
            _settings.requestTimeoutSeconds = EditorGUILayout.IntSlider("Request timeout (s)", _settings.requestTimeoutSeconds, 15, 600);
            if (GUI.changed) _settings.Persist();
        }

        private void DrawActions()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Publish", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Only", GUILayout.Height(30))) RunOperationAsync(BuildOnlyAsync);
                if (GUILayout.Button("Package Existing", GUILayout.Height(30))) RunOperationAsync(PackageExistingAsync);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build + Package", GUILayout.Height(30))) RunOperationAsync(BuildAndPackageAsync);
                if (GUILayout.Button("Upload Chunks", GUILayout.Height(30))) RunOperationAsync(UploadLastPackageAsync);
                if (GUILayout.Button("Build + Upload Chunks", GUILayout.Height(30))) RunOperationAsync(BuildAndUploadAsync);
            }
            EditorGUILayout.HelpBox(
                "Bản Foundation/Upload hiện tạo staging upload an toàn. Finalize, Activate và Rollback được mở ở giai đoạn Release tiếp theo.",
                MessageType.None);
        }

        private void DrawProgress()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Progress", EditorStyles.boldLabel);
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, Mathf.Clamp01(_progress), _status);
            if (_isBusy && GUILayout.Button("Cancel")) _cancellation?.Cancel();
        }

        private void DrawLog()
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Log", EditorStyles.boldLabel);
                if (GUILayout.Button("Copy", GUILayout.Width(70))) EditorGUIUtility.systemCopyBuffer = _log.ToString();
                if (GUILayout.Button("Clear", GUILayout.Width(70))) _log.Clear();
            }
            using (var scroll = new EditorGUILayout.ScrollViewScope(_logScroll, GUILayout.MinHeight(160)))
            {
                _logScroll = scroll.scrollPosition;
                EditorGUILayout.SelectableLabel(_log.ToString(), EditorStyles.textArea, GUILayout.ExpandHeight(true));
            }
        }

        private string DrawFolderField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                value = EditorGUILayout.TextField(label, value);
                if (GUILayout.Button("…", GUILayout.Width(32)))
                {
                    string selected = EditorUtility.OpenFolderPanel(label, string.IsNullOrWhiteSpace(value) ? Directory.GetCurrentDirectory() : Path.GetFullPath(value), string.Empty);
                    if (!string.IsNullOrWhiteSpace(selected)) value = selected.Replace('\\', '/');
                }
            }
            return value;
        }

        private void RunOperationAsync(Func<CancellationToken, Task> operation)
        {
            if (_isBusy) return;
            _isBusy = true;
            _progress = 0f;
            _cancellation = new CancellationTokenSource();
            Repaint();
            ExecuteOperationAsync(operation, _cancellation.Token);
        }

        private async void ExecuteOperationAsync(Func<CancellationToken, Task> operation, CancellationToken token)
        {
            try
            {
                _settings.Persist();
                _settings.StoreCredential(_password);
                await operation(token);
                _status = "Hoàn tất";
            }
            catch (OperationCanceledException)
            {
                _status = "Đã hủy";
                AppendLog("Đã hủy thao tác.");
            }
            catch (FYDPublisherApiException exception)
            {
                _status = exception.Code;
                AppendLog("Lỗi API " + exception.Code + ": " + exception.Message +
                          (string.IsNullOrEmpty(exception.RequestId) ? string.Empty : " | requestId=" + exception.RequestId));
            }
            catch (Exception exception)
            {
                _status = "Lỗi";
                AppendLog(exception.GetType().Name + ": " + exception.Message);
            }
            finally
            {
                _isBusy = false;
                _cancellation?.Dispose();
                _cancellation = null;
                Repaint();
            }
        }

        private async Task TestConnectionAsync(CancellationToken token)
        {
            _status = "Đang kiểm tra kết nối";
            FYDStatusResponse response = await CreateClient().GetStatusAsync(token);
            _progress = 1f;
            AppendLog("Kết nối thành công. Plugin " + response.data.pluginVersion + ", API " + response.data.apiVersion +
                      ", user " + response.data.user + ".");
        }

        private Task BuildOnlyAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _status = "Đang build WebGL";
            string path = FYDWebGLBuildService.Build(_settings);
            _existingBuildFolder = path;
            _progress = 1f;
            AppendLog("Build thành công: " + path);
            return Task.CompletedTask;
        }

        private async Task PackageExistingAsync(CancellationToken token)
        {
            await PackageFolderAsync(_existingBuildFolder, token);
        }

        private async Task BuildAndPackageAsync(CancellationToken token)
        {
            await BuildOnlyAsync(token);
            await PackageFolderAsync(_existingBuildFolder, token);
        }

        private async Task UploadLastPackageAsync(CancellationToken token)
        {
            if (_lastPackage == null || !File.Exists(_lastPackage.ArchivePath))
            {
                throw new InvalidOperationException("Chưa có package trong phiên Editor này.");
            }
            await UploadPackageAsync(_lastPackage, token);
        }

        private async Task BuildAndUploadAsync(CancellationToken token)
        {
            await BuildAndPackageAsync(token);
            await UploadPackageAsync(_lastPackage, token);
        }

        private async Task PackageFolderAsync(string folder, CancellationToken token)
        {
            _status = "Đang kiểm tra và đóng gói";
            var progress = new Progress<float>(value => { _progress = value; Repaint(); });
            _lastPackage = await FYDPackagingService.PackageAsync(folder, _settings, progress, token);
            _existingBuildFolder = _lastPackage.BuildDirectory;
            AppendLog("Package: " + _lastPackage.ArchivePath);
            AppendLog("Manifest: " + _lastPackage.ManifestPath);
            AppendLog("SHA-256: " + _lastPackage.Manifest.archiveSha256);
        }

        private async Task UploadPackageAsync(FYDPackagingResult package, CancellationToken token)
        {
            _status = "Đang upload theo chunk";
            _progress = 0f;
            var upload = new FYDChunkUploadService(CreateClient());
            var progress = new Progress<float>(value => { _progress = value; Repaint(); });
            var log = new Progress<string>(AppendLog);
            _lastUploadSession = await upload.UploadAsync(
                package,
                _settings.chunkSizeMiB * 1024 * 1024,
                _lastUploadSession,
                progress,
                log,
                token);
            AppendLog("Upload ID: " + _lastUploadSession.UploadId + ".");
        }

        private FYDPublisherClient CreateClient()
        {
            if (string.IsNullOrWhiteSpace(_settings.wordpressUsername) || string.IsNullOrWhiteSpace(_password))
            {
                throw new InvalidOperationException("Cần WordPress username và Application Password.");
            }
            return new FYDPublisherClient(_settings.websiteUrl, _settings.wordpressUsername, _password, _settings.requestTimeoutSeconds);
        }

        private void AppendLog(string message)
        {
            _log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(message);
            _logScroll.y = float.MaxValue;
            Repaint();
        }
    }
}
