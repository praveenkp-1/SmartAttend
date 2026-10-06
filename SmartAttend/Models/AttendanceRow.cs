namespace SmartAttend.Models
{
    public class AttendanceRow
    {
        public string Name { get; set; } = "";
        public string UserId { get; set; } = "";
        public string CheckIn { get; set; } = "";
        public string CheckOut { get; set; } = "";
        public string Method { get; set; } = "";
        public string Hours { get; set; } = "";
        public string Status { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AvatarColor { get; set; } = "#EEF3FA";
        public string AvatarTextColor { get; set; } = "#1A3A6B";
    }
}