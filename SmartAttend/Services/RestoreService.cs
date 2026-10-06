using System;
using System.IO;
using SmartAttend.Database;
using SmartAttend.Database;

namespace SmartAttend.Services
{
    public static class RestoreService
    {
        

        // ── Restore from an explicitly chosen file ─────────
        // This is the primary path now: the user manually selects
        // their backup file via the file picker, so we don't need
        // to guess which folder it lives in.
        public static RestoreResult RestoreFromFile(string backupFilePath)
        {
            try
            {
                if (string.IsNullOrEmpty(backupFilePath) ||
                    !File.Exists(backupFilePath))
                {
                    return new RestoreResult
                    {
                        Success = false,
                        ErrorMessage = "Selected backup file was not found."
                    };
                }

                // Safety — back up the current (likely empty/fresh) db first
                string safetyPath = DatabaseHelper.DbPath + ".old";
                if (File.Exists(DatabaseHelper.DbPath))
                    File.Copy(DatabaseHelper.DbPath, safetyPath, overwrite: true);

                File.Copy(backupFilePath, DatabaseHelper.DbPath, overwrite: true);

                return new RestoreResult
                {
                    Success = true,
                    FileName = Path.GetFileName(backupFilePath)
                };
            }
            catch (Exception ex)
            {
                return new RestoreResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // ── Optional convenience: auto-detect latest backup ─
        // Kept for cases where a backup_path setting already exists
        // (e.g. same PC, already set up once before). Not relied on
        // for the main restore flow anymore since it can't find
        // anything on a genuinely fresh install / new PC.
        public static string? GetLatestBackup()
        {
            string backupFolder = DatabaseHelper.GetSetting("backup_path");

            if (string.IsNullOrEmpty(backupFolder))
            {
                try
                {
                    backupFolder = BackupService.GetBackupFolder();
                }
                catch
                {
                    return null;
                }
            }

            if (string.IsNullOrEmpty(backupFolder) ||
                !Directory.Exists(backupFolder))
                return null;

            var latest = Directory
                .GetFiles(backupFolder, "smartattend_backup_*.db")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .FirstOrDefault();

            return latest;
        }

        // ── Get latest backup display name for UI ──────────
        // Only meaningful if GetLatestBackup() found something via
        // the auto-detect convenience path above.
        public static string GetLatestBackupName()
        {
            string? latest = GetLatestBackup();
            if (latest == null)
                return "No backups found";

            var name = Path.GetFileNameWithoutExtension(latest);
            var parts = name.Split('_');
            if (parts.Length >= 4 &&
                DateTime.TryParse(parts[2], out DateTime dt))
            {
                return $"{dt:MMMM dd, yyyy} (Backup #{parts[3]})";
            }
            return Path.GetFileName(latest);
        }
    }

    // ── Result model ───────────────────────────────────────
    public class RestoreResult
    {
        public bool Success { get; set; }
        public string? FileName { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
