using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using SmartAttend.Services;

namespace SmartAttend
{
    public partial class RestorePromptWindow : Window
    {
        private string? _selectedBackupPath;

        public RestorePromptWindow()
        {
            InitializeComponent();
        }

        // ── Select backup file manually ─────────────────────
        private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select SmartAttend Backup File",
                Filter = "SmartAttend Backup (*.db)|*.db|All Files (*.*)|*.*",
                InitialDirectory = GetInitialBrowseFolder()
            };

            if (dialog.ShowDialog() != true)
                return;

            _selectedBackupPath = dialog.FileName;

            TxtBackupName.Text = Path.GetFileName(dialog.FileName);
            TxtBackupPath.Text = dialog.FileName;
            BackupInfoCard.Visibility = Visibility.Visible;

            TxtStatus.Visibility = Visibility.Collapsed;
            BtnRestore.IsEnabled = true;
        }

        // Best-effort starting folder for the dialog — just a convenience,
        // not something we depend on for correctness.
        private string GetInitialBrowseFolder()
        {
            try
            {
                string guess = BackupService.GetBackupFolder();
                if (Directory.Exists(guess))
                    return guess;
            }
            catch
            {
                // ignore — fall back below
            }

            return Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
        }

        // ── Restore from the manually selected file ─────────
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedBackupPath))
            {
                ShowStatus("Please select a backup file first.");
                return;
            }

            BtnRestore.IsEnabled = false;
            BtnSelectFile.IsEnabled = false;
            BtnFresh.IsEnabled = false;

            var result = RestoreService.RestoreFromFile(_selectedBackupPath);

            if (!result.Success)
            {
                MessageBox.Show(
                    $"Restore failed:\n{result.ErrorMessage}",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                BtnRestore.IsEnabled = true;
                BtnSelectFile.IsEnabled = true;
                BtnFresh.IsEnabled = true;
                return;
            }

            MessageBox.Show(
                "Data restored successfully!\n" +
                "SmartAttend will now restart to load your restored data.",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            RestartApplication();
        }

        // ── Start fresh (no backup) ─────────────────────────
        private void BtnFresh_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Are you sure you want to start fresh?\n" +
                "Any existing backup files will not be deleted but\n" +
                "this device will start with no data.",
                "SmartAttend",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            SetupWindow setup = new SetupWindow();
            setup.Show();
            this.Close();
        }

        private void ShowStatus(string message)
        {
            TxtStatus.Text = message;
            TxtStatus.Visibility = Visibility.Visible;
        }

        // Restart the whole process so the app reads the freshly
        // restored database cleanly, with no stale connections
        // or cached settings left over from the empty pre-restore DB.
        private void RestartApplication()
        {
            try
            {
                string? exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                    System.Diagnostics.Process.Start(exePath);
            }
            catch
            {
                // If relaunch fails for any reason, fall through and
                // still shut down — better than leaving a half-restored
                // session running.
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }
    }
}
