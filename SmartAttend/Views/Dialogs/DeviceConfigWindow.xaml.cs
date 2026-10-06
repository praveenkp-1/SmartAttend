using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartAttend.ViewModels;

namespace SmartAttend
{
    public partial class DeviceConfigWindow : Window
    {
        private DeviceConfigViewModel _viewModel;

        public DeviceConfigWindow()
        {
            InitializeComponent();
            _viewModel = new DeviceConfigViewModel();
            DataContext = _viewModel;
            Loaded += (s, e) => LoadDevices();
        }

        // ── Load devices ───────────────────────────────────
        private void LoadDevices()
        {
            var devices = _viewModel.LoadDevices();
            DeviceList.ItemsSource = devices;
            TxtNoDevices.Visibility = devices.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        // ── Scan network ───────────────────────────────────
        private async void Scan_Click(
            object sender, RoutedEventArgs e)
        {
            TxtScanStatus.Foreground =
                new SolidColorBrush(Colors.Gray);
            TxtScanStatus.Text = "Scanning all IPs...";

            string found = await _viewModel.ScanForDevicesAsync();

            if (!string.IsNullOrEmpty(found))
            {
                TxtDeviceIP.Text = found;
                TxtScanStatus.Text = _viewModel.ScanStatus;
                TxtScanStatus.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(59, 109, 17));
            }
            else
            {
                TxtScanStatus.Text = _viewModel.ScanStatus;
                TxtScanStatus.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(163, 45, 45));
            }
        }

        // ── Connect device ─────────────────────────────────
        private async void Connect_Click(
            object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;
            TxtScanStatus.Text = "Connecting...";
            TxtScanStatus.Foreground =
                new SolidColorBrush(Colors.Gray);

            bool success = await _viewModel.ConnectDeviceAsync(
                TxtDeviceName.Text.Trim(),
                TxtDeviceIP.Text.Trim(),
                TxtDevicePort.Text.Trim(),
                TxtWifiSSID.Text.Trim(),
                TxtWifiPassword.Password.Trim());

            if (!success)
            {
                TxtError.Text = _viewModel.ErrorMessage;
                TxtError.Visibility = Visibility.Visible;
                TxtScanStatus.Text = _viewModel.ScanStatus;
                return;
            }

            MessageBox.Show(
                $"Device '{TxtDeviceName.Text.Trim()}' " +
                $"connected!\n" +
                $"IP: {TxtDeviceIP.Text.Trim()}\n" +
                $"{_viewModel.ScanStatus}",
                "Device Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            LoadDevices();
        }

        // ── Test device ────────────────────────────────────
        private async void TestDevice_Click(
            object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string ip = btn.Tag?.ToString() ?? "";

            bool online = await _viewModel.TestDeviceAsync(ip);

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

        // ── Push config to device ──────────────────────────
        //private async void PushConfig_Click(
        //    object sender, RoutedEventArgs e)
        //{
        //    if (sender is not Button btn) return;
        //    string ip = btn.Tag?.ToString() ?? "";

        //    bool ok = await _viewModel.PushConfigToDeviceAsync(
        //        ip, 5001, "", "",
        //        _viewModel.GetLocalIP());

        //    MessageBox.Show(
        //        ok
        //            ? "Work rules pushed to device successfully!"
        //            : "Failed to push config. Is device online?",
        //        "Push Config",
        //        MessageBoxButton.OK,
        //        ok
        //            ? MessageBoxImage.Information
        //            : MessageBoxImage.Warning);
        //}

        // ── Factory reset device ───────────────────────────
        //private async void FactoryReset_Click(
        //    object sender, RoutedEventArgs e)
        //{
        //    if (sender is not Button btn) return;
        //    string ip = btn.Tag?.ToString() ?? "";

        //    // Check for unsynced records first
        //    var (success, pending) =
        //        await _viewModel.FactoryResetDevice(ip);

        //    if (!success && pending > 0)
        //    {
        //        var result = MessageBox.Show(
        //            $"Device has {pending} unsynced attendance " +
        //            $"records!\n\n" +
        //            $"Wait for WiFi sync, or reset anyway " +
        //            $"and lose those records?",
        //            "Unsynced Records",
        //            MessageBoxButton.YesNo,
        //            MessageBoxImage.Warning);

        //        if (result != MessageBoxResult.Yes)
        //            return;

        //        // Force reset
        //        await _viewModel.FactoryResetDevice(ip);
        //    }
        //    else if (!success && pending == -1)
        //    {
        //        MessageBox.Show(
        //            "Cannot reach device.",
        //            "SmartAttend",
        //            MessageBoxButton.OK,
        //            MessageBoxImage.Error);
        //        return;
        //    }

        //    MessageBox.Show(
        //        "Device factory reset initiated.\n" +
        //        "Device will restart.",
        //        "Factory Reset",
        //        MessageBoxButton.OK,
        //        MessageBoxImage.Information);

        //    LoadDevices();
        //}

        // ── Remove device ──────────────────────────────────
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
                _viewModel.RemoveDevice(id);
                LoadDevices();
            }
        }
    }
}
