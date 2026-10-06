using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;
using SmartAttend.Database;
using SmartAttend.Models;
using SmartAttend.Helpers;

namespace SmartAttend.ViewModels
{
    public class SetupViewModel : INotifyPropertyChanged
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

        // ── Main setup method ──────────────────────────────
        public bool RunSetup(SetupModel setup)
        {
            // Validate fields
            if (string.IsNullOrEmpty(setup.CompanyName) ||
                string.IsNullOrEmpty(setup.Username) ||
                string.IsNullOrEmpty(setup.Password) ||
                string.IsNullOrEmpty(setup.ConfirmPassword))
            {
                ErrorMessage = "Please fill in all fields.";
                return false;
            }

            // Check password length
            if (setup.Password.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters.";
                return false;
            }

            // Check passwords match
            if (setup.Password != setup.ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return false;
            }

            if (string.IsNullOrEmpty(setup.SecurityQuestion))
            {
                ErrorMessage = "Please select a security question.";
                return false;
            }

            if (string.IsNullOrEmpty(setup.SecurityAnswer))
            {
                ErrorMessage = "Please provide a security answer.";
                return false;
            }

            // Hash and save
            string hashed = PasswordHelper.HashPassword(setup.Password);
            DatabaseHelper.SaveSetting("company_name", setup.CompanyName);
            DatabaseHelper.SaveSetting("username", setup.Username);
            DatabaseHelper.SaveSetting("password", hashed);
            DatabaseHelper.SaveSetting("setup_complete", "true");

            string hashedAnswer = PasswordHelper.HashPassword(
            setup.SecurityAnswer.ToLower());
            DatabaseHelper.SaveSetting("security_question", setup.SecurityQuestion);
            DatabaseHelper.SaveSetting("security_answer", hashedAnswer);

            // Default departments
            DatabaseHelper.AddDepartment("Production");
            DatabaseHelper.AddDepartment("Admin");
            DatabaseHelper.AddDepartment("Security");

            // Cleanup
            if (File.Exists("setup.dat"))
                File.Delete("setup.dat");

            return true;
        }

       


    }
}