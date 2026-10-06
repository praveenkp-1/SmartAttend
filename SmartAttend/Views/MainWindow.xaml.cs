using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartAttend.View;
using SmartAttend.ViewModels;

namespace SmartAttend
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;
        private System.Windows.Threading.DispatcherTimer? _refreshTimer;
        private System.Windows.Threading.DispatcherTimer? _endOfDayTimer;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            DataContext = _viewModel;

            _viewModel.LoadManagerInfo();
            TopDate.Text = _viewModel.TodayDate;

            // Clock timer
            var clockTimer = new System.Windows.Threading.DispatcherTimer();
            clockTimer.Interval = TimeSpan.FromSeconds(1);
            clockTimer.Tick += (s, e) =>
            {
                TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            clockTimer.Start();

            // Set initial time immediately so it doesn't show 00:00:00 on load
            TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
            MainContent.Content = new DashboardView();

            StartRefreshTimer();
            StartEndOfDayTimer();
            _ = _viewModel.PingDeviceAsync();
        }

        // ── Navigation ─────────────────────────────────────
        public void NavigateTo(string tag)
        {
            foreach (var btn in new[] {
                BtnDashboard, BtnEmployees, BtnAttendance,
                BtnReports, BtnEnrollment,  BtnSettings })
            {
                btn.Style = (Style)FindResource("NavBtn");
            }

            Button active = tag switch
            {
                "Dashboard" => BtnDashboard,
                "Employees" => BtnEmployees,
                "Attendance" => BtnAttendance,
                "Reports" => BtnReports,
                "Enrollment" => BtnEnrollment,
                "Settings" => BtnSettings,
                _ => BtnDashboard
            };

            active.Style = (Style)FindResource("NavBtnActive");
            TopTitle.Text = tag;

            MainContent.Content = tag switch
            {
                "Dashboard" => new DashboardView(),
                "Employees" => new EmployeesView(),
                "Attendance" => new AttendanceView(),
                "Reports" => new ReportsView(),
                "Enrollment" => new EnrollmentView(),
                "Settings" => new SettingView(),
                _ => new DashboardView()
            };
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            NavigateTo(btn.Tag?.ToString() ?? "");
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to sign out?",
                "Sign Out",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
                this.Close();
        }

        // ── Device status UI ───────────────────────────────
        private async void RefreshDeviceStatus()
        {
            await _viewModel.PingDeviceAsync();

            if (_viewModel.IsDeviceOnline)
            {
                TxtDeviceStatus.Text = "SmartAttend-01";
                TxtDeviceStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                TxtDeviceIp.Text = "192.168.1.104 · Port 5000";
                DeviceStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 198, 174));  // teal
            }
            else
            {
                TxtDeviceStatus.Text = "Device Offline";
                TxtDeviceStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                TxtDeviceIp.Text = "Not connected";
                DeviceStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 90, 90));  // red
            }
        }

        // ── Timers ─────────────────────────────────────────
        private void StartRefreshTimer()
        {
            _refreshTimer = new System.Windows.Threading.DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(30);
            _refreshTimer.Tick += (s, e) =>
            {
                if (MainContent.Content is DashboardView view &&
                view.DataContext is DashboardViewModel dashVM)
                    dashVM.LoadDashboard();
                RefreshDeviceStatus();
            };
            _refreshTimer.Start();
        }

        private void StartEndOfDayTimer()
        {
            _endOfDayTimer = new System.Windows.Threading.DispatcherTimer();
            _endOfDayTimer.Interval = TimeSpan.FromMinutes(1);
            _endOfDayTimer.Tick += (s, e) =>
            {
                //bool ran = _viewModel.ShouldRunEndOfDay();
                if (MainContent.Content is DashboardView view &&
                    view.DataContext is DashboardViewModel dashVM)
                    dashVM.LoadDashboard();
            };
            _endOfDayTimer.Start();
        }
    }
}