using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using SmartAttend.Database;
using SmartAttend.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Linq;
using QContainer = QuestPDF.Infrastructure.IContainer;
using SmartAttend.Helpers;

namespace SmartAttend.ViewModels
{
    public class AttendanceViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));



        // ── Attendance list ────────────────────────────────
        private ObservableCollection<AttendanceRow> _attendance;
        public ObservableCollection<AttendanceRow> Attendance
        {
            get => _attendance;
            set { _attendance = value; OnPropertyChanged(nameof(Attendance)); }
        }

        // ── Load attendance ────────────────────────────────
        public void LoadAttendance(string date, string search, string status)
        {
            var data = DatabaseHelper.GetAttendance(date, search, status);
            var result = new ObservableCollection<AttendanceRow>();
            int colorIndex = 0;

            foreach (var row in data)
            {
                // filter by name OR id
                if (!string.IsNullOrWhiteSpace(search))
                {
                    bool matchName = row.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
                    bool matchId = row.UserId?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
                    if (!matchName && !matchId) continue;
                }

                var color = NameHelper.GetAvatarColor(colorIndex++);
                string name = row.Name ?? "";

                result.Add(new AttendanceRow
                {
                    Name = row.Name,
                    UserId = row.UserId,
                    CheckIn = row.CheckIn,
                    CheckOut = row.CheckOut,
                    Method = row.Method,
                    Hours = row.Hours,
                    Status = row.Status,
                    Initials = NameHelper.GetInitials(name),
                    AvatarColor = color.bg,
                    AvatarTextColor = color.text
                });
            }

            Attendance = result;
        }

        public void ExportPdf(string date)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Attendance Report",
                FileName = $"SmartAttend_Attendance_{date}",
                DefaultExt = ".pdf",
                Filter = "PDF Files (*.pdf)|*.pdf"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                // ── Pre-calculate summary ────────────────────────────
                int present = Attendance.Count(r => r.Status?.Equals("Present", StringComparison.OrdinalIgnoreCase) == true);
                int absent = Attendance.Count(r => r.Status?.Equals("Absent", StringComparison.OrdinalIgnoreCase) == true);
                int late = Attendance.Count(r => r.Status?.Equals("Late", StringComparison.OrdinalIgnoreCase) == true);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                        // ── HEADER ───────────────────────────────────
                        page.Header().Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                // Left: Company name + title
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("SmartAttend")
                                        .FontSize(20).Bold().FontColor("#1a1a2e");
                                    c.Item().Text("Attendance Management System")
                                        .FontSize(10).FontColor("#555555");
                                });

                                // Right: Report meta
                                row.ConstantItem(200).AlignRight().Column(c =>
                                {
                                    c.Item().Text("ATTENDANCE REPORT")
                                        .FontSize(13).Bold().FontColor("#1a1a2e");
                                    c.Item().Text($"Date: {date}")
                                        .FontSize(10).FontColor("#333333");
                                    c.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd  HH:mm}")
                                        .FontSize(9).FontColor("#888888");
                                });
                            });

                            // Divider line
                            col.Item().PaddingTop(6).BorderBottom(2).BorderColor("#1a1a2e");
                        });

                        // ── BODY ─────────────────────────────────────
                        page.Content().PaddingTop(16).Column(col =>
                        {
                            // Summary badges row
                            col.Item().Row(row =>
                            {
                                SummaryBadge(row, "Total", Attendance.Count.ToString(), "#1a1a2e", "#e8eaf6");
                                SummaryBadge(row, "Present", present.ToString(), "#1b5e20", "#e8f5e9");
                                SummaryBadge(row, "Absent", absent.ToString(), "#b71c1c", "#ffebee");
                                SummaryBadge(row, "Late", late.ToString(), "#e65100", "#fff3e0");
                            });

                            col.Item().PaddingTop(14);

                            // ── Table ─────────────────────────────────
                            col.Item().Table(table =>
                            {
                                // Column definitions
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(30);   // No.
                                    cols.RelativeColumn(3);    // Name
                                    cols.RelativeColumn(2);    // Check In
                                    cols.RelativeColumn(2);    // Check Out
                                    cols.RelativeColumn(1.5f); // Method
                                    cols.RelativeColumn(1.5f); // Duration
                                    cols.RelativeColumn(1.5f); // Status
                                });

                                // Header row
                                static QContainer HeaderCell(QContainer c) =>
                                    c.Background("#1a1a2e").Padding(6).AlignMiddle();

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("No.").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Employee Name").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Check In").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Check Out").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Method").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Duration").FontColor("#ffffff").Bold().FontSize(9);
                                    header.Cell().Element(HeaderCell).Text("Status").FontColor("#ffffff").Bold().FontSize(9);
                                });

                                // Data rows
                                int index = 1;
                                foreach (var r in Attendance)
                                {
                                    bool isEven = index % 2 == 0;
                                    string rowBg = isEven ? "#f5f5f5" : "#ffffff";

                                    QContainer DataCell(QContainer c) =>
                                        c.Background(rowBg).BorderBottom(1).BorderColor("#e0e0e0")
                                         .Padding(5).AlignMiddle();

                                    string statusColor = r.Status?.ToLower() switch
                                    {
                                        "present" => "#1b5e20",
                                        "absent" => "#b71c1c",
                                        "late" => "#e65100",
                                        _ => "#333333"
                                    };

                                    table.Cell().Element(DataCell).Text(index.ToString()).FontSize(9).FontColor("#888888");
                                    table.Cell().Element(DataCell).Text(r.Name ?? "-").FontSize(9).Bold();
                                    table.Cell().Element(DataCell).Text(r.CheckIn ?? "-").FontSize(9);
                                    table.Cell().Element(DataCell).Text(r.CheckOut ?? "-").FontSize(9);
                                    table.Cell().Element(DataCell).Text(r.Method ?? "-").FontSize(9);
                                    table.Cell().Element(DataCell).Text(r.Hours).FontSize(9);
                                    table.Cell().Element(DataCell)
                                        .Text(r.Status ?? "-").FontSize(9).Bold().FontColor(statusColor);

                                    index++;
                                }
                            });
                        });

                        // ── FOOTER ───────────────────────────────────
                        page.Footer().AlignCenter()
                            .Text(t =>
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
                    $"PDF exported successfully.\nSaved to: {dialog.FileName}",
                    "SmartAttend — Export Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Export failed:\n{ex.Message}",
                    "SmartAttend — Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        //private string FormatHours(object? hoursRaw)
        //{
        //    if (hoursRaw == null) return "-";
        //    if (!double.TryParse(hoursRaw.ToString(), out double h)) return "-";
        //    int hrs = (int)h;
        //    int mins = (int)Math.Round((h - hrs) * 60);
        //    if (hrs == 0 && mins == 0) return "-";
        //    if (hrs == 0) return $"{mins}m";
        //    if (mins == 0) return $"{hrs}h";
        //    return $"{hrs}h {mins}m";
        //}

        // ── Summary badge helper ─────────────────────────────────
        private void SummaryBadge(RowDescriptor row, string label, string value, string textColor, string bgColor)
        {
            row.RelativeItem().Border(1).BorderColor("#e0e0e0").Background(bgColor)
                .Padding(10).AlignCenter().Column(c =>
                {
                    c.Item().AlignCenter().Text(value).FontSize(18).Bold().FontColor(textColor);
                    c.Item().AlignCenter().Text(label).FontSize(9).FontColor(textColor);
                });
        }


    }
}