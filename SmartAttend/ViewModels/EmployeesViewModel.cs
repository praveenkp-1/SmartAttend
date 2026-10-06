using System.Collections.ObjectModel;
using System.ComponentModel;
using SmartAttend.Database;
using SmartAttend.Helpers;
using SmartAttend.Models;

namespace SmartAttend.ViewModels
{
    public class EmployeesViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private List<EmployeeRow> _allEmployees = new();
        public List<EmployeeRow> AllEmployees
        {
            get => _allEmployees;
            set { _allEmployees = value; OnPropertyChanged(nameof(AllEmployees)); }
        }

        private ObservableCollection<string> _departments = new();
        public ObservableCollection<string> Departments
        {
            get => _departments;
            set { _departments = value; OnPropertyChanged(nameof(Departments)); }
        }

        public List<string> LoadDepartments() =>
            DatabaseHelper.GetDepartments()
                          .Select(d => d.ToString())
                          .ToList();

        public void LoadEmployees()
        {
            var stats = DatabaseHelper.GetEmployeeAttendanceStats();
            var data = DatabaseHelper.GetAllEmployees();
            var result = new List<EmployeeRow>();
            int colorIndex = 0;

            foreach (var emp in data)
            {
                string name = emp.Name?.ToString() ?? "";
                string userId = emp.UserId?.ToString() ?? "";
                var color = NameHelper.GetAvatarColor(colorIndex++);
                stats.TryGetValue(userId, out var att);

                result.Add(new EmployeeRow
                {
                    Name = name,
                    UserId = userId,
                    Department = emp.Department?.ToString() ?? "",
                    Position = emp.Position?.ToString() ?? "",
                    Contact = emp.Contact?.ToString() ?? "",
                    Email = emp.Email?.ToString() ?? "",
                    Enrolled = emp.Enrolled?.ToString() ?? "",
                    Status = emp.Status?.ToString() ?? "",
                    JoinDate = emp.JoinDate?.ToString() ?? "",
                    HasFingerprint = emp.FingerprintId?.ToString() == "1",
                    HasRFID = emp.RFIDCard.ToString() == "1",
                    HasPIN = emp.PIN?.ToString() == "1",
                    Initials = NameHelper.GetInitials(name),
                    AvatarColor = color.bg,
                    AvatarTextColor = color.text,

                    // Attendance stats
                    AttendanceRate = att.Rate,
                    LastSeen = att.LastSeen ?? "—",
                    IsCheckedInToday = att.IsToday
                });
            }

            AllEmployees = result;
        }

        public List<EmployeeRow> FilterEmployees(
            string search, string department, string status, string authMethod)
        {
            var filtered = AllEmployees.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.ToLower();
                filtered = filtered.Where(e =>
                    e.Name.ToLower().Contains(s) ||
                    e.UserId.ToLower().Contains(s) ||
                    e.Department.ToLower().Contains(s));
            }

            if (!string.IsNullOrEmpty(department) && department != "All Departments")
                filtered = filtered.Where(e => e.Department == department);

            if (!string.IsNullOrEmpty(status) && status != "All Status")
                filtered = filtered.Where(e => e.Status == status);

            filtered = authMethod switch
            {
                "Fingerprint" => filtered.Where(e => e.HasFingerprint),
                "RFID" => filtered.Where(e => e.HasRFID),
                "PIN" => filtered.Where(e => e.HasPIN),
                _ => filtered
            };

            return filtered.ToList();
        }
    }
}