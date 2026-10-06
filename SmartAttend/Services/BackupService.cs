using System;
using System.IO;
using System.Linq;
using SmartAttend.Database;

namespace SmartAttend.Services
{
    public static class BackupService
    {
        // ── Constants ──────────────────────────────────────
        private const int KeepDays = 30;
        

        // ── Get backup folder ──────────────────────────────
        public static string GetBackupFolder()
        {
            // Try OneDrive first
            string oneDrive = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "OneDrive", "SmartAttend Backups");

            if (Directory.Exists(Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile),
                "OneDrive")))
            {
                return oneDrive;
            }

            // Fall back to Documents
            return Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "SmartAttend Backups");
        }

        // ── Run backup ─────────────────────────────────────
        public static BackupResult RunBackup()
        {
            try
            {
                // Get or use saved folder path
                string savedPath =
                    DatabaseHelper.GetSetting("backup_path");
                string backupFolder =
                    string.IsNullOrEmpty(savedPath)
                    ? GetBackupFolder()
                    : savedPath;

                // Create folder if not exists
                Directory.CreateDirectory(backupFolder);

                // Generate filename with timestamp
                // e.g. smartattend_backup_2026-06-25_2.db
                string datePart =
                    DateTime.Now.ToString("yyyy-MM-dd");
                int count = CountTodayBackups(
                    backupFolder, datePart) + 1;
                string fileName =
                    $"smartattend_backup_{datePart}_{count}.db";
                string destPath =
                    Path.Combine(backupFolder, fileName);

                // Copy database
                File.Copy(DatabaseHelper.DbPath, destPath, overwrite: false);

                // Save last backup info
                DatabaseHelper.SaveSetting(
                    "backup_last_run",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                DatabaseHelper.SaveSetting(
                    "backup_path", backupFolder);

                // Cleanup old backups
                CleanupOldBackups(backupFolder);

                return new BackupResult
                {
                    Success = true,
                    FileName = fileName,
                    Path = destPath
                };
            }
            catch (Exception ex)
            {
                return new BackupResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // ── Count how many backups exist for today ─────────
        private static int CountTodayBackups(
            string folder, string datePart)
        {
            return Directory
                .GetFiles(folder,
                    $"smartattend_backup_{datePart}_*.db")
                .Length;
        }

        // ── Delete backups older than 30 days ──────────────
        private static void CleanupOldBackups(string folder)
        {
            var cutoff = DateTime.Now.AddDays(-KeepDays);

            var oldFiles = Directory
                .GetFiles(folder, "smartattend_backup_*.db")
                .Where(f =>
                {
                    // Extract date from filename
                    // smartattend_backup_2026-06-25_1.db
                    var name = Path.GetFileNameWithoutExtension(f);
                    var parts = name.Split('_');
                    // parts[2] = date part
                    if (parts.Length >= 3 &&
                        DateTime.TryParse(parts[2],
                            out DateTime fileDate))
                    {
                        return fileDate < cutoff;
                    }
                    return false;
                });

            foreach (var file in oldFiles)
                File.Delete(file);
        }

        // ── Get last backup info for UI display ────────────
        public static string GetLastBackupTime()
        {
            string last =
                DatabaseHelper.GetSetting("backup_last_run");
            if (string.IsNullOrEmpty(last))
                return "Never";

            if (DateTime.TryParse(last, out DateTime dt))
                return dt.ToString("MMMM dd, yyyy 'at' HH:mm");

            return "Never";
        }
    }

    // ── Result model ───────────────────────────────────────
    public class BackupResult
    {
        public bool Success { get; set; }
        public string FileName { get; set; }
        public string Path { get; set; }
        public string ErrorMessage { get; set; }
    }
}