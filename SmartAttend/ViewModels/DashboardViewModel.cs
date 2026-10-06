using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using SmartAttend.Database;
using SmartAttend.Helpers;
using SmartAttend.Models;

namespace SmartAttend.ViewModels
{
    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private DispatcherTimer _refreshTimer;

        // ── Constructor ────────────────────────────────────
        public DashboardViewModel()
        {
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            _refreshTimer.Tick += (s, e) => LoadRecentCheckIns();
            _refreshTimer.Start();
        }

        public void StopTimer() => _refreshTimer?.Stop();

        // ── Stats ──────────────────────────────────────────
        private DashboardStats _stats;
        public DashboardStats Stats
        {
            get => _stats;
            set { _stats = value; OnPropertyChanged(nameof(Stats)); }
        }

        // ── Recent Check-ins ───────────────────────────────
        private ObservableCollection<RecentCheckIn> _recentCheckIns;
        public ObservableCollection<RecentCheckIn> RecentCheckIns
        {
            get => _recentCheckIns;
            set { _recentCheckIns = value; OnPropertyChanged(nameof(RecentCheckIns)); }
        }

        private ObservableCollection<TodaAttendance> _todaAttendance;
        public ObservableCollection<TodaAttendance> TodaAttendance
        {
            get => _todaAttendance;
            set { _todaAttendance = value; OnPropertyChanged(nameof(TodaAttendance)); }
        }

        // ── Weekly Bars ────────────────────────────────────
        private ObservableCollection<WeeklyBar> _weeklyData;
        public ObservableCollection<WeeklyBar> WeeklyData
        {
            get => _weeklyData;
            set { _weeklyData = value; OnPropertyChanged(nameof(WeeklyData)); }
        }

        // ── Today date ────────────────────────────────────
        public string TodayDate => DateTime.Now.ToString("yyyy-MM-dd");

        // ── Load All ───────────────────────────────────────
        public void LoadDashboard()
        {
            LoadStats();
            LoadRecentCheckIns();
            LoadWeeklyBars();
        }

        private void LoadStats()
        {
            var raw = DatabaseHelper.GetDashboardStats();
            string workStart = DatabaseHelper.GetSetting("work_start");

            Stats = new DashboardStats
            {
                TotalEmployees = raw.TotalEmployees.ToString(),
                PresentToday = raw.PresentToday.ToString(),
                AbsentToday = raw.AbsentToday.ToString(),
                LateToday = raw.LateToday.ToString(),
                PendingEnrollment = $"{raw.PendingEnrollment} pending enrollment",
                AttendanceRate = $"{raw.AttendanceRate}% attendance rate",
                WorkStart = $"After {(string.IsNullOrEmpty(workStart) ? "08:30" : workStart)} AM"
            };
        }

        private void LoadRecentCheckIns()
        {
            var list = DatabaseHelper.GetRecentCheckIns(4);
            var result = new ObservableCollection<RecentCheckIn>();
            int colorIndex = 0;

            foreach (var item in list)
            {
                var color = NameHelper.GetAvatarColor(colorIndex);
                colorIndex++;

                result.Add(new RecentCheckIn
                {
                    Name = item.Name,
                    Method = item.Method,
                    Check = item.Check,
                    Status = item.Status,
                    Time = item.Time,
                    Initials = GetInitials(item.Name),
                    AvatarColor = color.bg,
                    AvatarTextColor = color.text
                });
            }

            RecentCheckIns = result;
        }

        public void LoadAttendance(string date, string search, string status)
        {
            var data = DatabaseHelper.GetAttendance(date, search, status);
            var result = new ObservableCollection<TodaAttendance>();
            int colorIndex = 0;

            foreach (var row in data)
            {
                string name = row.Name?.ToString() ?? "";
                var color = NameHelper.GetAvatarColor(colorIndex);
                colorIndex++;


                result.Add(new TodaAttendance
                {


                    Name = row.Name,
                    UserId = row.UserId,
                    CheckIn = row.CheckIn,
                    CheckOut = row.CheckOut,
                    Method = row.Method,
                    Status = row.Status,
                    Initials = NameHelper.GetInitials(name),
                    AvatarColor = color.bg,
                    AvatarTextColor = color.text
                });
            }

            TodaAttendance = result;
        }

        private void LoadWeeklyBars()
        {
            var data = DatabaseHelper.GetWeeklyAttendance();
            var result = new ObservableCollection<WeeklyBar>();

            foreach (var item in data)
            {
                result.Add(new WeeklyBar
                {
                    Day = item.Day,
                    Percentage = item.Percentage
                });
            }

            WeeklyData = result;
        }

        private string GetInitials(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";
            var parts = fullName.Trim().Split(' ');
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return (parts[0][0].ToString() + parts[parts.Length - 1][0].ToString()).ToUpper();
        }
    }
}