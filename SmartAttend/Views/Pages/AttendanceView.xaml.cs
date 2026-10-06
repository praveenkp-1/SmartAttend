using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SmartAttend.ViewModels;

namespace SmartAttend.View
{
    public partial class AttendanceView : UserControl
    {
        private AttendanceViewModel _viewModel;
        private string _activeStatus = "All"; // tracks current chip filter

        public AttendanceView()
        {
            InitializeComponent();
            _viewModel = new AttendanceViewModel();
            DataContext = _viewModel;
            DpDate.SelectedDate = DateTime.Today;
            Loaded += (s, e) => LoadAttendance();
        }

        private void LoadAttendance()
        {
            string date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
            string search = TxtSearch.Text.Trim();

            _viewModel.LoadAttendance(date, search, _activeStatus);
            AttendanceGrid.ItemsSource = _viewModel.Attendance;

            TxtStatTotal.Text = _viewModel.Attendance.Count.ToString();
            TxtStatPresent.Text = _viewModel.Attendance
                .Count(a => a.Status == "Present" || a.Status == "Late" || a.Status == "Checked Out")
                .ToString();
            TxtStatAbsent.Text = _viewModel.Attendance
                .Count(a => a.Status == "Absent").ToString();
            TxtStatLate.Text = _viewModel.Attendance
                .Count(a => a.Status == "Late").ToString();

            TxtCardDate.Text = DpDate.SelectedDate?.ToString("dd MMMM yyyy") ?? "";
        }

        private void Filter_Changed(object sender, EventArgs e)
        {
            if (AttendanceGrid == null) return;
            LoadAttendance();
        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            if (AttendanceGrid == null) return;
            LoadAttendance();
        }

        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            string date = DpDate.SelectedDate?.ToString("yyyy-MM-dd") ?? "";
            _viewModel.ExportPdf(date);
        }

        // ── Chip filters ──────────────────────────────────
        private void SetChipActive(Button active)
        {
            foreach (var chip in new[] { ChipAll, ChipPresent, ChipAbsent, ChipLate })
            {
                var bg = chip.Template.FindName("bg", chip) as Border;
                if (bg == null) continue;
                bg.Background = System.Windows.Media.Brushes.White;
                bg.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(228, 234, 242));
            }

            var activeBg = active.Template.FindName("bg", active) as Border;
            if (activeBg != null)
            {
                activeBg.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(223, 246, 243));
                activeBg.BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0, 198, 174));
            }
        }

        private void ChipAll_Click(object sender, RoutedEventArgs e)
        {
            _activeStatus = "All";
            SetChipActive(ChipAll);
            LoadAttendance();
        }

        private void ChipPresent_Click(object sender, RoutedEventArgs e)
        {
            _activeStatus = "Present";
            SetChipActive(ChipPresent);
            LoadAttendance();
        }

        private void ChipAbsent_Click(object sender, RoutedEventArgs e)
        {
            _activeStatus = "Absent";
            SetChipActive(ChipAbsent);
            LoadAttendance();
        }

        private void ChipLate_Click(object sender, RoutedEventArgs e)
        {
            _activeStatus = "Late";
            SetChipActive(ChipLate);
            LoadAttendance();
        }
    }
}