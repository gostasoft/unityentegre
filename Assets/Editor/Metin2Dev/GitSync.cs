using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Metin2Dev
{
    [InitializeOnLoad]
    public static class GitSync
    {
        private const string AutoSyncKey = "Metin2Dev.AutoSync";
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static GitSync()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetBool(AutoSyncKey, false))
                    TryPullLatest(false);
            };
        }

        [MenuItem("Tools/Metin2 Dev/Pull Latest")]
        public static void PullLatest()
        {
            TryPullLatest(true);
        }

        [MenuItem("Tools/Metin2 Dev/Auto Sync")]
        public static void ToggleAutoSync()
        {
            bool next = !EditorPrefs.GetBool(AutoSyncKey, false);
            EditorPrefs.SetBool(AutoSyncKey, next);
            Menu.SetChecked("Tools/Metin2 Dev/Auto Sync", next);
            Debug.Log($"[Metin2Dev] Auto Sync: {(next ? "ON" : "OFF")}");
        }

        [MenuItem("Tools/Metin2 Dev/Auto Sync", true)]
        private static bool ValidateAutoSync()
        {
            Menu.SetChecked("Tools/Metin2 Dev/Auto Sync", EditorPrefs.GetBool(AutoSyncKey, false));
            return true;
        }

        [MenuItem("Tools/Metin2 Dev/Status")]
        public static void ShowStatus()
        {
            string branch = RunGit("rev-parse --abbrev-ref HEAD", out int branchCode).Trim();
            string status = RunGit("status --short", out int statusCode).Trim();
            string remote = RunGit("remote get-url origin", out int remoteCode).Trim();

            if (branchCode != 0 || statusCode != 0 || remoteCode != 0)
            {
                EditorUtility.DisplayDialog("Metin2 Dev Git Status", "Git repository bilgileri okunamadı. Console'u kontrol et.", "Tamam");
                return;
            }

            string message = $"Branch: {branch}\nRemote: {remote}\n\nWorking tree: {(string.IsNullOrEmpty(status) ? "CLEAN" : "LOCAL CHANGES VAR")}";
            EditorUtility.DisplayDialog("Metin2 Dev Git Status", message, "Tamam");
        }

        private static void TryPullLatest(bool showDialog)
        {
            string localChanges = RunGit("status --porcelain", out int statusCode);
            if (statusCode != 0)
            {
                Report("Git status alınamadı. Git kurulumunu ve repo bağlantısını kontrol et.", true, showDialog);
                return;
            }

            if (!string.IsNullOrWhiteSpace(localChanges))
            {
                Report("Yerel değişiklikler var. Güvenlik için otomatik pull yapılmadı. Önce commit/stash yap.", true, showDialog);
                return;
            }

            string fetch = RunGit("fetch origin main", out int fetchCode);
            if (fetchCode != 0)
            {
                Report("GitHub fetch başarısız:\n" + fetch, true, showDialog);
                return;
            }

            string behind = RunGit("rev-list --count HEAD..origin/main", out int behindCode).Trim();
            if (behindCode != 0)
            {
                Report("Git commit durumu okunamadı.", true, showDialog);
                return;
            }

            if (behind == "0")
            {
                Report("Proje zaten güncel.", false, showDialog);
                return;
            }

            string pull = RunGit("pull --ff-only origin main", out int pullCode);
            if (pullCode != 0)
            {
                Report("Pull başarısız. Fast-forward mümkün olmayabilir:\n" + pull, true, showDialog);
                return;
            }

            AssetDatabase.Refresh();
            Report($"GitHub'dan {behind} yeni commit çekildi. Unity yenilendi.", false, showDialog);
        }

        private static string RunGit(string arguments, out int exitCode)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = ProjectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
                return string.IsNullOrWhiteSpace(error) ? output : output + "\n" + error;
            }
            catch (Exception ex)
            {
                exitCode = -1;
                return ex.ToString();
            }
        }

        private static void Report(string message, bool isError, bool showDialog)
        {
            if (isError) Debug.LogError("[Metin2Dev] " + message);
            else Debug.Log("[Metin2Dev] " + message);

            if (showDialog)
                EditorUtility.DisplayDialog("Metin2 Dev", message, "Tamam");
        }
    }
}
