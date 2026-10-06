using System;
using System.ComponentModel;
using System.Threading.Tasks;
using SmartAttend.Database;
using SmartAttend.Services;

namespace SmartAttend.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private readonly DeviceService _deviceService = new DeviceService();

        // ── Manager info ───────────────────────────────────
        private string _managerName;
        public string ManagerName
        {
            get => _managerName;
            set { _managerName = value; OnPropertyChanged(nameof(ManagerName)); }
        }

        private string _managerInitial;
        public string ManagerInitial
        {
            get => _managerInitial;
            set { _managerInitial = value; OnPropertyChanged(nameof(ManagerInitial)); }
        }

        // ── Device status ──────────────────────────────────
        private bool _isDeviceOnline;
        public bool IsDeviceOnline
        {
            get => _isDeviceOnline;
            set { _isDeviceOnline = value; OnPropertyChanged(nameof(IsDeviceOnline)); }
        }

        // ── Top date ───────────────────────────────────────
        public string TodayDate => DateTime.Now.ToString("dddd, dd MMM yyyy");

        // ── Load manager ───────────────────────────────────
        public void LoadManagerInfo()
        {
            string username = DatabaseHelper.GetSetting("username");
            if (!string.IsNullOrEmpty(username))
            {
                ManagerName = username;
                ManagerInitial = username[0].ToString().ToUpper();
            }
        }

        // ── Ping device ────────────────────────────────────
        public async Task PingDeviceAsync()
        {
            IsDeviceOnline = await _deviceService.PingDeviceAsync();
        }

        //// ── End of day ─────────────────────────────────────
        //private DateTime _lastEndOfDayRun = DateTime.MinValue;

        //public bool ShouldRunEndOfDay()
        //{
        //    string workEnd = DatabaseHelper.GetSetting("work_end");
        //    if (string.IsNullOrEmpty(workEnd))
        //        workEnd = "17:00";

        //    if (!TimeSpan.TryParse(workEnd, out TimeSpan endTime))
        //        return false;

        //    TimeSpan now = DateTime.Now.TimeOfDay;
        //    bool alreadyRan = _lastEndOfDayRun.Date == DateTime.Today;

        //    if (now >= endTime && !alreadyRan)
        //    {
        //        _lastEndOfDayRun = DateTime.Now;
        //        DatabaseHelper.ProcessEndOfDay();
        //        return true;
        //    }

        //    return false;
        //}
    }
}