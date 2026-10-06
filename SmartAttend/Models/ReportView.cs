namespace SmartAttend.Models
{
    public class ReportRow
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Department { get; set; } = "";   // NEW – shown as column & used for chart grouping
        public string Present { get; set; } = "";
        public string Absent { get; set; } = "";
        public string Late { get; set; } = "";
        public string TotalHours { get; set; } = "";
        public string Rate { get; set; } = "";
        public string RateColor { get; set; } = "green"; // "green" | "amber" | "red"
        public double RateValue { get; set; }             // 0-100, used for bar width & chart
        public double RateBarWidth { get; set; }             // Pixels (max 108px = column 140 - 2*16 margin)
        public string Initials { get; set; } = "";
        public string AvatarColor { get; set; } = "#EEF3FA";
        public string AvatarTextColor { get; set; } = "#1A3A6B";
    }

    public class DayAttendance
    {
        public string Day { get; set; }        // "Mon", "Tue", ...
        public double AvgRate { get; set; }     // overall % for that day
        public double PresentPct { get; set; }  // 0-100
        public double LatePct { get; set; }     // 0-100
        public double AbsentPct { get; set; }   // 0-100 (Present+Late+Absent ≈ 100)
    }
}