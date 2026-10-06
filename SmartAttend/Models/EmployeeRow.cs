using System.ComponentModel;

namespace SmartAttend.Models
{
    public class EmployeeRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Name { get; set; } = "";
        public string UserId { get; set; } = "";
        public string Department { get; set; } = "";
        public string Position { get; set; } = "";
        public string Contact { get; set; } = "";
        public string Enrolled { get; set; } = "";
        public string JoinDate { get; set; } = "";
        public string Initials { get; set; } = "";
        public string AvatarColor { get; set; } = "#D0F5F0";
        public string AvatarTextColor { get; set; } = "#007d6e";
        public string Email { get; set; } = "";

        public bool HasFingerprint { get; set; } = false;
        public bool HasRFID { get; set; } = false;
        public bool HasPIN { get; set; } = false;

        private string _status = "";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public double AttendanceRate { get; set; } = 0;
        public double AttendanceBarWidth => AttendanceRate * 0.70;
        public string AttendanceBarColor => AttendanceRate >= 75 ? "#00C6AE"
                                          : AttendanceRate >= 50 ? "#F59E0B"
                                          : "#FF5A5A";
        public string AttendanceRateText => $"{AttendanceRate:0}%";

        public string LastSeen { get; set; } = "—";
        public bool IsCheckedInToday { get; set; } = false;
    }
}