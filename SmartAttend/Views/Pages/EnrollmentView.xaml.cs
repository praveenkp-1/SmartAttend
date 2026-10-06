using System.Windows;
using System.Windows.Controls;
using SmartAttend.Database;

namespace SmartAttend.View
{
    public partial class EnrollmentView : UserControl
    {
        public EnrollmentView()
        {
            InitializeComponent();
            LoadDepartments();
            LoadStats();
        }

        private void LoadDepartments()
        {
            var departments = DatabaseHelper.GetDepartments();
            CmbDepartment.ItemsSource = departments;
            if (departments.Count > 0)
                CmbDepartment.SelectedIndex = 0;
        }

        // ── Stat cards: total / enrolled / pending ──────────────────
        private void LoadStats()
        {
            var data = DatabaseHelper.GetEnrollmentStatus();

            int total = 0, enrolled = 0, pending = 0;
            foreach (var row in data)
            {
                total++;

                bool fp = row.Fingerprint?.ToString() == "1";
                bool rfid = row.RFID?.ToString() == "1";
                bool pin = row.PIN?.ToString() == "1";

                bool hasAuth = fp || rfid || pin;

                if (hasAuth) enrolled++;
                else pending++;
            }

            TxtTotalCount.Text = total.ToString();
            TxtEnrolledCount.Text = enrolled.ToString();
            TxtPendingCount.Text = pending.ToString();
        }

        private void CreateProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtName.Text.Trim();
            string dept = CmbDepartment.SelectedItem?.ToString() ?? "";
            string position = TxtPosition.Text.Trim();
            string contact = TxtContact.Text.Trim();
            string email = TxtEmail.Text.Trim();

            // Validate
            if (string.IsNullOrEmpty(name) ||
                string.IsNullOrEmpty(dept) ||
                string.IsNullOrEmpty(position))
            {
                MessageBox.Show(
                    "Please fill in all fields.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Generate ID and save to database
            string userId = DatabaseHelper.GenerateUserId();
            DatabaseHelper.AddEmployee(userId, name, dept, position, contact, email);

            // Show in the Next Steps panel
            TxtUserId.Text = userId;
            TxtNewEmployeeName.Text = name;
            PnlGeneratedId.Visibility = Visibility.Visible;

            MessageBox.Show(
                $"Profile created successfully!\n\n" +
                $"User ID: {userId}\n\n" +
                $"Hand this ID to authorized staff to enroll biometrics on the ESP32 device.",
                "Profile Created",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Refresh stat cards
            LoadStats();

            // Clear form (keep Next Steps panel showing the new ID)
            TxtName.Text = "";
            TxtPosition.Text = "";
            TxtContact.Text = "";
            TxtEmail.Text = "";
            if (CmbDepartment.Items.Count > 0)
                CmbDepartment.SelectedIndex = 0;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            TxtName.Text = "";
            TxtPosition.Text = "";
            TxtEmail.Text = "";
            TxtContact.Text = "";
            if (CmbDepartment.Items.Count > 0)
                CmbDepartment.SelectedIndex = 0;
        }

        private void CopyId_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtUserId.Text) && TxtUserId.Text != "—")
            {
                Clipboard.SetText(TxtUserId.Text);
                MessageBox.Show(
                    "User ID copied to clipboard!",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "No User ID generated yet.",
                    "SmartAttend",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Called from App.xaml.cs when device sends enrollment update
        public void RefreshEnrollmentStatus()
        {
            Dispatcher.Invoke(() => LoadStats());
        }
    }
}