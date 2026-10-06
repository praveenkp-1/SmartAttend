using System.Collections.Generic;
using System.ComponentModel;
using SmartAttend.Database;

namespace SmartAttend.ViewModels
{
    public class AddEmployeeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── Error message ──────────────────────────────────
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        // ── Load departments ───────────────────────────────
        public List<String> LoadDepartments()
        {
            return DatabaseHelper.GetDepartments();
        }

        // ── Add employee ───────────────────────────────────
        public (bool success, string userId) AddEmployee(
            string name,
            string dept,
            string position,
            string contact,
            string email)
        {
            // Validate
            if (string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(dept) ||
                string.IsNullOrEmpty(position))
            {
                ErrorMessage = "Please fill in all fields.";
                return (false, "");
            }

            // Generate unique ID and save
            string userId = DatabaseHelper.GenerateUserId();
            DatabaseHelper.AddEmployee(userId, name, dept,position, contact,email);

            return (true, userId);
        }
    }
}