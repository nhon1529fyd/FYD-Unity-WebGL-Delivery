using System;
using System.Diagnostics;
using System.IO;

namespace FYD.UnityPublisher.Editor.Packaging
{
    public sealed class FYDGitInfo
    {
        public string Commit { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public bool IsDirty { get; set; }
        public string Warning { get; set; } = string.Empty;
    }

    public static class FYDGitMetadata
    {
        public static FYDGitInfo Read(string workingDirectory)
        {
            var info = new FYDGitInfo();
            try
            {
                info.Commit = Run("rev-parse --short HEAD", workingDirectory);
                info.Branch = Run("branch --show-current", workingDirectory);
                info.IsDirty = !string.IsNullOrWhiteSpace(Run("status --porcelain", workingDirectory));
            }
            catch (Exception exception)
            {
                info.Warning = "Không đọc được Git metadata: " + exception.Message;
            }
            return info;
        }

        private static string Run(string arguments, string workingDirectory)
        {
            var startInfo = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Không khởi động được git.");
                }
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(3000))
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Lệnh git quá thời gian.");
                }
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(error.Trim());
                }
                return output.Trim();
            }
        }
    }
}
