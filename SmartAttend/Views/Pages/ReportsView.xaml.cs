using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SmartAttend.Models;
using SmartAttend.ViewModels;

namespace SmartAttend.View
{
    public partial class ReportsView : UserControl
    {
        private ReportsViewModel _viewModel;

        public ReportsView()
        {
            InitializeComponent();
            _viewModel = new ReportsViewModel();
            DataContext = _viewModel;

            Loaded += (s, e) =>
            {
                LoadDepartments();
                LoadMonthYearPickers();
                GenerateReport();
            };
        }

        // ── Departments ────────────────────────────────────
        private void LoadDepartments()
        {
            CmbDepartment.Items.Clear();
            CmbDepartment.Items.Add(
                new ComboBoxItem
                {
                    Content = "All Departments",
                    IsSelected = true
                });
            foreach (var d in _viewModel.LoadDepartments())
                CmbDepartment.Items.Add(new ComboBoxItem { Content = d });
            CmbDepartment.SelectedIndex = 0;
        }

        // ── Month / Year pickers ───────────────────────────
        private void LoadMonthYearPickers()
        {
            CmbMonth.Items.Clear();
            for (int m = 1; m <= 12; m++)
                CmbMonth.Items.Add(new ComboBoxItem
                {
                    Content = CultureInfo.CurrentCulture
                        .DateTimeFormat.GetMonthName(m),
                    Tag = m
                });
            CmbMonth.SelectedIndex = DateTime.Today.Month - 1;

            CmbYear.Items.Clear();
            int currentYear = DateTime.Today.Year;
            for (int y = currentYear - 3; y <= currentYear; y++)
                CmbYear.Items.Add(new ComboBoxItem
                {
                    Content = y.ToString(),
                    Tag = y
                });
            CmbYear.SelectedIndex = CmbYear.Items.Count - 1;

            // Default week end-date = today (past-only, never future)
            DpWeek.SelectedDate = DateTime.Today;
            UpdateWeekRangeLabel();
        }

        // ── Report type changed ────────────────────────────
        private void ReportType_Changed(
            object sender, SelectionChangedEventArgs e)
        {
            if (PnlMonthly == null) return;

            string type = (CmbReportType.SelectedItem as ComboBoxItem)
                          ?.Content?.ToString() ?? "";

            bool isMonthly = type == "Monthly Report";
            PnlMonthly.Visibility = isMonthly ? Visibility.Visible : Visibility.Collapsed;
            PnlWeekly.Visibility = isMonthly ? Visibility.Collapsed : Visibility.Visible;
            PnlWeekRange.Visibility = isMonthly ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Week end-date picker changed ────────────────────
        private void DpWeek_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent picking a future date — clamp to today
            if (DpWeek.SelectedDate.HasValue && DpWeek.SelectedDate.Value.Date > DateTime.Today)
            {
                DpWeek.SelectedDate = DateTime.Today;
                return; // this re-entrant call will fire the event again with the clamped date
            }
            UpdateWeekRangeLabel();
        }

        private void UpdateWeekRangeLabel()
        {
            if (TxtWeekRange == null) return;
            DateTime end = DpWeek.SelectedDate ?? DateTime.Today;
            DateTime start = end.AddDays(-6);
            TxtWeekRange.Text = $"{start:MMM dd} – {end:MMM dd, yyyy}";
        }

        // ── Generate ───────────────────────────────────────
        private void GenerateReport()
        {
            string type = (CmbReportType.SelectedItem as ComboBoxItem)
                          ?.Content?.ToString() ?? "Monthly Report";
            string dept = (CmbDepartment.SelectedItem as ComboBoxItem)
                          ?.Content?.ToString() ?? "All Departments";
            int month = ((CmbMonth.SelectedItem as ComboBoxItem)
                          ?.Tag as int?) ?? DateTime.Today.Month;
            int year = ((CmbYear.SelectedItem as ComboBoxItem)
                          ?.Tag as int?) ?? DateTime.Today.Year;

            // Week END date (start is computed inside the ViewModel as end - 6 days)
            DateTime weekEnd = DpWeek.SelectedDate ?? DateTime.Today;
            if (weekEnd.Date > DateTime.Today) weekEnd = DateTime.Today; // safety clamp

            _viewModel.GenerateReport(
                type,
                dept,
                weekEnd,
                month,
                year);

            // ── Push results to UI ──────────────────────────
            ReportGrid.ItemsSource = _viewModel.ReportRows;
            TxtAvgAttendance.Text = _viewModel.AvgAttendance;
            TxtPerfect.Text = _viewModel.PerfectCount;
            TxtAtRisk.Text = _viewModel.AtRiskCount;
            TxtReportPeriod.Text = _viewModel.ReportPeriod;
            TxtTotalEmployees.Text = _viewModel.TotalEmployees;

            // Update line chart header to match selected period
            string reportType = (CmbReportType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Monthly Report";
            TxtLineChartTitle.Text = reportType == "Weekly Report"
                ? $"Attendance Trend ({_viewModel.ReportPeriod})"
                : $"Attendance Trend — {_viewModel.ReportPeriod}";

            int rowCount = _viewModel.ReportRows?.Count ?? 0;
            

            bool isEmpty = rowCount == 0;
            if (TxtEmptyState != null)
                TxtEmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

            // Draw charts after layout pass so canvases have their ActualWidth
            LineChartCanvas.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(DrawLineChart));
            StackedBarCanvas.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(DrawStackedBarChart));
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
            => GenerateReport();

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportPdf(
                _viewModel.ReportPeriod ?? "Report",
                (CmbReportType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Report"
            );
        }

        // ══════════════════════════════════════════════════════════════
        //  Line Chart — Attendance Trend (no design change, same as before)
        // ══════════════════════════════════════════════════════════════
        private void DrawLineChart()
        {
            LineChartCanvas.Children.Clear();
            var data = _viewModel.TrendData;
            if (data == null || data.Count == 0) return;

            double canvasW = LineChartCanvas.ActualWidth;
            double canvasH = LineChartCanvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) return;

            const double PADDING_L = 4;
            const double PADDING_R = 30;
            const double PADDING_TOP = 10;   // ← NEW: room for dot radius + 100% label
            const double CHART_H = 95;

            int count = data.Count;
            double plotW = canvasW - PADDING_L - PADDING_R;
            double stepX = count > 1 ? plotW / (count - 1) : 0;
            double baseline = PADDING_TOP + CHART_H;   // ← shift baseline down

            foreach (int pct in new[] { 25, 50, 75, 100 })
            {
                double y = baseline - (pct / 100.0 * CHART_H);   // now min y = PADDING_TOP, not 0
                var line = new Line
                {
                    X1 = PADDING_L,
                    X2 = canvasW - PADDING_R,
                    Y1 = y,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(80, 180, 195, 220)),
                    StrokeThickness = 0.5,
                    StrokeDashArray = pct < 100 ? new DoubleCollection { 3, 3 } : null
                };
                LineChartCanvas.Children.Add(line);

                var lbl = new TextBlock
                {
                    Text = $"{pct}%",
                    FontSize = 9,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBD))
                };
                Canvas.SetLeft(lbl, canvasW - PADDING_R + 4);
                Canvas.SetTop(lbl, y - 7);
                LineChartCanvas.Children.Add(lbl);
            }

            var teal = Color.FromRgb(0x00, 0xB0, 0x9C);
            Point? prevPoint = null;
            int labelStep = count > 14 ? (int)Math.Ceiling(count / 10.0) : 1;

            for (int i = 0; i < count; i++)
            {
                double x = PADDING_L + i * stepX;
                double y = baseline - (data[i].AvgRate / 100.0 * CHART_H);   // min y = PADDING_TOP now
                var point = new Point(x, y);

                if (prevPoint.HasValue)
                {
                    var segment = new Line
                    {
                        X1 = prevPoint.Value.X,
                        Y1 = prevPoint.Value.Y,
                        X2 = point.X,
                        Y2 = point.Y,
                        Stroke = new SolidColorBrush(teal),
                        StrokeThickness = 2,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round
                    };
                    LineChartCanvas.Children.Add(segment);
                }

                if (count <= 14 || i % labelStep == 0 || i == count - 1)
                {
                    var dot = new Ellipse
                    {
                        Width = 6,
                        Height = 6,
                        Fill = new SolidColorBrush(Colors.White),
                        Stroke = new SolidColorBrush(teal),
                        StrokeThickness = 2
                    };
                    Canvas.SetLeft(dot, x - 3);
                    Canvas.SetTop(dot, y - 3);   // worst case now: PADDING_TOP - 3 = 7, still positive
                    LineChartCanvas.Children.Add(dot);
                }

                if (i % labelStep == 0 || i == count - 1)
                {
                    var dayLbl = new TextBlock
                    {
                        Text = data[i].Day,
                        FontSize = 9,
                        FontFamily = new FontFamily("Segoe UI"),
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBD)),
                        Width = 28,
                        TextAlignment = TextAlignment.Center
                    };
                    Canvas.SetLeft(dayLbl, x - 14);
                    Canvas.SetTop(dayLbl, baseline + 4);
                    LineChartCanvas.Children.Add(dayLbl);
                }

                prevPoint = point;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  Stacked Bar Chart — Daily Breakdown (no design change, same as before)
        // ══════════════════════════════════════════════════════════════
        private void DrawStackedBarChart()
        {
            StackedBarCanvas.Children.Clear();

            var data = _viewModel.TrendData;
            if (data == null || data.Count == 0) return;

            double canvasW = StackedBarCanvas.ActualWidth;
            double canvasH = StackedBarCanvas.ActualHeight;
            if (canvasW <= 0 || canvasH <= 0) return;

            const double PADDING_L = 4;
            const double PADDING_R = 4;
            const double BAR_GAP = 6;
            const double CHART_H = 95;

            int count = data.Count;
            double totalW = canvasW - PADDING_L - PADDING_R;
            double barWidth = Math.Max(6, (totalW - (count - 1) * BAR_GAP) / count);
            double baseline = CHART_H;

            var presentColor = Color.FromRgb(0x00, 0xB0, 0x9C);
            var lateColor = Color.FromRgb(0xF5, 0xA6, 0x23);
            var absentColor = Color.FromRgb(0xE0, 0x52, 0x52);

            // Label every Nth bar to avoid overlap when count is large (monthly)
            int labelStep = count > 14 ? (int)Math.Ceiling(count / 10.0) : 1;

            for (int i = 0; i < count; i++)
            {
                var d = data[i];
                double x = PADDING_L + i * (barWidth + BAR_GAP);

                double presentH = (d.PresentPct / 100.0) * CHART_H;
                double lateH = (d.LatePct / 100.0) * CHART_H;
                double absentH = (d.AbsentPct / 100.0) * CHART_H;

                double yPresent = baseline - presentH;
                AddSegment(x, yPresent, barWidth, presentH, presentColor, roundTop: false);

                double yLate = yPresent - lateH;
                AddSegment(x, yLate, barWidth, lateH, lateColor, roundTop: false);

                double yAbsent = yLate - absentH;
                AddSegment(x, yAbsent, barWidth, absentH, absentColor, roundTop: true);

                if (i % labelStep == 0 || i == count - 1)
                {
                    var dayLbl = new TextBlock
                    {
                        Text = d.Day,
                        FontSize = 9,
                        FontFamily = new FontFamily("Segoe UI"),
                        Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9B, 0xBD)),
                        Width = barWidth + BAR_GAP,
                        TextAlignment = TextAlignment.Center
                    };
                    Canvas.SetLeft(dayLbl, x - BAR_GAP / 2);
                    Canvas.SetTop(dayLbl, baseline + 4);
                    StackedBarCanvas.Children.Add(dayLbl);
                }
            }

            void AddSegment(double x, double y, double width, double height, Color color, bool roundTop)
            {
                if (height <= 0) return;
                var rect = new Rectangle
                {
                    Width = width,
                    Height = Math.Max(2, height),
                    Fill = new SolidColorBrush(color),
                    RadiusX = roundTop ? 2 : 0,
                    RadiusY = roundTop ? 2 : 0
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                StackedBarCanvas.Children.Add(rect);
            }


        }

        private void Search_Changed(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text?.Trim().ToLower() ?? "";
            var allRows = _viewModel.ReportRows ?? new List<ReportRow>();

            ReportGrid.ItemsSource = string.IsNullOrEmpty(query)
                ? allRows
                : allRows.Where(r =>
                    (r.Name?.ToLower().Contains(query) ?? false) ||
                    (r.UserId?.ToLower().Contains(query) ?? false)
                  ).ToList();
        }
        private void Chart_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_viewModel == null || !e.WidthChanged) return;

            if (sender == LineChartCanvas)
                DrawLineChart();
            else if (sender == StackedBarCanvas)
                DrawStackedBarChart();
        }
    }

}