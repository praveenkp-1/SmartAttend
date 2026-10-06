using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SmartAttend.Database;
using SmartAttend.Services;
using SmartAttend.ViewModels;
using Microsoft.Win32;


namespace SmartAttend.View
{
    public partial class SettingView : UserControl
    {
        private SettingsViewModel _viewModel;
        private DeviceConfigViewModel _deviceConfigViewModel;
        private ChangePasswordViewModel _passwordViewModel;

        // Active nav colors
        private static readonly SolidColorBrush ActiveBg = new SolidColorBrush(Color.FromRgb(0xE3, 0xF8, 0xF2));
        private static readonly SolidColorBrush ActiveFg = new SolidColorBrush(Color.FromRgb(0x00, 0xC9, 0xA7));
        private static readonly SolidColorBrush InactiveBg = Brushes.Transparent;
        private static readonly SolidColorBrush InactiveFg = new SolidColorBrush(Color.FromRgb(0x4A, 0x65, 0x90));
        private static readonly SolidColorBrush InactiveIcon = new SolidColorBrush(Color.FromRgb(0x5E, 0x70, 0x8A));

        public SettingView()
        {
            InitializeComponent();
            _viewModel = new SettingsViewModel();
            _deviceConfigViewModel = new DeviceConfigViewModel();
            _passwordViewModel = new ChangePasswordViewModel();
            DataContext = _viewModel;

            Loaded += (s, e) =>
            {
                LoadSettings();

                foreach (ComboBoxItem item in CmbAuthMode.Items)
                {
                    if (int.Parse(item.Tag.ToString()) == _viewModel.AuthMode)
                    {
                        CmbAuthMode.SelectedItem = item;
                        break;
                    }
                }
                LoadDepartments();
                LoadDevices();
            };
        }

        // ── Sidebar navigation ──────────────────────────────
        private void NavItem_Click(object sender, MouseButtonEventArgs e)
        {
            var tag = (sender as Border)?.Tag?.ToString();

            var map = new Dictionary<string, FrameworkElement>
    {
        { "General",         SectionGeneral },
        { "AttendanceRules", SectionAttendanceRules },
        { "DeviceSettings",  SectionDeviceSettings },
        { "DeviceConfig",    SectionDeviceConfig },
        { "Departments",     SectionDepartments },
        { "Security",        SectionSecurity },
        { "Backup",          SectionBackup },
    };

            if (tag != null && map.TryGetValue(tag, out var target))
            {
                SmoothScrollToElement(target);
            }

            // Update sidebar highlight
            foreach (var child in SidebarPanel.Children.OfType<Border>())
            {
                bool isActive = child.Tag?.ToString() == tag;
                child.Background = isActive
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E3F8F2"))
                    : Brushes.Transparent;

                var texts = child.FindVisualChildren<TextBlock>();
                foreach (var tb in texts)
                    tb.Foreground = isActive
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C9A7"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A6590"));
            }
        }

        private void ShowSection(string section)
        {
            SectionGeneral.Visibility = Visibility.Collapsed;
            SectionAttendanceRules.Visibility = Visibility.Collapsed;
            SectionDeviceSettings.Visibility = Visibility.Collapsed;
            SectionDeviceConfig.Visibility = Visibility.Collapsed;
            SectionDepartments.Visibility = Visibility.Collapsed;
            SectionSecurity.Visibility = Visibility.Collapsed;
            SectionBackup.Visibility = Visibility.Collapsed;

            switch (section)
            {
                case "General":
                    SectionGeneral.Visibility = Visibility.Visible;
                    break;
                case "AttendanceRules":
                    SectionAttendanceRules.Visibility = Visibility.Visible;
                    break;
                case "DeviceSettings":
                    SectionDeviceSettings.Visibility = Visibility.Visible;
                    break;
                case "DeviceConfig":
                    SectionDeviceConfig.Visibility = Visibility.Visible;
                    break;
                case "Departments":
                    SectionDepartments.Visibility = Visibility.Visible;
                    break;
                case "Security":
                    SectionSecurity.Visibility = Visibility.Visible;
                    break;
                case "Backup":
                    SectionBackup.Visibility = Visibility.Visible;
                    break;
            }
        }

        // ── Load settings ──────────────────────────────────
        private void LoadSettings()
        {
            _viewModel.LoadSettings();
            _viewModel.LoadGeneralSettings();

            // Load into fields
            TxtCompanyName.Text = _viewModel.CompanyName;
            TxtAdminUsername.Text = _viewModel.AdminUsername;

            if (!string.IsNullOrEmpty(_viewModel.WorkStart))
                TxtStartTime.Text = _viewModel.WorkStart;
            if (!string.IsNullOrEmpty(_viewModel.WorkEnd))
                TxtEndTime.Text = _viewModel.WorkEnd;
            if (!string.IsNullOrEmpty(_viewModel.GracePeriod))
                TxtGracePeriod.Text = _viewModel.GracePeriod;

            // Sync toggle visuals with loaded state
            UpdateToggleVisual(ToggleAutoSync,
                DotAutoSync, _viewModel.AutoSync);
            UpdateToggleVisual(ToggleSdBackup,
                DotSdBackup, _viewModel.SdBackup);
            UpdateToggleVisual(ToggleBuzzer,
                DotBuzzer, _viewModel.BuzzerEnabled);
            UpdateToggleVisual(ToggleRequireLogin,
                DotRequireLogin,_viewModel.RequireLogin);

        }

        // ── Load backup info on page load ─────────────────────
        private void LoadBackupInfo()
        {
            // Set folder path
            string savedPath = DatabaseHelper.GetSetting("backup_path");
            TxtBackupFolder.Text = string.IsNullOrEmpty(savedPath)
                ? BackupService.GetBackupFolder()
                : savedPath;

            // Set last backup time
            TxtLastBackup.Text = BackupService.GetLastBackupTime();

            // Set status badge
            string last = DatabaseHelper.GetSetting("backup_last_run");
            if (string.IsNullOrEmpty(last))
            {
                // Never backed up
                BackupStatusBadge.Background = new SolidColorBrush(
                    Color.FromRgb(255, 235, 235));
                TxtBackupStatus.Text = "Never";
                TxtBackupStatus.Foreground = new SolidColorBrush(
                    Color.FromRgb(192, 57, 43));
            }
            else
            {
                // Has backup
                BackupStatusBadge.Background = new SolidColorBrush(
                    Color.FromRgb(232, 255, 248));
                TxtBackupStatus.Text = "✓ Done";
                TxtBackupStatus.Foreground = new SolidColorBrush(
                    Color.FromRgb(0, 168, 140));
            }
        }

        // ── Toggle visual helper ───────────────────────────
        private void UpdateToggleVisual(
            Border toggle, Ellipse dot, bool isOn)
        {
            if (toggle == null || dot == null) return;

            toggle.Background = new SolidColorBrush(
                isOn
                    ? Color.FromRgb(0x00, 0xC9, 0xA7)
                    : Color.FromRgb(0xDD, 0xE4, 0xF0));

            dot.HorizontalAlignment = isOn
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;

            dot.Margin = isOn
                ? new Thickness(0, 0, 3, 0)
                : new Thickness(3, 0, 0, 0);
        }

        // ── Toggle clicks ──────────────────────────────────
        private async void ToggleAutoSync_Click(
            object sender, RoutedEventArgs e)
        {
            _viewModel.AutoSync = !_viewModel.AutoSync;
            UpdateToggleVisual(ToggleAutoSync,
                DotAutoSync, _viewModel.AutoSync);
            await _viewModel.PushTogglesToDevice();
        }

        private async void ToggleSdBackup_Click(
            object sender, RoutedEventArgs e)
        {
            _viewModel.SdBackup = !_viewModel.SdBackup;
            UpdateToggleVisual(ToggleSdBackup,
                DotSdBackup, _viewModel.SdBackup);
            await _viewModel.PushTogglesToDevice();
        }

        private async void ToggleBuzzer_Click(
            object sender, RoutedEventArgs e)
        {
            _viewModel.BuzzerEnabled = !_viewModel.BuzzerEnabled;
            UpdateToggleVisual(ToggleBuzzer,
                DotBuzzer, _viewModel.BuzzerEnabled);
            await _viewModel.PushTogglesToDevice();
        }

        private async void TogglePin_Click(
            object sender, RoutedEventArgs e)
        {
            _viewModel.AllowPIN = !_viewModel.AllowPIN;
            await _viewModel.PushTogglesToDevice();
        }

        private void ToggleRequireLogin_Click(object sender, MouseButtonEventArgs e)
        {
            _viewModel.RequireLogin = !_viewModel.RequireLogin;
            UpdateToggleVisual(ToggleRequireLogin,
                // find the ellipse inside
                ToggleRequireLogin.Child as Ellipse,
                _viewModel.RequireLogin);
        }

        // ── Departments ────────────────────────────────────
        private void LoadDepartments()
        {
            DepartmentList.ItemsSource = null;
            DepartmentList.ItemsSource =
                _viewModel.LoadDepartments();
        }

        private void AddDepartment_Click(
            object sender, RoutedEventArgs e)
        {
            string name = TxtDepartment.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show(
                    "Please enter a department name.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            _viewModel.AddDepartment(name);
            TxtDepartment.Text = "";
            LoadDepartments();
        }

        private void DeleteDepartment_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string name = btn.Tag?.ToString() ?? "";

            var result = MessageBox.Show(
                $"Delete department '{name}'?",
                "SmartAttend",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteDepartment(name);
                LoadDepartments();
            }
        }

        // ── Save rules ─────────────────────────────────────
        private async void SaveRules_Click(object sender, RoutedEventArgs e)
        {
            var selected = (ComboBoxItem)CmbAuthMode.SelectedItem;
            int authMode = selected != null ? int.Parse(selected.Tag.ToString()) : 0;

            await _viewModel.SaveAttendanceRulesAsync(
                TxtStartTime.Text.Trim(),
                TxtEndTime.Text.Trim(),
                TxtGracePeriod.Text.Trim(),
                authMode);
        }

        // ── Device Config (moved inline from DeviceConfigWindow) ──
        private void LoadDevices()
        {
            var devices = _deviceConfigViewModel.LoadDevices();
            DeviceList.ItemsSource = devices;
            TxtNoDevices.Visibility = devices.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private async void Scan_Click(
            object sender, RoutedEventArgs e)
        {
            TxtScanStatus.Foreground =
                new SolidColorBrush(Colors.Gray);
            TxtScanStatus.Text = "Scanning all IPs...";

            string found = await _deviceConfigViewModel.ScanForDevicesAsync();

            if (!string.IsNullOrEmpty(found))
            {
                TxtDeviceIP.Text = found;
                TxtScanStatus.Text = _deviceConfigViewModel.ScanStatus;
                TxtScanStatus.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0x00, 0xC9, 0xA7));
            }
            else
            {
                TxtScanStatus.Text = _deviceConfigViewModel.ScanStatus;
                TxtScanStatus.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0xC0, 0x39, 0x2B));
            }
        }

        private async void Connect_Click(
            object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtScanStatus.Text = "Connecting...";
            TxtScanStatus.Foreground =
                new SolidColorBrush(Colors.Gray);

            bool success = await _deviceConfigViewModel.ConnectDeviceAsync(
                TxtDeviceName.Text.Trim(),
                TxtDeviceIP.Text.Trim(),
                TxtDevicePort.Text.Trim(),
                TxtWifiSSID.Text.Trim(),
                TxtWifiPassword.Password.Trim());

            if (!success)
            {
                TxtError.Text = _deviceConfigViewModel.ErrorMessage;
                TxtError.Visibility = Visibility.Visible;
                TxtScanStatus.Text = _deviceConfigViewModel.ScanStatus;
                return;
            }

            MessageBox.Show(
                $"Device '{TxtDeviceName.Text.Trim()}' " +
                $"connected!\n" +
                $"IP: {TxtDeviceIP.Text.Trim()}\n" +
                $"{_deviceConfigViewModel.ScanStatus}",
                "Device Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LoadDevices();
        }

        private async void TestDevice_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string ip = btn.Tag?.ToString() ?? "";

            bool online = await _deviceConfigViewModel.TestDeviceAsync(ip);

            MessageBox.Show(
                online
                    ? $"Device at {ip} is Online ✓"
                    : $"Device at {ip} is Offline",
                "Test Connection",
                MessageBoxButton.OK,
                online
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);

            LoadDevices();
        }

        private void RemoveDevice_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string id = btn.Tag?.ToString() ?? "";

            var result = MessageBox.Show(
                "Remove this device from the app?\n" +
                "The device will need to be reconfigured.",
                "SmartAttend",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _deviceConfigViewModel.RemoveDevice(id);
                LoadDevices();
            }
        }

        // ── Change Password (moved inline from ChangePasswordWindow) ──
        private void Change_Click(object sender, RoutedEventArgs e)
        {
            TxtPasswordError.Visibility = Visibility.Collapsed;

            bool success = _passwordViewModel.ChangePassword(
                TxtCurretPassword.Password.Trim(),
                TxtPassword.Password.Trim(),
                TxtConfirmPassword.Password.Trim());

            if (!success)
            {
                TxtPasswordError.Text = _passwordViewModel.ErrorMessage;
                TxtPasswordError.Visibility = Visibility.Visible;
                return;
            }

            TxtCurretPassword.Password = "";
            TxtPassword.Password = "";
            TxtConfirmPassword.Password = "";

            MessageBox.Show(
                "Password was changed.",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void SmoothScrollToElement(FrameworkElement target)
        {
            var transform = target.TransformToAncestor(MainScroller);
            var targetPosition = transform.Transform(new Point(0, 0));

            double from = MainScroller.VerticalOffset;
            double to = MainScroller.VerticalOffset + targetPosition.Y - 20; // -20 for padding
            double duration = 400; // milliseconds
            int steps = 30;
            double interval = duration / steps;
            double step = (to - from) / steps;
            int currentStep = 0;

            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(interval);
            timer.Tick += (s, e) =>
            {
                currentStep++;
                double progress = (double)currentStep / steps;

                // Ease in-out curve
                double eased = progress < 0.5
                    ? 2 * progress * progress
                    : 1 - Math.Pow(-2 * progress + 2, 2) / 2;

                MainScroller.ScrollToVerticalOffset(from + (to - from) * eased);

                if (currentStep >= steps)
                {
                    timer.Stop();
                    MainScroller.ScrollToVerticalOffset(to);
                }
            };
            timer.Start();
        }

        private void SaveGeneral_Click(object sender, RoutedEventArgs e)
        {
            bool success = _viewModel.SaveGeneralSettings(
                TxtCompanyName.Text.Trim(),
                TxtAdminUsername.Text.Trim());

            if (!success)
            {
                MessageBox.Show(
                    "Company name and username cannot be empty.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(
                "General settings saved!",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ── Browse folder ──────────────────────────────────────
        private void BtnBrowseFolder_Click(object sender,
    RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Backup Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                TxtBackupFolder.Text = dialog.FolderName;
                DatabaseHelper.SaveSetting(
                    "backup_path", dialog.FolderName);
            }
        }

        // ── Backup Now ─────────────────────────────────────────
        private void BtnBackupNow_Click(object sender,
            RoutedEventArgs e)
        {
            BtnBackupNow.IsEnabled = false;
            BtnBackupNow.Content = "☁  Backing up...";

            var result = BackupService.RunBackup();

            if (!result.Success)
            {
                MessageBox.Show(
                    $"Backup failed:\n{result.ErrorMessage}",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                BtnBackupNow.IsEnabled = true;
                BtnBackupNow.Content = "☁  Backup Now";
                return;
            }

            // Show warning about end of day
            BackupWarning.Visibility = Visibility.Visible;

            // Refresh info
            LoadBackupInfo();

            BtnBackupNow.IsEnabled = true;
            BtnBackupNow.Content = "☁  Backup Now";

            MessageBox.Show(
                $"Backup complete ✅\n\n" +
                $"File: {result.FileName}\n\n" +
                $"Note: If employees are still checking\n" +
                $"in/out today, please backup again\n" +
                $"at end of day for complete data.",
                "SmartAttend",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
    // -- Helper 
    public static class VisualHelper
    {
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child))
                    yield return sub;
            }
        }
    }
}