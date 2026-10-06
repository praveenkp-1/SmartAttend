using System.ComponentModel;
using SmartAttend.Database;
using SmartAttend.Helpers;

namespace SmartAttend.ViewModels
{
    public class ForgotPasswordViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // ── State ──────────────────────────────────────────
        public bool IsUsernameVerified { get; private set; } = false;
        public string SecurityQuestion { get; private set; } = "";

        private string _message = "";
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); }
        }

        // ── Step 1: Verify username ────────────────────────
        public bool VerifyUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                Message = "Please enter your username.";
                return false;
            }

            string stored = DatabaseHelper.GetSetting("username");

            if (!string.Equals(username, stored,
                    System.StringComparison.Ordinal))
            {
                Message = "Username not found.";
                return false;
            }

            // Load the security question
            SecurityQuestion = DatabaseHelper.GetSetting("security_question");
            IsUsernameVerified = true;
            return true;
        }

        // ── Step 2: Verify answer + save new password ──────
        public bool ResetPassword(
            string answer,
            string newPassword,
            string confirmPassword)
        {
            // Validate all fields filled
            if (string.IsNullOrWhiteSpace(answer) ||
                string.IsNullOrWhiteSpace(newPassword) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                Message = "Please fill in all fields.";
                return false;
            }

            // Verify answer (case-insensitive, same as how we stored it)
            string hashedAnswer = PasswordHelper.HashPassword(
                answer.ToLower());
            string storedAnswer = DatabaseHelper.GetSetting("security_answer");

            if (hashedAnswer != storedAnswer)
            {
                Message = "Security answer is incorrect.";
                return false;
            }

            // Validate new password
            if (newPassword.Length < 6)
            {
                Message = "Password must be at least 6 characters.";
                return false;
            }

            if (newPassword != confirmPassword)
            {
                Message = "Passwords do not match.";
                return false;
            }

            // Save new hashed password
            string hashed = PasswordHelper.HashPassword(newPassword);
            DatabaseHelper.SaveSetting("password", hashed);

            Message = "Password reset successfully! Redirecting…";
            return true;
        }
    }
}