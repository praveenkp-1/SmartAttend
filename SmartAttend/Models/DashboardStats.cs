namespace SmartAttend.Models
{
     public class DashboardStats
     {
         public string TotalEmployees { get; set; }
         public string PresentToday { get; set; }
         public string AbsentToday { get; set; }
         public string LateToday { get; set; }
         public string PendingEnrollment { get; set; }
         public string AttendanceRate { get; set; }
         public string WorkStart { get; set; }
     }
    

    public class RecentCheckIn
    {
        public string Name { get; set; }
        public string Method { get; set; }
        public string Check { get; set; }
        public string Status { get; set; }
        public string Initials { get; set; }
        public string Time { get; set; }
        public string AvatarColor { get; set; } = "#EEF3FA";
        public string AvatarTextColor { get; set; } = "#1A3A6B";
    }
    public class TodaAttendance
    {
        public string Name { get; set; }
        public string UserId { get; set; }
        public string CheckIn { get; set; }
        public string CheckOut { get; set; }
        public string Method { get; set; }
        public string Status { get; set; }
        public string Initials { get; set; }
        public string AvatarColor { get; set; } = "#EEF3FA";
        public string AvatarTextColor { get; set; } = "#1A3A6B";
    } 

    public class WeeklyBar
    {
        public string Day { get; set; }
        public int Percentage { get; set; }
    }
}