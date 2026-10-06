using System.Windows.Controls;
using SmartAttend.ViewModels;

namespace SmartAttend.View
{
    public partial class DashboardView : UserControl
    {
        private DashboardViewModel _viewModel;

        public DashboardView()
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;
            Loaded += (s, e) => LoadDashboard();
            Unloaded += (s, e) => _viewModel.StopTimer();
        }

        public void LoadDashboard()
        {
            _viewModel.LoadDashboard();
            UpdateStats();
            UpdateTodayAttendance();
            UpdateLiveFeed();
            UpdateWeeklyBars();
        }

        // ── Stats ──────────────────────────────────────────
        private void UpdateStats()
        {
            var s = _viewModel.Stats;
            if (s == null) return;

            TxtTotalEmployees.Text = s.TotalEmployees;
            TxtPresentToday.Text = s.PresentToday;
            TxtAbsentToday.Text = s.AbsentToday;
            TxtLateToday.Text = s.LateToday;
            TxtPendingEnrollment.Text = s.PendingEnrollment;
            TxtAttendanceRate.Text = s.AttendanceRate;
            TxtWorkStart.Text = s.WorkStart;
            TxtTodayDate.Text = _viewModel.TodayDate;
        }

        // ── Today's Attendance DataGrid ────────────────────
        private void UpdateTodayAttendance()
        {
            string date = _viewModel.TodayDate;
            string search = "";
            string status = "All";

            _viewModel.LoadAttendance(date, search, status);
            DashboardAttendanceGrid.ItemsSource = _viewModel.TodaAttendance;
        }


        // ── Live Feed (the 3 named rows on the right) ──────
        private void UpdateLiveFeed()
        {
            var list = _viewModel.RecentCheckIns;
            if (list == null) return;
            LiveActivityList.ItemsSource = null;  // force reset
            LiveActivityList.ItemsSource = list;
        }

        // ── Weekly Bars ────────────────────────────────────
        private void UpdateWeeklyBars()
        {
            var data = _viewModel.WeeklyData;
            if (data == null) return;

            var bars = new[] {
                BarMon, BarTue, BarWed, BarThu, BarFri, BarSat, BarSun };
            var containers = new[] {
                BarMonContainer, BarTueContainer, BarWedContainer,
                BarThuContainer, BarFriContainer, BarSatContainer,
                BarSunContainer };
            var texts = new[] {
                TxtMon, TxtTue, TxtWed, TxtThu, TxtFri, TxtSat, TxtSun };

            for (int i = 0; i < data.Count && i < 7; i++)
            {
                int pct = data[i].Percentage;
                texts[i].Text = pct + "%";
                containers[i].UpdateLayout();
                double w = containers[i].ActualWidth;
                bars[i].Width = (w * pct) / 100.0;
            }
        }
    }
}