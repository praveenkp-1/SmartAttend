using System.ComponentModel;
using System.Windows;
using SmartAttend.Database;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace SmartAttend.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(name));

        private readonly DeviceConfigViewModel _deviceVM =
            new DeviceConfigViewModel();

        // ── Attendance rule properties ─────────────────────
        private string _workStart;
        public string WorkStart
        {
            get => _workStart;
            set { _workStart = value; OnPropertyChanged(nameof(WorkStart)); }
        }

        private string _workEnd;
        public string WorkEnd
        {
            get => _workEnd;
            set { _workEnd = value; OnPropertyChanged(nameof(WorkEnd)); }
        }

        private string _gracePeriod;
        public string GracePeriod
        {
            get => _gracePeriod;
            set { _gracePeriod = value; OnPropertyChanged(nameof(GracePeriod)); }
        }

        // ── Toggle properties ──────────────────────────────
        private bool _autoSync;
        public bool AutoSync
        {
            get => _autoSync;
            set
            {
                _autoSync = true;
                OnPropertyChanged(nameof(AutoSync));
                DatabaseHelper.SaveSetting("auto_sync_enabled",
                    "true");
            }
        }

        private bool _sdBackup;
        public bool SdBackup
        {
            get => _sdBackup;
            set
            {
                _sdBackup = true;
                OnPropertyChanged(nameof(SdBackup));
                DatabaseHelper.SaveSetting("sd_backup_enabled",
                    "true");
            }
        }

        private bool _buzzerEnabled;
        public bool BuzzerEnabled
        {
            get => _buzzerEnabled;
            set
            {
                _buzzerEnabled = value;
                OnPropertyChanged(nameof(BuzzerEnabled));
                DatabaseHelper.SaveSetting("buzzer_enabled",
                    value ? "true" : "false");
            }
        }

        private bool _allowPIN;
        public bool AllowPIN
        {
            get => _allowPIN;
            set
            {
                _allowPIN = value;
                OnPropertyChanged(nameof(AllowPIN));
                // 0=ANY, 1=RFID, 2=FP, 3=PIN
                DatabaseHelper.SaveSetting("auth_mode",
                    value ? "0" : "1");
            }
        }

        private int _authMode;
        public int AuthMode
        {
            get => _authMode;
            set { _authMode = value; OnPropertyChanged(nameof(AuthMode)); }
        }

        private string _companyName;
        public string CompanyName
        {
            get => _companyName;
            set { _companyName = value; OnPropertyChanged(nameof(CompanyName)); }
        }

        private string _adminUsername;
        public string AdminUsername
        {
            get => _adminUsername;
            set { _adminUsername = value; OnPropertyChanged(nameof(AdminUsername)); }
        }

        public void LoadGeneralSettings()
        {
            CompanyName = DatabaseHelper.GetSetting("company_name") ?? "";
            AdminUsername = DatabaseHelper.GetSetting("username") ?? "";
        }

        public bool RequireLogin
        {
            get => DatabaseHelper.GetSetting("require_login") == "true";
            set => DatabaseHelper.SaveSetting("require_login", value ? "true" : "false");
        }

        // ── Load settings ──────────────────────────────────
        public void LoadSettings()
        {
            WorkStart = DatabaseHelper.GetSetting("work_start");
            WorkEnd = DatabaseHelper.GetSetting("work_end");
            GracePeriod = DatabaseHelper.GetSetting("grace_period");

            string buzzer = DatabaseHelper.GetSetting("buzzer_enabled");
            string authMode = DatabaseHelper.GetSetting("auth_mode");

            // Defaults — all on except PIN only mode
            _autoSync = true;
            _sdBackup = true;
            DatabaseHelper.SaveSetting("auto_sync_enabled", "true");
            DatabaseHelper.SaveSetting("sd_backup_enabled", "true");
            _buzzerEnabled = buzzer != "false";
            _authMode = int.TryParse(authMode, out int am) ? am : 0;

            OnPropertyChanged(nameof(AutoSync));
            OnPropertyChanged(nameof(SdBackup));
            OnPropertyChanged(nameof(BuzzerEnabled));
            OnPropertyChanged(nameof(AuthMode));
        }

        // ── Save attendance rules + push to device ─────────
        public async Task<bool> SaveAttendanceRulesAsync(
            string start, string end, string grace, int authMode)
        {
            if (!IsValidTime(start))
            {
                MessageBox.Show(
                    "Invalid work start time.\n" +
                    "Use HH:MM format e.g. 08:30",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            if (!IsValidTime(end))
            {
                MessageBox.Show(
                    "Invalid work end time.\n" +
                    "Use HH:MM format e.g. 17:00",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(grace, out int graceNum)
                || graceNum < 0 || graceNum > 120)
            {
                MessageBox.Show(
                    "Grace period must be 0–120 minutes.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }
            if (authMode < 0 || authMode > 6)
            {
                MessageBox.Show(
                    "Invalid authentication mode.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            // Save to database
            DatabaseHelper.SaveSetting("work_start", start);
            DatabaseHelper.SaveSetting("work_end", end);
            DatabaseHelper.SaveSetting("grace_period", grace);
            DatabaseHelper.SaveSetting("auth_mode", authMode.ToString());

            // Push to all connected devices
            bool pushed = await PushSettingsToAllDevicesAsync();

            string msg = pushed
                ? "Attendance rules saved and pushed to device!"
                : "Rules saved locally.\n" +
                  "Could not reach device — rules will sync next connect.";

            MessageBox.Show(msg, "SmartAttend",
                MessageBoxButton.OK,
                pushed
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);

            return true;
        }

        // ── Push toggle changes to device ──────────────────
        public async Task PushTogglesToDevice()
        {
            await PushSettingsToAllDevicesAsync();
        }

        // ── Departments ────────────────────────────────────
        public dynamic LoadDepartments()
        {
            return DatabaseHelper.GetDepartments();
        }

        public void AddDepartment(string name)
        {
            DatabaseHelper.AddDepartment(name);
        }

        public void DeleteDepartment(string name)
        {
            DatabaseHelper.DeleteDepartment(name);
        }

        // ── Helpers ────────────────────────────────────────
        private bool IsValidTime(string time)
        {
            if (string.IsNullOrEmpty(time)) return false;
            return System.TimeSpan.TryParse(time, out _);
        }

        public async Task<bool> PushSettingsToDeviceAsync(
            string ip,int port)
        {
            try
            {
                string workStart =
                    DatabaseHelper.GetSetting("work_start");

                string workEnd =
                    DatabaseHelper.GetSetting("work_end");

                string grace =
                    DatabaseHelper.GetSetting("grace_period");

                string buzzer =
                    DatabaseHelper.GetSetting("buzzer_enabled");

                string authMode =
                    DatabaseHelper.GetSetting("auth_mode");

                var payload = new
                {
                    authMode =
                        int.Parse(authMode),

                    workStart,

                    workEnd,

                    gracePeriod =
                        int.Parse(grace),

                    buzzer =
                        buzzer == "true",

                    sdBackup = true,
                    autoSync = true
                };

                using var client = new HttpClient();

                string json =
                    JsonSerializer.Serialize(payload);

                var content =
                    new StringContent(
                        json,
                        Encoding.UTF8,
                        "application/json");

                var response =
                    await client.PostAsync(
                        $"http://{ip}:{port}/settings",
                        content);

                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> PushSettingsToAllDevicesAsync()
        {
            var devices = DatabaseHelper.GetAllDevices();

            if (devices.Count == 0)
                return true;

            bool allOk = true;

            foreach (var device in devices)
            {
                string devIP =
                    device.IPAddress?.ToString() ?? "";

                if (string.IsNullOrEmpty(devIP))
                    continue;

                int devPort =
                    int.TryParse(
                        device.Port?.ToString(),
                        out int p)
                        ? p
                        : 5001;

                bool ok =
                    await PushSettingsToDeviceAsync(
                        devIP,
                        devPort);

                if (!ok &&
                    devIP != DeviceConfigViewModel.DeviceHostName &&
                    await _deviceVM.TestConnectionAsync(
                        DeviceConfigViewModel.DeviceHostName,
                        devPort))
                {
                    DatabaseHelper.UpdateDeviceAddress(
                        devIP,
                        DeviceConfigViewModel.DeviceHostName);
                    DatabaseHelper.SaveSetting(
                        "device_ip",
                        DeviceConfigViewModel.DeviceHostName);

                    ok = await PushSettingsToDeviceAsync(
                        DeviceConfigViewModel.DeviceHostName,
                        devPort);
                }

                if (!ok)
                    allOk = false;
            }

            return allOk;
        }

        public bool SaveGeneralSettings(string companyName, string adminUsername)
        {
            if (string.IsNullOrEmpty(companyName) || string.IsNullOrEmpty(adminUsername))
                return false;

            DatabaseHelper.SaveSetting("company_name", companyName);
            DatabaseHelper.SaveSetting("username", adminUsername);
            return true;
        }
    }
}
