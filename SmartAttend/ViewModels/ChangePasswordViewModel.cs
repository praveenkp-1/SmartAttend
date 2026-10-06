using System.ComponentModel;
using SmartAttend.Database;
using SmartAttend.Helpers;

namespace SmartAttend.ViewModels
{
    public class ChangePasswordViewModel : INotifyPropertyChanged
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

        // ── Change password ────────────────────────────────
        public bool ChangePassword(
            string current,
            string newPassword,
            string confirm)
        {
            // Check empty fields
            if (string.IsNullOrEmpty(current) ||
                string.IsNullOrEmpty(newPassword) ||
                string.IsNullOrEmpty(confirm))
            {
                ErrorMessage = "Please fill in all fields.";
                return false;
            }

            // Check password length
            if (newPassword.Length < 6)
            {
                ErrorMessage = "Password must be at least 6 characters.";
                return false;
            }

            // Check passwords match
            if (newPassword != confirm)
            {
                ErrorMessage = "Passwords do not match.";
                return false;
            }

            // Verify current password
            string storedPassword = DatabaseHelper.GetSetting("password");
            string hashedInput = PasswordHelper.HashPassword(current);

            if (hashedInput != storedPassword)
            {
                ErrorMessage = "Incorrect current password.";
                return false;
            }

            // Save new password
            string hashedNew = PasswordHelper.HashPassword(newPassword);
            DatabaseHelper.SaveSetting("password", hashedNew);

            return true;
        }
    }
}