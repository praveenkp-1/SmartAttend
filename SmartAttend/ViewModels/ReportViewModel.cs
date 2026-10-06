using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using SmartAttend.Database;
using SmartAttend.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QContainer = QuestPDF.Infrastructure.IContainer;
using SmartAttend.Helpers;
using System.Xml.Linq;

namespace SmartAttend.ViewModels
{
    public class ReportsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Report results ─────────────────────────────────────────
        private List<ReportRow> _reportRows;
        public List<ReportRow> ReportRows
        {
            get => _reportRows;
            set { _reportRows = value; OnPropertyChanged(nameof(ReportRows)); }
        }

        // ── Summary props ──────────────────────────────────────────
        private string _avgAttendance;
        public string AvgAttendance
        {
            get => _avgAttendance;
            set { _avgAttendance = value; OnPropertyChanged(nameof(AvgAttendance)); }
        }

        private string _perfectCount;
        public string PerfectCount
        {
            get => _perfectCount;
            set { _perfectCount = value; OnPropertyChanged(nameof(PerfectCount)); }
        }

        private string _atRiskCount;
        public string AtRiskCount
        {
            get => _atRiskCount;
            set { _atRiskCount = value; OnPropertyChanged(nameof(AtRiskCount)); }
        }

        private string _reportPeriod;
        public string ReportPeriod
        {
            get => _reportPeriod;
            set { _reportPeriod = value; OnPropertyChanged(nameof(ReportPeriod)); }
        }

        private string _totalEmployees;
        public string TotalEmployees
        {
            get => _totalEmployees;
            set { _totalEmployees = value; OnPropertyChanged(nameof(TotalEmployees)); }
        }

        // ── Trend data consumed by both charts ──────────────────────
        // Weekly Report  -> 7 points (computed end date back 6 days)
        // Monthly Report -> all days in the selected month
        public List<DayAttendance> TrendData { get; private set; } = new();

        // ── Load departments ───────────────────────────────────────
        public List<string> LoadDepartments()
        {
            var result = new List<string>();
            foreach (var d in DatabaseHelper.GetDepartments())
                result.Add(d.ToString());
            return result;
        }

        // ── Generate report ────────────────────────────────────────
        public void GenerateReport(
            string type,
            string dept,
            DateTime weekEndDate,
            int month,
            int year)
        {
            List<dynamic> data;
            string periodLabel;
            DateTime trendStart, trendEnd;

            if (type == "Weekly Report")
            {
                trendEnd = weekEndDate.Date > DateTime.Today ? DateTime.Today : weekEndDate.Date;
                trendStart = trendEnd.AddDays(-6);

                string weekStr = trendStart.ToString("yyyy-MM-dd");
                data = DatabaseHelper.GetWeeklyReport(weekStr, dept);
                periodLabel = $"{trendStart:MMM dd} – {trendEnd:MMM dd, yyyy}";
            }
            else // Monthly Report
            {
                string monthStr = $"{year:0000}-{month:00}";
                data = DatabaseHelper.GetMonthlyReport(monthStr, dept);
                periodLabel = new DateTime(year, month, 1).ToString("MMMM yyyy");

                trendStart = new DateTime(year, month, 1);
                trendEnd = trendStart.AddMonths(1).AddDays(-1);
                if (trendEnd > DateTime.Today) trendEnd = DateTime.Today; // never project into the future
            }

            // ── Build rows ─────────────────────────────────────────
            const double BAR_MAX_PX = 108.0; // Rate column width 140 - 2×16 margin
            var list = new List<ReportRow>();
            double rateSum = 0;
            int perfectCount = 0;
            int atRiskCount = 0;
            int colorIndex = 0;

            foreach (var row in data)
            {
                int present = int.TryParse(row.Present?.ToString(), out int p) ? p : 0;
                int absent = int.TryParse(row.Absent?.ToString(), out int a) ? a : 0;
                int late = int.TryParse(row.Late?.ToString(), out int l) ? l : 0;

                int worked = present + late;
                int total = worked + absent;
                double rate = total > 0 ? (worked * 100.0) / total : 0;

                if (absent == 0 && late == 0 && present > 0) perfectCount++;
                if (rate < 60) atRiskCount++;
                rateSum += rate;

                string color = GetRateColor(rate);

                var avatarColor = NameHelper.GetAvatarColor(colorIndex++);
                string name = row.Name ?? "";

                list.Add(new ReportRow
                {
                    UserId = row.UserId?.ToString() ?? "",
                    Name = row.Name?.ToString() ?? "",
                    Department = row.Department?.ToString() ?? "",
                    Present = present.ToString(),
                    Absent = absent.ToString(),
                    Late = late.ToString(),
                    TotalHours = row.TotalHours?.ToString() ?? "",
                    Initials = NameHelper.GetInitials(name),
                    AvatarColor = avatarColor.bg,
                    AvatarTextColor = avatarColor.text,
                    Rate = $"{(int)rate}%",
                    RateColor = color,
                    RateValue = rate,
                    RateBarWidth = Math.Round(rate / 100.0 * BAR_MAX_PX, 1)
                });
            }

            ReportRows = list;

            // ── Summary ────────────────────────────────────────────
            int empCount = list.Count;
            AvgAttendance = empCount > 0 ? $"{(int)(rateSum / empCount)}%" : "—";
            PerfectCount = perfectCount.ToString();
            AtRiskCount = atRiskCount.ToString();
            ReportPeriod = periodLabel;
            TotalEmployees = empCount.ToString();

            // ── Trend data for both charts (Weekly: 7 days, Monthly: all days) ──
            var trend = new List<DayAttendance>();
            for (DateTime d = trendStart; d <= trendEnd; d = d.AddDays(1))
            {
                string dStr = d.ToString("yyyy-MM-dd");
                List<dynamic> dayData;

                try
                {
                    dayData = DatabaseHelper.GetDailyReport(dStr, dept);
                }
                catch
                {
                    dayData = new List<dynamic>();
                }

                int dPresent = 0, dAbsent = 0, dLate = 0;
                foreach (var row in dayData ?? new List<dynamic>())
                {
                    dPresent += int.TryParse(row.Present?.ToString(), out int p2) ? p2 : 0;
                    dAbsent += int.TryParse(row.Absent?.ToString(), out int a2) ? a2 : 0;
                    dLate += int.TryParse(row.Late?.ToString(), out int l2) ? l2 : 0;
                }

                int dTotal = dPresent + dAbsent + dLate;
                double presentPct = dTotal > 0 ? dPresent * 100.0 / dTotal : 0;
                double latePct = dTotal > 0 ? dLate * 100.0 / dTotal : 0;
                double absentPct = dTotal > 0 ? dAbsent * 100.0 / dTotal : 0;
                double avgRate = presentPct + (latePct * 0.5);

                trend.Add(new DayAttendance
                {
                    Day = type == "Weekly Report" ? d.ToString("ddd") : d.Day.ToString(),
                    AvgRate = Math.Round(avgRate, 1),
                    PresentPct = presentPct,
                    LatePct = latePct,
                    AbsentPct = absentPct
                });
            }
            TrendData = trend;
        }

        // ── Export PDF ─────────────────────────────────────────────
        public void ExportPdf(string periodLabel, string reportType)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Attendance Report",
                FileName = $"SmartAttend_Report_{periodLabel.Replace(" ", "_").Replace(",", "")}",
                DefaultExt = ".pdf",
                Filter = "PDF Files (*.pdf)|*.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                int totalEmp = ReportRows?.Count ?? 0;
                int perfect = int.TryParse(PerfectCount, out int pc) ? pc : 0;
                int atRisk = int.TryParse(AtRiskCount, out int ar) ? ar : 0;
                string avgAtt = AvgAttendance ?? "—";

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("SmartAttend")
                                        .FontSize(20).Bold().FontColor("#1a2f5a");
                                    c.Item().Text("Attendance Management System")
                                        .FontSize(10).FontColor("#555555");
                                });
                                row.ConstantItem(220).AlignRight().Column(c =>
                                {
                                    c.Item().Text(reportType.ToUpper())
                                        .FontSize(13).Bold().FontColor("#1a2f5a");
                                    c.Item().Text($"Period: {periodLabel}")
                                        .FontSize(10).FontColor("#333333");
                                    c.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                                        .FontSize(9).FontColor("#888888");
                                });
                            });
                            col.Item().PaddingTop(6).BorderBottom(2).BorderColor("#00b09c");
                        });

                        page.Content().PaddingTop(16).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                SummaryBadge(row, "Total Employees", totalEmp.ToString(), "#1a2f5a", "#eef3fa");
                                SummaryBadge(row, "Avg Attendance", avgAtt, "#007d6e", "#e0f5f2");
                                SummaryBadge(row, "Perfect", perfect.ToString(), "#1b5e20", "#e8f5e9");
                                SummaryBadge(row, "At Risk", atRisk.ToString(), "#b71c1c", "#ffebee");
                            });

                            col.Item().PaddingTop(14);

                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(30);
                                    cols.ConstantColumn(70);
                                    cols.RelativeColumn(3);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.2f);
                                    cols.RelativeColumn(1.5f);
                                    cols.RelativeColumn(1.5f);
                                });

                                static QContainer HeaderCell(QContainer c) =>
                                    c.Background("#1a2f5a").Padding(6).AlignMiddle();

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("No.").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("User ID").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Employee").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Department").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Present").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Absent").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Late").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Total Hours").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Rate").FontColor("#ffffff").Bold().FontSize(9);
                                });

                                int index = 1;
                                foreach (var r in ReportRows ?? new List<ReportRow>())
                                {
                                    bool isEven = index % 2 == 0;
                                    string rowBg = isEven ? "#f7faff" : "#ffffff";

                                    QContainer DataCell(QContainer c) =>
                                        c.Background(rowBg).BorderBottom(1).BorderColor("#e8edf5")
                                         .Padding(5).AlignMiddle();

                                    string rateColor = r.RateColor?.ToLower() switch
                                    {
                                        "green" => "#007d6e",
                                        "amber" => "#d4851b",
                                        "red" => "#c94040",
                                        _ => "#333333"
                                    };

                                    table.Cell().Element(DataCell).Text(index.ToString()).FontSize(9).FontColor("#888888");
                                    table.Cell().Element(DataCell).Text(r.UserId).FontSize(9).FontColor("#555555");
                                    table.Cell().Element(DataCell).Text(r.Name).FontSize(9).Bold();
                                    table.Cell().Element(DataCell).Text(r.Department).FontSize(9).FontColor("#4a6590");
                                    table.Cell().Element(DataCell).Text(r.Present).FontSize(9).AlignCenter();
                                    table.Cell().Element(DataCell).Text(r.Absent).FontSize(9).AlignCenter();
                                    table.Cell().Element(DataCell).Text(r.Late).FontSize(9).AlignCenter();
                                    table.Cell().Element(DataCell).Text(r.TotalHours).FontSize(9).AlignCenter();
                                    table.Cell().Element(DataCell).Text(r.Rate)
                                        .FontSize(9).Bold().FontColor(rateColor).AlignCenter();

                                    index++;
                                }
                            });
                        });

                        page.Footer().AlignCenter().Text(t =>
                        {
                            t.Span("SmartAttend  •  ").FontColor("#aaaaaa").FontSize(8);
                            t.Span("Page ").FontColor("#aaaaaa").FontSize(8);
                            t.CurrentPageNumber().FontColor("#aaaaaa").FontSize(8);
                            t.Span(" of ").FontColor("#aaaaaa").FontSize(8);
                            t.TotalPages().FontColor("#aaaaaa").FontSize(8);
                        });
                    });
                })
                .GeneratePdf(dialog.FileName);

                MessageBox.Show(
                    $"Report exported successfully.\nSaved to: {dialog.FileName}",
                    "SmartAttend — Export Complete",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed:\n{ex.Message}",
                    "SmartAttend — Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Helpers ────────────────────────────────────────────────
        private void SummaryBadge(RowDescriptor row, string label, string value,
                                   string textColor, string bgColor)
        {
            row.RelativeItem()
               .Border(1).BorderColor("#e8edf5").Background(bgColor)
               .Padding(10).AlignCenter().Column(c =>
               {
                   c.Item().AlignCenter().Text(value).FontSize(18).Bold().FontColor(textColor);
                   c.Item().AlignCenter().Text(label).FontSize(9).FontColor(textColor);
               });
        }

        public static string GetRateColor(double val)
        {
            if (val >= 80) return "green";
            if (val >= 60) return "amber";
            return "red";
        }
    }
}