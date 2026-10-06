using System.ComponentModel;
using SmartAttend.Database;
using SmartAttend.Helpers;
using SmartAttend.Models;

namespace SmartAttend.ViewModels
{
    public class LoginViewModel : INotifyPropertyChanged
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

        // ── Validate login ─────────────────────────────────
        public bool Login(LoginModel login)
        {
            // Check empty fields
            if (string.IsNullOrEmpty(login.Username) ||
                string.IsNullOrEmpty(login.Password))
            {
                ErrorMessage = "Please enter username and password.";
                return false;
            }

            // Get stored credentials
            string storedUsername = DatabaseHelper.GetSetting("username");
            string storedPassword = DatabaseHelper.GetSetting("password");

            // Hash entered password and compare
            string hashedInput = PasswordHelper.HashPassword(login.Password);

            if (login.Username == storedUsername &&
                hashedInput == storedPassword)
            {
                return true;
            }

            ErrorMessage = "Incorrect username or password.";
            return false;
        }
    }
}