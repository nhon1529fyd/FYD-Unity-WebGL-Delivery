using System;
using System.IO;
using System.Linq;
using FYD.UnityPublisher.Editor.Configuration;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace FYD.UnityPublisher.Editor.Build
{
    /// <summary>Runs Unity's WebGL build pipeline using enabled Build Settings scenes.</summary>
    public static class FYDWebGLBuildService
    {
        public static string Build(FYDPublisherSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException("Unity Web Build Support chưa được cài.");
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException("Đã hủy vì còn scene chưa lưu.");
            }

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path).Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("Build Settings chưa có scene được bật.");
            }

            string outputPath = Path.GetFullPath(settings.buildOutputFolder);
            Directory.CreateDirectory(outputPath);
            BuildOptions options = settings.developmentBuild ? BuildOptions.Development : BuildOptions.None;
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = options
            };
            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unity WebGL build thất bại: {report.summary.totalErrors} lỗi, {report.summary.totalWarnings} cảnh báo.");
            }
            return outputPath;
        }
    }
}
